namespace ItemSkillRequirementExpanded.Core
{
    /// <summary>
    /// Every tunable number of the mod (round 2: penalties bite - an item one or two tiers above your skill must come
    /// out WORSE than the one you meet, Toni 2026-11-05). The maths in
    /// <see cref="Requirements"/> and <see cref="Penalties"/> clamps nonsense values itself (negative scale, penalty
    /// above 100%, anchor ratios out of order), so no value can ever produce a negative multiplier. Shown and edited in MCM
    /// through <see cref="SettingsMenu"/> (a new setting = a property here + a line in <see cref="SettingsMenu.All"/>).
    /// </summary>
    public sealed class SkillRequirementSettings
    {
        // ---- Requirements: one tier table for every category (round 2, Toni 2026-11-05) ----
        // R = round-to-5(table(item.Tierf) x category scale). The table is piecewise-linear through the points below,
        // held flat outside them. Vanilla shows "Tier N" for round(Tierf) clamped to 0..6, so displayed "Tier 6" is
        // Tierf 5.5 and up (body armour reaches ~9) - it asks 140 at its low edge, 150 at 6.0, 200 from TopTier on.
        // Plain properties on purpose: SettingsMenu binds them to MCM one by one.

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

        // ---- Penalty curve (round 2, Toni 2026-11-05) ----
        // Skill >= R: no penalty at all (the snap). Below it, by the ratio r = skill / R the penalty runs in straight
        // lines through three anchors: r just under 1 -> Low, r = PenaltyMidRatio -> Mid, r = PenaltyHighRatio -> High,
        // held at High below that. One Low / Mid / High triple per penalised stat, as fractions (0.25 = 25%).

        /// <summary>Ratio skill / R at which every penalty reaches its Mid value.</summary>
        public float PenaltyMidRatio { get; set; } = 0.6f;

        /// <summary>Ratio skill / R at (and below) which every penalty reaches its High value. Must be below
        /// <see cref="PenaltyMidRatio"/>, else both ratios fall back to 0.6 / 0.3.</summary>
        public float PenaltyHighRatio { get; set; } = 0.3f;

        /// <summary>Melee / thrown weapon damage lost: just below the requirement.</summary>
        public float MeleeDamagePenaltyLow { get; set; } = 0.25f;

        /// <summary>Melee / thrown weapon damage lost: at the mid ratio.</summary>
        public float MeleeDamagePenaltyMid { get; set; } = 0.5f;

        /// <summary>Melee / thrown weapon damage lost: at the high ratio and below.</summary>
        public float MeleeDamagePenaltyHigh { get; set; } = 0.75f;

        /// <summary>Melee swing (and thrust / throw-ready) speed lost: just below the requirement.</summary>
        public float MeleeSwingSpeedPenaltyLow { get; set; } = 0.125f;

        /// <summary>Melee swing (and thrust / throw-ready) speed lost: at the mid ratio.</summary>
        public float MeleeSwingSpeedPenaltyMid { get; set; } = 0.25f;

        /// <summary>Melee swing (and thrust / throw-ready) speed lost: at the high ratio and below.</summary>
        public float MeleeSwingSpeedPenaltyHigh { get; set; } = 0.375f;

        /// <summary>Melee weapon handling lost: just below the requirement.</summary>
        public float MeleeHandlingPenaltyLow { get; set; } = 0.125f;

        /// <summary>Melee weapon handling lost: at the mid ratio.</summary>
        public float MeleeHandlingPenaltyMid { get; set; } = 0.25f;

        /// <summary>Melee weapon handling lost: at the high ratio and below.</summary>
        public float MeleeHandlingPenaltyHigh { get; set; } = 0.375f;

        /// <summary>Extra damage the shield itself takes (Athletics vs the shield's R): just below the requirement.</summary>
        public float ShieldDamageTakenBonusLow { get; set; } = 0.25f;

        /// <summary>Extra damage the shield itself takes (Athletics vs the shield's R): at the mid ratio.</summary>
        public float ShieldDamageTakenBonusMid { get; set; } = 0.5f;

        /// <summary>Extra damage the shield itself takes (Athletics vs the shield's R): at the high ratio and below.</summary>
        public float ShieldDamageTakenBonusHigh { get; set; } = 0.75f;

        /// <summary>Foot max speed lost, per worn armour piece by its own ratio, then shared by armour weight (the whole set at the high anchor = the high value): just below the requirement.</summary>
        public float ArmourSpeedPenaltyLow { get; set; } = 0.2f;

        /// <summary>Foot max speed lost, per worn armour piece by its own ratio, then shared by armour weight (the whole set at the high anchor = the high value): at the mid ratio.</summary>
        public float ArmourSpeedPenaltyMid { get; set; } = 0.4f;

        /// <summary>Foot max speed lost, per worn armour piece by its own ratio, then shared by armour weight (the whole set at the high anchor = the high value): at the high ratio and below.</summary>
        public float ArmourSpeedPenaltyHigh { get; set; } = 0.6f;

        /// <summary>Bow / crossbow / sling missile damage lost: just below the requirement.</summary>
        public float RangedDamagePenaltyLow { get; set; } = 0.25f;

        /// <summary>Bow / crossbow / sling missile damage lost: at the mid ratio.</summary>
        public float RangedDamagePenaltyMid { get; set; } = 0.5f;

        /// <summary>Bow / crossbow / sling missile damage lost: at the high ratio and below.</summary>
        public float RangedDamagePenaltyHigh { get; set; } = 0.75f;

        /// <summary>Bow / crossbow / sling reload and draw speed lost: just below the requirement.</summary>
        public float RangedReloadSpeedPenaltyLow { get; set; } = 0.125f;

        /// <summary>Bow / crossbow / sling reload and draw speed lost: at the mid ratio.</summary>
        public float RangedReloadSpeedPenaltyMid { get; set; } = 0.25f;

        /// <summary>Bow / crossbow / sling reload and draw speed lost: at the high ratio and below.</summary>
        public float RangedReloadSpeedPenaltyHigh { get; set; } = 0.375f;

        /// <summary>Extra bow / crossbow / sling inaccuracy: just below the requirement.</summary>
        public float RangedInaccuracyBonusLow { get; set; } = 0.25f;

        /// <summary>Extra bow / crossbow / sling inaccuracy: at the mid ratio.</summary>
        public float RangedInaccuracyBonusMid { get; set; } = 0.5f;

        /// <summary>Extra bow / crossbow / sling inaccuracy: at the high ratio and below.</summary>
        public float RangedInaccuracyBonusHigh { get; set; } = 0.75f;

        /// <summary>
        /// Starter launchers (Toni, 2026-11-05: there are no tier 0 bows or crossbows, so a hero with no skill had no
        /// way to start learning without the full penalty): bows / crossbows / slings whose displayed tier
        /// (round(Tierf) clamped 0..6) is at most this have every penalty capped at <see cref="StarterGearMaxPenalty"/>.
        /// </summary>
        public int StarterGearMaxTier { get; set; } = 1;

        /// <summary>The cap on each penalty of a starter launcher (damage and reload / draw speed lost, extra
        /// inaccuracy), as a fraction: 0.2 = at most -20% / +20%. 1 or more = no cap.</summary>
        public float StarterGearMaxPenalty { get; set; } = 0.2f;

        /// <summary>Horse speed lost (rider's Riding vs the horse's R): just below the requirement.</summary>
        public float MountSpeedPenaltyLow { get; set; } = 0.25f;

        /// <summary>Horse speed lost (rider's Riding vs the horse's R): at the mid ratio.</summary>
        public float MountSpeedPenaltyMid { get; set; } = 0.5f;

        /// <summary>Horse speed lost (rider's Riding vs the horse's R): at the high ratio and below.</summary>
        public float MountSpeedPenaltyHigh { get; set; } = 0.75f;

        /// <summary>Horse maneuver lost (rider's Riding vs the horse's R): just below the requirement.</summary>
        public float MountManeuverPenaltyLow { get; set; } = 0.25f;

        /// <summary>Horse maneuver lost (rider's Riding vs the horse's R): at the mid ratio.</summary>
        public float MountManeuverPenaltyMid { get; set; } = 0.5f;

        /// <summary>Horse maneuver lost (rider's Riding vs the horse's R): at the high ratio and below.</summary>
        public float MountManeuverPenaltyHigh { get; set; } = 0.75f;

        // ---- Who pays (never troops: non-hero agents always fight as in vanilla) ----

        /// <summary>The player's own hero.</summary>
        public bool ApplyToPlayer { get; set; } = true;

        /// <summary>Heroes of the player's clan other than the player (companions, family).</summary>
        public bool ApplyToCompanions { get; set; } = true;

        /// <summary>Every other hero: lords, ladies and wanderers, friendly and enemy (custom battle heroes too).</summary>
        public bool ApplyToOtherHeroes { get; set; } = true;

        /// <summary>Penalties in tournament fights (off = everyone fights a tournament as in vanilla).</summary>
        public bool ApplyInTournaments { get; set; } = true;

        // ---- Inventory ----

        /// <summary>Tint inventory list rows orange when the selected hero's skill is below the item's requirement
        /// (<see cref="InventoryHighlight"/>). Vanilla's red "can't use" wins.</summary>
        public bool HighlightItemsAboveSkill { get; set; } = true;

        // ---- Debug ----

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
