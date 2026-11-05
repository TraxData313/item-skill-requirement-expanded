using GearMastery.Core;

namespace GearMastery.Core.Tests;

public class PenaltiesTests
{
    private static readonly GearMasterySettings Defaults = new();
    private static readonly GearMasterySettings Linear = new() { PenaltyExponent = 1f };

    [Fact]
    public void DefaultsAreTheMildOnes()
    {
        Assert.Equal(0.15f, Defaults.MaxWeaponDamagePenalty);
        Assert.Equal(0.10f, Defaults.MaxSwingSpeedPenalty);
        Assert.Equal(0.15f, Defaults.MaxHandlingPenalty);
        Assert.Equal(0.25f, Defaults.MaxShieldDamageTakenBonus);
        Assert.Equal(0.08f, Defaults.MaxArmourSpeedPenalty);
        Assert.Equal(0.15f, Defaults.MaxRangedDamagePenalty);
        Assert.Equal(0.10f, Defaults.MaxReloadSpeedPenalty);
        Assert.Equal(0.30f, Defaults.MaxInaccuracyBonus);
        Assert.Equal(0.10f, Defaults.MaxHorseSpeedPenalty);
        Assert.Equal(0.10f, Defaults.MaxHorseManeuverPenalty);
        Assert.Equal(0.5f, Defaults.PenaltyExponent);
        Assert.False(Defaults.HeroesOnly);
        Assert.Equal(12f, Defaults.WeaponSlope);
        Assert.Equal(10f, Defaults.ArmourSlope);
    }

    [Fact]
    public void NoShortfallNoPenalty()
    {
        Assert.Equal(1f, Penalties.WeaponDamageMultiplier(0f, Defaults));
        Assert.Equal(1f, Penalties.SwingSpeedMultiplier(0f, Defaults));
        Assert.Equal(1f, Penalties.HandlingMultiplier(0f, Defaults));
        Assert.Equal(1f, Penalties.ShieldDamageTakenMultiplier(0f, Defaults));
        Assert.Equal(1f, Penalties.RangedDamageMultiplier(0f, Defaults));
        Assert.Equal(1f, Penalties.ReloadSpeedMultiplier(0f, Defaults));
        Assert.Equal(1f, Penalties.InaccuracyMultiplier(0f, Defaults));
        Assert.Equal(1f, Penalties.HorseSpeedMultiplier(0f, Defaults));
        Assert.Equal(1f, Penalties.HorseManeuverMultiplier(0f, Defaults));
    }

    [Fact]
    public void FullShortfallGivesTheMaxima()
    {
        Assert.Equal(0.85f, Penalties.WeaponDamageMultiplier(1f, Defaults), 5);
        Assert.Equal(0.90f, Penalties.SwingSpeedMultiplier(1f, Defaults), 5);
        Assert.Equal(0.85f, Penalties.HandlingMultiplier(1f, Defaults), 5);
        Assert.Equal(1.25f, Penalties.ShieldDamageTakenMultiplier(1f, Defaults), 5);
        Assert.Equal(0.85f, Penalties.RangedDamageMultiplier(1f, Defaults), 5);
        Assert.Equal(0.90f, Penalties.ReloadSpeedMultiplier(1f, Defaults), 5);
        Assert.Equal(1.30f, Penalties.InaccuracyMultiplier(1f, Defaults), 5);
        Assert.Equal(0.90f, Penalties.HorseSpeedMultiplier(1f, Defaults), 5);
        Assert.Equal(0.90f, Penalties.HorseManeuverMultiplier(1f, Defaults), 5);
    }

    [Fact]
    public void LinearExponentIsLinear()
    {
        Assert.Equal(1f - 0.15f * 0.5f, Penalties.WeaponDamageMultiplier(0.5f, Linear), 5);
        Assert.Equal(1f + 0.25f * 0.4f, Penalties.ShieldDamageTakenMultiplier(0.4f, Linear), 5);
    }

    [Fact]
    public void DefaultCurveIsSquareRoot()
    {
        Assert.Equal(0.5f, Penalties.Curve(0.25f, Defaults), 5);
        Assert.Equal(1f - 0.15f * 0.5f, Penalties.WeaponDamageMultiplier(0.25f, Defaults), 5);
        // concave: a moderate gap already bites more than linear
        Assert.True(Penalties.Curve(0.3f, Defaults) > 0.3f);
    }

    [Theory]
    [InlineData(0f)]
    [InlineData(-1f)]
    [InlineData(float.NaN)]
    public void BadExponentFallsBackToLinear(float p)
    {
        var s = new GearMasterySettings { PenaltyExponent = p };
        Assert.Equal(0.3f, Penalties.Curve(0.3f, s), 5);
    }

    [Theory]
    [InlineData(-0.5f, 0f)]
    [InlineData(2f, 1f)]
    [InlineData(float.NaN, 0f)]
    public void CurveClampsShortfall(float shortfall, float expected) =>
        Assert.Equal(expected, Penalties.Curve(shortfall, Defaults), 5);

    [Fact]
    public void ShortfallOutOfRangeIsClamped()
    {
        Assert.Equal(1f, Penalties.WeaponDamageMultiplier(-3f, Defaults));
        Assert.Equal(0.85f, Penalties.WeaponDamageMultiplier(7f, Defaults), 5);
        Assert.Equal(1.25f, Penalties.ShieldDamageTakenMultiplier(7f, Defaults), 5);
    }

    [Fact]
    public void NonsenseMaximaNeverGoNegative()
    {
        var s = new GearMasterySettings
        {
            MaxWeaponDamagePenalty = 3f, MaxSwingSpeedPenalty = -1f, MaxHandlingPenalty = float.NaN,
            MaxShieldDamageTakenBonus = -0.5f, MaxInaccuracyBonus = float.PositiveInfinity,
        };
        Assert.Equal(0f, Penalties.WeaponDamageMultiplier(1f, s), 5);
        Assert.Equal(1f, Penalties.SwingSpeedMultiplier(1f, s), 5);
        Assert.Equal(1f, Penalties.HandlingMultiplier(1f, s), 5);
        Assert.Equal(1f, Penalties.ShieldDamageTakenMultiplier(1f, s), 5);
        Assert.Equal(1f, Penalties.InaccuracyMultiplier(1f, s), 5);
    }

    [Fact]
    public void NullSettingsThrow() =>
        Assert.Throws<ArgumentNullException>(() => Penalties.WeaponDamageMultiplier(0.5f, null!));

    // ---- Armour aggregation ----

    [Fact]
    public void ArmourNothingWornNoPenalty()
    {
        Assert.Equal(1f, Penalties.ArmourSpeedMultiplier(Array.Empty<ArmourPiece>(), Defaults));
        Assert.Equal(1f, Penalties.ArmourSpeedMultiplier(null!, Defaults));
    }

    [Fact]
    public void ArmourZeroTotalWeightIsGuarded()
    {
        var worn = new[] { new ArmourPiece(0f, 1f), new ArmourPiece(-2f, 1f), new ArmourPiece(float.NaN, 1f) };
        Assert.Equal(0f, Penalties.ArmourShortfall(worn));
        Assert.Equal(1f, Penalties.ArmourSpeedMultiplier(worn, Defaults));
    }

    [Fact]
    public void ArmourAllShortGivesMax()
    {
        var worn = new[] { new ArmourPiece(20f, 1f), new ArmourPiece(3f, 1f) };
        Assert.Equal(1f, Penalties.ArmourShortfall(worn), 5);
        Assert.Equal(0.92f, Penalties.ArmourSpeedMultiplier(worn, Defaults), 5);
    }

    [Fact]
    public void ArmourIsWeightedByAllWornPieces()
    {
        // 1 kg helmet at full shortfall, 19 kg of met armour: A = 1/20 = 0.05
        var worn = new[] { new ArmourPiece(1f, 1f), new ArmourPiece(15f, 0f), new ArmourPiece(4f, 0f) };
        Assert.Equal(0.05f, Penalties.ArmourShortfall(worn), 5);
        Assert.Equal(1f - 0.08f * 0.05f, Penalties.ArmourSpeedMultiplier(worn, Linear), 5);
        // curve applies to the aggregate
        Assert.Equal(1f - 0.08f * (float)Math.Sqrt(0.05), Penalties.ArmourSpeedMultiplier(worn, Defaults), 5);
    }

    [Fact]
    public void ArmourMixedShortfalls()
    {
        // (10 x 0.5 + 10 x 0.1) / 20 = 0.3
        var worn = new[] { new ArmourPiece(10f, 0.5f), new ArmourPiece(10f, 0.1f) };
        Assert.Equal(0.3f, Penalties.ArmourShortfall(worn), 5);
    }

    [Fact]
    public void ArmourPieceShortfallIsClamped()
    {
        var worn = new[] { new ArmourPiece(10f, 5f), new ArmourPiece(10f, -1f) };
        Assert.Equal(0.5f, Penalties.ArmourShortfall(worn), 5);
    }

    // ---- Calibration (Toni's intent: a big sword in unskilled hands ~ a standard sword) ----

    [Fact]
    public void TraderLordWithTopSwordIsRoughlyEvenWithMidSword()
    {
        // Wielder skill 30. Mid sword R 30 (no penalty). Top sword R 90, raw damage 25% higher, same speed.
        float ratio = Calibration.MeleeOutputRatio(125f, 90f, 90, 100f, 90f, 30, 30, Defaults);
        Assert.InRange(ratio, 0.90f, 1.02f);   // ~1.007 with p = 0.5
        float linear = Calibration.MeleeOutputRatio(125f, 90f, 90, 100f, 90f, 30, 30, Linear);
        Assert.InRange(linear, 1.04f, 1.06f);  // ~1.05: linear would still clearly favour the top sword
    }

    [Fact]
    public void SkilledWielderGetsTheFullTopSword()
    {
        float ratio = Calibration.MeleeOutputRatio(125f, 90f, 90, 100f, 90f, 30, 90, Defaults);
        Assert.Equal(1.25f, ratio, 4);
    }

    [Fact]
    public void CalibrationGuardsZeroOutput() =>
        Assert.Equal(0f, Calibration.MeleeOutputRatio(100f, 90f, 30, 0f, 90f, 30, 30, Defaults));
}
