namespace Beep.KocAiCommunity.Application.RealTime;

/// <summary>A competition's leaderboard changed — relayed to the <c>competition:{id}</c> group.</summary>
public sealed record LeaderboardUpdatedEvent(Guid CompetitionId) : IDomainEvent
{
    public string EventType => "leaderboard.updated";
}

/// <summary>A user received a notification — relayed to the <c>user:{id}</c> group.</summary>
public sealed record NotificationCreatedEvent(string UserId) : IDomainEvent
{
    public string EventType => "notification.created";
}
