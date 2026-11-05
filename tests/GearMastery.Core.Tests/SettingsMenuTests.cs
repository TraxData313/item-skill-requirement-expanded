using System.Reflection;
using GearMastery.Core;

namespace GearMastery.Core.Tests;

public class SettingsMenuTests
{
    private static readonly PropertyInfo[] SettingProperties = typeof(GearMasterySettings)
        .GetProperties(BindingFlags.Public | BindingFlags.Instance)
        .Where(p => p.CanWrite)
        .ToArray();

    [Fact]
    public void EverySettingIsOnThePageExactlyOnce()
    {
        var ids = SettingsMenu.All.Select(s => s.Id).ToList();
        Assert.Equal(ids.Count, ids.Distinct().Count());
        Assert.Equal(SettingProperties.Select(p => p.Name).OrderBy(n => n), ids.OrderBy(n => n));
        Assert.Equal(51, ids.Count);
    }

    [Fact]
    public void EachEntryReadsAndWritesItsOwnProperty()
    {
        foreach (var s in SettingsMenu.All)
        {
            var settings = new GearMasterySettings();
            var prop = SettingProperties.Single(p => p.Name == s.Id);
            var before = SettingProperties.ToDictionary(p => p.Name, p => p.GetValue(settings));

            double target = s.Kind == MenuKind.Bool ? 1 - s.Get(settings) : s.Get(settings) == s.Max ? s.Min : s.Max;
            Assert.True(s.Set(settings, target), s.Id);
            Assert.Equal(target, s.Get(settings), 3);

            foreach (var other in SettingProperties.Where(p => p != prop))
                Assert.Equal(before[other.Name], other.GetValue(settings));
            Assert.NotEqual(before[prop.Name], prop.GetValue(settings));
        }
    }

    [Fact]
    public void DefaultsSitInsideTheirRanges()
    {
        var d = new GearMasterySettings();
        foreach (var s in SettingsMenu.All)
        {
            double v = s.Get(d);
            Assert.InRange(v, s.Min, s.Max);
            Assert.True(s.Min < s.Max, s.Id);
        }
    }

    [Fact]
    public void PercentsShowAsPercentAndStoreFractions()
    {
        var s = new GearMasterySettings();
        var high = SettingsMenu.Find(nameof(GearMasterySettings.MeleeDamagePenaltyHigh))!;
        Assert.Equal(MenuKind.Percent, high.Kind);
        Assert.Equal(75, high.Get(s), 4);
        high.Set(s, 40);
        Assert.Equal(0.4f, s.MeleeDamagePenaltyHigh, 5);

        var swing = SettingsMenu.Find(nameof(GearMasterySettings.MeleeSwingSpeedPenaltyLow))!;
        Assert.Equal(12.5, swing.Get(s), 4);

        var mid = SettingsMenu.Find(nameof(GearMasterySettings.PenaltyMidRatio))!;
        Assert.Equal(60, mid.Get(s), 4);
        Assert.Equal("0.0'%'", high.Format);
    }

    [Fact]
    public void SetClampsAndRoundsAndIgnoresNaN()
    {
        var s = new GearMasterySettings();
        var dmg = SettingsMenu.Find(nameof(GearMasterySettings.MeleeDamagePenaltyHigh))!;
        dmg.Set(s, 250);
        Assert.Equal(0.9f, s.MeleeDamagePenaltyHigh, 5); // reductions stop at 90%
        dmg.Set(s, -5);
        Assert.Equal(0f, s.MeleeDamagePenaltyHigh, 5);
        Assert.False(dmg.Set(s, double.NaN));

        var tier6 = SettingsMenu.Find(nameof(GearMasterySettings.RequirementAtTier6))!;
        tier6.Set(s, 162.6);
        Assert.Equal(163, s.RequirementAtTier6);
        tier6.Set(s, 1000);
        Assert.Equal(330, s.RequirementAtTier6);

        var flag = SettingsMenu.Find(nameof(GearMasterySettings.ApplyToCompanions))!;
        flag.Set(s, 0);
        Assert.False(s.ApplyToCompanions);
        Assert.False(flag.Set(s, 0)); // unchanged
    }

    [Fact]
    public void RequirementSettingsAreFlaggedAndNothingElse()
    {
        var req = SettingsMenu.All.Where(s => s.AffectsRequirements).Select(s => s.Id).OrderBy(n => n).ToList();
        var expected = new[]
        {
            "RequirementAtTier0", "RequirementAtTier1", "RequirementAtTier2", "RequirementAtTier3", "RequirementAtTier4",
            "RequirementAtTier5", "RequirementAtTier6", "TopTier", "RequirementAtTopTier",
            "MeleeRequirementScale", "ShieldRequirementScale", "ArmourRequirementScale", "RangedRequirementScale", "MountRequirementScale",
        }.OrderBy(n => n);
        Assert.Equal(expected, req);
        Assert.All(SettingsMenu.All.Where(s => s.AffectsRequirements), s => Assert.Equal(SettingsMenu.Requirements, s.Group));
    }

    [Fact]
    public void GroupsAreInPageOrderAndEachHasSettings()
    {
        Assert.Equal(new[] { "Who is affected", "Requirements", "Penalty curve", "Melee weapons", "Armour", "Shields",
            "Bows, crossbows and slings", "Horses", "Debug" }, SettingsMenu.Groups.Select(g => g.Name));
        Assert.Equal(Enumerable.Range(0, SettingsMenu.Groups.Count), SettingsMenu.Groups.Select(g => g.Order));
        foreach (var g in SettingsMenu.Groups)
            Assert.NotEmpty(SettingsMenu.InGroup(g));
        Assert.All(SettingsMenu.All, s => Assert.Contains(s.Group, SettingsMenu.Groups));
        Assert.All(SettingsMenu.All, s => Assert.False(string.IsNullOrWhiteSpace(s.Hint), s.Id));
        Assert.All(SettingsMenu.InGroup(SettingsMenu.Who), s => Assert.Contains("roops", s.Hint)); // "Troops never ..."
    }

    [Fact]
    public void MenuChangesReachThePenaltyMaths()
    {
        var s = new GearMasterySettings();
        SettingsMenu.Find(nameof(GearMasterySettings.MeleeDamagePenaltyLow))!.Set(s, 10);
        // skill 99 of R 100: just below -> about the Low value
        Assert.Equal(0.9f, Penalties.WeaponDamageMultiplier(0.0001f, s), 2);

        SettingsMenu.Find(nameof(GearMasterySettings.MeleeRequirementScale))!.Set(s, 0.5);
        Assert.Equal(75, Requirements.Requirement(SkillKind.Weapon, 6f, s)); // 150 x 0.5
    }
}
