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
    /// <summary>
    /// An item's soft requirement: one R for the whole item, judged by the skill of the usage in use. A bastard sword
    /// asks One Handed in its 1H usage and Two Handed in its 2H usage (same R); a javelin asks Throwing when thrown and
    /// its melee skill when held. Shields and armour ask Athletics, horses Riding, whatever the usage.
    /// </summary>
    public sealed class ItemRequirement
    {
        private readonly WeaponComponentData?[] _usages;
        private readonly SkillObject[] _usageSkills;

        /// <param name="usages">The item's Weapons in usage-index order (empty for armour / horses).</param>
        /// <param name="usageSkills">Skill per usage, same length as <paramref name="usages"/>.</param>
        public ItemRequirement(SkillObject skill, int value, SkillKind kind, float tierf,
            WeaponComponentData?[]? usages = null, SkillObject[]? usageSkills = null)
        {
            Skill = skill;
            Value = value;
            Kind = kind;
            Tierf = tierf;
            _usages = usages ?? Array.Empty<WeaponComponentData?>();
            _usageSkills = usageSkills ?? Array.Empty<SkillObject>();
            if (_usages.Length != _usageSkills.Length) throw new ArgumentException("one skill per usage");

            var distinct = new List<SkillObject> { skill };
            foreach (var s in _usageSkills)
                if (s != null && !distinct.Contains(s)) distinct.Add(s);
            Skills = distinct;
        }

        /// <summary>The skill of the item's primary usage (Weapons[0]) - the one to show when no usage is known.</summary>
        public SkillObject Skill { get; }

        /// <summary>Every distinct skill the item can be judged by, primary first (one per tooltip line).</summary>
        public IReadOnlyList<SkillObject> Skills { get; }

        /// <summary>R. May be 0 (eligible item that asks nothing) - check <see cref="HasRequirement"/>.</summary>
        public int Value { get; }

        public SkillKind Kind { get; }

        /// <summary>The item's Tierf the requirement was computed from.</summary>
        public float Tierf { get; }

        /// <summary>vanilla's displayed "Tier N" (round(Tierf) clamped to 0..6).</summary>
        public int DisplayedTier => Requirements.DisplayedTier(Tierf);

        public bool HasRequirement => Value > 0;

        /// <summary>Number of weapon usages known (0 for armour and horses).</summary>
        public int UsageCount => _usages.Length;

        /// <summary>
        /// The skill for this weapon usage (a MissionWeapon's CurrentUsageItem, a blow's WeaponComponentData, one of
        /// item.Weapons). Matched by reference, then by WeaponDescriptionId (a usage of the same kind); anything
        /// unknown or null - and every armour / horse / shield query - gets <see cref="Skill"/>.
        /// </summary>
        public SkillObject SkillFor(WeaponComponentData? usage)
        {
            if (usage == null) return Skill;
            for (int i = 0; i < _usages.Length; i++)
                if (ReferenceEquals(_usages[i], usage)) return _usageSkills[i];
            var id = usage.WeaponDescriptionId;
            if (!string.IsNullOrEmpty(id))
                for (int i = 0; i < _usages.Length; i++)
                    if (_usages[i]?.WeaponDescriptionId == id) return _usageSkills[i];
            return Skill;
        }

        /// <summary>The skill for usage index <paramref name="usageIndex"/> of item.Weapons (MissionWeapon.CurrentUsageIndex,
        /// the inventory tooltip's usage tab); <see cref="Skill"/> when out of range.</summary>
        public SkillObject SkillForUsage(int usageIndex) =>
            usageIndex >= 0 && usageIndex < _usageSkills.Length ? _usageSkills[usageIndex] : Skill;
    }

    /// <summary>
    /// The mod's requirement table: ItemObject -> (skill, R), computed lazily on first query and cached for the
    /// running game (crafted weapons made mid-campaign are picked up on their first query). Never writes
    /// ItemObject.Difficulty.
    ///
    /// Every category (melee / throwing weapons, shields, armour, bows / crossbows / slings, horses):
    /// R = round5(tier table(item.Tierf) x category scale) (Core.Requirements; round 2 - vanilla Difficulty is no longer
    /// used). item.Tierf is NOT stored: each read calls Game.Current.BasicModels.ItemValueModel.CalculateTier(item),
    /// unless the item XML has tier_override. So it needs a running Game - hence lazy, and the cache is reset per game.
    /// Skills: weapons per usage (<see cref="ItemRequirement.SkillFor"/>), shields + armour Athletics, horses Riding.
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

        /// <summary>THE live settings object, read at use time. The MCM page (<see cref="McmSettings"/>) writes it in place
        /// (a requirement change also calls <see cref="Reset"/>); without MCM it keeps the defaults. Never replaced.</summary>
        public static GearMasterySettings Settings { get; } = new GearMasterySettings();

        // Bumped by every Reset: a Get that computed with the OLD table while an MCM change reset the cache does not
        // write its stale result back (the next query computes afresh).
        private static int _generation;

        /// <summary>Forget everything (new game / game end, or a requirement setting changed in MCM).</summary>
        public static void Reset()
        {
            System.Threading.Interlocked.Increment(ref _generation);
            Cache.Clear();
        }

        /// <summary>
        /// The item's requirement, or null when the item is not covered (ammo, banners, goods, pack animals, firearms)
        /// or no game is running. Never throws.
        /// </summary>
        public static ItemRequirement? Get(ItemObject? item)
        {
            if (item == null) return null;
            if (Cache.TryGetValue(item, out var cached)) return cached;
            if (Game.Current?.BasicModels?.ItemValueModel == null) return null; // Tierf needs a running game; don't cache
            int generation = System.Threading.Volatile.Read(ref _generation);
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
            if (System.Threading.Volatile.Read(ref _generation) == generation) Cache[item] = result;
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

        /// <summary>Shortfall of this character on this item in this usage (null = primary), 0 when not covered or met.</summary>
        public static float Shortfall(BasicCharacterObject? character, ItemObject? item, WeaponComponentData? usage = null)
        {
            var r = Get(item);
            if (r == null || !r.HasRequirement) return 0f;
            return Requirements.Shortfall(r.Value, GetSkill(character, r.SkillFor(usage)));
        }

        private static ItemRequirement? Compute(ItemObject item)
        {
            switch (item.ItemType)
            {
                case ItemObject.ItemTypeEnum.HeadArmor:
                case ItemObject.ItemTypeEnum.BodyArmor:
                case ItemObject.ItemTypeEnum.LegArmor:
                case ItemObject.ItemTypeEnum.HandArmor:
                case ItemObject.ItemTypeEnum.Cape:
                    return Fixed(item, DefaultSkills.Athletics, SkillKind.Armour);

                case ItemObject.ItemTypeEnum.Shield:
                    // Round 2 (Toni): Athletics, not vanilla's One Handed (GetRelevantSkillFromWeaponClass(Small/LargeShield)).
                    return Fixed(item, DefaultSkills.Athletics, SkillKind.Shield);

                case ItemObject.ItemTypeEnum.Horse:
                    if (item.HorseComponent == null) return null;
                    return Fixed(item, DefaultSkills.Riding, SkillKind.Mount);

                case ItemObject.ItemTypeEnum.OneHandedWeapon:
                case ItemObject.ItemTypeEnum.TwoHandedWeapon:
                case ItemObject.ItemTypeEnum.Polearm:
                case ItemObject.ItemTypeEnum.Thrown:
                    return PerUsage(item, SkillKind.Weapon, IsMeleeOrThrowingSkill);

                case ItemObject.ItemTypeEnum.Bow:
                case ItemObject.ItemTypeEnum.Crossbow:
                case ItemObject.ItemTypeEnum.Sling:
                    return PerUsage(item, SkillKind.Ranged, IsMissileSkill);

                default:
                    return null; // ammo, banners, goods, animals, harness, firearms, books
            }
        }

        private static bool IsMeleeOrThrowingSkill(SkillObject? s) =>
            s != null && (s == DefaultSkills.OneHanded || s == DefaultSkills.TwoHanded || s == DefaultSkills.Polearm || s == DefaultSkills.Throwing);

        // Throwing = slings (their RelevantSkill).
        private static bool IsMissileSkill(SkillObject? s) =>
            s != null && (s == DefaultSkills.Bow || s == DefaultSkills.Crossbow || s == DefaultSkills.Throwing);

        /// <summary>One skill for the whole item (armour, shields, horses); usages, if any, all map to it.</summary>
        private static ItemRequirement Fixed(ItemObject item, SkillObject skill, SkillKind kind)
        {
            float tier = SafeTier(item);
            var usages = Usages(item);
            var skills = new SkillObject[usages.Length];
            for (int i = 0; i < skills.Length; i++) skills[i] = skill;
            return new ItemRequirement(skill, Requirements.Requirement(kind, tier, Settings), kind, tier, usages, skills);
        }

        /// <summary>
        /// Weapons: each usage is judged by its own RelevantSkill when that is one of ours; a usage with another skill
        /// (or none) falls back to the primary usage's skill. Null when no usage has one of ours.
        /// </summary>
        private static ItemRequirement? PerUsage(ItemObject item, SkillKind kind, Func<SkillObject?, bool> ours)
        {
            var usages = Usages(item);
            SkillObject? primary = null;
            foreach (var u in usages)
                if (ours(u?.RelevantSkill)) { primary = u!.RelevantSkill; break; }
            if (primary == null && ours(item.RelevantSkill)) primary = item.RelevantSkill;
            if (primary == null) return null;
            // Prefer Weapons[0]'s skill as "the" skill when it is ours (it usually is).
            if (usages.Length > 0 && ours(usages[0]?.RelevantSkill)) primary = usages[0]!.RelevantSkill;

            var skills = new SkillObject[usages.Length];
            for (int i = 0; i < usages.Length; i++)
            {
                var s = usages[i]?.RelevantSkill;
                skills[i] = ours(s) ? s! : primary;
            }
            float tier = SafeTier(item);
            return new ItemRequirement(primary, Requirements.Requirement(kind, tier, Settings), kind, tier, usages, skills);
        }

        private static WeaponComponentData?[] Usages(ItemObject item)
        {
            var weapons = item.Weapons;
            if (weapons == null || weapons.Count == 0) return Array.Empty<WeaponComponentData?>();
            var result = new WeaponComponentData?[weapons.Count];
            for (int i = 0; i < result.Length; i++) result[i] = weapons[i];
            return result;
        }

        private static float SafeTier(ItemObject item)
        {
            float t = item.Tierf;
            return float.IsNaN(t) || float.IsInfinity(t) || t < 0f ? 0f : t;
        }

        // ---- Debug dump ----

        public static string DumpPath => Path.Combine(ModLog.Folder, "item_requirements.csv");

        /// <summary>
        /// Writes every registered item we cover to <see cref="DumpPath"/>: id, name, type, kind, displayed tier, tierf,
        /// skills (primary first, ';'-separated), vanilla difficulty, weight, R. One pass over the item list at game
        /// start; cheap (a few thousand rows). Never throws.
        /// </summary>
        public static void WriteDump()
        {
            try
            {
                var items = MBObjectManager.Instance?.GetObjectTypeList<ItemObject>();
                if (items == null) return;
                var sb = new StringBuilder("id,name,type,kind,tier,tierf,skills,difficulty,weight,R\n");
                int rows = 0;
                foreach (var item in items)
                {
                    var r = Get(item);
                    if (r == null) continue;
                    var skills = new List<string>(r.Skills.Count);
                    foreach (var s in r.Skills) skills.Add(s?.StringId ?? "");
                    sb.Append(Csv(item.StringId)).Append(',')
                      .Append(Csv(item.Name?.ToString())).Append(',')
                      .Append(item.ItemType).Append(',')
                      .Append(r.Kind).Append(',')
                      .Append(r.DisplayedTier).Append(',')
                      .Append(r.Tierf.ToString("0.###", CultureInfo.InvariantCulture)).Append(',')
                      .Append(Csv(string.Join(";", skills))).Append(',')
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
