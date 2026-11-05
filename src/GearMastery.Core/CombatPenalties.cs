namespace GearMastery.Core
{
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
        /// <summary>Does this agent pay penalties at all? <see cref="GearMasterySettings.HeroesOnly"/> spares troops.</summary>
        public static bool AppliesTo(bool isHero, GearMasterySettings settings) =>
            settings != null && (!settings.HeroesOnly || isHero);

        /// <summary>
        /// The wielded item's multipliers by its kind:
        /// - Weapon (melee / throwing): damage, swing speed, thrust (and throw-ready) speed = the swing multiplier, handling.
        /// - Ranged (bow / crossbow / sling): damage, reload, draw (ThrustOrRangedReady) = the reload multiplier, inaccuracy.
        /// - Shield / Armour / Mount: nothing here (they have their own hooks).
        /// </summary>
        public static WieldedMultipliers ForWielded(SkillKind kind, float shortfall, GearMasterySettings settings)
        {
            if (settings == null || !(shortfall > 0f)) return WieldedMultipliers.Identity;
            switch (kind)
            {
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
        /// The damage multiplier for a blow: a melee / thrown weapon's own requirement (kind Weapon), or - for a missile
        /// that is AMMO (arrow, bolt, sling stone) - the requirement of the launcher in hand (kind Ranged). Anything else 1.
        /// </summary>
        public static float DamageMultiplier(SkillKind kind, bool blowIsAmmo, float shortfall, GearMasterySettings settings)
        {
            if (settings == null || !(shortfall > 0f)) return 1f;
            if (blowIsAmmo) return kind == SkillKind.Ranged ? Penalties.RangedDamageMultiplier(shortfall, settings) : 1f;
            return kind == SkillKind.Weapon ? Penalties.WeaponDamageMultiplier(shortfall, settings) : 1f;
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
