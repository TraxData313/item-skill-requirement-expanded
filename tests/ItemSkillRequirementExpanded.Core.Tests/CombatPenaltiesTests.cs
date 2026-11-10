using ItemSkillRequirementExpanded.Core;

namespace ItemSkillRequirementExpanded.Core.Tests;

public class CombatPenaltiesTests
{
    private static readonly SkillRequirementSettings Defaults = new();

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
    public void HeroesPayByDefaultButNotInTournaments(PenaltyTarget who)
    {
        Assert.True(CombatPenalties.AppliesTo(who, false, Defaults));
        Assert.False(CombatPenalties.AppliesTo(who, true, Defaults)); // tournaments off by default (2026-11-08)
    }

    [Fact]
    public void EachToggleSparesOnlyItsOwnGroup()
    {
        var noPlayer = new SkillRequirementSettings { ApplyToPlayer = false };
        Assert.False(CombatPenalties.AppliesTo(PenaltyTarget.Player, false, noPlayer));
        Assert.True(CombatPenalties.AppliesTo(PenaltyTarget.Companion, false, noPlayer));
        Assert.True(CombatPenalties.AppliesTo(PenaltyTarget.OtherHero, false, noPlayer));

        var noCompanions = new SkillRequirementSettings { ApplyToCompanions = false };
        Assert.True(CombatPenalties.AppliesTo(PenaltyTarget.Player, false, noCompanions));
        Assert.False(CombatPenalties.AppliesTo(PenaltyTarget.Companion, false, noCompanions));
        Assert.True(CombatPenalties.AppliesTo(PenaltyTarget.OtherHero, false, noCompanions));

        var noLords = new SkillRequirementSettings { ApplyToOtherHeroes = false };
        Assert.True(CombatPenalties.AppliesTo(PenaltyTarget.Player, false, noLords));
        Assert.True(CombatPenalties.AppliesTo(PenaltyTarget.Companion, false, noLords));
        Assert.False(CombatPenalties.AppliesTo(PenaltyTarget.OtherHero, false, noLords));
    }

    [Fact]
    public void TournamentToggleSparesEveryoneInATournamentOnly()
    {
        var s = new SkillRequirementSettings { ApplyInTournaments = false };
        Assert.False(CombatPenalties.AppliesTo(PenaltyTarget.Player, true, s));
        Assert.False(CombatPenalties.AppliesTo(PenaltyTarget.OtherHero, true, s));
        Assert.True(CombatPenalties.AppliesTo(PenaltyTarget.Player, false, s));
    }

    [Fact]
    public void TrainingIsSparedByDefaultAndByItsOwnToggle()
    {
        Assert.False(CombatPenalties.AppliesTo(PenaltyTarget.Player, false, true, Defaults)); // off by default (2026-11-10)
        Assert.True(CombatPenalties.AppliesTo(PenaltyTarget.Player, false, false, Defaults));

        var on = new SkillRequirementSettings { ApplyInTraining = true };
        Assert.True(CombatPenalties.AppliesTo(PenaltyTarget.Player, false, true, on));
        Assert.True(CombatPenalties.AppliesTo(PenaltyTarget.Companion, false, true, on));
        Assert.False(CombatPenalties.AppliesTo(PenaltyTarget.Troop, false, true, on)); // troops never

        // The two toggles are independent: tournaments on does not open training, and the reverse.
        var tourneyOnly = new SkillRequirementSettings { ApplyInTournaments = true };
        Assert.True(CombatPenalties.AppliesTo(PenaltyTarget.Player, true, false, tourneyOnly));
        Assert.False(CombatPenalties.AppliesTo(PenaltyTarget.Player, false, true, tourneyOnly));
        Assert.False(CombatPenalties.AppliesTo(PenaltyTarget.Player, true, false, on));
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

        var tough = new SkillRequirementSettings { RangedDamagePenaltyHigh = 0.6f };
        Assert.Equal(0.4f, CombatPenalties.DamageMultiplier(SkillKind.Ranged, true, 1f, tough), 4);
    }

    [Fact]
    public void ThrownUsageTakesTheRangedAnchors()
    {
        var tough = new SkillRequirementSettings { RangedDamagePenaltyHigh = 0.6f, RangedInaccuracyBonusHigh = 1f };
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
        var settings = new SkillRequirementSettings { StarterGearMaxPenalty = 1f, RangedInaccuracyBonusHigh = 2.5f };
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
    public void StarterCap_EveryItemAtOrBelowTheMaxTier()
    {
        Assert.Equal(0.2f, CombatPenalties.StarterCap(0, Defaults));
        Assert.Equal(0.2f, CombatPenalties.StarterCap(1, Defaults));
        Assert.Equal(1f, CombatPenalties.StarterCap(2, Defaults));
        Assert.Equal(1f, CombatPenalties.StarterCap(null, Defaults));
        var tier3 = new SkillRequirementSettings { StarterGearMaxTier = 3 };
        Assert.Equal(0.2f, CombatPenalties.StarterCap(3, tier3));
        // A thrown tier 1 javelin is starter gear too (Toni, 2026-11-05: all tier 1 gear).
        Assert.Equal(0.8f, CombatPenalties.DamageMultiplier(SkillKind.Weapon, false, 1f, Defaults, true, 1));
        Assert.Equal(1.2f, CombatPenalties.InaccuracyMultiplier(SkillKind.Weapon, true, 1f, Defaults, 1));
    }

    // ---- starter cap for all tier 1 gear (Toni, 2026-11-05: spears have no tier 0 either) ----

    private static float ShortAtZero(SkillKind kind, float tierf) =>
        Requirements.Shortfall(Requirements.Requirement(kind, tierf, Defaults), 0);

    [Fact]
    public void TierOneSpearAtSkillZero_DamageMinusTwentyPercent()
    {
        Assert.Equal(1, Requirements.DisplayedTier(1f));
        float s = ShortAtZero(SkillKind.Weapon, 1f);
        Assert.Equal(1f, s);
        Assert.Equal(0.8f, CombatPenalties.DamageMultiplier(SkillKind.Weapon, false, s, Defaults, false, 1));
        var m = CombatPenalties.ForWielded(SkillKind.Weapon, s, Defaults, false, 1);
        Assert.Equal(0.8f, m.Damage);
        // Swing / handling High is 37.5% -> capped at 20% too.
        Assert.Equal(0.8f, m.Swing);
        Assert.Equal(0.8f, m.ThrustOrRangedReady);
        Assert.Equal(0.8f, m.Handling);
        // Tooltip: same functions.
        Assert.Equal(0.8f, TooltipStats.Multiplier(SkillKind.Weapon, false, TooltipStat.ThrustDamage, s, Defaults, 1));
        Assert.Equal(0.8f, TooltipStats.Multiplier(SkillKind.Weapon, false, TooltipStat.SwingSpeed, s, Defaults, 1));
        var parts = TooltipPenalties.For(SkillKind.Weapon, s, Defaults, false, 1);
        Assert.All(parts, p => Assert.Equal(-20, p.Percent));
    }

    [Fact]
    public void TierOneArmourSetAtAthleticsZero_FootSpeedMinusTwentyPercent()
    {
        float s = ShortAtZero(SkillKind.Armour, 1f);
        var set = new[]
        {
            CombatPenalties.ArmourPieceFor(8f, s, Defaults, 1),
            CombatPenalties.ArmourPieceFor(2f, s, Defaults, 1),
            CombatPenalties.ArmourPieceFor(1f, s, Defaults, 1),
            CombatPenalties.ArmourPieceFor(1.5f, s, Defaults, 1),
        };
        Assert.Equal(0.8f, Penalties.ArmourSpeedMultiplier(set, Defaults), 4);
        // Tooltip compact line: "up to -20% foot speed".
        var parts = TooltipPenalties.For(SkillKind.Armour, s, Defaults, false, 1);
        Assert.Equal(-20, Assert.Single(parts).Percent);
        // Mixed: a capped tier 1 helmet and an uncapped tier 3 cuirass, each capped before the weight share.
        float s3 = ShortAtZero(SkillKind.Armour, 3f);
        var mixed = new[] { CombatPenalties.ArmourPieceFor(1f, s, Defaults, 1), CombatPenalties.ArmourPieceFor(3f, s3, Defaults, 3) };
        Assert.Equal(1f - (1f * 0.2f + 3f * 0.6f) / 4f, Penalties.ArmourSpeedMultiplier(mixed, Defaults), 4);
    }

    [Fact]
    public void TierOneHorseAndShieldAtSkillZero_Capped()
    {
        float h = ShortAtZero(SkillKind.Mount, 1f);
        Assert.Equal(0.8f, CombatPenalties.HorseSpeedMultiplier(h, Defaults, 1));
        Assert.Equal(0.8f, CombatPenalties.HorseManeuverMultiplier(h, Defaults, 1));
        Assert.Equal(0.8f, TooltipStats.Multiplier(SkillKind.Mount, false, TooltipStat.MountSpeed, h, Defaults, 1));
        Assert.Equal(0.8f, TooltipStats.Multiplier(SkillKind.Mount, false, TooltipStat.MountManeuver, h, Defaults, 1));
        float sh = ShortAtZero(SkillKind.Shield, 1f);
        Assert.Equal(1.2f, CombatPenalties.ShieldDamageTakenMultiplier(sh, Defaults, 1));
        Assert.Equal(20, Assert.Single(TooltipPenalties.For(SkillKind.Shield, sh, Defaults, false, 1)).Percent);
    }

    [Fact]
    public void TierTwoGear_NotCapped()
    {
        Assert.Equal(2, Requirements.DisplayedTier(2f));
        float w = ShortAtZero(SkillKind.Weapon, 2f);
        Assert.Equal(Penalties.WeaponDamageMultiplier(w, Defaults), CombatPenalties.DamageMultiplier(SkillKind.Weapon, false, w, Defaults, false, 2));
        Assert.Equal(0.25f, CombatPenalties.DamageMultiplier(SkillKind.Weapon, false, w, Defaults, false, 2), 4);
        float a = ShortAtZero(SkillKind.Armour, 2f);
        Assert.Equal(0.4f, Penalties.ArmourSpeedMultiplier(new[] { CombatPenalties.ArmourPieceFor(10f, a, Defaults, 2) }, Defaults), 4);
        float h = ShortAtZero(SkillKind.Mount, 2f);
        Assert.Equal(Penalties.HorseSpeedMultiplier(h, Defaults), CombatPenalties.HorseSpeedMultiplier(h, Defaults, 2));
        Assert.Equal(Penalties.HorseManeuverMultiplier(h, Defaults), CombatPenalties.HorseManeuverMultiplier(h, Defaults, 2));
        float sh = ShortAtZero(SkillKind.Shield, 2f);
        Assert.Equal(Penalties.ShieldDamageTakenMultiplier(sh, Defaults), CombatPenalties.ShieldDamageTakenMultiplier(sh, Defaults, 2));
        // No tier passed (null) = no cap either.
        Assert.Equal(0.25f, CombatPenalties.DamageMultiplier(SkillKind.Weapon, false, ShortAtZero(SkillKind.Weapon, 1f), Defaults), 4);
    }
}
