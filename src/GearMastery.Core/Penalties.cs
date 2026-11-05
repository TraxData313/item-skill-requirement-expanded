using System;
using System.Collections.Generic;

namespace GearMastery.Core
{
    /// <summary>One worn armour piece, as the armour speed penalty sees it.</summary>
    public readonly struct ArmourPiece
    {
        public ArmourPiece(float weight, float shortfall)
        {
            Weight = weight;
            Shortfall = shortfall;
        }

        /// <summary>Item weight in kg. Negative or NaN counts as 0.</summary>
        public float Weight { get; }

        /// <summary>The wearer's shortfall on this piece, from <see cref="Requirements.Shortfall"/> (clamped to [0, 1]).</summary>
        public float Shortfall { get; }
    }

    /// <summary>
    /// Shortfall to stat multipliers. Every penalty is max x curve(shortfall), curve(s) = s^PenaltyExponent
    /// (default 0.5). Maxima come from <see cref="GearMasterySettings"/> and are clamped so a multiplier never goes
    /// negative: reductions to [0, 1], bonuses (shield damage taken, inaccuracy) to &gt;= 0.
    /// </summary>
    public static class Penalties
    {
        /// <summary>The shaped shortfall in [0, 1]: clamp01(shortfall)^exponent (exponent &lt;= 0 or NaN = linear).</summary>
        public static float Curve(float shortfall, GearMasterySettings settings)
        {
            float s = Clamp01(shortfall);
            if (s <= 0f || s >= 1f) return s;
            float p = Settings(settings).PenaltyExponent;
            if (float.IsNaN(p) || float.IsInfinity(p) || p <= 0f) p = 1f;
            return Clamp01((float)Math.Pow(s, p));
        }

        // ---- Melee / throwing ----

        /// <summary>Multiplier on weapon damage (default 0.85 at full shortfall).</summary>
        public static float WeaponDamageMultiplier(float shortfall, GearMasterySettings settings) =>
            Reduce(shortfall, Settings(settings).MaxWeaponDamagePenalty, settings);

        /// <summary>Multiplier on swing (and thrust) speed (default 0.90 at full shortfall).</summary>
        public static float SwingSpeedMultiplier(float shortfall, GearMasterySettings settings) =>
            Reduce(shortfall, Settings(settings).MaxSwingSpeedPenalty, settings);

        /// <summary>Multiplier on weapon handling (default 0.85 at full shortfall).</summary>
        public static float HandlingMultiplier(float shortfall, GearMasterySettings settings) =>
            Reduce(shortfall, Settings(settings).MaxHandlingPenalty, settings);

        // ---- Shields ----

        /// <summary>Multiplier on the damage a shield takes (default 1.25 at full shortfall).</summary>
        public static float ShieldDamageTakenMultiplier(float shortfall, GearMasterySettings settings) =>
            Increase(shortfall, Settings(settings).MaxShieldDamageTakenBonus, settings);

        // ---- Bows / crossbows / slings ----

        /// <summary>Multiplier on missile damage (default 0.85 at full shortfall).</summary>
        public static float RangedDamageMultiplier(float shortfall, GearMasterySettings settings) =>
            Reduce(shortfall, Settings(settings).MaxRangedDamagePenalty, settings);

        /// <summary>Multiplier on reload / draw speed (default 0.90 at full shortfall).</summary>
        public static float ReloadSpeedMultiplier(float shortfall, GearMasterySettings settings) =>
            Reduce(shortfall, Settings(settings).MaxReloadSpeedPenalty, settings);

        /// <summary>Multiplier on inaccuracy (default 1.30 at full shortfall).</summary>
        public static float InaccuracyMultiplier(float shortfall, GearMasterySettings settings) =>
            Increase(shortfall, Settings(settings).MaxInaccuracyBonus, settings);

        // ---- Horses ----

        /// <summary>Multiplier on mount speed (default 0.90 at full shortfall).</summary>
        public static float HorseSpeedMultiplier(float shortfall, GearMasterySettings settings) =>
            Reduce(shortfall, Settings(settings).MaxHorseSpeedPenalty, settings);

        /// <summary>Multiplier on mount maneuver (default 0.90 at full shortfall).</summary>
        public static float HorseManeuverMultiplier(float shortfall, GearMasterySettings settings) =>
            Reduce(shortfall, Settings(settings).MaxHorseManeuverPenalty, settings);

        // ---- Armour ----

        /// <summary>
        /// Multiplier on foot max speed from all worn armour: 1 - max x curve(A), A = (sum weight_i x shortfall_i) /
        /// (sum weight_i), the sums running over EVERY worn armour piece (met or not), so one light under-skilled
        /// helmet barely matters. 1 when nothing is worn or the total weight is 0. The curve is applied to the
        /// aggregate A, not per piece.
        /// </summary>
        public static float ArmourSpeedMultiplier(IEnumerable<ArmourPiece> worn, GearMasterySettings settings) =>
            Reduce(ArmourShortfall(worn), Settings(settings).MaxArmourSpeedPenalty, settings);

        /// <summary>The weight-averaged shortfall over all worn armour, in [0, 1]. 0 for no pieces / no weight.</summary>
        public static float ArmourShortfall(IEnumerable<ArmourPiece> worn)
        {
            if (worn == null) return 0f;
            double total = 0, shorted = 0;
            foreach (var piece in worn)
            {
                float w = piece.Weight;
                if (float.IsNaN(w) || float.IsInfinity(w) || w <= 0f) continue;
                total += w;
                shorted += w * Clamp01(piece.Shortfall);
            }
            if (total <= 0) return 0f;
            return Clamp01((float)(shorted / total));
        }

        private static float Reduce(float shortfall, float max, GearMasterySettings settings)
        {
            if (float.IsNaN(max) || max < 0f) max = 0f;
            else if (max > 1f) max = 1f;
            return 1f - max * Curve(shortfall, settings);
        }

        private static float Increase(float shortfall, float max, GearMasterySettings settings)
        {
            if (float.IsNaN(max) || float.IsInfinity(max) || max < 0f) max = 0f;
            return 1f + max * Curve(shortfall, settings);
        }

        private static float Clamp01(float x)
        {
            if (float.IsNaN(x) || x <= 0f) return 0f;
            return x >= 1f ? 1f : x;
        }

        private static GearMasterySettings Settings(GearMasterySettings settings) =>
            settings ?? throw new ArgumentNullException(nameof(settings));
    }

    /// <summary>
    /// Calibration aid for tuning the defaults: a melee weapon's rough output (damage x speed, i.e. damage per
    /// second-ish) in the hands of a wielder with a given skill, after our penalties.
    /// </summary>
    public static class Calibration
    {
        public static float EffectiveMeleeOutput(float rawDamage, float rawSpeed, int requirement, int skill, GearMasterySettings settings)
        {
            float s = Requirements.Shortfall(requirement, skill);
            return rawDamage * Penalties.WeaponDamageMultiplier(s, settings)
                 * rawSpeed * Penalties.SwingSpeedMultiplier(s, settings);
        }

        /// <summary>Output of weapon A divided by output of weapon B, same wielder.</summary>
        public static float MeleeOutputRatio(
            float damageA, float speedA, int requirementA,
            float damageB, float speedB, int requirementB,
            int skill, GearMasterySettings settings)
        {
            float b = EffectiveMeleeOutput(damageB, speedB, requirementB, skill, settings);
            return b <= 0f ? 0f : EffectiveMeleeOutput(damageA, speedA, requirementA, skill, settings) / b;
        }
    }
}
