using GearMastery.Core;
using Xunit;

namespace GearMastery.Core.Tests
{
    public class TooltipStatsTests
    {
        private static readonly GearMasterySettings Defaults = new GearMasterySettings();

        [Theory]
        [InlineData(99, 0.375f, 37)]   // 37.125
        [InlineData(52, 0.75f, 39)]    // Swenryn Pony speed, the 25% Low anchor
        [InlineData(10, 0.25f, 3)]     // 2.5 rounds away from zero
        [InlineData(10, 0.35f, 4)]     // 3.5
        [InlineData(91, 1f, 91)]
        [InlineData(91, float.NaN, 91)]
        public void After_multiplies_and_rounds_half_away_from_zero(int value, float multiplier, int expected) =>
            Assert.Equal(expected, TooltipStats.After(TooltipStat.SwingDamage, value, multiplier));

        [Theory]
        [InlineData(95, 1.5f, 93)]   // spread 5 -> 7.5: 92.5 -> 93
        [InlineData(80, 1.75f, 65)]  // spread 20 -> 35
        [InlineData(100, 1.75f, 100)] // no spread stays none
        [InlineData(10, 2f, 0)]       // floored at 0
        public void Accuracy_maps_the_inaccuracy_multiplier(int accuracy, float inaccuracyMultiplier, int expected) =>
            Assert.Equal(expected, TooltipStats.After(TooltipStat.Accuracy, accuracy, inaccuracyMultiplier));

        [Theory]
        [InlineData(37, 99, "37 (99-62)")]
        [InlineData(39, 52, "39 (52-13)")]
        [InlineData(60, 50, "60 (50+10)")]
        public void Breakdown_shows_after_then_base_and_delta(int after, int value, string expected) =>
            Assert.Equal(expected, TooltipStats.Breakdown(after, value));

        [Theory]
        [InlineData("99 Cut", 99, "37 (99-62) Cut")]
        [InlineData("52", 52, "37 (99-62)")]
        [InlineData("Cut 99", 99, "Cut 37 (99-62)")]
        [InlineData("199 Cut 99", 99, "199 Cut 37 (99-62)")] // whole-number token only
        [InlineData("990", 99, null)]
        [InlineData("-99", 99, null)]
        [InlineData("Pierce", 60, null)]
        [InlineData(null, 60, null)]
        public void InsertBreakdown_replaces_the_number_token(string? text, int value, string? expected) =>
            Assert.Equal(expected, TooltipStats.InsertBreakdown(text, value, "37 (99-62)"));

        [Theory]
        [InlineData(39, 46, -1)]
        [InlineData(46, 39, 1)]
        [InlineData(5, 5, 0)]
        public void Compare_is_higher_better(int t, int c, int expected) =>
            Assert.Equal(expected, TooltipStats.Compare(t, c));

        [Fact]
        public void Melee_rows_use_the_battle_multipliers()
        {
            float s = Requirements.Shortfall(160, 71);
            var m = CombatPenalties.ForWielded(SkillKind.Weapon, s, Defaults);
            Assert.Equal(m.Swing, TooltipStats.Multiplier(SkillKind.Weapon, false, TooltipStat.SwingSpeed, s, Defaults));
            Assert.Equal(m.ThrustOrRangedReady, TooltipStats.Multiplier(SkillKind.Weapon, false, TooltipStat.ThrustSpeed, s, Defaults));
            Assert.Equal(m.Handling, TooltipStats.Multiplier(SkillKind.Weapon, false, TooltipStat.Handling, s, Defaults));
            Assert.Equal(m.Damage, TooltipStats.Multiplier(SkillKind.Weapon, false, TooltipStat.SwingDamage, s, Defaults));
            Assert.Equal(m.Damage, TooltipStats.Multiplier(SkillKind.Weapon, false, TooltipStat.ThrustDamage, s, Defaults));
            Assert.Equal(1f, TooltipStats.Multiplier(SkillKind.Weapon, false, TooltipStat.Accuracy, s, Defaults));
        }

        [Fact]
        public void Thrown_usage_rows_use_the_ranged_anchors()
        {
            // Broad Blade Javelin: Throwing 90, skill 30 (r 1/3).
            float s = Requirements.Shortfall(90, 30);
            Assert.Equal(Penalties.RangedDamageMultiplier(s, Defaults),
                TooltipStats.Multiplier(SkillKind.Weapon, true, TooltipStat.ThrownDamage, s, Defaults));
            Assert.Equal(Penalties.InaccuracyMultiplier(s, Defaults),
                TooltipStats.Multiplier(SkillKind.Weapon, true, TooltipStat.Accuracy, s, Defaults));
            Assert.Equal(1f, TooltipStats.Multiplier(SkillKind.Weapon, true, TooltipStat.SwingSpeed, s, Defaults));
            Assert.Equal(1f, TooltipStats.Multiplier(SkillKind.Weapon, true, TooltipStat.Handling, s, Defaults));
            Assert.Equal(CombatPenalties.DamageMultiplier(SkillKind.Weapon, false, s, Defaults, true),
                TooltipStats.Multiplier(SkillKind.Weapon, true, TooltipStat.ThrownDamage, s, Defaults));
        }

        [Fact]
        public void Launcher_rows_use_the_battle_multipliers()
        {
            float s = Requirements.Shortfall(150, 64);
            var m = CombatPenalties.ForWielded(SkillKind.Ranged, s, Defaults);
            Assert.Equal(m.ThrustOrRangedReady, TooltipStats.Multiplier(SkillKind.Ranged, false, TooltipStat.LauncherSpeed, s, Defaults));
            Assert.Equal(m.Damage, TooltipStats.Multiplier(SkillKind.Ranged, false, TooltipStat.LauncherDamage, s, Defaults));
            Assert.Equal(m.Inaccuracy, TooltipStats.Multiplier(SkillKind.Ranged, false, TooltipStat.Accuracy, s, Defaults));
        }

        [Fact]
        public void Swenryn_pony_speed_drops_below_the_hobby()
        {
            // Riding 50 vs the pony's R 100 (r 0.5): speed 52 -> well under the met Hobby's 46.
            float s = Requirements.Shortfall(100, 50);
            float m = TooltipStats.Multiplier(SkillKind.Mount, false, TooltipStat.MountSpeed, s, Defaults);
            Assert.Equal(Penalties.HorseSpeedMultiplier(s, Defaults), m);
            int after = TooltipStats.After(TooltipStat.MountSpeed, 52, m);
            Assert.True(after < 46);
            Assert.Equal(-1, TooltipStats.Compare(after, 46));
        }

        [Theory]
        [InlineData(SkillKind.Shield)]
        [InlineData(SkillKind.Armour)]
        public void Shield_and_armour_change_no_row(SkillKind kind)
        {
            foreach (TooltipStat stat in System.Enum.GetValues(typeof(TooltipStat)))
                Assert.Equal(1f, TooltipStats.Multiplier(kind, false, stat, 1f, Defaults));
        }

        [Fact]
        public void No_shortfall_changes_nothing()
        {
            foreach (TooltipStat stat in System.Enum.GetValues(typeof(TooltipStat)))
                Assert.Equal(1f, TooltipStats.Multiplier(SkillKind.Weapon, false, stat, 0f, Defaults));
        }
    }
}
