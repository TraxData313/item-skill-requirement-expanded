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
        /// </summary>
        public static WieldedMultipliers ForWielded(SkillKind kind, float shortfall, GearMasterySettings settings, bool thrownUsage = false)
        {
            if (settings == null || !(shortfall > 0f)) return WieldedMultipliers.Identity;
            switch (kind)
            {
                case SkillKind.Weapon when thrownUsage:
                    return new WieldedMultipliers(
                        Penalties.RangedDamageMultiplier(shortfall, settings),
                        1f, 1f, 1f, 1f,
                        Penalties.InaccuracyMultiplier(shortfall, settings));
                case SkillKind.Weapon:
                {
                    float swing = Penalties.SwingSpeedMultiplier(shortfall, settings);
                    return new WieldedMultipliers(
                        Penalties.WeaponDamageMultiplier(shortfall, settings),
                        swing,
                        swing,
                        Penalties.HandlingMultiplier(shortfall, settings),
                        1f,
                        1f);
                }
                case SkillKind.Ranged:
                {
                    float reload = Penalties.ReloadSpeedMultiplier(shortfall, settings);
                    return new WieldedMultipliers(
                        Penalties.RangedDamageMultiplier(shortfall, settings),
                        1f,
                        reload,
                        1f,
                        reload,
                        Penalties.InaccuracyMultiplier(shortfall, settings));
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
        public static float DamageMultiplier(SkillKind kind, bool blowIsAmmo, float shortfall, GearMasterySettings settings, bool thrownUsage = false)
        {
            if (settings == null || !(shortfall > 0f)) return 1f;
            if (blowIsAmmo) return kind == SkillKind.Ranged ? Penalties.RangedDamageMultiplier(shortfall, settings) : 1f;
            if (kind != SkillKind.Weapon) return 1f;
            return thrownUsage ? Penalties.RangedDamageMultiplier(shortfall, settings) : Penalties.WeaponDamageMultiplier(shortfall, settings);
        }

        /// <summary>The inaccuracy multiplier for a ranged usage: launchers (kind Ranged) and thrown usages of
        /// Weapon-kind items; 1 for everything else.</summary>
        public static float InaccuracyMultiplier(SkillKind kind, bool thrownUsage, float shortfall, GearMasterySettings settings)
        {
            if (settings == null || !(shortfall > 0f)) return 1f;
            return kind == SkillKind.Ranged || IsThrownUsage(kind, thrownUsage)
                ? Penalties.InaccuracyMultiplier(shortfall, settings)
                : 1f;
        }

        /// <summary>
        /// The mounting check with vanilla's skill lock lifted: the base model's answer, or yes when the rider can ride
        /// at all (AgentFlag.CanRide) and his skill below the horse's difficulty is the one thing that could have said
        /// no. (The decorator also zeroes the mount's MountDifficulty, so the base model rarely says no for skill.)
        /// </summary>
        public static bool AllowMount(bool baseAllows, bool canRide, float riderSkill, float mountDifficulty) =>
            baseAllows || (canRide && riderSkill < mountDifficulty);
    }
}
