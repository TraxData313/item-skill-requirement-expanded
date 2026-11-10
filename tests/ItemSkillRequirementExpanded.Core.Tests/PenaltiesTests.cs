using ItemSkillRequirementExpanded.Core;

namespace ItemSkillRequirementExpanded.Core.Tests;

public class PenaltiesTests
{
    private static readonly SkillRequirementSettings Defaults = new();

    /// <summary>The shortfall for skill / R, as the module computes it.</summary>
    private static float S(int r, int skill) => Requirements.Shortfall(r, skill);

    [Fact]
    public void DefaultAnchorsAreRoundTwos()
    {
        Assert.Equal(0.6f, Defaults.PenaltyMidRatio);
        Assert.Equal(0.3f, Defaults.PenaltyHighRatio);
        Assert.Equal((0.25f, 0.5f, 0.75f), (Defaults.MeleeDamagePenaltyLow, Defaults.MeleeDamagePenaltyMid, Defaults.MeleeDamagePenaltyHigh));
        Assert.Equal((0.125f, 0.25f, 0.375f), (Defaults.MeleeSwingSpeedPenaltyLow, Defaults.MeleeSwingSpeedPenaltyMid, Defaults.MeleeSwingSpeedPenaltyHigh));
        Assert.Equal((0.125f, 0.25f, 0.375f), (Defaults.MeleeHandlingPenaltyLow, Defaults.MeleeHandlingPenaltyMid, Defaults.MeleeHandlingPenaltyHigh));
        Assert.Equal((0.2f, 0.4f, 0.6f), (Defaults.ArmourSpeedPenaltyLow, Defaults.ArmourSpeedPenaltyMid, Defaults.ArmourSpeedPenaltyHigh));
        Assert.Equal((0.25f, 0.5f, 0.75f), (Defaults.ShieldDamageTakenBonusLow, Defaults.ShieldDamageTakenBonusMid, Defaults.ShieldDamageTakenBonusHigh));
        Assert.Equal((0.25f, 0.5f, 0.75f), (Defaults.RangedDamagePenaltyLow, Defaults.RangedDamagePenaltyMid, Defaults.RangedDamagePenaltyHigh));
        Assert.Equal((0.125f, 0.25f, 0.375f), (Defaults.RangedReloadSpeedPenaltyLow, Defaults.RangedReloadSpeedPenaltyMid, Defaults.RangedReloadSpeedPenaltyHigh));
        Assert.Equal((0.25f, 0.5f, 0.75f), (Defaults.RangedInaccuracyBonusLow, Defaults.RangedInaccuracyBonusMid, Defaults.RangedInaccuracyBonusHigh));
        Assert.Equal((0.25f, 0.5f, 0.75f), (Defaults.MountSpeedPenaltyLow, Defaults.MountSpeedPenaltyMid, Defaults.MountSpeedPenaltyHigh));
        Assert.Equal((0.25f, 0.5f, 0.75f), (Defaults.MountManeuverPenaltyLow, Defaults.MountManeuverPenaltyMid, Defaults.MountManeuverPenaltyHigh));
        Assert.True(Defaults.ApplyToPlayer && Defaults.ApplyToCompanions && Defaults.ApplyToOtherHeroes && !Defaults.ApplyInTournaments
            && !Defaults.ApplyInTraining);
        Assert.Equal(150, Defaults.RequirementAtTier6);
    }

    [Fact]
    public void MeetingTheRequirementSnapsToZero()
    {
        foreach (var skill in new[] { 100, 101, 300 })
        {
            float s = S(100, skill);
            Assert.Equal(1f, Penalties.WeaponDamageMultiplier(s, Defaults));
            Assert.Equal(1f, Penalties.SwingSpeedMultiplier(s, Defaults));
            Assert.Equal(1f, Penalties.HandlingMultiplier(s, Defaults));
            Assert.Equal(1f, Penalties.ShieldDamageTakenMultiplier(s, Defaults));
            Assert.Equal(1f, Penalties.RangedDamageMultiplier(s, Defaults));
            Assert.Equal(1f, Penalties.ReloadSpeedMultiplier(s, Defaults));
            Assert.Equal(1f, Penalties.InaccuracyMultiplier(s, Defaults));
            Assert.Equal(1f, Penalties.HorseSpeedMultiplier(s, Defaults));
            Assert.Equal(1f, Penalties.HorseManeuverMultiplier(s, Defaults));
        }
        // No requirement, nothing.
        Assert.Equal(1f, Penalties.WeaponDamageMultiplier(S(0, 0), Defaults));
    }

    [Fact]
    public void OnePointShortAlreadyPaysTheLowAnchor()
    {
        float s = S(100, 99); // r 0.99
        Assert.Equal(1f - 0.25f - 0.25f * 0.01f / 0.4f, Penalties.WeaponDamageMultiplier(s, Defaults), 4);
        Assert.True(Penalties.WeaponDamageMultiplier(s, Defaults) < 0.75f + 1e-4f);
    }

    [Theory]
    // r -> 1: Low; r 0.6: Mid; r 0.3 and below: High; straight lines between.
    [InlineData(0.6f, 0.50f)]
    [InlineData(0.8f, 0.375f)]
    [InlineData(0.45f, 0.625f)]
    [InlineData(0.3f, 0.75f)]
    [InlineData(0.1f, 0.75f)]
    [InlineData(0f, 0.75f)]
    public void DamageCurveThroughTheAnchors(float ratio, float penalty) =>
        Assert.Equal(penalty, Penalties.Amount(1f - ratio, 0.25f, 0.5f, 0.75f, Defaults), 4);

    [Fact]
    public void AmountIsMonotoneInSkill()
    {
        float last = 1f;
        for (int skill = 0; skill <= 160; skill++)
        {
            float a = Penalties.Amount(S(160, skill), 0.25f, 0.5f, 0.75f, Defaults);
            Assert.True(a <= last + 1e-6f, "skill " + skill);
            last = a;
        }
        Assert.Equal(0f, last);
    }

    [Fact]
    public void FullShortfallGivesTheHighAnchors()
    {
        Assert.Equal(0.25f, Penalties.WeaponDamageMultiplier(1f, Defaults), 5);
        Assert.Equal(0.625f, Penalties.SwingSpeedMultiplier(1f, Defaults), 5);
        Assert.Equal(0.625f, Penalties.HandlingMultiplier(1f, Defaults), 5);
        Assert.Equal(1.75f, Penalties.ShieldDamageTakenMultiplier(1f, Defaults), 5);
        Assert.Equal(0.25f, Penalties.RangedDamageMultiplier(1f, Defaults), 5);
        Assert.Equal(0.625f, Penalties.ReloadSpeedMultiplier(1f, Defaults), 5);
        Assert.Equal(1.75f, Penalties.InaccuracyMultiplier(1f, Defaults), 5);
        Assert.Equal(0.25f, Penalties.HorseSpeedMultiplier(1f, Defaults), 5);
        Assert.Equal(0.25f, Penalties.HorseManeuverMultiplier(1f, Defaults), 5);
    }

    // ---- Toni's calibration points ----

    [Fact]
    public void Tier6SwordAtOneHanded71CostsAbout62Percent()
    {
        // R 160, skill 71: r 0.44 -> 50 + 25 x (0.6 - 0.444) / 0.3 = 63%.
        float s = S(160, 71);
        float penalty = 1f - Penalties.WeaponDamageMultiplier(s, Defaults);
        Assert.InRange(penalty, 0.60f, 0.65f);
        Assert.Equal(0.63f, penalty, 2);
    }

    [Fact]
    public void SwordJustAboveSkillCostsAQuarter()
    {
        // R 75, skill 71: r 0.947 -> 25 + 25 x 0.053 / 0.4 = 28%.
        float penalty = 1f - Penalties.WeaponDamageMultiplier(S(75, 71), Defaults);
        Assert.InRange(penalty, 0.25f, 0.30f);
        Assert.Equal(0.283f, penalty, 2);
    }

    [Fact]
    public void PonyAboveYourRidingIsWorseThanTheHobbyYouMeet()
    {
        // Swenryn Pony speed 52, R 100 vs Battanian Hobby speed 46 (met): any Riding 20..99 makes the pony slower.
        for (int riding = 20; riding <= 99; riding++)
        {
            float pony = 52f * Penalties.HorseSpeedMultiplier(S(100, riding), Defaults);
            Assert.True(pony < 46f, "riding " + riding + ": pony " + pony);
        }
        Assert.Equal(52f, 52f * Penalties.HorseSpeedMultiplier(S(100, 100), Defaults));
    }

    [Fact]
    public void NextTierSwordIsWorseThanTheOneYouMeet()
    {
        // Skill 75: a tier 3 sword (R 75, met) vs a tier 4 sword (R 100) with 15% more raw damage and the same speed.
        float ratio = Calibration.MeleeOutputRatio(115f, 90f, 100, 100f, 90f, 75, 75, Defaults);
        Assert.True(ratio < 1f, "ratio " + ratio);
        Assert.Equal(1.15f, Calibration.MeleeOutputRatio(115f, 90f, 100, 100f, 90f, 75, 100, Defaults), 4);
    }

    [Fact]
    public void CalibrationGuardsZeroOutput() =>
        Assert.Equal(0f, Calibration.MeleeOutputRatio(100f, 90f, 30, 0f, 90f, 30, 30, Defaults));

    // ---- Robustness ----

    [Fact]
    public void ShortfallOutOfRangeIsClamped()
    {
        Assert.Equal(1f, Penalties.WeaponDamageMultiplier(-3f, Defaults));
        Assert.Equal(1f, Penalties.WeaponDamageMultiplier(float.NaN, Defaults));
        Assert.Equal(0.25f, Penalties.WeaponDamageMultiplier(7f, Defaults), 5);
        Assert.Equal(1.75f, Penalties.ShieldDamageTakenMultiplier(7f, Defaults), 5);
    }

    [Fact]
    public void NonsenseAnchorsNeverGoNegative()
    {
        var s = new SkillRequirementSettings
        {
            MeleeDamagePenaltyHigh = 5f, MeleeSwingSpeedPenaltyHigh = -1f, MeleeHandlingPenaltyHigh = float.NaN,
            ShieldDamageTakenBonusHigh = -2f, RangedInaccuracyBonusHigh = float.NaN,
        };
        Assert.Equal(0f, Penalties.WeaponDamageMultiplier(1f, s), 5);
        // A High below Mid (negative / NaN) is raised to Mid: the penalty never shrinks as the skill falls.
        Assert.Equal(0.75f, Penalties.SwingSpeedMultiplier(1f, s), 5);
        Assert.Equal(0.75f, Penalties.HandlingMultiplier(1f, s), 5);
        Assert.Equal(1.5f, Penalties.ShieldDamageTakenMultiplier(1f, s), 5);
        Assert.Equal(1.5f, Penalties.InaccuracyMultiplier(1f, s), 5);
    }

    [Fact]
    public void MisorderedAnchorsAreRaisedToARunningMaximum()
    {
        // Low 50 / Mid 25 / High 10 (a user dragging sliders the wrong way): flat 50% everywhere below R.
        foreach (float s in new[] { 0.01f, 0.2f, 0.4f, 0.55f, 0.7f, 1f })
            Assert.Equal(0.5f, Penalties.Amount(s, 0.5f, 0.25f, 0.1f, Defaults), 5);
        // Low 10 / Mid 60 / High 30: Mid stands, High raised to 60.
        Assert.Equal(0.6f, Penalties.Amount(0.4f, 0.1f, 0.6f, 0.3f, Defaults), 5);
        Assert.Equal(0.6f, Penalties.Amount(0.9f, 0.1f, 0.6f, 0.3f, Defaults), 5);
        // Still exactly 0 at the requirement (the snap), whatever the anchors.
        Assert.Equal(0f, Penalties.Amount(0f, 0.5f, 0.25f, 0.1f, Defaults));
    }

    [Fact]
    public void PenaltyIsMonotoneForAnyAnchors()
    {
        var rnd = new Random(7);
        for (int n = 0; n < 200; n++)
        {
            float low = (float)rnd.NextDouble(), mid = (float)rnd.NextDouble(), high = (float)rnd.NextDouble();
            float prev = 0f;
            for (int skill = 100; skill >= 0; skill--)
            {
                float a = Penalties.Amount(S(100, skill), low, mid, high, Defaults);
                Assert.True(a >= prev - 1e-6f, $"anchors {low}/{mid}/{high}, skill {skill}: {a} < {prev}");
                prev = a;
            }
        }
    }

    [Fact]
    public void BadAnchorRatiosFallBackToTheDefaults()
    {
        var swapped = new SkillRequirementSettings { PenaltyMidRatio = 0.3f, PenaltyHighRatio = 0.6f };
        Assert.Equal(0.5f, Penalties.Amount(0.4f, 0.25f, 0.5f, 0.75f, swapped), 5); // r 0.6 -> Mid
        var custom = new SkillRequirementSettings { PenaltyMidRatio = 0.8f, PenaltyHighRatio = 0.5f };
        Assert.Equal(0.5f, Penalties.Amount(0.2f, 0.25f, 0.5f, 0.75f, custom), 5);  // r 0.8 -> Mid
        Assert.Equal(0.75f, Penalties.Amount(0.5f, 0.25f, 0.5f, 0.75f, custom), 5); // r 0.5 -> High
    }

    [Fact]
    public void NullSettingsThrow() =>
        Assert.Throws<ArgumentNullException>(() => Penalties.WeaponDamageMultiplier(0.5f, null!));

    // ---- Armour: per piece by its own ratio, shared by weight ----

    [Fact]
    public void NoArmourNoPenalty()
    {
        Assert.Equal(1f, Penalties.ArmourSpeedMultiplier(Array.Empty<ArmourPiece>(), Defaults));
        Assert.Equal(1f, Penalties.ArmourSpeedMultiplier(null!, Defaults));
        Assert.Equal(1f, Penalties.ArmourSpeedMultiplier(new[] { new ArmourPiece(0f, 1f), new ArmourPiece(float.NaN, 1f) }, Defaults));
    }

    [Fact]
    public void MetArmourNoPenalty()
    {
        var worn = new[] { new ArmourPiece(20f, 0f), new ArmourPiece(3f, 0f) };
        Assert.Equal(1f, Penalties.ArmourSpeedMultiplier(worn, Defaults));
    }

    [Fact]
    public void FullSetAtRatio03GivesSixtyPercent()
    {
        float s = S(200, 60); // r 0.3
        var worn = new[] { new ArmourPiece(20f, s), new ArmourPiece(3f, s), new ArmourPiece(2f, s), new ArmourPiece(1f, s), new ArmourPiece(1.5f, s) };
        Assert.Equal(0.4f, Penalties.ArmourSpeedMultiplier(worn, Defaults), 4);
    }

    [Fact]
    public void LightHelmetBarelyMattersHeavyCuirassDominates()
    {
        float deep = S(200, 60); // r 0.3 -> 60% for that piece
        var helmetOnly = new[] { new ArmourPiece(20f, 0f), new ArmourPiece(1f, deep) };
        Assert.Equal(1f - 0.6f / 21f, Penalties.ArmourSpeedMultiplier(helmetOnly, Defaults), 4); // ~ -2.9%
        var cuirassOnly = new[] { new ArmourPiece(20f, deep), new ArmourPiece(1f, 0f) };
        Assert.Equal(1f - 0.6f * 20f / 21f, Penalties.ArmourSpeedMultiplier(cuirassOnly, Defaults), 4); // ~ -57%
    }

    [Fact]
    public void EachPieceUsesItsOwnRatio()
    {
        // 10 kg at r 0.6 (40%) + 10 kg at r 0.3 (60%) -> 50%.
        var worn = new[] { new ArmourPiece(10f, S(100, 60)), new ArmourPiece(10f, S(100, 30)) };
        Assert.Equal(0.5f, Penalties.ArmourSpeedMultiplier(worn, Defaults), 4);
        Assert.Equal(0.4f, Penalties.ArmourPiecePenalty(S(100, 60), Defaults), 4);
    }
}
