using GearMastery.Core;

namespace GearMastery.Core.Tests;

public class SettingsFileTests
{
    [Fact]
    public void DefaultFileReadsBackAsDefaultsWithNoIssues()
    {
        var text = SettingsFile.Write(new GearMasterySettings());
        var r = SettingsFile.Read(text);
        Assert.Null(r.Error);
        Assert.Empty(r.Issues);
        Assert.Empty(r.Missing);
        Assert.Equal(SettingsFile.All.Count, r.FromFile);
        foreach (var def in SettingsFile.All)
            Assert.Equal(def.Get(new GearMasterySettings()), def.Get(r.Settings), 5);
    }

    [Fact]
    public void WrittenFileCarriesCommentsAndEveryKey()
    {
        var text = SettingsFile.Write(new GearMasterySettings());
        Assert.Contains("// ", text);
        foreach (var def in SettingsFile.All)
            Assert.Contains("\"" + def.Key + "\":", text);
        Assert.Contains("\"MaxWeaponDamagePenalty\": 0.15", text);
        Assert.Contains("\"HeroesOnly\": false", text);
        Assert.Contains("\"RequirementAtTier6\": 150", text);
    }

    [Fact]
    public void CustomValuesRoundTrip()
    {
        var s = new GearMasterySettings { MaxWeaponDamagePenalty = 0.3f, HeroesOnly = true, RequirementAtTopTier = 180, TopTier = 8f, PenaltyExponent = 1f };
        var r = SettingsFile.Read(SettingsFile.Write(s));
        Assert.Empty(r.Issues);
        Assert.Equal(0.3f, r.Settings.MaxWeaponDamagePenalty, 5);
        Assert.True(r.Settings.HeroesOnly);
        Assert.Equal(180, r.Settings.RequirementAtTopTier);
        Assert.Equal(8f, r.Settings.TopTier, 5);
        Assert.Equal(1f, r.Settings.PenaltyExponent, 5);
    }

    [Fact]
    public void ToleratesCommentsTrailingCommasAndKeyCase()
    {
        var r = SettingsFile.Read("// hi\n{ /* block */ \"maxswingspeedpenalty\": 0.2, // line\n \"heroesonly\": true, }\n");
        Assert.Null(r.Error);
        Assert.Empty(r.Issues);
        Assert.Equal(0.2f, r.Settings.MaxSwingSpeedPenalty, 5);
        Assert.True(r.Settings.HeroesOnly);
        Assert.Equal(2, r.FromFile);
        Assert.Contains("MaxWeaponDamagePenalty", r.Missing);
    }

    [Theory]
    [InlineData("{\"MaxWeaponDamagePenalty\": 1.5}")]   // above the 0.9 cap
    [InlineData("{\"MaxWeaponDamagePenalty\": -0.1}")]  // negative
    [InlineData("{\"MaxWeaponDamagePenalty\": \"0.2\"}")] // a string
    [InlineData("{\"MaxWeaponDamagePenalty\": null}")]
    [InlineData("{\"MaxWeaponDamagePenalty\": [0.2]}")]
    [InlineData("{\"MaxWeaponDamagePenalty\": true}")]
    public void InvalidValueFallsBackToDefaultWithAnIssue(string text)
    {
        var r = SettingsFile.Read(text);
        Assert.Null(r.Error);
        Assert.Single(r.Issues);
        Assert.Contains("MaxWeaponDamagePenalty", r.Issues[0]);
        Assert.Equal(0.15f, r.Settings.MaxWeaponDamagePenalty, 5);
        Assert.Equal(0, r.FromFile);
    }

    [Fact]
    public void WholeNumberAndBoolAreChecked()
    {
        var r = SettingsFile.Read("{\"RequirementAtTier6\": 50.5, \"HeroesOnly\": 1, \"RequirementAtTier5\": 80}");
        Assert.Equal(2, r.Issues.Count);
        Assert.Equal(150, r.Settings.RequirementAtTier6);
        Assert.False(r.Settings.HeroesOnly);
        Assert.Equal(80, r.Settings.RequirementAtTier5);
    }

    [Fact]
    public void UnknownKeyIsReportedAndIgnored()
    {
        var r = SettingsFile.Read("{\"MaxWeaponDamagePenatly\": 0.5}");
        Assert.Single(r.Issues);
        Assert.Contains("not a setting", r.Issues[0]);
        Assert.Equal(0.15f, r.Settings.MaxWeaponDamagePenalty, 5);
    }

    [Fact]
    public void DuplicateKeyLastWins()
    {
        var r = SettingsFile.Read("{\"ShieldRequirementScale\": 0.8, \"ShieldRequirementScale\": 0.9}");
        Assert.Single(r.Issues);
        Assert.Equal(0.9f, r.Settings.ShieldRequirementScale, 5);
    }

    [Theory]
    [InlineData("")]
    [InlineData("not json")]
    [InlineData("{\"TopTier\": 7.5")]
    [InlineData("{\"TopTier\": 7,5}")]   // decimal comma
    [InlineData("{\"TopTier\" 7.5}")]
    [InlineData("[1, 2]")]
    [InlineData("{} trailing")]
    public void BrokenFileMeansAllDefaultsAndAnError(string text)
    {
        var r = SettingsFile.Read(text);
        Assert.NotNull(r.Error);
        Assert.Equal(7.5f, r.Settings.TopTier, 5);
        Assert.Equal(SettingsFile.All.Count, r.Missing.Count);
    }

    [Fact]
    public void ErrorNamesTheLine()
    {
        var r = SettingsFile.Read("{\n\"TopTier\": 7.5,\n\"ShieldRequirementScale\": oops\n}");
        Assert.StartsWith("line 3", r.Error);
    }

    [Fact]
    public void NullTextIsAnError() => Assert.NotNull(SettingsFile.Read(null).Error);

    [Fact]
    public void ByteOrderMarkIsFine()
    {
        var r = SettingsFile.Read("﻿{\"TopTier\": 8}");
        Assert.Null(r.Error);
        Assert.Equal(8f, r.Settings.TopTier, 5);
    }

    [Fact]
    public void EveryDefaultIsInsideItsOwnRange()
    {
        var d = new GearMasterySettings();
        foreach (var def in SettingsFile.All)
        {
            var v = def.Get(d);
            Assert.InRange(v, def.Min, def.Max);
        }
    }
}
