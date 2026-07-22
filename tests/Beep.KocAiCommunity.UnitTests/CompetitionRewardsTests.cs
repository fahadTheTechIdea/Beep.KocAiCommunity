using Beep.KocAiCommunity.Contracts.Competitions;
using Beep.KocAiCommunity.Infrastructure.Engagement;
using FluentAssertions;
using Xunit;

namespace Beep.KocAiCommunity.UnitTests;

/// <summary>
/// The arena UI advertises prizes from <see cref="CompetitionRewards"/>; these tests lock those
/// constants to what the engagement engine actually awards so the copy can never drift.
/// </summary>
public class CompetitionRewardsTests
{
    [Fact]
    public void Advertised_rewards_match_actual_awards()
    {
        XpSources.Points(XpSources.CompetitionTop3).Should().Be(CompetitionRewards.PodiumBarrels);
        XpSources.Points(XpSources.SubmissionScored).Should().Be(CompetitionRewards.ScoredSubmissionBarrels);
        XpSources.Points(XpSources.SubmissionFirst).Should().Be(CompetitionRewards.FirstSubmissionBonusBarrels);
        XpSources.Points(XpSources.CompetitionWin).Should().Be(0);   // the win itself is a badge, not extra bbl
    }
}
