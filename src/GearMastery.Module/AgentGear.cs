using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using GearMastery.Core;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;
using TaleWorlds.ObjectSystem;

namespace GearMastery
{
    /// <summary>
    /// Per-agent glue between the requirement table and the decorator models: which item a blow / a wielded slot
    /// belongs to, the agent's shortfall on it (cached per agent and item for the mission - UpdateAgentStats runs on
    /// every weapon switch, mount, ammo change), and the armour speed multiplier (cached once: spawn armour does not
    /// change in a mission). Agent objects are new each mission, so the ConditionalWeakTable clears itself.
    /// Thread-safe: blow damage may be computed off the main thread.
    /// </summary>
    internal static class AgentGear
    {
        private sealed class Cache
        {
            public readonly Dictionary<ItemObject, float> Shortfall = new Dictionary<ItemObject, float>(4);
            public float? ArmourSpeed;
        }

        private static readonly ConditionalWeakTable<Agent, Cache> Caches = new ConditionalWeakTable<Agent, Cache>();

        // WeaponComponentData -> its ItemObject, for a blow whose item is no longer in any slot (the last javelin thrown).
        private static readonly object MapGate = new object();
        private static Dictionary<WeaponComponentData, ItemObject>? _weaponToItem;

        /// <summary>New game / game end: items may differ.</summary>
        public static void Reset()
        {
            lock (MapGate) _weaponToItem = null;
        }

        /// <summary>A human agent with a character that pays penalties under the current settings.</summary>
        public static bool Pays(Agent? agent)
        {
            if (agent == null || !agent.IsHuman || agent.Character == null) return false;
            return CombatPenalties.AppliesTo(agent.IsHero, ItemRequirements.Settings);
        }

        /// <summary>
        /// The agent's shortfall on the item (0 when not covered / met), using the model's effective skill
        /// (captain perks etc.). Cached per agent and item for the mission.
        /// </summary>
        public static float Shortfall(AgentStatCalculateModel model, Agent agent, ItemObject? item, out ItemRequirement? requirement)
        {
            requirement = ItemRequirements.Get(item);
            if (item == null || requirement == null || !requirement.HasRequirement || requirement.Skill == null) return 0f;
            var cache = Caches.GetValue(agent, _ => new Cache());
            lock (cache)
            {
                if (cache.Shortfall.TryGetValue(item, out float s)) return s;
            }
            int skill = model.GetEffectiveSkill(agent, requirement.Skill);
            float shortfall = Requirements.Shortfall(requirement.Value, skill);
            lock (cache) cache.Shortfall[item] = shortfall;
            return shortfall;
        }

        /// <summary>The foot max-speed multiplier from all worn armour (weight-weighted shortfall), cached per agent.</summary>
        public static float ArmourSpeed(AgentStatCalculateModel model, Agent agent)
        {
            var cache = Caches.GetValue(agent, _ => new Cache());
            lock (cache)
            {
                if (cache.ArmourSpeed.HasValue) return cache.ArmourSpeed.Value;
            }
            var pieces = new List<ArmourPiece>(5);
            var equipment = agent.SpawnEquipment;
            if (equipment != null)
            {
                for (var i = EquipmentIndex.ArmorItemBeginSlot; i < EquipmentIndex.ArmorItemEndSlot; i++)
                {
                    var element = equipment[i];
                    var item = element.Item;
                    if (item == null) continue;
                    float s = Shortfall(model, agent, item, out _);
                    pieces.Add(new ArmourPiece(element.Weight, s));
                }
            }
            float m = Penalties.ArmourSpeedMultiplier(pieces, ItemRequirements.Settings);
            lock (cache) cache.ArmourSpeed = m;
            return m;
        }

        /// <summary>The item in the agent's main hand, or null.</summary>
        public static ItemObject? WieldedItem(Agent agent, out WeaponComponentData? usage)
        {
            usage = null;
            var index = agent.GetPrimaryWieldedItemIndex();
            if (index == EquipmentIndex.None) return null;
            var weapon = agent.Equipment[index];
            if (weapon.IsEmpty) return null;
            usage = weapon.CurrentUsageItem;
            return weapon.Item;
        }

        /// <summary>
        /// The ItemObject a WeaponComponentData belongs to: first the agent's own weapon slots (crafted weapons too), then
        /// a map over every registered item (built once per game on the first miss).
        /// </summary>
        public static ItemObject? ItemOf(Agent agent, WeaponComponentData weapon)
        {
            var equipment = agent.Equipment;
            if (equipment != null)
            {
                for (var i = EquipmentIndex.WeaponItemBeginSlot; i < EquipmentIndex.NumAllWeaponSlots; i++)
                {
                    var mw = equipment[i];
                    if (mw.IsEmpty) continue;
                    var item = mw.Item;
                    if (item != null && Owns(item, weapon)) return item;
                }
            }
            var map = WeaponMap();
            return map != null && map.TryGetValue(weapon, out var found) ? found : null;
        }

        private static bool Owns(ItemObject item, WeaponComponentData weapon)
        {
            var weapons = item.Weapons;
            if (weapons == null) return false;
            for (int k = 0; k < weapons.Count; k++)
                if (ReferenceEquals(weapons[k], weapon)) return true;
            return false;
        }

        private static Dictionary<WeaponComponentData, ItemObject>? WeaponMap()
        {
            lock (MapGate)
            {
                if (_weaponToItem != null) return _weaponToItem;
                var items = MBObjectManager.Instance?.GetObjectTypeList<ItemObject>();
                if (items == null) return null;
                var map = new Dictionary<WeaponComponentData, ItemObject>();
                foreach (var item in items)
                {
                    var weapons = item?.Weapons;
                    if (weapons == null) continue;
                    for (int k = 0; k < weapons.Count; k++)
                        if (weapons[k] != null && !map.ContainsKey(weapons[k])) map[weapons[k]] = item!;
                }
                _weaponToItem = map;
                return map;
            }
        }
    }
}
