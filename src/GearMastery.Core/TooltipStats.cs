using System;
using System.Globalization;

namespace GearMastery.Core
{
    /// <summary>A stat row of vanilla's inventory tooltip that one of our penalties changes.</summary>
    public enum TooltipStat
    {
        /// <summary>"Swing Speed" of a melee usage (SwingSpeedMultiplier).</summary>
        SwingSpeed,
        /// <summary>"Swing Damage" of a melee usage.</summary>
        SwingDamage,
        /// <summary>"Thrust Speed" of a melee usage (ThrustOrRangedReadySpeedMultiplier = the swing multiplier).</summary>
        ThrustSpeed,
        /// <summary>"Thrust Damage" of a melee usage.</summary>
        ThrustDamage,
        /// <summary>"Handling" of a melee usage.</summary>
        Handling,
        /// <summary>"Damage" of a thrown usage (javelin thrown: missile damage, ranged anchors).</summary>
        ThrownDamage,
        /// <summary>"Swing Speed" vanilla shows for a bow / crossbow / sling: its draw / reload speed rating.</summary>
        LauncherSpeed,
        /// <summary>"Damage" of a bow / crossbow / sling (its arrows' blows use the launcher's requirement).</summary>
        LauncherDamage,
        /// <summary>"Accuracy" of a launcher or a thrown usage (inaccuracy multiplier, mapped onto accuracy).</summary>
        Accuracy,
        /// <summary>Horse "Speed".</summary>
        MountSpeed,
        /// <summary>Horse "Maneuver".</summary>
        MountManeuver,
    }

    /// <summary>
    /// The inventory tooltip's stat rows after our penalty: "Swing Damage: 37 (99-62) Cut", "Speed: 39 (52-13)".
    /// Every multiplier comes from the SAME Core functions the battle decorators use
    /// (<see cref="CombatPenalties.ForWielded"/>, <see cref="CombatPenalties.DamageMultiplier"/>,
    /// <see cref="CombatPenalties.InaccuracyMultiplier"/>, <see cref="Penalties.HorseSpeedMultiplier"/> /
    /// <see cref="Penalties.HorseManeuverMultiplier"/>), at the same shortfall and per-usage skill.
    /// </summary>
    public static class TooltipStats
    {
        /// <summary>
        /// The multiplier battle applies to what this row shows, for an item of <paramref name="kind"/> at this
        /// shortfall (1 = row untouched). Rows of the wrong family (a shield's swing speed, armour values) give 1.
        /// </summary>
        public static float Multiplier(SkillKind kind, bool thrownUsage, TooltipStat stat, float shortfall, GearMasterySettings settings)
        {
            if (settings == null || !(shortfall > 0f)) return 1f;
            bool thrown = CombatPenalties.IsThrownUsage(kind, thrownUsage);
            switch (kind)
            {
                case SkillKind.Weapon when thrown:
                    switch (stat)
                    {
                        case TooltipStat.ThrownDamage: return CombatPenalties.DamageMultiplier(kind, false, shortfall, settings, true);
                        case TooltipStat.Accuracy: return CombatPenalties.InaccuracyMultiplier(kind, true, shortfall, settings);
                        default: return 1f;
                    }
                case SkillKind.Weapon:
                {
                    var m = CombatPenalties.ForWielded(kind, shortfall, settings);
                    switch (stat)
                    {
                        case TooltipStat.SwingSpeed: return m.Swing;
                        case TooltipStat.ThrustSpeed: return m.ThrustOrRangedReady;
                        case TooltipStat.Handling: return m.Handling;
                        case TooltipStat.SwingDamage:
                        case TooltipStat.ThrustDamage: return CombatPenalties.DamageMultiplier(kind, false, shortfall, settings);
                        default: return 1f;
                    }
                }
                case SkillKind.Ranged:
                    switch (stat)
                    {
                        // Draw (ThrustOrRangedReady) and reload share one multiplier.
                        case TooltipStat.LauncherSpeed: return CombatPenalties.ForWielded(kind, shortfall, settings).ThrustOrRangedReady;
                        case TooltipStat.LauncherDamage: return CombatPenalties.DamageMultiplier(kind, true, shortfall, settings);
                        case TooltipStat.Accuracy: return CombatPenalties.InaccuracyMultiplier(kind, false, shortfall, settings);
                        default: return 1f;
                    }
                case SkillKind.Mount:
                    switch (stat)
                    {
                        case TooltipStat.MountSpeed: return Penalties.HorseSpeedMultiplier(shortfall, settings);
                        case TooltipStat.MountManeuver: return Penalties.HorseManeuverMultiplier(shortfall, settings);
                        default: return 1f;
                    }
                default:
                    return 1f;
            }
        }

        /// <summary>
        /// The row's value after the multiplier. Accuracy maps the INACCURACY multiplier k: the engine's spread is
        /// proportional to (100 - accuracy) (Sandbox GetWeaponInaccuracy), so the equivalent accuracy is
        /// 100 - (100 - A) x k, floored at 0. Everything else: value x multiplier. Rounded half away from zero.
        /// </summary>
        public static int After(TooltipStat stat, int value, float multiplier)
        {
            if (float.IsNaN(multiplier) || float.IsInfinity(multiplier)) return value;
            // Float maths, as the game's own (10 x 0.35f is 3.5 in float, 3.4999999 in double).
            float after = stat == TooltipStat.Accuracy
                ? 100f - (100f - value) * multiplier
                : value * multiplier;
            if (stat == TooltipStat.Accuracy && after < 0) after = 0;
            return (int)Math.Round(after, MidpointRounding.AwayFromZero);
        }

        /// <summary>"37 (99-62)" for a loss, "60 (50+10)" for a gain. ASCII signs: the game font is not trusted with U+2212.</summary>
        public static string Breakdown(int after, int value)
        {
            int delta = after - value;
            string sign = delta < 0 ? "-" : "+";
            return Str(after) + " (" + Str(value) + sign + Str(Math.Abs(delta)) + ")";
        }

        /// <summary>
        /// Puts the breakdown in place of the row's number: "99 Cut" -> "37 (99-62) Cut", "52" -> "39 (52-13)".
        /// The FIRST whole-number token equal to <paramref name="value"/> is replaced (digits on both sides must not
        /// continue it). Null when the text holds no such token - the caller then leaves the row as it is.
        /// </summary>
        public static string? InsertBreakdown(string? vanillaText, int value, string breakdown)
        {
            if (vanillaText == null || breakdown == null) return null;
            string token = Str(value);
            int from = 0;
            while (from <= vanillaText.Length - token.Length)
            {
                int at = vanillaText.IndexOf(token, from, StringComparison.Ordinal);
                if (at < 0) return null;
                int end = at + token.Length;
                bool leftOk = at == 0 || !IsNumberChar(vanillaText[at - 1]);
                bool rightOk = end == vanillaText.Length || !char.IsDigit(vanillaText[end]);
                if (leftOk && rightOk) return vanillaText.Substring(0, at) + breakdown + vanillaText.Substring(end);
                from = at + 1;
            }
            return null;
        }

        /// <summary>Vanilla's comparison sign for "higher is better" rows: 1 target better, -1 worse, 0 equal.</summary>
        public static int Compare(int target, int compared) => target == compared ? 0 : (target > compared ? 1 : -1);

        private static bool IsNumberChar(char c) => char.IsDigit(c) || c == '-' || c == '.' || c == ',';

        private static string Str(int v) => v.ToString(CultureInfo.InvariantCulture);
    }
}
