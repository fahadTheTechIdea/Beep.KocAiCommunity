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

/// <summary>
/// A celebration worth a burst of confetti — a badge earned or a level-up — relayed to the
/// <c>user:{id}</c> group so the shell can react in real time. <paramref name="Kind"/> is
/// <c>badge</c> or <c>levelup</c>; <paramref name="IconFile"/> is the O&amp;G badge art (may be empty).
/// </summary>
public sealed record EngagementCelebrationEvent(string UserId, string Kind, string Title, string Message, string IconFile) : IDomainEvent
{
    public string EventType => "engagement.celebrate";
}

/// <summary>
/// A background run changed state or logged a line — relayed to the <c>run:{id}</c> group so the
/// run detail page updates live. <paramref name="Status"/> is a <c>JobStatus</c> value.
/// </summary>
public sealed record RunProgressEvent(Guid JobId, string Status, string Message) : IDomainEvent
{
    public string EventType => "run.progress";
}
