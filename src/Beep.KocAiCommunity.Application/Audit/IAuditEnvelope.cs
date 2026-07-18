namespace Beep.KocAiCommunity.Application.Audit;

/// <summary>A single auditable action. The actor and request metadata are filled in by the writer.</summary>
public sealed record AuditEntry(
    string Action,
    string Resource,
    string? ResourceId = null,
    string? BeforeJson = null,
    string? AfterJson = null);

/// <summary>Writes an <see cref="AuditEntry"/> to the durable audit log.</summary>
public interface IAuditEnvelope
{
    Task WriteAsync(AuditEntry entry, CancellationToken ct = default);
}
