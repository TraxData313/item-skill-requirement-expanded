using System;
using System.Collections.Generic;

namespace ItemSkillRequirementExpanded.Core
{
    /// <summary>Item tier to skill requirement, and skill to shortfall. Pure maths.</summary>
    public static class Requirements
    {
        /// <summary>
        /// R for an item of this category: round-to-5(table(tierf) x category scale), never below 0. The same tier
        /// table serves every category (melee, shields, armour, bows/crossbows/slings, horses); only the scale differs.
        /// NaN / infinite tiers ask nothing.
        /// </summary>
        public static int Requirement(SkillKind kind, float tierf, SkillRequirementSettings settings)
        {
            if (settings == null) throw new ArgumentNullException(nameof(settings));
            float scale = settings.ScaleFor(kind);
            if (float.IsNaN(scale) || float.IsInfinity(scale) || scale <= 0f) return 0;
            return RoundTo5(FromTable(tierf, settings.TierTable()) * scale);
        }

        /// <summary>
        /// Piecewise-linear table lookup: interpolates between the two points around <paramref name="tierf"/>, held
        /// flat at the first / last point outside them. Points are sorted by tier here (input order does not matter);
        /// points with a NaN/infinite tier are ignored; a point asking less than one at a lower tier is raised to it (the
        /// table never falls). 0 for an empty table or a NaN/infinite tier.
        /// </summary>
        public static float FromTable(float tierf, IReadOnlyList<TierPoint> points)
        {
            if (points == null || float.IsNaN(tierf) || float.IsInfinity(tierf)) return 0f;
            var sorted = new List<TierPoint>(points.Count);
            foreach (var p in points)
                if (!float.IsNaN(p.Tier) && !float.IsInfinity(p.Tier) && !float.IsNaN(p.Requirement) && !float.IsInfinity(p.Requirement))
                    sorted.Add(p);
            if (sorted.Count == 0) return 0f;
            sorted.Sort((a, b) => a.Tier.CompareTo(b.Tier));
            // A table set out of order in the menu (tier 4 asking less than tier 3): a better item never asks less -
            // each point is raised to the highest requirement below it.
            for (int i = 1; i < sorted.Count; i++)
                if (sorted[i].Requirement < sorted[i - 1].Requirement)
                    sorted[i] = new TierPoint(sorted[i].Tier, sorted[i - 1].Requirement);

            if (tierf <= sorted[0].Tier) return sorted[0].Requirement;
            var last = sorted[sorted.Count - 1];
            if (tierf >= last.Tier) return last.Requirement;
            for (int i = 1; i < sorted.Count; i++)
            {
                var hi = sorted[i];
                if (tierf > hi.Tier) continue;
                var lo = sorted[i - 1];
                float span = hi.Tier - lo.Tier;
                if (span <= 0f) return hi.Requirement; // duplicate tier: take the later point
                return lo.Requirement + (hi.Requirement - lo.Requirement) * (tierf - lo.Tier) / span;
            }
            return last.Requirement;
        }

        /// <summary>Nearest multiple of 5 (halves away from zero), floored at 0; NaN asks nothing, huge values saturate.</summary>
        public static int RoundTo5(double value)
        {
            if (double.IsNaN(value) || value <= 0) return 0;
            if (value >= 1e9) return 1000000000;
            return (int)(Math.Round(value / 5.0, MidpointRounding.AwayFromZero) * 5.0);
        }

        /// <summary>Vanilla's displayed "Tier N" (ItemObject.Tier + 1): round(Tierf) clamped to 0..6. MathF.Round
        /// rounds halves to even, like Math.Round's default.</summary>
        public static int DisplayedTier(float tierf)
        {
            if (float.IsNaN(tierf)) return 0;
            double r = Math.Round((double)tierf);
            return r < 0 ? 0 : r > 6 ? 6 : (int)r;
        }

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

        /// <summary>
        /// <see cref="Shortfall(int, int)"/> for a fractional skill (the same culture bonus, <see cref="CultureBonus"/>):
        /// clamp((R - skill) / R, 0, 1); 0 when R &lt;= 0 or skill &gt;= R, 1 for NaN.
        /// </summary>
        public static float Shortfall(int requirement, double skill)
        {
            if (requirement <= 0 || skill >= requirement) return 0f;
            if (double.IsNaN(skill)) return 1f;
            double s = (requirement - skill) / requirement;
            return s >= 1.0 ? 1f : (float)s;
        }
    }
}
