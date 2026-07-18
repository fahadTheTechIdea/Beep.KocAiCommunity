using Beep.KocAiCommunity.Domain.Organization;
using FluentAssertions;
using Xunit;

namespace Beep.KocAiCommunity.UnitTests;

public class OrgModelTests
{
    [Fact]
    public void VisibilityScope_has_the_four_org_levels()
    {
        Enum.GetValues<VisibilityScope>().Should().BeEquivalentTo(
            [VisibilityScope.Team, VisibilityScope.Group, VisibilityScope.Directorate, VisibilityScope.Company]);
    }

    [Fact]
    public void PositionLevel_orders_from_employee_up_to_ceo()
    {
        ((int)PositionLevel.Employee).Should().BeLessThan((int)PositionLevel.TeamLeader);
        ((int)PositionLevel.TeamLeader).Should().BeLessThan((int)PositionLevel.Manager);
        ((int)PositionLevel.Manager).Should().BeLessThan((int)PositionLevel.DCEO);
        ((int)PositionLevel.DCEO).Should().BeLessThan((int)PositionLevel.CEO);
    }
}
