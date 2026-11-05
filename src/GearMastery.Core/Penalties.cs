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

        /// <summary>The wearer's shortfall on this piece, from <see cref="Requirements.Shortfall"/> (= 1 - skill / R).</summary>
        public float Shortfall { get; }
    }

    /// <summary>
    /// Shortfall to stat multipliers, round 2 ("snap" curve). The shortfall s = clamp((R - skill) / R, 0, 1) from
    /// <see cref="Requirements.Shortfall"/> is 1 - r, r = skill / R. s = 0 (skill meets R): no penalty at all.
    /// Otherwise the penalty is piecewise-linear in r through the stat's three anchors (r -> 1: Low,
    /// r = PenaltyMidRatio: Mid, r = PenaltyHighRatio: High), held at High below the high ratio - see
    /// <see cref="Amount"/>. Results are clamped so a multiplier never goes negative: reductions to [0, 1], bonuses
    /// (shield damage taken, inaccuracy) to &gt;= 0.
    /// </summary>
    public static class Penalties
    {
        private const float DefaultMidRatio = 0.6f;
        private const float DefaultHighRatio = 0.3f;

        /// <summary>
        /// The penalty fraction at this shortfall for one stat's anchors: 0 when s &lt;= 0 (or NaN); else, with
        /// r = 1 - s: Low + (Mid - Low) x (1 - r) / (1 - midRatio) for r &gt;= midRatio; Mid + (High - Mid) x
        /// (midRatio - r) / (midRatio - highRatio) down to highRatio; High below. The anchor ratios fall back to
        /// 0.6 / 0.3 unless 0 &lt; high &lt; mid &lt; 1. Non-finite anchor values count as 0; misordered anchors are
        /// raised to a running maximum (Mid &gt;= Low, High &gt;= Mid), so the penalty never falls as the skill falls.
        /// Callers clamp the result.
        /// </summary>
        public static float Amount(float shortfall, float low, float mid, float high, GearMasterySettings settings)
        {
            if (float.IsNaN(shortfall) || shortfall <= 0f) return 0f;
            float r = shortfall >= 1f ? 0f : 1f - shortfall;
            var st = Settings(settings);
            float midRatio = st.PenaltyMidRatio, highRatio = st.PenaltyHighRatio;
            if (!(highRatio > 0f && highRatio < midRatio && midRatio < 1f))
            {
                midRatio = DefaultMidRatio;
                highRatio = DefaultHighRatio;
            }
            low = Finite(low);
            mid = Finite(mid);
            high = Finite(high);
            // Anchors set out of order in the menu (e.g. Low above High): never let the penalty shrink as the skill
            // drops - each anchor is at least the one before it.
            if (mid < low) mid = low;
            if (high < mid) high = mid;
            if (r >= midRatio) return low + (mid - low) * (1f - r) / (1f - midRatio);
            if (r > highRatio) return mid + (high - mid) * (midRatio - r) / (midRatio - highRatio);
            return high;
        }

        // ---- Melee / throwing ----

        /// <summary>Multiplier on weapon damage (default 0.75 just short, 0.50 at r 0.6, 0.25 at r 0.3 and below).</summary>
        public static float WeaponDamageMultiplier(float shortfall, GearMasterySettings settings)
        {
            var s = Settings(settings);
            return Reduce(shortfall, s.MeleeDamagePenaltyLow, s.MeleeDamagePenaltyMid, s.MeleeDamagePenaltyHigh, s);
        }

        /// <summary>Multiplier on swing (and thrust) speed.</summary>
        public static float SwingSpeedMultiplier(float shortfall, GearMasterySettings settings)
        {
            var s = Settings(settings);
            return Reduce(shortfall, s.MeleeSwingSpeedPenaltyLow, s.MeleeSwingSpeedPenaltyMid, s.MeleeSwingSpeedPenaltyHigh, s);
        }

        /// <summary>Multiplier on weapon handling.</summary>
        public static float HandlingMultiplier(float shortfall, GearMasterySettings settings)
        {
            var s = Settings(settings);
            return Reduce(shortfall, s.MeleeHandlingPenaltyLow, s.MeleeHandlingPenaltyMid, s.MeleeHandlingPenaltyHigh, s);
        }

        // ---- Shields ----

        /// <summary>Multiplier on the damage a shield takes (default 1.25 / 1.50 / 1.75).</summary>
        public static float ShieldDamageTakenMultiplier(float shortfall, GearMasterySettings settings)
        {
            var s = Settings(settings);
            return Increase(shortfall, s.ShieldDamageTakenBonusLow, s.ShieldDamageTakenBonusMid, s.ShieldDamageTakenBonusHigh, s);
        }

        // ---- Bows / crossbows / slings ----

        /// <summary>Multiplier on missile damage.</summary>
        public static float RangedDamageMultiplier(float shortfall, GearMasterySettings settings)
        {
            var s = Settings(settings);
            return Reduce(shortfall, s.RangedDamagePenaltyLow, s.RangedDamagePenaltyMid, s.RangedDamagePenaltyHigh, s);
        }

        /// <summary>Multiplier on reload / draw speed.</summary>
        public static float ReloadSpeedMultiplier(float shortfall, GearMasterySettings settings)
        {
            var s = Settings(settings);
            return Reduce(shortfall, s.RangedReloadSpeedPenaltyLow, s.RangedReloadSpeedPenaltyMid, s.RangedReloadSpeedPenaltyHigh, s);
        }

        /// <summary>Multiplier on inaccuracy (default 1.25 / 1.50 / 1.75).</summary>
        public static float InaccuracyMultiplier(float shortfall, GearMasterySettings settings)
        {
            var s = Settings(settings);
            return Increase(shortfall, s.RangedInaccuracyBonusLow, s.RangedInaccuracyBonusMid, s.RangedInaccuracyBonusHigh, s);
        }

        // ---- Horses ----

        /// <summary>Multiplier on mount speed.</summary>
        public static float HorseSpeedMultiplier(float shortfall, GearMasterySettings settings)
        {
            var s = Settings(settings);
            return Reduce(shortfall, s.MountSpeedPenaltyLow, s.MountSpeedPenaltyMid, s.MountSpeedPenaltyHigh, s);
        }

        /// <summary>Multiplier on mount maneuver.</summary>
        public static float HorseManeuverMultiplier(float shortfall, GearMasterySettings settings)
        {
            var s = Settings(settings);
            return Reduce(shortfall, s.MountManeuverPenaltyLow, s.MountManeuverPenaltyMid, s.MountManeuverPenaltyHigh, s);
        }

        // ---- Armour ----

        /// <summary>The foot speed penalty fraction ONE armour piece carries at its own shortfall, in [0, 1].</summary>
        public static float ArmourPiecePenalty(float shortfall, GearMasterySettings settings)
        {
            var s = Settings(settings);
            return Clamp01(Amount(shortfall, s.ArmourSpeedPenaltyLow, s.ArmourSpeedPenaltyMid, s.ArmourSpeedPenaltyHigh, s));
        }

        /// <summary>
        /// Multiplier on foot max speed from all worn armour: 1 - (sum weight_i x penalty_i) / (sum weight_i), where
        /// penalty_i is the piece's own <see cref="ArmourPiecePenalty"/> and the sums run over EVERY worn piece (met
        /// ones add weight, no penalty). A light helmet barely matters, a 20 kg cuirass dominates; the whole set at
        /// r 0.3 gives the full High (default -60%). 1 when nothing is worn or the total weight is 0.
        /// </summary>
        public static float ArmourSpeedMultiplier(IEnumerable<ArmourPiece> worn, GearMasterySettings settings)
        {
            var s = Settings(settings);
            if (worn == null) return 1f;
            double total = 0, weighted = 0;
            foreach (var piece in worn)
            {
                float w = piece.Weight;
                if (float.IsNaN(w) || float.IsInfinity(w) || w <= 0f) continue;
                total += w;
                if (piece.Shortfall > 0f) weighted += w * ArmourPiecePenalty(piece.Shortfall, s);
            }
            if (total <= 0) return 1f;
            return 1f - Clamp01((float)(weighted / total));
        }

        private static float Reduce(float shortfall, float low, float mid, float high, GearMasterySettings settings) =>
            1f - Clamp01(Amount(shortfall, low, mid, high, settings));

        private static float Increase(float shortfall, float low, float mid, float high, GearMasterySettings settings)
        {
            float a = Amount(shortfall, low, mid, high, settings);
            return a > 0f ? 1f + a : 1f;
        }

        private static float Finite(float x) => float.IsNaN(x) || float.IsInfinity(x) ? 0f : x;

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
