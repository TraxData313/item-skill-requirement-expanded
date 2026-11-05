using GearMastery.Core;

namespace GearMastery.Core.Tests;

public class RequirementsTests
{
    private static readonly GearMasterySettings Defaults = new();

    [Theory]
    [InlineData(1.0f, 10)]   // 12 -> 10
    [InlineData(2.0f, 25)]   // 24 -> 25
    [InlineData(3.0f, 35)]   // 36 -> 35
    [InlineData(4.0f, 50)]   // 48 -> 50
    [InlineData(5.0f, 60)]   // 60
    [InlineData(6.0f, 70)]   // 72 -> 70
    [InlineData(0.2f, 0)]    // 2.4 -> 0
    [InlineData(0.25f, 5)]   // 3.0 -> 5
    public void WeaponUsesSlope12RoundedTo5(float tier, int expected) =>
        Assert.Equal(expected, Requirements.Requirement(SkillKind.Weapon, tier, Defaults));

    [Theory]
    [InlineData(1.0f, 10)]
    [InlineData(3.3f, 35)]   // 33 -> 35
    [InlineData(6.5f, 65)]
    [InlineData(7.24f, 70)]  // 72.4 -> 70
    public void ArmourUsesSlope10(float tier, int expected) =>
        Assert.Equal(expected, Requirements.Requirement(SkillKind.Armour, tier, Defaults));

    [Fact]
    public void ShieldUsesShieldSlope()
    {
        Assert.Equal(35, Requirements.Requirement(SkillKind.Shield, 3f, Defaults));
        var s = new GearMasterySettings { ShieldSlope = 10f };
        Assert.Equal(30, Requirements.Requirement(SkillKind.Shield, 3f, s));
    }

    [Fact]
    public void MidpointRoundsAwayFromZero()
    {
        // 12 x 0.625 = 7.5 -> 10 (banker's rounding would give 10 too; 12 x 1.875 = 22.5 -> 25, banker's gives 20)
        Assert.Equal(10, Requirements.Requirement(SkillKind.Weapon, 0.625f, Defaults));
        Assert.Equal(25, Requirements.Requirement(SkillKind.Weapon, 1.875f, Defaults));
    }

    [Theory]
    [InlineData(-1f)]
    [InlineData(-0.01f)]
    [InlineData(0f)]
    [InlineData(float.NaN)]
    [InlineData(float.NegativeInfinity)]
    public void NonPositiveOrBadTierAsksNothing(float tier)
    {
        Assert.Equal(0, Requirements.Requirement(SkillKind.Weapon, tier, Defaults));
        Assert.Equal(0, Requirements.Requirement(SkillKind.Armour, tier, Defaults));
    }

    [Fact]
    public void ClampsAtCap()
    {
        Assert.Equal(100, Requirements.Requirement(SkillKind.Weapon, 50f, Defaults));
        Assert.Equal(0, Requirements.Requirement(SkillKind.Weapon, float.PositiveInfinity, Defaults)); // infinity is bad data
        Assert.Equal(100, Requirements.Requirement(SkillKind.Armour, 12f, Defaults));
        var s = new GearMasterySettings { WeaponCap = 42, ArmourCap = 33 };
        Assert.Equal(42, Requirements.Requirement(SkillKind.Weapon, 6f, s));  // 72 > 42: cap itself, not a multiple of 5
        Assert.Equal(42, Requirements.Requirement(SkillKind.Shield, 6f, s));
        Assert.Equal(33, Requirements.Requirement(SkillKind.Armour, 6f, s));
    }

    [Fact]
    public void RoundingNeverExceedsCap()
    {
        // 12 x 8.2 = 98.4 rounds to 100 but cap 99 must win.
        Assert.Equal(99, Requirements.Requirement(8.2f, 12f, 99));
    }

    [Theory]
    [InlineData(0f, 100)]
    [InlineData(-5f, 100)]
    [InlineData(float.NaN, 100)]
    [InlineData(12f, 0)]
    [InlineData(12f, -10)]
    public void BadSlopeOrCapAsksNothing(float slope, int cap) =>
        Assert.Equal(0, Requirements.Requirement(5f, slope, cap));

    [Fact]
    public void DifficultyKindsIgnoreTierFormula()
    {
        Assert.Equal(0, Requirements.Requirement(SkillKind.Ranged, 5f, Defaults));
        Assert.Equal(0, Requirements.Requirement(SkillKind.Mount, 5f, Defaults));
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(-10, 0)]
    [InlineData(150, 150)]
    [InlineData(7, 7)]
    public void FromDifficultyFloorsAtZero(int difficulty, int expected) =>
        Assert.Equal(expected, Requirements.FromDifficulty(difficulty));

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
