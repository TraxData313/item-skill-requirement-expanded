using ItemSkillRequirementExpanded.Core;

namespace ItemSkillRequirementExpanded.Core.Tests;

public class CultureBonusTests
{
    private static readonly SkillRequirementSettings Defaults = new();

    [Fact]
    public void DefaultsAreOnPlus5Times1Point2()
    {
        Assert.True(Defaults.SameCultureBonusEnabled);
        Assert.Equal(5, Defaults.SameCultureBonusPoints);
        Assert.Equal(1.2f, Defaults.SameCultureBonusMultiplier);
    }

    [Theory]
    [InlineData("battania", "battania", true)]
    [InlineData("battania", "vlandia", false)]
    [InlineData("neutral_culture", "neutral_culture", false)] // vanilla's "nobody's" culture never counts
    [InlineData(null, "battania", false)]
    [InlineData("battania", null, false)]
    [InlineData("", "", false)]
    [InlineData("Battania", "battania", false)] // StringIds compare exactly
    public void OnlyARealSharedCultureMatches(string? item, string? hero, bool expected) =>
        Assert.Equal(expected, CultureBonus.Matches(item, hero));

    [Fact]
    public void SkillTimesMultiplierPlusPoints()
    {
        Assert.Equal(185.0, CultureBonus.Effective(150, true, Defaults), 6); // 150 x 1.2 + 5
        Assert.Equal(150.0, CultureBonus.Effective(150, false, Defaults), 6);
        Assert.Equal(5.0, CultureBonus.Effective(0, true, Defaults), 6);
    }

    [Fact]
    public void ToggleOffOrNoSettingsGivesNothing()
    {
        Assert.Equal(150.0, CultureBonus.Effective(150, true, new SkillRequirementSettings { SameCultureBonusEnabled = false }), 6);
        Assert.Equal(150.0, CultureBonus.Effective(150, true, null), 6);
    }

    [Fact]
    public void NeverLowersASkill()
    {
        Assert.Equal(100.0, CultureBonus.Effective(100, -10, 0.5), 6);
        Assert.Equal(100.0, CultureBonus.Effective(100, double.NaN, double.NaN), 6);
    }

    [Fact]
    public void FloatMultiplierDoesNotLeaveAHairUnder() =>
        Assert.Equal(230.0, CultureBonus.Effective(200, 0, 1.15f), 6); // 1.15f = 1.14999997...

    [Fact]
    public void BattanianArmourExample()
    {
        // Tier 7.5 Battanian armour: Athletics 200. (200 - 5) / 1.2 = 162.5 -> 163.
        Assert.Equal(163, CultureBonus.MinimumSkill(200, 5, 1.2f));
        Assert.True(Requirements.Shortfall(200, CultureBonus.Effective(163, true, Defaults)) == 0f); // 200.6
        Assert.True(Requirements.Shortfall(200, CultureBonus.Effective(162, true, Defaults)) > 0f); // 199.4
        Assert.Equal(199, CultureBonus.Shown(CultureBonus.Effective(162, true, Defaults)));          // never "200" when short
    }

    [Theory]
    [InlineData(199.4, 199)]
    [InlineData(200.6, 200)]
    [InlineData(185.0, 185)]
    [InlineData(-3.0, 0)]
    public void ShownRoundsDown(double effective, int shown) => Assert.Equal(shown, CultureBonus.Shown(effective));

    [Fact]
    public void ShownBonusIsTheShownDifference()
    {
        Assert.Equal(35, CultureBonus.ShownBonus(150, 185.0));
        Assert.Equal(0, CultureBonus.ShownBonus(150, 150.0));
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(5, 0)]   // the points alone meet it
    [InlineData(6, 1)]   // 1 x 1.2 + 5 = 6.2
    [InlineData(150, 121)] // 120 -> 149, 121 -> 150.2
    public void MinimumSkillIsTheSmallestThatMeetsIt(int requirement, int expected)
    {
        int n = CultureBonus.MinimumSkill(requirement, 5, 1.2f);
        Assert.Equal(expected, n);
        Assert.True(CultureBonus.Effective(n, 5, 1.2f) >= requirement);
        if (n > 0) Assert.True(CultureBonus.Effective(n - 1, 5, 1.2f) < requirement);
    }

    [Fact]
    public void FractionalShortfallMatchesTheIntegerOne()
    {
        Assert.Equal(Requirements.Shortfall(200, 150), Requirements.Shortfall(200, 150.0), 6);
        Assert.Equal(0.075f, Requirements.Shortfall(200, 185.0), 5);
        Assert.Equal(0f, Requirements.Shortfall(0, 10.0));
        Assert.Equal(1f, Requirements.Shortfall(200, -5.0));
    }

    [Fact]
    public void BonusLiftsThePenaltyCurve()
    {
        // 150 Athletics on a 200 armour: r = 0.75 raw, 0.925 with the bonus -> a smaller foot speed penalty.
        float raw = Requirements.Shortfall(200, 150);
        float boosted = Requirements.Shortfall(200, CultureBonus.Effective(150, true, Defaults));
        Assert.True(boosted < raw);
        Assert.True(Penalties.ArmourPiecePenalty(boosted, Defaults) < Penalties.ArmourPiecePenalty(raw, Defaults));
    }

    [Fact]
    public void MenuSnapsTheMultiplierTo005AndClampsTheRange()
    {
        var s = new SkillRequirementSettings();
        var mult = SettingsMenu.Find(nameof(SkillRequirementSettings.SameCultureBonusMultiplier))!;
        Assert.Equal(1.0, mult.Min);
        Assert.Equal(2.0, mult.Max);
        mult.Set(s, 1.33);
        Assert.Equal(1.35f, s.SameCultureBonusMultiplier, 4);
        mult.Set(s, 5);
        Assert.Equal(2f, s.SameCultureBonusMultiplier, 4);

        var add = SettingsMenu.Find(nameof(SkillRequirementSettings.SameCultureBonusPoints))!;
        Assert.Equal(0.0, add.Min);
        Assert.Equal(50.0, add.Max);
        Assert.False(add.AffectsRequirements); // the skill moves, not R: no requirement table rebuild needed
        Assert.False(mult.AffectsRequirements);
    }

    [Theory]
    [InlineData("battania", "Battania", "Battanian")]
    [InlineData("empire", "Empire", "Imperial")]
    [InlineData("nord", "Nord", "Nord")]
    [InlineData("darshi", "Darshi", "Darshi")]   // no adjective known: the game's name
    [InlineData("modded", null, "modded")]       // nothing at all: the id
    public void AdjectiveForTheTooltip(string id, string? name, string expected) =>
        Assert.Equal(expected, CultureBonus.Adjective(id, name));

    [Fact]
    public void YoursTextShowsTheBonusOnlyWhenThereIsOne()
    {
        Assert.Equal("150 +35 Battanian", CultureBonus.YoursText(150, CultureBonus.Effective(150, true, Defaults), "Battanian"));
        Assert.Equal("150", CultureBonus.YoursText(150, 150.0, "Battanian"));
        Assert.Equal("150 +35", CultureBonus.YoursText(150, 185.0, null));
    }

    [Fact]
    public void HighlightUsesTheEffectiveSkill()
    {
        // 162 raw -> 199.4: still orange; 163 -> 200.6: not.
        Assert.True(InventoryHighlight.AboveSkill(Defaults, true, true, 200, CultureBonus.Effective(162, true, Defaults)));
        Assert.False(InventoryHighlight.AboveSkill(Defaults, true, true, 200, CultureBonus.Effective(163, true, Defaults)));
        Assert.True(TooltipPenalties.IsShort(200, 199.4));
        Assert.False(TooltipPenalties.IsShort(200, 200.0));
    }
}
