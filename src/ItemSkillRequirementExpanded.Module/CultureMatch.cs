using System;
using ItemSkillRequirementExpanded.Core;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;

namespace ItemSkillRequirementExpanded
{
    /// <summary>
    /// The game side of the same culture bonus (v1.2.0, Core <see cref="CultureBonus"/>): the hero's culture and
    /// whether it matches the item's (<see cref="ItemRequirement.CultureId"/>). Nothing cached: the settings are read
    /// at every call (an MCM change applies at the next stat refresh / tooltip / row refresh) and a hero's culture is a
    /// property read. Never throws.
    /// </summary>
    internal static class CultureMatch
    {
        /// <summary>
        /// The character's culture StringId. A campaign hero: his Hero's culture (CharacterObject.Culture hides the base
        /// property and returns HeroObject.Culture - read through the CharacterObject, not BasicCharacterObject, or a
        /// hero would get his template's culture). Anyone else (custom battle): BasicCharacterObject.Culture.
        /// </summary>
        public static string? CultureIdOf(BasicCharacterObject? character)
        {
            if (character == null) return null;
            try
            {
                if (character is CharacterObject co) return co.Culture?.StringId;
                return character.Culture?.StringId;
            }
            catch (Exception e)
            {
                ModLog.ErrorOnce("CultureMatch.CultureIdOf", e);
                return null;
            }
        }

        /// <summary>Item and character share a culture that counts (never the neutral culture, never a culture-less item).</summary>
        public static bool Same(BasicCharacterObject? character, ItemRequirement? req) =>
            req != null && req.CultureId != null && CultureBonus.Matches(req.CultureId, CultureIdOf(character));

        /// <summary>The skill that counts against the item's requirement: <paramref name="skill"/> boosted when the
        /// cultures match and the bonus is on (live settings), else unchanged.</summary>
        public static double Effective(BasicCharacterObject? character, ItemRequirement? req, int skill) =>
            CultureBonus.Effective(skill, Same(character, req), ItemRequirements.Settings);

        /// <summary>The culture word of the tooltip's "+35 Battanian".</summary>
        public static string Adjective(ItemRequirement req) => CultureBonus.Adjective(req.CultureId, req.CultureName);
    }
}
