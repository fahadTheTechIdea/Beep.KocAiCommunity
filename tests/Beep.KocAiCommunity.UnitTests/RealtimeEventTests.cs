using System.Text.Json;
using Beep.KocAiCommunity.Application.RealTime;
using FluentAssertions;
using Xunit;

namespace Beep.KocAiCommunity.UnitTests;

/// <summary>
/// The outbox stores events as JSON and the dispatcher deserializes them to route to a SignalR
/// group. These round-trips guard that routing key (competition id / user id).
/// </summary>
public class RealtimeEventTests
{
    [Fact]
    public void Leaderboard_event_round_trips_its_competition_id()
    {
        var id = Guid.NewGuid();
        IDomainEvent evt = new LeaderboardUpdatedEvent(id);
        evt.EventType.Should().Be("leaderboard.updated");

        var json = JsonSerializer.Serialize(evt, evt.GetType());
        var back = JsonSerializer.Deserialize<LeaderboardUpdatedEvent>(json);

        back.Should().NotBeNull();
        back!.CompetitionId.Should().Be(id);
    }

    [Fact]
    public void Notification_event_round_trips_its_user_id()
    {
        IDomainEvent evt = new NotificationCreatedEvent("dev-emp-1");
        evt.EventType.Should().Be("notification.created");

        var json = JsonSerializer.Serialize(evt, evt.GetType());
        var back = JsonSerializer.Deserialize<NotificationCreatedEvent>(json);

        back.Should().NotBeNull();
        back!.UserId.Should().Be("dev-emp-1");
    }
}
