using System.Linq;
using GearMastery.Core;
using Xunit;

namespace GearMastery.Core.Tests
{
    public class TooltipPenaltiesTests
    {
        private static readonly GearMasterySettings Defaults = new GearMasterySettings();

        [Theory]
        [InlineData(0.912f, -9)]
        [InlineData(0.905f, -10)] // -9.5 rounds away from zero
        [InlineData(0.85f, -15)]
        [InlineData(1.125f, 13)]
        [InlineData(1.0f, 0)]
        [InlineData(0.996f, 0)]
        [InlineData(float.NaN, 0)]
        public void Percent_rounds_to_signed_whole_percent(float multiplier, int expected) =>
            Assert.Equal(expected, TooltipPenalties.Percent(multiplier));

        [Theory]
        [InlineData(-9, "-9")]
        [InlineData(12, "+12")]
        [InlineData(0, "0")]
        public void Signed_formats_with_explicit_plus(int percent, string expected) =>
            Assert.Equal(expected, TooltipPenalties.Signed(percent));

        [Theory]
        [InlineData(90, 30, true)]
        [InlineData(90, 90, false)]
        [InlineData(90, 120, false)]
        [InlineData(0, 0, false)]
        public void IsShort_is_skill_below_R(int r, int skill, bool expected) =>
            Assert.Equal(expected, TooltipPenalties.IsShort(r, skill));

        [Fact]
        public void No_shortfall_gives_no_parts()
        {
            Assert.Empty(TooltipPenalties.For(SkillKind.Weapon, 0f, Defaults));
            Assert.Empty(TooltipPenalties.For(SkillKind.Mount, -0.3f, Defaults));
        }

        [Fact]
        public void Thrown_usage_shows_damage_and_inaccuracy()
        {
            var parts = TooltipPenalties.For(SkillKind.Weapon, 1f, Defaults, thrownUsage: true);
            Assert.Equal(new[] { PenaltyStat.Damage, PenaltyStat.Inaccuracy }, parts.Select(p => p.Stat));
            Assert.Equal(new[] { -75, 75 }, parts.Select(p => p.Percent));
        }

        [Fact]
        public void Full_shortfall_weapon_shows_the_maxima()
        {
            var parts = TooltipPenalties.For(SkillKind.Weapon, 1f, Defaults);
            Assert.Equal(new[] { PenaltyStat.Damage, PenaltyStat.SwingSpeed, PenaltyStat.Handling }, parts.Select(p => p.Stat));
            Assert.Equal(new[] { -75, -38, -38 }, parts.Select(p => p.Percent));
        }

        [Fact]
        public void Weapon_parts_match_the_battle_multipliers()
        {
            // skill 30 on R 90: r 1/3 -> damage 50 + 25 x 0.267 / 0.3 = 72.2%, swing 36.1%
            float s = Requirements.Shortfall(90, 30);
            var parts = TooltipPenalties.For(SkillKind.Weapon, s, Defaults);
            Assert.Equal(TooltipPenalties.Percent(Penalties.WeaponDamageMultiplier(s, Defaults)), parts[0].Percent);
            Assert.Equal(-72, parts[0].Percent);
            Assert.Equal(-36, parts[1].Percent);
        }

        [Fact]
        public void Shield_and_inaccuracy_are_positive()
        {
            var shield = TooltipPenalties.For(SkillKind.Shield, 1f, Defaults).Single();
            Assert.Equal(PenaltyStat.ShieldDamageTaken, shield.Stat);
            Assert.Equal(75, shield.Percent);
            Assert.Equal("+75", shield.SignedPercent);

            var ranged = TooltipPenalties.For(SkillKind.Ranged, 1f, Defaults);
            Assert.Equal(new[] { PenaltyStat.Damage, PenaltyStat.ReloadSpeed, PenaltyStat.Inaccuracy }, ranged.Select(p => p.Stat));
            Assert.Equal(new[] { -75, -38, 75 }, ranged.Select(p => p.Percent));
        }

        [Fact]
        public void Armour_is_the_all_pieces_upper_bound()
        {
            var part = TooltipPenalties.For(SkillKind.Armour, 1f, Defaults).Single();
            Assert.Equal(PenaltyStat.FootSpeedUpTo, part.Stat);
            Assert.Equal(-60, part.Percent);
        }

        [Fact]
        public void Mount_shows_speed_and_maneuver()
        {
            var parts = TooltipPenalties.For(SkillKind.Mount, 1f, Defaults);
            Assert.Equal(new[] { PenaltyStat.HorseSpeed, PenaltyStat.HorseManeuver }, parts.Select(p => p.Stat));
            Assert.Equal(new[] { -75, -75 }, parts.Select(p => p.Percent));
        }

        [Fact]
        public void Tiny_shortfall_already_pays_the_low_anchor()
        {
            // The snap: one hair short is already -25% damage, -12.5% swing / handling.
            var parts = TooltipPenalties.For(SkillKind.Weapon, 0.001f, Defaults);
            Assert.Equal(new[] { -25, -13, -13 }, parts.Select(p => p.Percent));
        }

        [Fact]
        public void Parts_that_round_to_zero_are_dropped()
        {
            var s = new GearMasterySettings { MeleeHandlingPenaltyLow = 0.001f, MeleeHandlingPenaltyMid = 0.001f };
            var parts = TooltipPenalties.For(SkillKind.Weapon, 0.001f, s);
            Assert.Equal(new[] { PenaltyStat.Damage, PenaltyStat.SwingSpeed }, parts.Select(p => p.Stat));
        }
    }
}
