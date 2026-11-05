using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;
using GearMastery.Core;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;
using TaleWorlds.ObjectSystem;

namespace GearMastery
{
    /// <summary>An item's soft requirement: which skill, how much, and where the number came from.</summary>
    public sealed class ItemRequirement
    {
        public ItemRequirement(SkillObject skill, int value, SkillKind kind, float tierf)
        {
            Skill = skill;
            Value = value;
            Kind = kind;
            Tierf = tierf;
        }

        public SkillObject Skill { get; }

        /// <summary>R. May be 0 (eligible item that asks nothing) - check <see cref="HasRequirement"/>.</summary>
        public int Value { get; }

        public SkillKind Kind { get; }

        /// <summary>The tier the requirement was computed from (Ranged/Mount: the item's tier, informational only).</summary>
        public float Tierf { get; }

        public bool HasRequirement => Value > 0;
    }

    /// <summary>
    /// The mod's requirement table: ItemObject -> (skill, R), computed lazily on first query and cached for the
    /// running game (crafted weapons made mid-campaign are picked up on their first query). Never writes
    /// ItemObject.Difficulty.
    ///
    /// Sources:
    /// - melee / throwing weapons, shields, armour: R = round5(slope x item.Tierf), clamp [0, cap] (Core.Requirements).
    ///   item.Tierf is NOT stored: each read calls Game.Current.BasicModels.ItemValueModel.CalculateTier(item), unless
    ///   the item XML has tier_override. So it needs a running Game - hence lazy, and the cache is reset per game.
    /// - bows, crossbows, slings, horses: R = vanilla item.Difficulty (Toni, 2026-11-05: these become soft too).
    /// </summary>
    public static class ItemRequirements
    {
        private sealed class RefComparer : IEqualityComparer<ItemObject>
        {
            public static readonly RefComparer Instance = new RefComparer();
            public bool Equals(ItemObject x, ItemObject y) => ReferenceEquals(x, y);
            public int GetHashCode(ItemObject obj) => RuntimeHelpers.GetHashCode(obj);
        }

        // null value = "looked at, not eligible" (ConcurrentDictionary accepts null values).
        private static readonly ConcurrentDictionary<ItemObject, ItemRequirement?> Cache =
            new ConcurrentDictionary<ItemObject, ItemRequirement?>(RefComparer.Instance);

        /// <summary>Live settings, from settings.json at every game start (<see cref="SettingsLoader"/>).</summary>
        public static GearMasterySettings Settings { get; set; } = new GearMasterySettings();

        /// <summary>Forget everything (new game / game end: models and items may differ).</summary>
        public static void Reset() => Cache.Clear();

        /// <summary>
        /// The item's requirement, or null when the item is not covered (ammo, banners, goods, pack animals, firearms)
        /// or no game is running. Never throws.
        /// </summary>
        public static ItemRequirement? Get(ItemObject? item)
        {
            if (item == null) return null;
            if (Cache.TryGetValue(item, out var cached)) return cached;
            if (Game.Current?.BasicModels?.ItemValueModel == null) return null; // Tierf needs a running game; don't cache
            ItemRequirement? result;
            try
            {
                result = Compute(item);
            }
            catch (Exception e)
            {
                ModLog.ErrorOnce("ItemRequirements.Get", e);
                result = null;
            }
            Cache[item] = result;
            return result;
        }

        /// <summary>Convenience: (skill, R) only when R &gt; 0.</summary>
        public static bool TryGet(ItemObject? item, out SkillObject? skill, out int requirement)
        {
            var r = Get(item);
            if (r != null && r.HasRequirement && r.Skill != null)
            {
                skill = r.Skill;
                requirement = r.Value;
                return true;
            }
            skill = null;
            requirement = 0;
            return false;
        }

        /// <summary>The character's raw skill value (hero skills for heroes). 0 for null input.
        /// Inside combat models prefer the model's own GetEffectiveSkill (captain/perk effects).</summary>
        public static int GetSkill(BasicCharacterObject? character, SkillObject? skill)
        {
            if (character == null || skill == null) return 0;
            try { return character.GetSkillValue(skill); }
            catch (Exception e) { ModLog.ErrorOnce("ItemRequirements.GetSkill", e); return 0; }
        }

        public static int GetSkill(Agent? agent, SkillObject? skill) => GetSkill(agent?.Character, skill);

        /// <summary>Shortfall of this character on this item, 0 when not covered or met.</summary>
        public static float Shortfall(BasicCharacterObject? character, ItemObject? item)
        {
            var r = Get(item);
            if (r == null || !r.HasRequirement) return 0f;
            return Requirements.Shortfall(r.Value, GetSkill(character, r.Skill));
        }

        private static ItemRequirement? Compute(ItemObject item)
        {
            var settings = Settings;
            switch (item.ItemType)
            {
                case ItemObject.ItemTypeEnum.HeadArmor:
                case ItemObject.ItemTypeEnum.BodyArmor:
                case ItemObject.ItemTypeEnum.LegArmor:
                case ItemObject.ItemTypeEnum.HandArmor:
                case ItemObject.ItemTypeEnum.Cape:
                    return FromTier(item, DefaultSkills.Athletics, SkillKind.Armour, settings);

                case ItemObject.ItemTypeEnum.Shield:
                    // Vanilla agrees: WeaponComponentData.GetRelevantSkillFromWeaponClass(Small/LargeShield) = OneHanded.
                    return FromTier(item, DefaultSkills.OneHanded, SkillKind.Shield, settings);

                case ItemObject.ItemTypeEnum.OneHandedWeapon:
                case ItemObject.ItemTypeEnum.TwoHandedWeapon:
                case ItemObject.ItemTypeEnum.Polearm:
                case ItemObject.ItemTypeEnum.Thrown:
                {
                    var skill = item.PrimaryWeapon?.RelevantSkill;
                    if (skill != DefaultSkills.OneHanded && skill != DefaultSkills.TwoHanded
                        && skill != DefaultSkills.Polearm && skill != DefaultSkills.Throwing)
                        return null;
                    return FromTier(item, skill!, SkillKind.Weapon, settings);
                }

                case ItemObject.ItemTypeEnum.Bow:
                case ItemObject.ItemTypeEnum.Crossbow:
                case ItemObject.ItemTypeEnum.Sling:
                {
                    var skill = item.RelevantSkill; // Bow / Crossbow / Throwing (sling)
                    if (skill == null) return null;
                    return new ItemRequirement(skill, Requirements.FromDifficulty(item.Difficulty), SkillKind.Ranged, SafeTier(item));
                }

                case ItemObject.ItemTypeEnum.Horse:
                    if (item.HorseComponent == null) return null;
                    return new ItemRequirement(DefaultSkills.Riding, Requirements.FromDifficulty(item.Difficulty), SkillKind.Mount, SafeTier(item));

                default:
                    return null; // ammo, banners, goods, animals, harness, firearms, books
            }
        }

        private static ItemRequirement FromTier(ItemObject item, SkillObject skill, SkillKind kind, GearMasterySettings settings)
        {
            float tier = SafeTier(item);
            return new ItemRequirement(skill, Requirements.Requirement(kind, tier, settings), kind, tier);
        }

        private static float SafeTier(ItemObject item)
        {
            float t = item.Tierf;
            return float.IsNaN(t) || float.IsInfinity(t) || t < 0f ? 0f : t;
        }

        // ---- Debug dump ----

        public static string DumpPath => Path.Combine(ModLog.Folder, "item_requirements.csv");

        /// <summary>
        /// Writes every registered item we cover to <see cref="DumpPath"/>: id, type, kind, skill, tierf, difficulty, R.
        /// One pass over the item list at game start; cheap (a few thousand rows). Never throws.
        /// </summary>
        public static void WriteDump()
        {
            try
            {
                var items = MBObjectManager.Instance?.GetObjectTypeList<ItemObject>();
                if (items == null) return;
                var sb = new StringBuilder("id,type,kind,skill,tierf,difficulty,weight,R\n");
                int rows = 0;
                foreach (var item in items)
                {
                    var r = Get(item);
                    if (r == null) continue;
                    sb.Append(Csv(item.StringId)).Append(',')
                      .Append(item.ItemType).Append(',')
                      .Append(r.Kind).Append(',')
                      .Append(Csv(r.Skill?.StringId)).Append(',')
                      .Append(r.Tierf.ToString("0.###", CultureInfo.InvariantCulture)).Append(',')
                      .Append(item.Difficulty).Append(',')
                      .Append(item.Weight.ToString("0.###", CultureInfo.InvariantCulture)).Append(',')
                      .Append(r.Value).Append('\n');
                    rows++;
                }
                Directory.CreateDirectory(ModLog.Folder);
                File.WriteAllText(DumpPath, sb.ToString());
                ModLog.Info("dump", "wrote " + rows + " item requirements to " + DumpPath);
            }
            catch (Exception e)
            {
                ModLog.Error("dump", "item requirement CSV failed", e);
            }
        }

        private static string Csv(string? s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            return s!.IndexOfAny(new[] { ',', '"', '\n' }) < 0 ? s : "\"" + s.Replace("\"", "\"\"") + "\"";
        }
    }
}
