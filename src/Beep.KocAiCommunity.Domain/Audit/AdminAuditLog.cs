namespace Beep.KocAiCommunity.Domain.Audit;

/// <summary>
/// One row per audited action, written in the same transaction as the change where possible.
/// Secrets in <see cref="BeforeJson"/>/<see cref="AfterJson"/> are redacted before write.
/// </summary>
public class AdminAuditLog
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string ActorUserId { get; set; } = default!;
    public string? ActorRole { get; set; }
    public string Action { get; set; } = default!;         // e.g. "setting.update", "first-admin-grant"
    public string Resource { get; set; } = default!;       // e.g. "setting", "user", "competition"
    public string? ResourceId { get; set; }
    public string? BeforeJson { get; set; }
    public string? AfterJson { get; set; }
    public string? IpAddress { get; set; }
    public string? UserAgent { get; set; }
    public string? RequestId { get; set; }
    public DateTime OccurredUtc { get; set; }
}
