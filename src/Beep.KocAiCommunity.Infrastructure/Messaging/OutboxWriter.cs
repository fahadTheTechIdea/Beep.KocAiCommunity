using System.Text.Json;
using Beep.KocAiCommunity.Application.RealTime;
using Beep.KocAiCommunity.Domain.Messaging;
using Beep.KocAiCommunity.Infrastructure.Persistence;

namespace Beep.KocAiCommunity.Infrastructure.Messaging;

/// <summary>
/// Enqueues events into the outbox <b>within the caller's unit of work</b>: it only adds the row;
/// the caller's <c>SaveChangesAsync</c> commits the event and the state change together.
/// </summary>
public sealed class OutboxWriter(KocDbContext db) : IOutboxWriter
{
    public Task EnqueueAsync(IDomainEvent domainEvent, CancellationToken ct = default)
    {
        db.Set<OutboxMessage>().Add(new OutboxMessage
        {
            Type = domainEvent.EventType,
            PayloadJson = JsonSerializer.Serialize(domainEvent, domainEvent.GetType()),
            CreatedUtc = DateTime.UtcNow,
        });

        return Task.CompletedTask;
    }
}
