using ItemSkillRequirementExpanded.Core;

namespace ItemSkillRequirementExpanded.Core.Tests;

public class RequirementsTests
{
    private static readonly SkillRequirementSettings Defaults = new();

    [Theory]
    [InlineData(0f, 0)]
    [InlineData(1f, 20)]
    [InlineData(2f, 45)]
    [InlineData(3f, 75)]
    [InlineData(4f, 100)]
    [InlineData(5f, 125)]
    [InlineData(6f, 150)]
    [InlineData(7.5f, 200)]
    public void DefaultTableHitsItsPoints(float tier, int expected) =>
        Assert.Equal(expected, Requirements.Requirement(SkillKind.Weapon, tier, Defaults));

    [Theory]
    [InlineData(0.5f, 10)]   // 10
    [InlineData(1.5f, 35)]   // 32.5 -> 35 (half away from zero)
    [InlineData(2.5f, 60)]   // 60
    [InlineData(5.5f, 140)]  // 137.5 -> 140: displayed "Tier 6" low edge
    [InlineData(6.28f, 160)] // Falx (battania_2hsword_6): 159.3 -> 160
    [InlineData(6.75f, 175)] // 175
    [InlineData(7f, 185)]    // 183.3 -> 185
    public void InterpolatesBetweenPointsAndRoundsTo5(float tier, int expected) =>
        Assert.Equal(expected, Requirements.Requirement(SkillKind.Weapon, tier, Defaults));

    [Theory]
    [InlineData(-1.5f, 0)]
    [InlineData(-0.01f, 0)]
    [InlineData(7.6f, 200)]
    [InlineData(9.1f, 200)]  // Khan's Coat of Plates
    [InlineData(22.7f, 200)] // dummy armour
    public void ClampedAtTheEnds(float tier, int expected) =>
        Assert.Equal(expected, Requirements.Requirement(SkillKind.Armour, tier, Defaults));

    [Theory]
    [InlineData(float.NaN)]
    [InlineData(float.PositiveInfinity)]
    [InlineData(float.NegativeInfinity)]
    public void BadTierAsksNothing(float tier) =>
        Assert.Equal(0, Requirements.Requirement(SkillKind.Weapon, tier, Defaults));

    [Fact]
    public void EveryCategoryUsesTheSameTableByDefault()
    {
        foreach (SkillKind kind in System.Enum.GetValues(typeof(SkillKind)))
            Assert.Equal(160, Requirements.Requirement(kind, 6.28f, Defaults));
    }

    [Fact]
    public void CategoryScaleMultipliesBeforeRounding()
    {
        var s = new SkillRequirementSettings { ArmourRequirementScale = 0.8f, MountRequirementScale = 1.1f, ShieldRequirementScale = 0f };
        Assert.Equal(100, Requirements.Requirement(SkillKind.Armour, 5f, s));  // 125 x 0.8
        Assert.Equal(165, Requirements.Requirement(SkillKind.Mount, 6f, s));   // 150 x 1.1
        Assert.Equal(0, Requirements.Requirement(SkillKind.Shield, 6f, s));    // scale 0 = off
        Assert.Equal(150, Requirements.Requirement(SkillKind.Weapon, 6f, s));  // untouched
        Assert.Equal(150, Requirements.Requirement(SkillKind.Ranged, 6f, s));
        var neg = new SkillRequirementSettings { MeleeRequirementScale = -1f };
        Assert.Equal(0, Requirements.Requirement(SkillKind.Weapon, 6f, neg));
    }

    [Fact]
    public void TablePointsAreSettings()
    {
        var s = new SkillRequirementSettings { RequirementAtTier6 = 170, TopTier = 8f, RequirementAtTopTier = 230 };
        Assert.Equal(170, Requirements.Requirement(SkillKind.Weapon, 6f, s));
        Assert.Equal(200, Requirements.Requirement(SkillKind.Weapon, 7f, s));  // 170 + 60 x 0.5
        Assert.Equal(230, Requirements.Requirement(SkillKind.Weapon, 9f, s));
    }

    [Fact]
    public void TopTierBelowSixIsSortedNotBroken()
    {
        // A bad TopTier (5) sorts before tier 6: the table still interpolates on sorted points, no exception, and
        // never falls - tier 6 (150) is raised to the 200 asked at tier 5.
        var s = new SkillRequirementSettings { TopTier = 5f, RequirementAtTopTier = 200 };
        Assert.Equal(200, Requirements.Requirement(SkillKind.Weapon, 7f, s));
    }

    [Fact]
    public void FromTableHandlesOddInput()
    {
        Assert.Equal(0f, Requirements.FromTable(3f, new TierPoint[0]));
        Assert.Equal(42f, Requirements.FromTable(3f, new[] { new TierPoint(1f, 42f) }));
        var unsorted = new[] { new TierPoint(2f, 20f), new TierPoint(0f, 0f) };
        Assert.Equal(10f, Requirements.FromTable(1f, unsorted), 4);
        var dup = new[] { new TierPoint(0f, 0f), new TierPoint(1f, 10f), new TierPoint(1f, 30f), new TierPoint(2f, 40f) };
        float v = Requirements.FromTable(1f, dup);
        Assert.True(v == 10f || v == 30f); // duplicate tiers: either point, never garbage
        Assert.Equal(40f, Requirements.FromTable(2f, dup), 4);
        var withNan = new[] { new TierPoint(0f, 0f), new TierPoint(float.NaN, 999f), new TierPoint(2f, 20f) };
        Assert.Equal(10f, Requirements.FromTable(1f, withNan), 4);
    }

    [Fact]
    public void MisorderedTierTableNeverFalls()
    {
        // Tier 4 set below tier 3 in the menu: tier 4 asks what tier 3 asks, never less.
        var s = new SkillRequirementSettings { RequirementAtTier4 = 10 };
        Assert.Equal(75, Requirements.Requirement(SkillKind.Weapon, 4f, s));
        Assert.Equal(75, Requirements.Requirement(SkillKind.Weapon, 3.5f, s));
        Assert.Equal(125, Requirements.Requirement(SkillKind.Weapon, 5f, s));
        int prev = 0;
        for (float t = 0f; t <= 10f; t += 0.05f)
        {
            int r = Requirements.Requirement(SkillKind.Weapon, t, s);
            Assert.True(r >= prev, $"tier {t}: {r} < {prev}");
            prev = r;
        }
        // Top point set below tier 6: held at tier 6's value.
        var top = new SkillRequirementSettings { RequirementAtTopTier = 50 };
        Assert.Equal(150, Requirements.Requirement(SkillKind.Weapon, 9f, top));
    }

    [Theory]
    [InlineData(0.0, 0)]
    [InlineData(-3.0, 0)]
    [InlineData(2.4, 0)]
    [InlineData(2.5, 5)]
    [InlineData(22.5, 25)]   // banker's would give 20
    [InlineData(137.5, 140)]
    [InlineData(double.NaN, 0)]
    public void RoundTo5HalvesAwayFromZero(double v, int expected) =>
        Assert.Equal(expected, Requirements.RoundTo5(v));

    [Theory]
    [InlineData(-1.2f, 0)]
    [InlineData(0.4f, 0)]
    [InlineData(0.5f, 0)]   // MathF.Round = banker's: 0.5 -> 0
    [InlineData(1.5f, 2)]
    [InlineData(2.5f, 2)]
    [InlineData(5.49f, 5)]
    [InlineData(5.5f, 6)]
    [InlineData(6.28f, 6)]
    [InlineData(9.1f, 6)]
    public void DisplayedTierMatchesVanilla(float tierf, int expected) =>
        Assert.Equal(expected, Requirements.DisplayedTier(tierf));

    [Theory]
    [InlineData(100, 0, 1f)]
    [InlineData(100, 50, 0.5f)]
    [InlineData(100, 75, 0.25f)]
    [InlineData(100, 100, 0f)]
    [InlineData(100, 330, 0f)]
    [InlineData(90, 30, 0.6666667f)]
    [InlineData(100, -20, 1f)]   // negative skill clamps to 1
    [InlineData(0, 0, 0f)]       // no requirement
    [InlineData(-5, 0, 0f)]
    public void ShortfallIsClampedShare(int r, int skill, float expected) =>
        Assert.Equal(expected, Requirements.Shortfall(r, skill), 5);
}
