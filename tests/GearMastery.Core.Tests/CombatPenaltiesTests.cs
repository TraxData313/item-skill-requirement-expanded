using GearMastery.Core;

namespace GearMastery.Core.Tests;

public class CombatPenaltiesTests
{
    private static readonly GearMasterySettings Defaults = new();
    private static readonly GearMasterySettings Linear = new() { PenaltyExponent = 1f };

    [Fact]
    public void HeroesOnlySparesTroops()
    {
        Assert.True(CombatPenalties.AppliesTo(false, Defaults));
        Assert.True(CombatPenalties.AppliesTo(true, Defaults));
        var heroes = new GearMasterySettings { HeroesOnly = true };
        Assert.False(CombatPenalties.AppliesTo(false, heroes));
        Assert.True(CombatPenalties.AppliesTo(true, heroes));
        Assert.False(CombatPenalties.AppliesTo(true, null!));
    }

    [Theory]
    [InlineData(SkillKind.Weapon)]
    [InlineData(SkillKind.Ranged)]
    [InlineData(SkillKind.Shield)]
    [InlineData(SkillKind.Armour)]
    [InlineData(SkillKind.Mount)]
    public void NoShortfallIsIdentity(SkillKind kind)
    {
        Assert.True(CombatPenalties.ForWielded(kind, 0f, Defaults).IsIdentity);
        Assert.True(CombatPenalties.ForWielded(kind, float.NaN, Defaults).IsIdentity);
        Assert.True(CombatPenalties.ForWielded(kind, -1f, Defaults).IsIdentity);
    }

    [Fact]
    public void MeleeWeaponAtFullShortfall()
    {
        var m = CombatPenalties.ForWielded(SkillKind.Weapon, 1f, Defaults);
        Assert.Equal(0.85f, m.Damage, 4);
        Assert.Equal(0.90f, m.Swing, 4);
        Assert.Equal(0.90f, m.ThrustOrRangedReady, 4);
        Assert.Equal(0.85f, m.Handling, 4);
        Assert.Equal(1f, m.Reload);
        Assert.Equal(1f, m.Inaccuracy);
    }

    [Fact]
    public void RangedAtFullShortfall()
    {
        var m = CombatPenalties.ForWielded(SkillKind.Ranged, 1f, Defaults);
        Assert.Equal(0.85f, m.Damage, 4);
        Assert.Equal(1f, m.Swing);
        Assert.Equal(1f, m.Handling);
        Assert.Equal(0.90f, m.Reload, 4);
        Assert.Equal(0.90f, m.ThrustOrRangedReady, 4);
        Assert.Equal(1.30f, m.Inaccuracy, 4);
    }

    [Fact]
    public void ShieldArmourMountHaveNoWieldedPenalty()
    {
        Assert.True(CombatPenalties.ForWielded(SkillKind.Shield, 1f, Defaults).IsIdentity);
        Assert.True(CombatPenalties.ForWielded(SkillKind.Armour, 1f, Defaults).IsIdentity);
        Assert.True(CombatPenalties.ForWielded(SkillKind.Mount, 1f, Defaults).IsIdentity);
    }

    [Fact]
    public void PartialShortfallFollowsTheCurve()
    {
        // p = 0.5: shortfall 0.25 -> curve 0.5 -> half the maximum.
        var m = CombatPenalties.ForWielded(SkillKind.Weapon, 0.25f, Defaults);
        Assert.Equal(1f - 0.15f * 0.5f, m.Damage, 4);
        Assert.Equal(1f - 0.10f * 0.5f, m.Swing, 4);
        var lin = CombatPenalties.ForWielded(SkillKind.Weapon, 0.25f, Linear);
        Assert.Equal(1f - 0.15f * 0.25f, lin.Damage, 4);
    }

    [Fact]
    public void DamageMultiplierPicksTheRightFamily()
    {
        // A sword blow: weapon penalty.
        Assert.Equal(0.85f, CombatPenalties.DamageMultiplier(SkillKind.Weapon, false, 1f, Defaults), 4);
        // An arrow from an under-skilled bow: ranged penalty.
        Assert.Equal(0.85f, CombatPenalties.DamageMultiplier(SkillKind.Ranged, true, 1f, Defaults), 4);
        // An arrow whose shooter now holds a sword: nothing.
        Assert.Equal(1f, CombatPenalties.DamageMultiplier(SkillKind.Weapon, true, 1f, Defaults));
        // A bow / shield used as the "weapon" of a non-ammo blow: nothing.
        Assert.Equal(1f, CombatPenalties.DamageMultiplier(SkillKind.Ranged, false, 1f, Defaults));
        Assert.Equal(1f, CombatPenalties.DamageMultiplier(SkillKind.Shield, false, 1f, Defaults));
        Assert.Equal(1f, CombatPenalties.DamageMultiplier(SkillKind.Weapon, false, 0f, Defaults));

        var tough = new GearMasterySettings { MaxRangedDamagePenalty = 0.4f };
        Assert.Equal(0.6f, CombatPenalties.DamageMultiplier(SkillKind.Ranged, true, 1f, tough), 4);
    }

    [Fact]
    public void MountLockLiftedOnlyForSkill()
    {
        Assert.True(CombatPenalties.AllowMount(true, true, 0f, 0f));
        // vanilla said no because skill 20 < difficulty 60: allowed.
        Assert.True(CombatPenalties.AllowMount(false, true, 20f, 60f));
        // cannot ride at all (no CanRide flag): stays no.
        Assert.False(CombatPenalties.AllowMount(false, false, 20f, 60f));
        // skill was enough, so another reason said no: stays no.
        Assert.False(CombatPenalties.AllowMount(false, true, 80f, 60f));
    }
}
