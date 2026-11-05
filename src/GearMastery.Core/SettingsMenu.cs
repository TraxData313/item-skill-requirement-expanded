using System;
using System.Collections.Generic;
using System.Linq;

namespace GearMastery.Core
{
    /// <summary>How a setting shows in the Mod Configuration Menu.</summary>
    public enum MenuKind
    {
        /// <summary>A checkbox.</summary>
        Bool,

        /// <summary>A whole-number slider (the value as stored).</summary>
        Int,

        /// <summary>A decimal slider (the value as stored).</summary>
        Float,

        /// <summary>A decimal slider in percent: the menu shows 25, the settings hold 0.25.</summary>
        Percent,
    }

    /// <summary>One group (section) of the menu page, in page order.</summary>
    public sealed class MenuGroup
    {
        internal MenuGroup(string name, int order)
        {
            Name = name;
            Order = order;
        }

        public string Name { get; }
        public int Order { get; }
    }

    /// <summary>
    /// One setting of the menu page and how it maps onto <see cref="GearMasterySettings"/>: <see cref="Get"/> gives the
    /// value in MENU units (percent for <see cref="MenuKind.Percent"/>, 0/1 for a bool), <see cref="Set"/> takes one,
    /// clamps it to <see cref="Min"/>..<see cref="Max"/> and writes the settings object. This is the whole adapter
    /// between MCM and the Core settings; the module only wraps Get/Set in MCM's ProxyRefs.
    /// </summary>
    public sealed class MenuSetting
    {
        private readonly Func<GearMasterySettings, double> _get;
        private readonly Action<GearMasterySettings, double> _set;

        internal MenuSetting(string id, string label, MenuGroup group, string hint, MenuKind kind, double min, double max,
            bool affectsRequirements, Func<GearMasterySettings, double> get, Action<GearMasterySettings, double> set)
        {
            Id = id;
            Label = label;
            Group = group;
            Hint = hint;
            Kind = kind;
            Min = min;
            Max = max;
            AffectsRequirements = affectsRequirements;
            _get = get;
            _set = set;
        }

        /// <summary>The MCM property id (= the <see cref="GearMasterySettings"/> property name).</summary>
        public string Id { get; }

        public string Label { get; }
        public MenuGroup Group { get; }
        public string Hint { get; }
        public MenuKind Kind { get; }

        /// <summary>Slider range, in menu units.</summary>
        public double Min { get; }

        /// <summary>Slider range, in menu units.</summary>
        public double Max { get; }

        /// <summary>True when a change alters item requirements (the module's per-item table must be rebuilt).</summary>
        public bool AffectsRequirements { get; }

        /// <summary>MCM's value format for the slider ("0'%'" style); null for a checkbox.</summary>
        public string? Format => Kind switch
        {
            MenuKind.Int => "0",
            MenuKind.Float => "0.00",
            MenuKind.Percent => "0.0'%'",
            _ => null,
        };

        /// <summary>The value in menu units.</summary>
        public double Get(GearMasterySettings settings) => _get(settings);

        /// <summary>Writes a menu-unit value, clamped to the range (NaN = left as it is). Returns true when the stored
        /// value changed.</summary>
        public bool Set(GearMasterySettings settings, double value)
        {
            if (double.IsNaN(value)) return false;
            if (Kind == MenuKind.Bool) value = value != 0 ? 1 : 0;
            else value = Math.Max(Min, Math.Min(Max, value));
            if (Kind == MenuKind.Int) value = Math.Round(value, MidpointRounding.AwayFromZero);
            double before = _get(settings);
            _set(settings, value);
            return Math.Abs(_get(settings) - before) > 1e-6;
        }
    }

    /// <summary>
    /// Every setting of the mod as the in-game Mod Configuration Menu page shows it: groups, order, labels, hints and
    /// ranges. Game-free on purpose (unit-tested); the module builds the MCM page from this list.
    /// </summary>
    public static class SettingsMenu
    {
        public const string PageId = "GearMastery_v1";
        public const string PageName = "Gear Mastery";
        public const string FolderName = "GearMastery";

        // A reduction of 100% would freeze an agent (no swing, no speed).
        private const double MaxReductionPercent = 90;
        private const double MaxBonusPercent = 300;
        private const double MaxRequirement = 330;

        public static readonly MenuGroup Who = new MenuGroup("Who is affected", 0);
        public static readonly MenuGroup Requirements = new MenuGroup("Requirements", 1);
        public static readonly MenuGroup Curve = new MenuGroup("Penalty curve", 2);
        public static readonly MenuGroup Starter = new MenuGroup("Starter gear", 3);
        public static readonly MenuGroup Melee = new MenuGroup("Melee weapons", 4);
        public static readonly MenuGroup Armour = new MenuGroup("Armour", 5);
        public static readonly MenuGroup Shields = new MenuGroup("Shields", 6);
        public static readonly MenuGroup Ranged = new MenuGroup("Bows, crossbows and slings", 7);
        public static readonly MenuGroup Mounts = new MenuGroup("Horses", 8);
        public static readonly MenuGroup Debug = new MenuGroup("Debug", 9);

        public static IReadOnlyList<MenuGroup> Groups { get; } =
            new[] { Who, Requirements, Curve, Starter, Melee, Armour, Shields, Ranged, Mounts, Debug };

        private const string LowHint = " Just below the requirement (skill/requirement close to 100%).";
        private const string MidHint = " At the curve's mid point (default: skill at 60% of the requirement).";
        private const string HighHint = " At the curve's high point (default: skill at 30% of the requirement) and below.";

        public static IReadOnlyList<MenuSetting> All { get; } = new List<MenuSetting>
        {
            // ---- Who is affected ----
            Flag(Who, nameof(GearMasterySettings.ApplyToPlayer), "Player",
                "You pay penalties for gear above your skill. Troops never do: they always fight as in vanilla. Default on.",
                s => s.ApplyToPlayer, (s, v) => s.ApplyToPlayer = v),
            Flag(Who, nameof(GearMasterySettings.ApplyToCompanions), "Companions",
                "Heroes of your clan (companions, family) pay penalties. Troops never do. Default on.",
                s => s.ApplyToCompanions, (s, v) => s.ApplyToCompanions = v),
            Flag(Who, nameof(GearMasterySettings.ApplyToOtherHeroes), "Other heroes",
                "Every other hero - lords, ladies and wanderers, friend and foe - pays penalties. Troops never do. Default on.",
                s => s.ApplyToOtherHeroes, (s, v) => s.ApplyToOtherHeroes = v),
            Flag(Who, nameof(GearMasterySettings.ApplyInTournaments), "In tournaments",
                "Penalties also apply in tournament fights. Off = everyone fights a tournament as in vanilla. Troops never pay. Default on.",
                s => s.ApplyInTournaments, (s, v) => s.ApplyInTournaments = v),

            // ---- Requirements: the tier table and the per-category scales ----
            Tier(0, s => s.RequirementAtTier0, (s, v) => s.RequirementAtTier0 = v, 0),
            Tier(1, s => s.RequirementAtTier1, (s, v) => s.RequirementAtTier1 = v, 20),
            Tier(2, s => s.RequirementAtTier2, (s, v) => s.RequirementAtTier2 = v, 45),
            Tier(3, s => s.RequirementAtTier3, (s, v) => s.RequirementAtTier3 = v, 75),
            Tier(4, s => s.RequirementAtTier4, (s, v) => s.RequirementAtTier4 = v, 100),
            Tier(5, s => s.RequirementAtTier5, (s, v) => s.RequirementAtTier5 = v, 125),
            Tier(6, s => s.RequirementAtTier6, (s, v) => s.RequirementAtTier6 = v, 150),
            Req(Num(Requirements, nameof(GearMasterySettings.TopTier), "Top tier", MenuKind.Float, 6.1, 15,
                "The item tier of the table's last point (the best of the best; body armour goes up to about 9). Items at or above it ask the top requirement. Default 7.5.",
                s => s.TopTier, (s, v) => s.TopTier = (float)v)),
            Req(Whole(Requirements, nameof(GearMasterySettings.RequirementAtTopTier), "Skill at the top tier", 0, MaxRequirement,
                "Skill required at the top tier and above. Default 200.",
                s => s.RequirementAtTopTier, (s, v) => s.RequirementAtTopTier = v)),
            Scale(nameof(GearMasterySettings.MeleeRequirementScale), "Melee scale", "melee and throwing weapons (their own weapon skill)",
                s => s.MeleeRequirementScale, (s, v) => s.MeleeRequirementScale = v),
            Scale(nameof(GearMasterySettings.ShieldRequirementScale), "Shield scale", "shields (Athletics)",
                s => s.ShieldRequirementScale, (s, v) => s.ShieldRequirementScale = v),
            Scale(nameof(GearMasterySettings.ArmourRequirementScale), "Armour scale", "armour (Athletics)",
                s => s.ArmourRequirementScale, (s, v) => s.ArmourRequirementScale = v),
            Scale(nameof(GearMasterySettings.RangedRequirementScale), "Ranged scale", "bows, crossbows and slings",
                s => s.RangedRequirementScale, (s, v) => s.RangedRequirementScale = v),
            Scale(nameof(GearMasterySettings.MountRequirementScale), "Mount scale", "horses (Riding)",
                s => s.MountRequirementScale, (s, v) => s.MountRequirementScale = v),

            // ---- Penalty curve ----
            Num(Curve, nameof(GearMasterySettings.PenaltyMidRatio), "Mid point (skill % of requirement)", MenuKind.Percent, 5, 95,
                "Skill as a share of the requirement where every penalty reaches its Mid value. Skill at or above the requirement = no penalty; just below = the Low value. Default 60%.",
                s => s.PenaltyMidRatio, (s, v) => s.PenaltyMidRatio = (float)v, percent: true),
            Num(Curve, nameof(GearMasterySettings.PenaltyHighRatio), "High point (skill % of requirement)", MenuKind.Percent, 1, 90,
                "Skill share where every penalty reaches its High value; held there below it. Must be below the mid point, otherwise both go back to 60% / 30%. Default 30%.",
                s => s.PenaltyHighRatio, (s, v) => s.PenaltyHighRatio = (float)v, percent: true),

            // ---- Starter gear (same ids as when they sat under Ranged: MCM keeps saved values by id) ----
            Whole(Starter, nameof(GearMasterySettings.StarterGearMaxTier), "Starter gear max tier", 0, 3, "Tier 1 gear has no easier option, so its penalties stop here: every item of this tier or lower (the tier the game shows) - weapons, shields, armour, bows, crossbows, slings and horses - has each penalty capped at 'Starter gear max penalty', so a hero with no skill can still learn on it. Default 1.", s => s.StarterGearMaxTier, (s, v) => s.StarterGearMaxTier = v),
            Pct(Starter, nameof(GearMasterySettings.StarterGearMaxPenalty), "Starter gear max penalty", 100, "Tier 1 gear has no easier option, so its penalties stop here: damage, speed, handling, maneuver lost and extra inaccuracy or shield damage taken each stop at this on starter gear (armour: per piece, so a full starter set too). 100% = no cap. Default 20%.", s => s.StarterGearMaxPenalty, (s, v) => s.StarterGearMaxPenalty = v),

            // ---- Melee ----
            Pct(Melee, nameof(GearMasterySettings.MeleeDamagePenaltyLow), "Damage lost - Low", MaxReductionPercent, "Melee and thrown weapon damage lost." + LowHint + " Default 25%.", s => s.MeleeDamagePenaltyLow, (s, v) => s.MeleeDamagePenaltyLow = v),
            Pct(Melee, nameof(GearMasterySettings.MeleeDamagePenaltyMid), "Damage lost - Mid", MaxReductionPercent, "Melee and thrown weapon damage lost." + MidHint + " Default 50%.", s => s.MeleeDamagePenaltyMid, (s, v) => s.MeleeDamagePenaltyMid = v),
            Pct(Melee, nameof(GearMasterySettings.MeleeDamagePenaltyHigh), "Damage lost - High", MaxReductionPercent, "Melee and thrown weapon damage lost." + HighHint + " Default 75%.", s => s.MeleeDamagePenaltyHigh, (s, v) => s.MeleeDamagePenaltyHigh = v),
            Pct(Melee, nameof(GearMasterySettings.MeleeSwingSpeedPenaltyLow), "Swing speed lost - Low", MaxReductionPercent, "Swing, thrust and throw-ready speed lost." + LowHint + " Default 12.5%.", s => s.MeleeSwingSpeedPenaltyLow, (s, v) => s.MeleeSwingSpeedPenaltyLow = v),
            Pct(Melee, nameof(GearMasterySettings.MeleeSwingSpeedPenaltyMid), "Swing speed lost - Mid", MaxReductionPercent, "Swing, thrust and throw-ready speed lost." + MidHint + " Default 25%.", s => s.MeleeSwingSpeedPenaltyMid, (s, v) => s.MeleeSwingSpeedPenaltyMid = v),
            Pct(Melee, nameof(GearMasterySettings.MeleeSwingSpeedPenaltyHigh), "Swing speed lost - High", MaxReductionPercent, "Swing, thrust and throw-ready speed lost." + HighHint + " Default 37.5%.", s => s.MeleeSwingSpeedPenaltyHigh, (s, v) => s.MeleeSwingSpeedPenaltyHigh = v),
            Pct(Melee, nameof(GearMasterySettings.MeleeHandlingPenaltyLow), "Handling lost - Low", MaxReductionPercent, "Weapon handling lost." + LowHint + " Default 12.5%.", s => s.MeleeHandlingPenaltyLow, (s, v) => s.MeleeHandlingPenaltyLow = v),
            Pct(Melee, nameof(GearMasterySettings.MeleeHandlingPenaltyMid), "Handling lost - Mid", MaxReductionPercent, "Weapon handling lost." + MidHint + " Default 25%.", s => s.MeleeHandlingPenaltyMid, (s, v) => s.MeleeHandlingPenaltyMid = v),
            Pct(Melee, nameof(GearMasterySettings.MeleeHandlingPenaltyHigh), "Handling lost - High", MaxReductionPercent, "Weapon handling lost." + HighHint + " Default 37.5%.", s => s.MeleeHandlingPenaltyHigh, (s, v) => s.MeleeHandlingPenaltyHigh = v),

            // ---- Armour ----
            Pct(Armour, nameof(GearMasterySettings.ArmourSpeedPenaltyLow), "Foot speed lost - Low", MaxReductionPercent, ArmourText + LowHint + " Default 20%.", s => s.ArmourSpeedPenaltyLow, (s, v) => s.ArmourSpeedPenaltyLow = v),
            Pct(Armour, nameof(GearMasterySettings.ArmourSpeedPenaltyMid), "Foot speed lost - Mid", MaxReductionPercent, ArmourText + MidHint + " Default 40%.", s => s.ArmourSpeedPenaltyMid, (s, v) => s.ArmourSpeedPenaltyMid = v),
            Pct(Armour, nameof(GearMasterySettings.ArmourSpeedPenaltyHigh), "Foot speed lost - High", MaxReductionPercent, ArmourText + HighHint + " Default 60%.", s => s.ArmourSpeedPenaltyHigh, (s, v) => s.ArmourSpeedPenaltyHigh = v),

            // ---- Shields ----
            Pct(Shields, nameof(GearMasterySettings.ShieldDamageTakenBonusLow), "Extra damage taken - Low", MaxBonusPercent, ShieldText + LowHint + " Default 25%.", s => s.ShieldDamageTakenBonusLow, (s, v) => s.ShieldDamageTakenBonusLow = v),
            Pct(Shields, nameof(GearMasterySettings.ShieldDamageTakenBonusMid), "Extra damage taken - Mid", MaxBonusPercent, ShieldText + MidHint + " Default 50%.", s => s.ShieldDamageTakenBonusMid, (s, v) => s.ShieldDamageTakenBonusMid = v),
            Pct(Shields, nameof(GearMasterySettings.ShieldDamageTakenBonusHigh), "Extra damage taken - High", MaxBonusPercent, ShieldText + HighHint + " Default 75%.", s => s.ShieldDamageTakenBonusHigh, (s, v) => s.ShieldDamageTakenBonusHigh = v),

            // ---- Ranged ----
            Pct(Ranged, nameof(GearMasterySettings.RangedDamagePenaltyLow), "Damage lost - Low", MaxReductionPercent, "Missile damage lost." + LowHint + " Default 25%.", s => s.RangedDamagePenaltyLow, (s, v) => s.RangedDamagePenaltyLow = v),
            Pct(Ranged, nameof(GearMasterySettings.RangedDamagePenaltyMid), "Damage lost - Mid", MaxReductionPercent, "Missile damage lost." + MidHint + " Default 50%.", s => s.RangedDamagePenaltyMid, (s, v) => s.RangedDamagePenaltyMid = v),
            Pct(Ranged, nameof(GearMasterySettings.RangedDamagePenaltyHigh), "Damage lost - High", MaxReductionPercent, "Missile damage lost." + HighHint + " Default 75%.", s => s.RangedDamagePenaltyHigh, (s, v) => s.RangedDamagePenaltyHigh = v),
            Pct(Ranged, nameof(GearMasterySettings.RangedReloadSpeedPenaltyLow), "Reload and draw speed lost - Low", MaxReductionPercent, "Reload and draw speed lost." + LowHint + " Default 12.5%.", s => s.RangedReloadSpeedPenaltyLow, (s, v) => s.RangedReloadSpeedPenaltyLow = v),
            Pct(Ranged, nameof(GearMasterySettings.RangedReloadSpeedPenaltyMid), "Reload and draw speed lost - Mid", MaxReductionPercent, "Reload and draw speed lost." + MidHint + " Default 25%.", s => s.RangedReloadSpeedPenaltyMid, (s, v) => s.RangedReloadSpeedPenaltyMid = v),
            Pct(Ranged, nameof(GearMasterySettings.RangedReloadSpeedPenaltyHigh), "Reload and draw speed lost - High", MaxReductionPercent, "Reload and draw speed lost." + HighHint + " Default 37.5%.", s => s.RangedReloadSpeedPenaltyHigh, (s, v) => s.RangedReloadSpeedPenaltyHigh = v),
            Pct(Ranged, nameof(GearMasterySettings.RangedInaccuracyBonusLow), "Extra inaccuracy - Low", MaxBonusPercent, "Extra aim spread." + LowHint + " Default 25%.", s => s.RangedInaccuracyBonusLow, (s, v) => s.RangedInaccuracyBonusLow = v),
            Pct(Ranged, nameof(GearMasterySettings.RangedInaccuracyBonusMid), "Extra inaccuracy - Mid", MaxBonusPercent, "Extra aim spread." + MidHint + " Default 50%.", s => s.RangedInaccuracyBonusMid, (s, v) => s.RangedInaccuracyBonusMid = v),
            Pct(Ranged, nameof(GearMasterySettings.RangedInaccuracyBonusHigh), "Extra inaccuracy - High", MaxBonusPercent, "Extra aim spread." + HighHint + " Default 75%.", s => s.RangedInaccuracyBonusHigh, (s, v) => s.RangedInaccuracyBonusHigh = v),

            // ---- Mounts ----
            Pct(Mounts, nameof(GearMasterySettings.MountSpeedPenaltyLow), "Speed lost - Low", MaxReductionPercent, "Horse speed lost (the rider's Riding vs the horse)." + LowHint + " Default 25%.", s => s.MountSpeedPenaltyLow, (s, v) => s.MountSpeedPenaltyLow = v),
            Pct(Mounts, nameof(GearMasterySettings.MountSpeedPenaltyMid), "Speed lost - Mid", MaxReductionPercent, "Horse speed lost (the rider's Riding vs the horse)." + MidHint + " Default 50%.", s => s.MountSpeedPenaltyMid, (s, v) => s.MountSpeedPenaltyMid = v),
            Pct(Mounts, nameof(GearMasterySettings.MountSpeedPenaltyHigh), "Speed lost - High", MaxReductionPercent, "Horse speed lost (the rider's Riding vs the horse)." + HighHint + " Default 75%.", s => s.MountSpeedPenaltyHigh, (s, v) => s.MountSpeedPenaltyHigh = v),
            Pct(Mounts, nameof(GearMasterySettings.MountManeuverPenaltyLow), "Maneuver lost - Low", MaxReductionPercent, "Horse maneuver lost (the rider's Riding vs the horse)." + LowHint + " Default 25%.", s => s.MountManeuverPenaltyLow, (s, v) => s.MountManeuverPenaltyLow = v),
            Pct(Mounts, nameof(GearMasterySettings.MountManeuverPenaltyMid), "Maneuver lost - Mid", MaxReductionPercent, "Horse maneuver lost (the rider's Riding vs the horse)." + MidHint + " Default 50%.", s => s.MountManeuverPenaltyMid, (s, v) => s.MountManeuverPenaltyMid = v),
            Pct(Mounts, nameof(GearMasterySettings.MountManeuverPenaltyHigh), "Maneuver lost - High", MaxReductionPercent, "Horse maneuver lost (the rider's Riding vs the horse)." + HighHint + " Default 75%.", s => s.MountManeuverPenaltyHigh, (s, v) => s.MountManeuverPenaltyHigh = v),

            // ---- Debug ----
            Flag(Debug, nameof(GearMasterySettings.DumpRequirementsCsv), "Write requirements CSV",
                "At each game start, write item_requirements.csv (every item's tier, skill and requirement) to Documents\\Mount and Blade II Bannerlord\\Configs\\GearMastery. Default off.",
                s => s.DumpRequirementsCsv, (s, v) => s.DumpRequirementsCsv = v),
        };

        private const string ArmourText = "Foot speed lost, per armour piece by its own requirement, shared by armour weight (the whole set this short = this value).";
        private const string ShieldText = "Extra damage the shield itself takes (Athletics vs the shield).";

        /// <summary>The settings of one group, in menu order.</summary>
        public static IEnumerable<MenuSetting> InGroup(MenuGroup group) => All.Where(s => s.Group == group);

        /// <summary>The setting with this id, or null.</summary>
        public static MenuSetting? Find(string id) => All.FirstOrDefault(s => s.Id == id);

        // ------------------------------------------------------------------ builders

        private static MenuSetting Flag(MenuGroup g, string id, string label, string hint,
            Func<GearMasterySettings, bool> get, Action<GearMasterySettings, bool> set) =>
            new MenuSetting(id, label, g, hint, MenuKind.Bool, 0, 1, false, s => get(s) ? 1 : 0, (s, v) => set(s, v != 0));

        private static MenuSetting Whole(MenuGroup g, string id, string label, double min, double max, string hint,
            Func<GearMasterySettings, int> get, Action<GearMasterySettings, int> set) =>
            new MenuSetting(id, label, g, hint, MenuKind.Int, min, max, false, s => get(s), (s, v) => set(s, (int)v));

        private static MenuSetting Num(MenuGroup g, string id, string label, MenuKind kind, double min, double max, string hint,
            Func<GearMasterySettings, float> get, Action<GearMasterySettings, double> set, bool percent = false) =>
            percent
                ? new MenuSetting(id, label, g, hint, kind, min, max, false, s => Math.Round(get(s) * 100.0, 4), (s, v) => set(s, v / 100.0))
                : new MenuSetting(id, label, g, hint, kind, min, max, false, s => Math.Round(get(s), 4), set);

        private static MenuSetting Pct(MenuGroup g, string id, string label, double maxPercent, string hint,
            Func<GearMasterySettings, float> get, Action<GearMasterySettings, float> set) =>
            Num(g, id, label, MenuKind.Percent, 0, maxPercent, hint, get, (s, v) => set(s, (float)v), percent: true);

        private static MenuSetting Req(MenuSetting m) =>
            new MenuSetting(m.Id, m.Label, m.Group, m.Hint, m.Kind, m.Min, m.Max, true, m.Get, (s, v) => m.Set(s, v));

        private static MenuSetting Tier(int tier, Func<GearMasterySettings, int> get, Action<GearMasterySettings, int> set, int def) =>
            Req(Whole(Requirements, "RequirementAtTier" + tier, "Skill at tier " + tier, 0, MaxRequirement,
                "Skill required by an item of tier " + tier + " (the item's exact tier, between the table's points the requirement runs in a straight line; rounded to 5, times the category scale)."
                + (tier == 6 ? " Vanilla already shows 'Tier 6' from 5.5." : "") + " Default " + def + ".",
                get, set));

        private static MenuSetting Scale(string id, string label, string what,
            Func<GearMasterySettings, float> get, Action<GearMasterySettings, float> set) =>
            Req(Num(Requirements, id, label, MenuKind.Float, 0, 3,
                "Multiplier on the tier table for " + what + ". 0 = no requirement at all. Default 1.00.",
                get, (s, v) => set(s, (float)v)));
    }
}
