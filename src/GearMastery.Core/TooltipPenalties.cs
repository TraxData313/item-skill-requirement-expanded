using System;
using System.Collections.Generic;

namespace GearMastery.Core
{
    /// <summary>One stat an under-skilled item costs, as the inventory tooltip names it.</summary>
    public enum PenaltyStat
    {
        /// <summary>Melee / thrown weapon damage, or missile damage of a bow / crossbow / sling.</summary>
        Damage,
        SwingSpeed,
        Handling,
        /// <summary>Extra damage the shield itself takes (a bonus, so the percent is positive).</summary>
        ShieldDamageTaken,
        /// <summary>Armour: the foot speed loss if ALL worn armour were this short (the real one is weight-shared).</summary>
        FootSpeedUpTo,
        /// <summary>Reload and draw speed (one multiplier drives both).</summary>
        ReloadSpeed,
        /// <summary>Extra inaccuracy (a bonus, positive).</summary>
        Inaccuracy,
        HorseSpeed,
        HorseManeuver,
    }

    /// <summary>A tooltip penalty: which stat and the signed whole percent (-9 = 9% less, +12 = 12% more).</summary>
    public readonly struct PenaltyPart
    {
        public PenaltyPart(PenaltyStat stat, int percent)
        {
            Stat = stat;
            Percent = percent;
        }

        public PenaltyStat Stat { get; }
        public int Percent { get; }

        /// <summary>"-9" / "+12" (no % sign: the localised text carries it).</summary>
        public string SignedPercent => TooltipPenalties.Signed(Percent);
    }

    /// <summary>
    /// What the inventory tooltip says about an item the character is short on. The SAME multipliers the battle
    /// decorators apply (<see cref="Penalties"/>), turned into whole percents; parts that round to 0 are dropped.
    /// </summary>
    public static class TooltipPenalties
    {
        /// <summary>Is the requirement unmet? (skill &lt; R; R &lt;= 0 never is.) Drives the red / green colour.</summary>
        public static bool IsShort(int requirement, int skill) => requirement > 0 && skill < requirement;

        /// <summary>
        /// The penalties of an item of this kind at this shortfall, in display order. Empty when the shortfall is 0
        /// or every part rounds to 0%.
        /// </summary>
        public static IReadOnlyList<PenaltyPart> For(SkillKind kind, float shortfall, GearMasterySettings settings)
        {
            var parts = new List<PenaltyPart>(3);
            if (settings == null || !(shortfall > 0f)) return parts;
            switch (kind)
            {
                case SkillKind.Weapon:
                    Add(parts, PenaltyStat.Damage, Penalties.WeaponDamageMultiplier(shortfall, settings));
                    Add(parts, PenaltyStat.SwingSpeed, Penalties.SwingSpeedMultiplier(shortfall, settings));
                    Add(parts, PenaltyStat.Handling, Penalties.HandlingMultiplier(shortfall, settings));
                    break;
                case SkillKind.Shield:
                    Add(parts, PenaltyStat.ShieldDamageTaken, Penalties.ShieldDamageTakenMultiplier(shortfall, settings));
                    break;
                case SkillKind.Armour:
                    // One piece alone: the speed loss if every worn piece were this short. The real penalty is the
                    // weight-weighted aggregate over all worn armour, so this is an upper bound - the text says "up to".
                    Add(parts, PenaltyStat.FootSpeedUpTo,
                        Penalties.ArmourSpeedMultiplier(new[] { new ArmourPiece(1f, shortfall) }, settings));
                    break;
                case SkillKind.Ranged:
                    Add(parts, PenaltyStat.Damage, Penalties.RangedDamageMultiplier(shortfall, settings));
                    Add(parts, PenaltyStat.ReloadSpeed, Penalties.ReloadSpeedMultiplier(shortfall, settings));
                    Add(parts, PenaltyStat.Inaccuracy, Penalties.InaccuracyMultiplier(shortfall, settings));
                    break;
                case SkillKind.Mount:
                    Add(parts, PenaltyStat.HorseSpeed, Penalties.HorseSpeedMultiplier(shortfall, settings));
                    Add(parts, PenaltyStat.HorseManeuver, Penalties.HorseManeuverMultiplier(shortfall, settings));
                    break;
            }
            return parts;
        }

        /// <summary>Multiplier to a signed whole percent: 0.912 -> -9, 1.125 -> +13 (midpoints away from zero).</summary>
        public static int Percent(float multiplier)
        {
            if (float.IsNaN(multiplier) || float.IsInfinity(multiplier)) return 0;
            return (int)Math.Round((multiplier - 1.0) * 100.0, MidpointRounding.AwayFromZero);
        }

        /// <summary>"-9", "+12", "0".</summary>
        public static string Signed(int percent) =>
            percent > 0 ? "+" + percent.ToString(System.Globalization.CultureInfo.InvariantCulture)
                        : percent.ToString(System.Globalization.CultureInfo.InvariantCulture);

        private static void Add(List<PenaltyPart> parts, PenaltyStat stat, float multiplier)
        {
            int p = Percent(multiplier);
            if (p != 0) parts.Add(new PenaltyPart(stat, p));
        }
    }
}
