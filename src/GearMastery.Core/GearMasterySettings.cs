namespace GearMastery.Core
{
    /// <summary>
    /// Every tunable number of the mod. Penalty defaults are deliberately MILD (Toni, 2026-11-05): an expensive sword
    /// in unskilled hands should be about as good as a standard sword, not useless. The maths in
    /// <see cref="Requirements"/> and <see cref="Penalties"/> clamps nonsense values itself (negative slope, penalty
    /// above 100%), so a hand-edited settings file can never produce a negative multiplier.
    /// </summary>
    public sealed class GearMasterySettings
    {
        // ---- Requirements (tier formula: melee, throwing, shields, armour) ----

        /// <summary>Skill points per item tier for melee weapons and throwing weapons (vanilla bows sit at ~12 x tier).</summary>
        public float WeaponSlope { get; set; } = 12f;

        /// <summary>Skill points per item tier for shields.</summary>
        public float ShieldSlope { get; set; } = 12f;

        /// <summary>Skill points per item tier for armour (10, else heavy body armour would ask ~110 Athletics).</summary>
        public float ArmourSlope { get; set; } = 10f;

        /// <summary>Highest requirement a weapon or shield can ask. Only modded items with absurd tiers reach it.</summary>
        public int WeaponCap { get; set; } = 100;

        /// <summary>Highest Athletics an armour piece can ask.</summary>
        public int ArmourCap { get; set; } = 100;

        // ---- Penalty curve ----

        /// <summary>
        /// Penalties scale with shortfall^exponent. 0.5 (concave) makes a moderate gap bite: skill 30 on an R 90 item
        /// (shortfall 0.67) already pays 82% of the maximum. 1 = linear. Values &lt;= 0 fall back to 1.
        /// </summary>
        public float PenaltyExponent { get; set; } = 0.5f;

        // ---- Melee / throwing weapons ----

        /// <summary>Weapon damage lost at full shortfall (0.15 = -15%).</summary>
        public float MaxWeaponDamagePenalty { get; set; } = 0.15f;

        /// <summary>Weapon swing (and thrust) speed lost at full shortfall.</summary>
        public float MaxSwingSpeedPenalty { get; set; } = 0.10f;

        /// <summary>Weapon handling lost at full shortfall.</summary>
        public float MaxHandlingPenalty { get; set; } = 0.15f;

        // ---- Shields ----

        /// <summary>Extra damage the shield takes at full shortfall (0.25 = +25%).</summary>
        public float MaxShieldDamageTakenBonus { get; set; } = 0.25f;

        // ---- Armour ----

        /// <summary>Foot max speed lost when ALL worn armour is at full shortfall.</summary>
        public float MaxArmourSpeedPenalty { get; set; } = 0.08f;

        // ---- Bows / crossbows / slings (R = vanilla item.Difficulty) ----

        /// <summary>Missile damage lost at full shortfall.</summary>
        public float MaxRangedDamagePenalty { get; set; } = 0.15f;

        /// <summary>Reload / draw speed lost at full shortfall.</summary>
        public float MaxReloadSpeedPenalty { get; set; } = 0.10f;

        /// <summary>Extra inaccuracy at full shortfall (0.30 = +30%).</summary>
        public float MaxInaccuracyBonus { get; set; } = 0.30f;

        // ---- Horses (R = vanilla item.Difficulty, Riding) ----

        /// <summary>Mount speed lost at full shortfall.</summary>
        public float MaxHorseSpeedPenalty { get; set; } = 0.10f;

        /// <summary>Mount maneuver lost at full shortfall.</summary>
        public float MaxHorseManeuverPenalty { get; set; } = 0.10f;

        // ---- Scope / debug ----

        /// <summary>When true, only heroes are penalised (troops fight as in vanilla).</summary>
        public bool HeroesOnly { get; set; }

        /// <summary>Write the item requirement CSV at game start even outside the dev install.</summary>
        public bool DumpRequirementsCsv { get; set; }

        /// <summary>Slope for the tier formula; 0 for kinds whose requirement is vanilla's Difficulty.</summary>
        public float SlopeFor(SkillKind kind) => kind switch
        {
            SkillKind.Weapon => WeaponSlope,
            SkillKind.Shield => ShieldSlope,
            SkillKind.Armour => ArmourSlope,
            _ => 0f,
        };

        public int CapFor(SkillKind kind) => kind switch
        {
            SkillKind.Weapon => WeaponCap,
            SkillKind.Shield => WeaponCap,
            SkillKind.Armour => ArmourCap,
            _ => 0,
        };
    }

    /// <summary>Which family an item's requirement belongs to: decides where R comes from and which penalties apply.</summary>
    public enum SkillKind
    {
        /// <summary>Melee or throwing weapon (One Handed / Two Handed / Polearm / Throwing). R from tier.</summary>
        Weapon,

        /// <summary>Shield (One Handed). R from tier.</summary>
        Shield,

        /// <summary>Head, body, leg, hand armour or cape (Athletics). R from tier.</summary>
        Armour,

        /// <summary>Bow, crossbow or sling (Bow / Crossbow / Throwing). R = vanilla item.Difficulty.</summary>
        Ranged,

        /// <summary>Horse (Riding). R = vanilla item.Difficulty.</summary>
        Mount,
    }
}
