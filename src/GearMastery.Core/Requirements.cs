using System;

namespace GearMastery.Core
{
    /// <summary>Item tier (or vanilla difficulty) to skill requirement, and skill to shortfall. Pure maths.</summary>
    public static class Requirements
    {
        /// <summary>
        /// Tier formula for <see cref="SkillKind.Weapon"/>, <see cref="SkillKind.Shield"/>, <see cref="SkillKind.Armour"/>:
        /// R = slope x tierf rounded to the nearest 5 (halves away from zero), clamped to [0, cap].
        /// Negative, NaN or infinite tiers ask nothing. Returns 0 for Ranged/Mount (use <see cref="FromDifficulty"/>).
        /// </summary>
        public static int Requirement(SkillKind kind, float tierf, GearMasterySettings settings)
        {
            if (settings == null) throw new ArgumentNullException(nameof(settings));
            return Requirement(tierf, settings.SlopeFor(kind), settings.CapFor(kind));
        }

        public static int Requirement(float tierf, float slope, int cap)
        {
            if (float.IsNaN(tierf) || float.IsInfinity(tierf) || tierf <= 0f) return 0;
            if (float.IsNaN(slope) || slope <= 0f || cap <= 0) return 0;
            double raw = (double)slope * tierf;
            if (raw >= cap) return cap;
            int rounded = (int)(Math.Round(raw / 5.0, MidpointRounding.AwayFromZero) * 5.0);
            return Math.Max(0, Math.Min(cap, rounded));
        }

        /// <summary>Bows, crossbows, slings and horses: R is vanilla's own item.Difficulty, floored at 0.</summary>
        public static int FromDifficulty(int difficulty) => difficulty > 0 ? difficulty : 0;

        /// <summary>
        /// How far below the requirement the wielder is, as a share of it: clamp((R - skill) / R, 0, 1).
        /// 0 when there is no requirement (R &lt;= 0) or the skill meets it; 1 at skill 0 (or below).
        /// </summary>
        public static float Shortfall(int requirement, int skill)
        {
            if (requirement <= 0 || skill >= requirement) return 0f;
            float s = (requirement - (float)skill) / requirement;
            return s >= 1f ? 1f : s;
        }
    }
}
