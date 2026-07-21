using Beep.KocAiCommunity.Client;
using FluentAssertions;
using Xunit;

namespace Beep.KocAiCommunity.ComponentTests;

/// <summary>
/// The persona predicates drive nav visibility and the route guard, and mirror the server policies
/// (RequireEmployee = any position, RequireSupervisor = team-lead+, Admin = PlatformAdmin).
/// </summary>
public class DevIdentityTests
{
    private static DevIdentity As(string persona)
    {
        var id = new DevIdentity();
        id.SetPersona(persona);
        return id;
    }

    [Fact]
    public void Guest_has_no_participant_supervisor_or_admin_access()
    {
        var id = As("guest");
        id.IsGuest.Should().BeTrue();
        id.HasAnyPosition.Should().BeFalse();
        id.IsSupervisor.Should().BeFalse();
        id.IsPlatformAdmin.Should().BeFalse();
    }

    [Fact]
    public void Employee_participates_but_does_not_supervise_or_administer()
    {
        var id = As("employee");
        id.IsGuest.Should().BeFalse();
        id.HasAnyPosition.Should().BeTrue();
        id.IsSupervisor.Should().BeFalse();
        id.IsPlatformAdmin.Should().BeFalse();
    }

    [Theory]
    [InlineData("teamleader")]
    [InlineData("manager")]
    [InlineData("ceo")]
    public void Supervisor_positions_can_supervise(string persona)
    {
        var id = As(persona);
        id.HasAnyPosition.Should().BeTrue();
        id.IsSupervisor.Should().BeTrue();
        id.IsPlatformAdmin.Should().BeFalse();
    }

    [Fact]
    public void Platform_admin_can_participate_supervise_and_administer()
    {
        var id = As("platformadmin");
        id.HasAnyPosition.Should().BeTrue();  // seeded with a Manager position too
        id.IsSupervisor.Should().BeTrue();
        id.IsPlatformAdmin.Should().BeTrue();
    }

    [Fact]
    public void Competition_admin_participates_without_supervising()
    {
        var id = As("compadmin");
        id.HasAnyPosition.Should().BeTrue();
        id.IsInRole("CompetitionAdmin").Should().BeTrue();
        id.IsSupervisor.Should().BeFalse();
    }

    [Fact]
    public void Switching_persona_raises_Changed_and_updates_the_user()
    {
        var id = As("employee");
        var raised = 0;
        id.Changed += () => raised++;

        id.SetPersona("ceo");
        raised.Should().Be(1);
        id.UserId.Should().Be("dev-ceo");

        id.SetPersona("ceo"); // same persona → no-op
        raised.Should().Be(1);
    }

    [Fact]
    public void Real_signed_in_user_forwards_its_id_and_defaults_to_employee()
    {
        var id = new DevIdentity();
        id.SetRealUser(@"KOC\aldhubaib", "aldhubaib", []);

        id.IsRealUser.Should().BeTrue();
        id.UserId.Should().Be(@"KOC\aldhubaib");
        id.Current.Label.Should().Be("aldhubaib");
        id.HasAnyPosition.Should().BeTrue();          // defaulted to Employee so the API accepts them
        id.IsPlatformAdmin.Should().BeFalse();

        // A dev persona still overrides it for testing.
        id.SetPersona("manager");
        id.IsRealUser.Should().BeFalse();
        id.UserId.Should().Be("dev-user");
    }
}
