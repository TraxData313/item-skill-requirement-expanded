using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using ItemSkillRequirementExpanded.Core;
using SandBox.Tournaments.MissionLogics;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;
using TaleWorlds.ObjectSystem;

namespace ItemSkillRequirementExpanded
{
    /// <summary>
    /// Glue between the requirement table and the decorator models: which item a blow / a wielded slot belongs to,
    /// the agent's shortfall on it and the armour speed multiplier. NOT cached per agent (review, step 5): the
    /// model's effective skill moves during a battle - formation captains are assigned after deployment
    /// (Sandbox's GetEffectiveSkill adds their perks), the Phalanx perk follows the arrangement order, a hero can
    /// level up mid-battle - and vanilla re-reads it on every stat update, so we do too. The cost is one
    /// GetEffectiveSkill per covered item per stat update / blow; the requirement itself is cached in
    /// <see cref="ItemRequirements"/>. Thread-safe (no per-agent state): blow damage may run off the main thread.
    /// </summary>
    internal static class AgentGear
    {
        // WeaponComponentData -> its ItemObject, for a blow whose item is no longer in any slot (the last javelin thrown).
        private static readonly object MapGate = new object();
        private static Dictionary<WeaponComponentData, ItemObject>? _weaponToItem;

        /// <summary>New game / game end: items may differ.</summary>
        public static void Reset()
        {
            lock (MapGate) _weaponToItem = null;
        }

        /// <summary>
        /// A human HERO agent the who-filter covers (player / companions / other heroes toggles, tournament and
        /// training toggles). Troops never pay: they keep vanilla behaviour, mount lock included. Cheap: a few
        /// property reads plus the per-mission cached kind.
        /// </summary>
        public static bool Pays(Agent? agent)
        {
            if (agent == null || !agent.IsHuman || agent.Character == null || !agent.IsHero) return false;
            var kind = KindOf(agent.Mission);
            return CombatPenalties.AppliesTo(Who(agent), kind.Tournament, kind.Training, ItemRequirements.Settings);
        }

        /// <summary>Who the agent is for the filter. Campaign: by its Hero (main hero / player clan / other);
        /// elsewhere (custom battle): the player's troop or the main agent is the player, any other hero "other".</summary>
        public static PenaltyTarget Who(Agent agent)
        {
            if (!agent.IsHero) return PenaltyTarget.Troop;
            var who = WhoOf(agent.Character);
            if (who == PenaltyTarget.Player || who == PenaltyTarget.Companion) return who;
            return agent.IsMainAgent ? PenaltyTarget.Player : PenaltyTarget.OtherHero;
        }

        /// <summary>Who a character is, outside a mission (the inventory tooltip): campaign heroes by their Hero
        /// (main hero / player clan / other); the game's PlayerTroop is the player; other heroes "other"; anyone
        /// else a troop.</summary>
        public static PenaltyTarget WhoOf(BasicCharacterObject? character)
        {
            if (character == null) return PenaltyTarget.Troop;
            if (Campaign.Current != null && character is CharacterObject co)
            {
                var hero = co.HeroObject;
                if (hero != null)
                {
                    if (hero == Hero.MainHero) return PenaltyTarget.Player;
                    var clan = hero.Clan;
                    return clan != null && clan == Clan.PlayerClan ? PenaltyTarget.Companion : PenaltyTarget.OtherHero;
                }
            }
            if (ReferenceEquals(character, Game.Current?.PlayerTroop)) return PenaltyTarget.Player;
            return character.IsHero ? PenaltyTarget.OtherHero : PenaltyTarget.Troop;
        }

        /// <summary>Would this character pay penalties (who-toggles; no tournament)? The inventory tooltip's filter.</summary>
        public static bool CharacterPays(BasicCharacterObject? character) =>
            CombatPenalties.AppliesTo(WhoOf(character), false, ItemRequirements.Settings);

        // ---- mission kind (tournament / training), cached per mission: one immutable object, swapped atomically
        // (blows may run off-thread)

        private sealed class MissionKind
        {
            public static readonly MissionKind None = new MissionKind(null, false, false);

            public MissionKind(Mission? mission, bool tournament, bool training)
            {
                Mission = mission;
                Tournament = tournament;
                Training = training;
            }

            public Mission? Mission { get; }
            public bool Tournament { get; }
            public bool Training { get; }
        }

        private static MissionKind? _kind;

        private static MissionKind KindOf(Mission? mission)
        {
            if (mission == null) return MissionKind.None;
            var cached = _kind;
            if (cached != null && ReferenceEquals(cached.Mission, mission)) return cached;
            var kind = new MissionKind(mission, DetectTournament(mission), DetectTraining(mission));
            _kind = kind;
            if (kind.Training) ModLog.Info("mission", "training fight: penalties " + (ItemRequirements.Settings.ApplyInTraining ? "on (MCM)" : "off"));
            return kind;
        }

        /// <summary>Is this mission a campaign tournament fight (SandBox's TournamentBehavior present)?</summary>
        public static bool InTournament(Mission? mission) => KindOf(mission).Tournament;

        /// <summary>Is this mission a training fight (Training Battles drill, training field, arena practice)?</summary>
        public static bool InTraining(Mission? mission) => KindOf(mission).Training;

        /// <summary>Mission over: drop the cached reference (so the old Mission can be collected).</summary>
        public static void ForgetMission(Mission? mission)
        {
            var cached = _kind;
            if (cached != null && (mission == null || ReferenceEquals(cached.Mission, mission))) _kind = null;
        }

        private static bool DetectTournament(Mission mission)
        {
            try
            {
                return Campaign.Current != null && HasTournamentBehavior(mission);
            }
            catch (Exception e)
            {
                ModLog.ErrorOnce("gear.tournament", e);
                return false;
            }
        }

        // Own method: SandBox.dll is JIT-resolved only here, inside the caller's try/catch.
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static bool HasTournamentBehavior(Mission mission) =>
            mission.GetMissionBehavior<TournamentBehavior>() != null;

        // Vanilla training missions, by behaviour type name (no StoryMode reference): StoryMode's training field
        // (the tutorial camp) and SandBox's arena practice fight. Both hand out practice weapons.
        private static readonly string[] TrainingBehaviourNames =
        {
            "TrainingFieldMissionController",
            "ArenaPracticeFightMissionController",
        };

        /// <summary>
        /// A training fight: a drill of Toni's Training Battles mod (its own flag
        /// <c>TrainingBattles.TrainingBattleBehavior.TrainingActive</c>, true from the drill's battle start to its
        /// aftermath - field, sea, siege and lord's hall alike; read by reflection, no dependency), or a vanilla
        /// training mission (<see cref="TrainingBehaviourNames"/>). Campaign only.
        /// </summary>
        private static bool DetectTraining(Mission mission)
        {
            try
            {
                if (Campaign.Current == null) return false;
                if (TrainingBattlesDrillActive()) return true;
                foreach (var behaviour in mission.MissionBehaviors)
                {
                    var name = behaviour?.GetType().Name;
                    if (name != null && Array.IndexOf(TrainingBehaviourNames, name) >= 0) return true;
                }
                return false;
            }
            catch (Exception e)
            {
                ModLog.ErrorOnce("gear.training", e);
                return false;
            }
        }

        private static PropertyInfo? _trainingActive;

        private static bool TrainingBattlesDrillActive()
        {
            var property = _trainingActive;
            if (property == null)
            {
                foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
                {
                    if (assembly.GetName().Name != "TrainingBattles") continue;
                    property = assembly.GetType("TrainingBattles.TrainingBattleBehavior", false)?
                        .GetProperty("TrainingActive", BindingFlags.Public | BindingFlags.Static);
                    if (property != null && property.PropertyType == typeof(bool)) break;
                    property = null;
                }
                if (property == null) return false; // mod not loaded (looked up again next mission: cheap)
                _trainingActive = property;
            }
            return property.GetValue(null) is bool active && active;
        }

        /// <summary>
        /// The agent's shortfall on the item (0 when not covered / met), using the model's effective skill
        /// (captain perks etc.), read fresh like vanilla's own stats. The skill is the one of <paramref name="usage"/>
        /// (bastard sword in 2H mode -> Two Handed); null usage = the item's primary skill. The same culture bonus
        /// (<see cref="CultureMatch"/>) is applied to it.
        /// </summary>
        public static float Shortfall(AgentStatCalculateModel model, Agent agent, ItemObject? item, WeaponComponentData? usage,
            out ItemRequirement? requirement)
        {
            requirement = ItemRequirements.Get(item);
            if (item == null || requirement == null || !requirement.HasRequirement) return 0f;
            var skill = requirement.SkillFor(usage);
            if (skill == null) return 0f;
            // Same culture bonus on top of the model's effective skill (read fresh, like the skill: nothing cached).
            return Requirements.Shortfall(requirement.Value, CultureMatch.Effective(agent.Character, requirement, model.GetEffectiveSkill(agent, skill)));
        }

        /// <summary>The foot max-speed multiplier from all worn armour (each piece by its own ratio, shared by weight).</summary>
        public static float ArmourSpeed(AgentStatCalculateModel model, Agent agent)
        {
            var equipment = agent.SpawnEquipment;
            if (equipment == null) return 1f;
            var settings = ItemRequirements.Settings;
            List<ArmourPiece>? pieces = null;
            bool anyShort = false;
            SkillObject? lastSkill = null;
            int lastValue = 0;
            for (var i = EquipmentIndex.ArmorItemBeginSlot; i < EquipmentIndex.ArmorItemEndSlot; i++)
            {
                var element = equipment[i];
                var item = element.Item;
                if (item == null) continue;
                float s = 0f;
                int? tier = null;
                var req = ItemRequirements.Get(item);
                if (req != null && req.HasRequirement && req.Skill != null)
                {
                    // Every armour piece asks Athletics: one effective-skill read per agent, not per piece.
                    if (!ReferenceEquals(req.Skill, lastSkill))
                    {
                        lastSkill = req.Skill;
                        lastValue = model.GetEffectiveSkill(agent, req.Skill);
                    }
                    // Each piece by its own culture (a Battanian helmet over a Vlandian mail).
                    s = Requirements.Shortfall(req.Value, CultureMatch.Effective(agent.Character, req, lastValue));
                    tier = req.DisplayedTier;
                }
                if (s > 0f) anyShort = true;
                // Each piece's penalty capped (starter gear) before the weight share.
                (pieces ??= new List<ArmourPiece>(5)).Add(CombatPenalties.ArmourPieceFor(element.Weight, s, settings, tier));
            }
            return anyShort && pieces != null ? Penalties.ArmourSpeedMultiplier(pieces, settings) : 1f;
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
