namespace ItemSkillRequirementExpanded.Core
{
    /// <summary>
    /// The inventory list's orange "above your skill" tint (v1.1.0, Toni 2026-11-05): which item rows get it. Pure, so
    /// the module's widget patch only gathers the facts. Vanilla's red ("this character can't use it": gender, civilian
    /// set, a hard lock) always wins - the tint is only for rows vanilla leaves in its default look.
    /// </summary>
    public static class InventoryHighlight
    {
        /// <summary>
        /// True when the row should be orange: the toggle is on (<see cref="SkillRequirementSettings.HighlightItemsAboveSkill"/>),
        /// the selected character pays penalties (who-toggles), vanilla shows the row in its default look
        /// (<paramref name="vanillaDefaultLook"/>: equippable by this character, usable in the open set), the item has a
        /// requirement and the character's skill (raw, as the tooltip, with the same culture bonus applied:
        /// <see cref="CultureBonus.Effective(double, bool, SkillRequirementSettings)"/>) is below it. Multi-usage weapons pass their
        /// PRIMARY usage's skill (the one vanilla's tooltip opens on).
        /// </summary>
        public static bool AboveSkill(SkillRequirementSettings? settings, bool characterPays, bool vanillaDefaultLook,
            int requirement, double skill)
        {
            if (settings == null || !settings.HighlightItemsAboveSkill) return false;
            if (!characterPays || !vanillaDefaultLook) return false;
            return Requirements.Shortfall(requirement, skill) > 0f;
        }
    }
}
