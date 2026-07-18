namespace Beep.KocAiCommunity.Domain.Messaging;

/// <summary>
/// A durably-persisted event awaiting delivery. Written in the same transaction as the state
/// change, then relayed to SignalR by the dispatcher — avoiding dual-write inconsistency.
/// Ordering is by <see cref="CreatedUtc"/> then <see cref="Id"/>.
/// </summary>
public class OutboxMessage
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Type { get; set; } = default!;
    public string PayloadJson { get; set; } = default!;
    public DateTime CreatedUtc { get; set; }
    public DateTime? ProcessedUtc { get; set; }
    public int RetryCount { get; set; }
    public string? LastError { get; set; }
}
