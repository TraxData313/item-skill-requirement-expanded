namespace GearMastery.Core
{
    /// <summary>
    /// Every tunable number of the mod. Penalty defaults are deliberately MILD (Toni, 2026-11-05): an expensive sword
    /// in unskilled hands should be about as good as a standard sword, not useless. The maths in
    /// <see cref="Requirements"/> and <see cref="Penalties"/> clamps nonsense values itself (negative scale, penalty
    /// above 100%), so a hand-edited settings file can never produce a negative multiplier.
    /// </summary>
    public sealed class GearMasterySettings
    {
        // ---- Requirements: one tier table for every category (round 2, Toni 2026-11-05) ----
        // R = round-to-5(table(item.Tierf) x category scale). The table is piecewise-linear through the points below,
        // held flat outside them. Vanilla shows "Tier N" for round(Tierf) clamped to 0..6, so displayed "Tier 6" is
        // Tierf 5.5 and up (body armour reaches ~9) - it asks 140 at its low edge, 150 at 6.0, 200 from TopTier on.
        // Plain properties on purpose: step 8 binds them to MCM one by one.

        /// <summary>Requirement at Tierf 0 (and below).</summary>
        public int RequirementAtTier0 { get; set; } = 0;

        /// <summary>Requirement at Tierf 1.</summary>
        public int RequirementAtTier1 { get; set; } = 20;

        /// <summary>Requirement at Tierf 2.</summary>
        public int RequirementAtTier2 { get; set; } = 45;

        /// <summary>Requirement at Tierf 3.</summary>
        public int RequirementAtTier3 { get; set; } = 75;

        /// <summary>Requirement at Tierf 4.</summary>
        public int RequirementAtTier4 { get; set; } = 100;

        /// <summary>Requirement at Tierf 5.</summary>
        public int RequirementAtTier5 { get; set; } = 125;

        /// <summary>Requirement at Tierf 6.</summary>
        public int RequirementAtTier6 { get; set; } = 150;

        /// <summary>Tierf of the last table point (the best of the best). Must be above 6; anything at or above it asks
        /// <see cref="RequirementAtTopTier"/>.</summary>
        public float TopTier { get; set; } = 7.5f;

        /// <summary>Requirement at <see cref="TopTier"/> and above.</summary>
        public int RequirementAtTopTier { get; set; } = 200;

        /// <summary>Multiplier on the table for melee and throwing weapons.</summary>
        public float MeleeRequirementScale { get; set; } = 1f;

        /// <summary>Multiplier on the table for shields.</summary>
        public float ShieldRequirementScale { get; set; } = 1f;

        /// <summary>Multiplier on the table for armour.</summary>
        public float ArmourRequirementScale { get; set; } = 1f;

        /// <summary>Multiplier on the table for bows, crossbows and slings.</summary>
        public float RangedRequirementScale { get; set; } = 1f;

        /// <summary>Multiplier on the table for horses.</summary>
        public float MountRequirementScale { get; set; } = 1f;

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

        // ---- Bows / crossbows / slings ----

        /// <summary>Missile damage lost at full shortfall.</summary>
        public float MaxRangedDamagePenalty { get; set; } = 0.15f;

        /// <summary>Reload / draw speed lost at full shortfall.</summary>
        public float MaxReloadSpeedPenalty { get; set; } = 0.10f;

        /// <summary>Extra inaccuracy at full shortfall (0.30 = +30%).</summary>
        public float MaxInaccuracyBonus { get; set; } = 0.30f;

        // ---- Horses (Riding) ----

        /// <summary>Mount speed lost at full shortfall.</summary>
        public float MaxHorseSpeedPenalty { get; set; } = 0.10f;

        /// <summary>Mount maneuver lost at full shortfall.</summary>
        public float MaxHorseManeuverPenalty { get; set; } = 0.10f;

        // ---- Scope / debug ----

        /// <summary>When true, only heroes are penalised (troops fight as in vanilla).</summary>
        public bool HeroesOnly { get; set; }

        /// <summary>Write the item requirement CSV at game start even outside the dev install.</summary>
        public bool DumpRequirementsCsv { get; set; }

        /// <summary>The category's multiplier on the tier table.</summary>
        public float ScaleFor(SkillKind kind) => kind switch
        {
            SkillKind.Weapon => MeleeRequirementScale,
            SkillKind.Shield => ShieldRequirementScale,
            SkillKind.Armour => ArmourRequirementScale,
            SkillKind.Ranged => RangedRequirementScale,
            SkillKind.Mount => MountRequirementScale,
            _ => 0f,
        };

        /// <summary>The tier table as (Tierf, R) points, in the order given (Requirements sorts defensively).</summary>
        public TierPoint[] TierTable() => new[]
        {
            new TierPoint(0f, RequirementAtTier0),
            new TierPoint(1f, RequirementAtTier1),
            new TierPoint(2f, RequirementAtTier2),
            new TierPoint(3f, RequirementAtTier3),
            new TierPoint(4f, RequirementAtTier4),
            new TierPoint(5f, RequirementAtTier5),
            new TierPoint(6f, RequirementAtTier6),
            new TierPoint(TopTier, RequirementAtTopTier),
        };
    }

    /// <summary>One point of the requirement tier table: at item Tierf <see cref="Tier"/> the requirement is
    /// <see cref="Requirement"/>.</summary>
    public readonly struct TierPoint
    {
        public TierPoint(float tier, float requirement)
        {
            Tier = tier;
            Requirement = requirement;
        }

        public float Tier { get; }
        public float Requirement { get; }
    }

    /// <summary>Which family an item's requirement belongs to: decides where R comes from and which penalties apply.</summary>
    public enum SkillKind
    {
        /// <summary>Melee or throwing weapon; skill per usage (One Handed / Two Handed / Polearm / Throwing).</summary>
        Weapon,

        /// <summary>Shield (Athletics).</summary>
        Shield,

        /// <summary>Head, body, leg, hand armour or cape (Athletics).</summary>
        Armour,

        /// <summary>Bow, crossbow or sling (Bow / Crossbow / Throwing).</summary>
        Ranged,

        /// <summary>Horse (Riding).</summary>
        Mount,
    }
}
