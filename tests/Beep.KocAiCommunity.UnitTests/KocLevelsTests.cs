using Beep.KocAiCommunity.Domain.Engagement;
using FluentAssertions;
using Xunit;

namespace Beep.KocAiCommunity.UnitTests;

public class KocLevelsTests
{
    [Theory]
    [InlineData(0, 1, "Roustabout", 100)]
    [InlineData(99, 1, "Roustabout", 100)]
    [InlineData(100, 2, "Roughneck", 300)]
    [InlineData(699, 3, "Derrickhand", 700)]
    [InlineData(700, 4, "Driller", 1500)]
    [InlineData(11999, 7, "Reservoir Analyst", 12000)]
    [InlineData(12000, 8, "Chief Geoscientist", null)]
    [InlineData(50000, 8, "Chief Geoscientist", null)]
    public void ForXp_maps_barrels_to_the_right_rung(int xp, int level, string title, int? nextXp)
    {
        var (l, t, _, next) = KocLevels.ForXp(xp);

        l.Should().Be(level);
        t.Should().Be(title);
        next.Should().Be(nextXp);
    }

    [Fact]
    public void LevelForXp_is_monotonic()
    {
        var last = 0;
        for (var xp = 0; xp <= 12_000; xp += 50)
        {
            var level = KocLevels.LevelForXp(xp);
            level.Should().BeGreaterThanOrEqualTo(last);
            last = level;
        }
    }
}
