using GearMastery.Core;

namespace GearMastery.Core.Tests;

public class CombatPenaltiesTests
{
    private static readonly GearMasterySettings Defaults = new();

    [Fact]
    public void TroopsNeverPay()
    {
        Assert.False(CombatPenalties.AppliesTo(PenaltyTarget.Troop, false, Defaults));
        Assert.False(CombatPenalties.AppliesTo(PenaltyTarget.Troop, true, Defaults));
        Assert.False(CombatPenalties.AppliesTo(PenaltyTarget.Player, false, null!));
    }

    [Theory]
    [InlineData(PenaltyTarget.Player)]
    [InlineData(PenaltyTarget.Companion)]
    [InlineData(PenaltyTarget.OtherHero)]
    public void HeroesPayByDefaultIncludingTournaments(PenaltyTarget who)
    {
        Assert.True(CombatPenalties.AppliesTo(who, false, Defaults));
        Assert.True(CombatPenalties.AppliesTo(who, true, Defaults));
    }

    [Fact]
    public void EachToggleSparesOnlyItsOwnGroup()
    {
        var noPlayer = new GearMasterySettings { ApplyToPlayer = false };
        Assert.False(CombatPenalties.AppliesTo(PenaltyTarget.Player, false, noPlayer));
        Assert.True(CombatPenalties.AppliesTo(PenaltyTarget.Companion, false, noPlayer));
        Assert.True(CombatPenalties.AppliesTo(PenaltyTarget.OtherHero, false, noPlayer));

        var noCompanions = new GearMasterySettings { ApplyToCompanions = false };
        Assert.True(CombatPenalties.AppliesTo(PenaltyTarget.Player, false, noCompanions));
        Assert.False(CombatPenalties.AppliesTo(PenaltyTarget.Companion, false, noCompanions));
        Assert.True(CombatPenalties.AppliesTo(PenaltyTarget.OtherHero, false, noCompanions));

        var noLords = new GearMasterySettings { ApplyToOtherHeroes = false };
        Assert.True(CombatPenalties.AppliesTo(PenaltyTarget.Player, false, noLords));
        Assert.True(CombatPenalties.AppliesTo(PenaltyTarget.Companion, false, noLords));
        Assert.False(CombatPenalties.AppliesTo(PenaltyTarget.OtherHero, false, noLords));
    }

    [Fact]
    public void TournamentToggleSparesEveryoneInATournamentOnly()
    {
        var s = new GearMasterySettings { ApplyInTournaments = false };
        Assert.False(CombatPenalties.AppliesTo(PenaltyTarget.Player, true, s));
        Assert.False(CombatPenalties.AppliesTo(PenaltyTarget.OtherHero, true, s));
        Assert.True(CombatPenalties.AppliesTo(PenaltyTarget.Player, false, s));
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
        Assert.Equal(0.25f, m.Damage, 4);
        Assert.Equal(0.625f, m.Swing, 4);
        Assert.Equal(0.625f, m.ThrustOrRangedReady, 4);
        Assert.Equal(0.625f, m.Handling, 4);
        Assert.Equal(1f, m.Reload);
        Assert.Equal(1f, m.Inaccuracy);
    }

    [Fact]
    public void RangedAtFullShortfall()
    {
        var m = CombatPenalties.ForWielded(SkillKind.Ranged, 1f, Defaults);
        Assert.Equal(0.25f, m.Damage, 4);
        Assert.Equal(1f, m.Swing);
        Assert.Equal(1f, m.Handling);
        Assert.Equal(0.625f, m.Reload, 4);
        Assert.Equal(0.625f, m.ThrustOrRangedReady, 4);
        Assert.Equal(1.75f, m.Inaccuracy, 4);
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
        // shortfall 0.4 = r 0.6: the Mid anchors.
        var m = CombatPenalties.ForWielded(SkillKind.Weapon, 0.4f, Defaults);
        Assert.Equal(0.5f, m.Damage, 4);
        Assert.Equal(0.75f, m.Swing, 4);
        Assert.Equal(0.75f, m.Handling, 4);
    }

    [Fact]
    public void DamageMultiplierPicksTheRightFamily()
    {
        // A sword blow: weapon penalty.
        Assert.Equal(0.25f, CombatPenalties.DamageMultiplier(SkillKind.Weapon, false, 1f, Defaults), 4);
        // An arrow from an under-skilled bow: ranged penalty.
        Assert.Equal(0.25f, CombatPenalties.DamageMultiplier(SkillKind.Ranged, true, 1f, Defaults), 4);
        // An arrow whose shooter now holds a sword: nothing.
        Assert.Equal(1f, CombatPenalties.DamageMultiplier(SkillKind.Weapon, true, 1f, Defaults));
        // A bow / shield used as the "weapon" of a non-ammo blow: nothing.
        Assert.Equal(1f, CombatPenalties.DamageMultiplier(SkillKind.Ranged, false, 1f, Defaults));
        Assert.Equal(1f, CombatPenalties.DamageMultiplier(SkillKind.Shield, false, 1f, Defaults));
        Assert.Equal(1f, CombatPenalties.DamageMultiplier(SkillKind.Weapon, false, 0f, Defaults));

        var tough = new GearMasterySettings { RangedDamagePenaltyHigh = 0.6f };
        Assert.Equal(0.4f, CombatPenalties.DamageMultiplier(SkillKind.Ranged, true, 1f, tough), 4);
    }

    [Fact]
    public void ThrownUsageTakesTheRangedAnchors()
    {
        var tough = new GearMasterySettings { RangedDamagePenaltyHigh = 0.6f, RangedInaccuracyBonusHigh = 1f };
        Assert.True(CombatPenalties.IsThrownUsage(SkillKind.Weapon, true));
        Assert.False(CombatPenalties.IsThrownUsage(SkillKind.Weapon, false));
        Assert.False(CombatPenalties.IsThrownUsage(SkillKind.Ranged, true));

        // Wielded javelin in throwing mode: ranged damage + aim, no swing / ready / handling / reload.
        var m = CombatPenalties.ForWielded(SkillKind.Weapon, 1f, tough, thrownUsage: true);
        Assert.Equal(0.4f, m.Damage, 4);
        Assert.Equal(2f, m.Inaccuracy, 4);
        Assert.Equal(1f, m.Swing);
        Assert.Equal(1f, m.ThrustOrRangedReady);
        Assert.Equal(1f, m.Handling);
        Assert.Equal(1f, m.Reload);

        // Its blow: ranged damage anchors; held (melee) usage keeps the melee ones.
        Assert.Equal(0.4f, CombatPenalties.DamageMultiplier(SkillKind.Weapon, false, 1f, tough, thrownUsage: true), 4);
        Assert.Equal(0.25f, CombatPenalties.DamageMultiplier(SkillKind.Weapon, false, 1f, tough, thrownUsage: false), 4);

        // Inaccuracy: launchers and thrown usages only.
        Assert.Equal(2f, CombatPenalties.InaccuracyMultiplier(SkillKind.Weapon, true, 1f, tough), 4);
        Assert.Equal(2f, CombatPenalties.InaccuracyMultiplier(SkillKind.Ranged, true, 1f, tough), 4);
        Assert.Equal(1f, CombatPenalties.InaccuracyMultiplier(SkillKind.Weapon, false, 1f, tough));
        Assert.Equal(1f, CombatPenalties.InaccuracyMultiplier(SkillKind.Ranged, true, 0f, tough));
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
    // ---- starter cap (Toni, 2026-11-05: Hunting Bow at Bow 0 showed -75%; there are no tier 0 bows) ----

    private const float HuntingBowTierf = 0.75f; // vanilla "Tier 1", R = 15

    [Fact]
    public void HuntingBowAtBowZero_DamageCappedAtTwentyPercent()
    {
        int r = Requirements.Requirement(SkillKind.Ranged, HuntingBowTierf, Defaults);
        Assert.Equal(15, r);
        int tier = Requirements.DisplayedTier(HuntingBowTierf);
        Assert.Equal(1, tier);
        float s = Requirements.Shortfall(r, 0);

        Assert.Equal(0.8f, CombatPenalties.DamageMultiplier(SkillKind.Ranged, true, s, Defaults, false, tier));
        var m = CombatPenalties.ForWielded(SkillKind.Ranged, s, Defaults, false, tier);
        Assert.Equal(0.8f, m.Damage);
        Assert.Equal(0.8f, m.Reload);
        Assert.Equal(0.8f, m.ThrustOrRangedReady);
        Assert.Equal(1.2f, m.Inaccuracy);
        Assert.Equal(1.2f, CombatPenalties.InaccuracyMultiplier(SkillKind.Ranged, false, s, Defaults, tier));

        // The tooltip reads the same functions: "Damage: 32 (40-8)".
        float tm = TooltipStats.Multiplier(SkillKind.Ranged, false, TooltipStat.LauncherDamage, s, Defaults, tier);
        Assert.Equal(32, TooltipStats.After(TooltipStat.LauncherDamage, 40, tm));
        Assert.Equal(0.8f, TooltipStats.Multiplier(SkillKind.Ranged, false, TooltipStat.LauncherSpeed, s, Defaults, tier));
        Assert.Equal(1.2f, TooltipStats.Multiplier(SkillKind.Ranged, false, TooltipStat.Accuracy, s, Defaults, tier));

        // Snap untouched: skill >= R -> no penalty.
        Assert.Equal(1f, CombatPenalties.DamageMultiplier(SkillKind.Ranged, true, Requirements.Shortfall(r, 15), Defaults, false, tier));
    }

    [Fact]
    public void TierTwoBow_NotCapped()
    {
        const float tierf = 2f;
        int tier = Requirements.DisplayedTier(tierf);
        Assert.Equal(2, tier);
        float s = Requirements.Shortfall(Requirements.Requirement(SkillKind.Ranged, tierf, Defaults), 0);
        Assert.Equal(Penalties.RangedDamageMultiplier(s, Defaults), CombatPenalties.DamageMultiplier(SkillKind.Ranged, true, s, Defaults, false, tier));
        Assert.Equal(0.25f, CombatPenalties.DamageMultiplier(SkillKind.Ranged, true, s, Defaults, false, tier), 4);
        var m = CombatPenalties.ForWielded(SkillKind.Ranged, s, Defaults, false, tier);
        Assert.Equal(Penalties.ReloadSpeedMultiplier(s, Defaults), m.Reload);
        Assert.Equal(Penalties.InaccuracyMultiplier(s, Defaults), m.Inaccuracy);
    }

    [Fact]
    public void StarterCapOfHundredPercent_ChangesNothing()
    {
        var settings = new GearMasterySettings { StarterGearMaxPenalty = 1f, RangedInaccuracyBonusHigh = 2.5f };
        int tier = Requirements.DisplayedTier(HuntingBowTierf);
        foreach (float s in new[] { 0.05f, 0.4f, 0.7f, 1f })
        {
            var capped = CombatPenalties.ForWielded(SkillKind.Ranged, s, settings, false, tier);
            var plain = CombatPenalties.ForWielded(SkillKind.Ranged, s, settings);
            Assert.Equal(plain.Damage, capped.Damage);
            Assert.Equal(plain.Reload, capped.Reload);
            Assert.Equal(plain.Inaccuracy, capped.Inaccuracy);
            Assert.Equal(Penalties.InaccuracyMultiplier(s, settings), CombatPenalties.InaccuracyMultiplier(SkillKind.Ranged, false, s, settings, tier));
        }
    }

    [Fact]
    public void StarterCap_OnlyLaunchersAtOrBelowTheMaxTier()
    {
        Assert.Equal(0.2f, CombatPenalties.StarterCap(SkillKind.Ranged, 0, Defaults));
        Assert.Equal(0.2f, CombatPenalties.StarterCap(SkillKind.Ranged, 1, Defaults));
        Assert.Equal(1f, CombatPenalties.StarterCap(SkillKind.Ranged, 2, Defaults));
        Assert.Equal(1f, CombatPenalties.StarterCap(SkillKind.Ranged, null, Defaults));
        Assert.Equal(1f, CombatPenalties.StarterCap(SkillKind.Weapon, 1, Defaults));
        Assert.Equal(1f, CombatPenalties.StarterCap(SkillKind.Mount, 1, Defaults));
        var tier3 = new GearMasterySettings { StarterGearMaxTier = 3 };
        Assert.Equal(0.2f, CombatPenalties.StarterCap(SkillKind.Ranged, 3, tier3));
        // A thrown javelin (Weapon kind) is not starter gear: full ranged anchors.
        Assert.Equal(Penalties.RangedDamageMultiplier(1f, Defaults), CombatPenalties.DamageMultiplier(SkillKind.Weapon, false, 1f, Defaults, true, 1));
    }
}
