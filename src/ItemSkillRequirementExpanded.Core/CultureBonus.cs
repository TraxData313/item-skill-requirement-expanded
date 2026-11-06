using System;

namespace ItemSkillRequirementExpanded.Core
{
    /// <summary>
    /// The same culture bonus (v1.2.0, Toni 2026-11-06): a hero knows his own people's gear better. When the item's
    /// culture equals the hero's culture, his skill counts as <c>skill x multiplier + points</c> before it is compared to
    /// the requirement R and before the penalty curve. Items without a culture or with vanilla's
    /// <see cref="NeutralCultureId"/> give no bonus. Pure; the module only reads the two culture ids.
    /// Example (defaults 1.2 and +5): a tier 7.5 Battanian armour asks Athletics 200, so a Battanian needs
    /// (200 - 5) / 1.2 = 162.5, i.e. 163 (162 -> 199.4 is still short).
    /// </summary>
    public static class CultureBonus
    {
        /// <summary>Vanilla's culture for items that belong to nobody (CampaignData.CultureNeutral).</summary>
        public const string NeutralCultureId = "neutral_culture";

        /// <summary>Do the item and the hero share a culture that counts? Null / empty / neutral never match;
        /// ids compare ordinally (vanilla StringIds).</summary>
        public static bool Matches(string? itemCultureId, string? heroCultureId)
        {
            if (string.IsNullOrEmpty(itemCultureId) || string.IsNullOrEmpty(heroCultureId)) return false;
            if (string.Equals(itemCultureId, NeutralCultureId, StringComparison.Ordinal)) return false;
            return string.Equals(itemCultureId, heroCultureId, StringComparison.Ordinal);
        }

        /// <summary>
        /// The skill that counts: <c>skill x mult + add</c> when <paramref name="sameCulture"/> and the bonus is on,
        /// else the skill unchanged. Rounded to 4 decimals, so a float multiplier such as 1.15f (= 1.1499999...)
        /// can't leave 200 x 1.15 a hair under 230. A multiplier below 1 counts as 1 and negative points as 0 (the
        /// bonus never lowers a skill); NaN settings count as no bonus.
        /// </summary>
        public static double Effective(double skill, bool sameCulture, SkillRequirementSettings? settings)
        {
            if (settings == null || !sameCulture || !settings.SameCultureBonusEnabled) return skill;
            return Effective(skill, settings.SameCultureBonusPoints, settings.SameCultureBonusMultiplier);
        }

        /// <summary><c>skill x mult + add</c>, clamped as <see cref="Effective(double, bool, SkillRequirementSettings)"/>.</summary>
        public static double Effective(double skill, double add, double mult)
        {
            if (double.IsNaN(mult) || mult < 1) mult = 1;
            if (double.IsNaN(add) || add < 0) add = 0;
            return Math.Round(skill * mult + add, 4);
        }

        /// <summary>
        /// The effective skill as shown, a whole number ROUNDED DOWN: with whole requirements, "shown &gt;= R" is then
        /// exactly "effective &gt;= R", so a line never shows 200 next to a red "Requires 200" (199.4 shows 199).
        /// </summary>
        public static int Shown(double effective)
        {
            if (double.IsNaN(effective) || effective <= 0) return 0;
            if (effective >= int.MaxValue) return int.MaxValue;
            return (int)Math.Floor(effective);
        }

        /// <summary>The bonus as shown: <see cref="Shown"/>(effective) - raw (0 when no bonus applies).</summary>
        public static int ShownBonus(int raw, double effective) => Math.Max(0, Shown(effective) - raw);

        /// <summary>
        /// The culture as an adjective for the tooltip ("+35 Battanian"): English adjectives for the main cultures and
        /// War Sails' Nords, else the game's own (localised) culture name, else the id.
        /// </summary>
        public static string Adjective(string? cultureId, string? cultureName)
        {
            switch (cultureId)
            {
                case "empire": return "Imperial";
                case "aserai": return "Aserai";
                case "sturgia": return "Sturgian";
                case "vlandia": return "Vlandian";
                case "battania": return "Battanian";
                case "khuzait": return "Khuzait";
                case "nord": return "Nord";
            }
            if (!string.IsNullOrWhiteSpace(cultureName)) return cultureName!;
            return cultureId ?? "";
        }

        /// <summary>
        /// The "yours" part of the requirement line: "150" without a bonus, "150 +35 Battanian" with one (the shown
        /// bonus, <see cref="ShownBonus"/>). A bonus that shows as +0 is left out.
        /// </summary>
        public static string YoursText(int raw, double effective, string? adjective)
        {
            int bonus = ShownBonus(raw, effective);
            if (bonus <= 0) return raw.ToString(System.Globalization.CultureInfo.InvariantCulture);
            string text = raw.ToString(System.Globalization.CultureInfo.InvariantCulture) + " +" + bonus.ToString(System.Globalization.CultureInfo.InvariantCulture);
            return string.IsNullOrWhiteSpace(adjective) ? text : text + " " + adjective;
        }

        /// <summary>The lowest whole raw skill that meets <paramref name="requirement"/> with the bonus (for docs and
        /// tests): the smallest n with Effective(n) &gt;= R. 0 when R &lt;= 0.</summary>
        public static int MinimumSkill(int requirement, double add, double mult)
        {
            if (requirement <= 0) return 0;
            if (double.IsNaN(mult) || mult < 1) mult = 1;
            if (double.IsNaN(add) || add < 0) add = 0;
            int n = Math.Max(0, (int)Math.Floor((requirement - add) / mult) - 1);
            while (Effective(n, add, mult) < requirement) n++;
            return n;
        }
    }
}
