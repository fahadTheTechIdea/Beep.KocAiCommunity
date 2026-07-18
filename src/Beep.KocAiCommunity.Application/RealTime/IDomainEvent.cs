namespace Beep.KocAiCommunity.Application.RealTime;

/// <summary>Marker for events enqueued to the transactional outbox for at-least-once delivery.</summary>
public interface IDomainEvent
{
    /// <summary>Stable event type discriminator (e.g. "leaderboard.updated").</summary>
    string EventType { get; }
}

/// <summary>Enqueues a domain event into the outbox within the current unit of work.</summary>
public interface IOutboxWriter
{
    Task EnqueueAsync(IDomainEvent domainEvent, CancellationToken ct = default);
}
