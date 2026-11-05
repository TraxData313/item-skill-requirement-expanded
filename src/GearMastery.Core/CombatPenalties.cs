namespace GearMastery.Core
{
    /// <summary>Who an agent is, for the who-filter (<see cref="CombatPenalties.AppliesTo"/>).</summary>
    public enum PenaltyTarget
    {
        /// <summary>A non-hero agent: never penalised.</summary>
        Troop,

        /// <summary>The player's own hero (campaign) or the player's character (custom battle).</summary>
        Player,

        /// <summary>A hero of the player's clan other than the player.</summary>
        Companion,

        /// <summary>Any other hero: lords, ladies, wanderers, friendly and enemy.</summary>
        OtherHero,
    }

    /// <summary>
    /// The multipliers the stat decorator applies for the item an agent WIELDS (main hand), on top of the base
    /// model's values. All 1 for an item that is not a weapon / launcher or has no shortfall.
    /// </summary>
    public readonly struct WieldedMultipliers
    {
        public WieldedMultipliers(float damage, float swing, float thrustOrRangedReady, float handling, float reload, float inaccuracy)
        {
            Damage = damage;
            Swing = swing;
            ThrustOrRangedReady = thrustOrRangedReady;
            Handling = handling;
            Reload = reload;
            Inaccuracy = inaccuracy;
        }

        public static WieldedMultipliers Identity => new WieldedMultipliers(1f, 1f, 1f, 1f, 1f, 1f);

        /// <summary>Weapon damage (GetWeaponDamageMultiplier).</summary>
        public float Damage { get; }

        /// <summary>AgentDrivenProperties.SwingSpeedMultiplier.</summary>
        public float Swing { get; }

        /// <summary>AgentDrivenProperties.ThrustOrRangedReadySpeedMultiplier (melee thrust; bow draw / throw ready).</summary>
        public float ThrustOrRangedReady { get; }

        /// <summary>AgentDrivenProperties.HandlingMultiplier.</summary>
        public float Handling { get; }

        /// <summary>AgentDrivenProperties.ReloadSpeed.</summary>
        public float Reload { get; }

        /// <summary>AgentDrivenProperties.WeaponInaccuracy and GetWeaponInaccuracy.</summary>
        public float Inaccuracy { get; }

        public bool IsIdentity =>
            Damage == 1f && Swing == 1f && ThrustOrRangedReady == 1f && Handling == 1f && Reload == 1f && Inaccuracy == 1f;
    }

    /// <summary>
    /// Which penalties hit which item family in battle, and the small rules around them. Pure, so the module's
    /// decorator models stay thin.
    /// </summary>
    /// <remarks>
    /// Every function that takes a <c>displayedTier</c> (vanilla's "Tier N" of the item) applies the starter cap
    /// (<see cref="StarterCap"/>): null = no cap. The battle models and the tooltip pass the same tier, so they agree.
    /// </remarks>
    public static class CombatPenalties
    {
        /// <summary>
        /// Does this agent pay penalties at all? Troops never do (they keep vanilla, locks included); the player,
        /// companions (player clan) and other heroes each by their toggle; in a tournament only when
        /// <see cref="GearMasterySettings.ApplyInTournaments"/>.
        /// </summary>
        public static bool AppliesTo(PenaltyTarget who, bool inTournament, GearMasterySettings settings)
        {
            if (settings == null) return false;
            if (inTournament && !settings.ApplyInTournaments) return false;
            return who switch
            {
                PenaltyTarget.Player => settings.ApplyToPlayer,
                PenaltyTarget.Companion => settings.ApplyToCompanions,
                PenaltyTarget.OtherHero => settings.ApplyToOtherHeroes,
                _ => false,
            };
        }

        /// <summary>
        /// Is this usage of a melee-family item a THROWN one (javelin / throwing axe / knife in its throwing mode)?
        /// Kind Weapon and the usage carries the RangedWeapon flag. Thrown usages are judged by the ranged anchors
        /// (damage + aim), not the melee swing / handling ones, which mean nothing for a throw (round 2, step 9).
        /// </summary>
        public static bool IsThrownUsage(SkillKind kind, bool usageIsRanged) => kind == SkillKind.Weapon && usageIsRanged;

        /// <summary>
        /// The wielded item's multipliers by its kind:
        /// - Weapon, melee usage: damage, swing speed, thrust speed = the swing multiplier, handling.
        /// - Weapon, THROWN usage (<paramref name="thrownUsage"/>): ranged damage and ranged inaccuracy only (no reload,
        ///   no ready / swing / handling penalty).
        /// - Ranged (bow / crossbow / sling): damage, reload, draw (ThrustOrRangedReady) = the reload multiplier, inaccuracy.
        /// - Shield / Armour / Mount: nothing here (they have their own hooks).
        /// Starter gear (<paramref name="displayedTier"/>) has each one capped (<see cref="StarterCap"/>).
        /// </summary>
        public static WieldedMultipliers ForWielded(SkillKind kind, float shortfall, GearMasterySettings settings, bool thrownUsage = false, int? displayedTier = null)
        {
            if (settings == null || !(shortfall > 0f)) return WieldedMultipliers.Identity;
            float cap = StarterCap(displayedTier, settings);
            switch (kind)
            {
                case SkillKind.Weapon when thrownUsage:
                    return new WieldedMultipliers(
                        CapLoss(Penalties.RangedDamageMultiplier(shortfall, settings), cap),
                        1f, 1f, 1f, 1f,
                        CapGain(Penalties.InaccuracyMultiplier(shortfall, settings), cap));
                case SkillKind.Weapon:
                {
                    float swing = CapLoss(Penalties.SwingSpeedMultiplier(shortfall, settings), cap);
                    return new WieldedMultipliers(
                        CapLoss(Penalties.WeaponDamageMultiplier(shortfall, settings), cap),
                        swing,
                        swing,
                        CapLoss(Penalties.HandlingMultiplier(shortfall, settings), cap),
                        1f,
                        1f);
                }
                case SkillKind.Ranged:
                {
                    float reload = CapLoss(Penalties.ReloadSpeedMultiplier(shortfall, settings), cap);
                    return new WieldedMultipliers(
                        CapLoss(Penalties.RangedDamageMultiplier(shortfall, settings), cap),
                        1f,
                        reload,
                        1f,
                        reload,
                        CapGain(Penalties.InaccuracyMultiplier(shortfall, settings), cap));
                }
                default:
                    return WieldedMultipliers.Identity;
            }
        }

        /// <summary>
        /// The damage multiplier for a blow: a melee weapon's own requirement (kind Weapon, melee anchors); a THROWN
        /// usage of a Weapon-kind item (javelin thrown) its own requirement with the RANGED damage anchors; a missile
        /// that is AMMO (arrow, bolt, sling stone) the requirement of the launcher in hand (kind Ranged). Anything else 1.
        /// </summary>
        public static float DamageMultiplier(SkillKind kind, bool blowIsAmmo, float shortfall, GearMasterySettings settings, bool thrownUsage = false, int? displayedTier = null)
        {
            if (settings == null || !(shortfall > 0f)) return 1f;
            float cap = StarterCap(displayedTier, settings);
            if (blowIsAmmo)
                return kind == SkillKind.Ranged ? CapLoss(Penalties.RangedDamageMultiplier(shortfall, settings), cap) : 1f;
            if (kind != SkillKind.Weapon) return 1f;
            return CapLoss(thrownUsage ? Penalties.RangedDamageMultiplier(shortfall, settings) : Penalties.WeaponDamageMultiplier(shortfall, settings), cap);
        }

        /// <summary>The inaccuracy multiplier for a ranged usage: launchers (kind Ranged) and thrown usages of
        /// Weapon-kind items; 1 for everything else.</summary>
        public static float InaccuracyMultiplier(SkillKind kind, bool thrownUsage, float shortfall, GearMasterySettings settings, int? displayedTier = null)
        {
            if (settings == null || !(shortfall > 0f)) return 1f;
            if (kind != SkillKind.Ranged && !IsThrownUsage(kind, thrownUsage)) return 1f;
            return CapGain(Penalties.InaccuracyMultiplier(shortfall, settings), StarterCap(displayedTier, settings));
        }

        /// <summary>Multiplier on the damage an under-skilled shield takes, starter cap included.</summary>
        public static float ShieldDamageTakenMultiplier(float shortfall, GearMasterySettings settings, int? displayedTier = null)
        {
            if (settings == null || !(shortfall > 0f)) return 1f;
            return CapGain(Penalties.ShieldDamageTakenMultiplier(shortfall, settings), StarterCap(displayedTier, settings));
        }

        /// <summary>Multiplier on an under-skilled rider's horse speed, starter cap included.</summary>
        public static float HorseSpeedMultiplier(float shortfall, GearMasterySettings settings, int? displayedTier = null)
        {
            if (settings == null || !(shortfall > 0f)) return 1f;
            return CapLoss(Penalties.HorseSpeedMultiplier(shortfall, settings), StarterCap(displayedTier, settings));
        }

        /// <summary>Multiplier on an under-skilled rider's horse maneuver, starter cap included.</summary>
        public static float HorseManeuverMultiplier(float shortfall, GearMasterySettings settings, int? displayedTier = null)
        {
            if (settings == null || !(shortfall > 0f)) return 1f;
            return CapLoss(Penalties.HorseManeuverMultiplier(shortfall, settings), StarterCap(displayedTier, settings));
        }

        /// <summary>
        /// One worn armour piece for <see cref="Penalties.ArmourSpeedMultiplier"/>, its own foot speed penalty capped
        /// BEFORE the weight-share aggregation - so a full starter set tops out at the cap in total too.
        /// </summary>
        public static ArmourPiece ArmourPieceFor(float weight, float shortfall, GearMasterySettings settings, int? displayedTier = null) =>
            new ArmourPiece(weight, shortfall, StarterCap(displayedTier, settings));

        /// <summary>
        /// The starter cap for an item, as the most any one penalty may take (0.2 = -20% damage / speed, +20%
        /// inaccuracy / shield damage taken); 1 = no cap. Every item whose displayed tier is at most
        /// <see cref="GearMasterySettings.StarterGearMaxTier"/> is starter gear, in every category: tier 1 gear has no
        /// easier option (there are no tier 0 bows, spears, ...), so without it a hero at skill 0 had no way to start
        /// learning (Toni, 2026-11-05). A cap of 100% or more changes nothing; the snap at skill &gt;= R is untouched
        /// (no shortfall, no penalty to cap).
        /// </summary>
        public static float StarterCap(int? displayedTier, GearMasterySettings settings)
        {
            if (settings == null || displayedTier == null) return 1f;
            if (displayedTier.Value > settings.StarterGearMaxTier) return 1f;
            float cap = settings.StarterGearMaxPenalty;
            if (float.IsNaN(cap)) return 1f;
            return cap < 0f ? 0f : cap > 1f ? 1f : cap;
        }

        /// <summary>A loss multiplier (below 1) held at 1 - cap; untouched when cap &gt;= 1.</summary>
        private static float CapLoss(float multiplier, float cap) =>
            cap >= 1f || multiplier >= 1f - cap ? multiplier : 1f - cap;

        /// <summary>A gain multiplier (above 1, e.g. inaccuracy) held at 1 + cap; untouched when cap &gt;= 1.</summary>
        private static float CapGain(float multiplier, float cap) =>
            cap >= 1f || multiplier <= 1f + cap ? multiplier : 1f + cap;

        /// <summary>
        /// The mounting check with vanilla's skill lock lifted: the base model's answer, or yes when the rider can ride
        /// at all (AgentFlag.CanRide) and his skill below the horse's difficulty is the one thing that could have said
        /// no. (The decorator also zeroes the mount's MountDifficulty, so the base model rarely says no for skill.)
        /// </summary>
        public static bool AllowMount(bool baseAllows, bool canRide, float riderSkill, float mountDifficulty) =>
            baseAllows || (canRide && riderSkill < mountDifficulty);
    }
}
