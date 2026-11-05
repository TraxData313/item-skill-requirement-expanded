using ItemSkillRequirementExpanded.Core;

namespace ItemSkillRequirementExpanded.Core.Tests;

public class InventoryHighlightTests
{
    private static readonly SkillRequirementSettings On = new();

    [Fact]
    public void OnByDefault() => Assert.True(new SkillRequirementSettings().HighlightItemsAboveSkill);

    [Theory]
    [InlineData(75, 0, true)]
    [InlineData(75, 74, true)]
    [InlineData(75, 75, false)] // the snap: meeting R is enough
    [InlineData(75, 200, false)]
    [InlineData(0, 0, false)]   // no requirement
    public void SkillBelowTheRequirementOnly(int requirement, int skill, bool expected) =>
        Assert.Equal(expected, InventoryHighlight.AboveSkill(On, characterPays: true, vanillaDefaultLook: true, requirement, skill));

    [Fact]
    public void VanillaRedWins() =>
        Assert.False(InventoryHighlight.AboveSkill(On, characterPays: true, vanillaDefaultLook: false, 150, 10));

    [Fact]
    public void OnlyForCoveredCharacters() =>
        Assert.False(InventoryHighlight.AboveSkill(On, characterPays: false, vanillaDefaultLook: true, 150, 10));

    [Fact]
    public void ToggleOffOrNoSettings()
    {
        var off = new SkillRequirementSettings { HighlightItemsAboveSkill = false };
        Assert.False(InventoryHighlight.AboveSkill(off, true, true, 150, 10));
        Assert.False(InventoryHighlight.AboveSkill(null, true, true, 150, 10));
    }

    [Fact]
    public void MenuToggleWritesTheSetting()
    {
        var s = new SkillRequirementSettings();
        var flag = SettingsMenu.Find(nameof(SkillRequirementSettings.HighlightItemsAboveSkill))!;
        Assert.Equal(SettingsMenu.Inventory, flag.Group);
        Assert.Equal(MenuKind.Bool, flag.Kind);
        Assert.False(flag.AffectsRequirements);
        Assert.True(flag.Set(s, 0));
        Assert.False(s.HighlightItemsAboveSkill);
        Assert.False(InventoryHighlight.AboveSkill(s, true, true, 150, 10));
    }
}
