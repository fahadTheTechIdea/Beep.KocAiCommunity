using Beep.KocAiCommunity.Domain.Common;

namespace Beep.KocAiCommunity.Domain.Notifications;

/// <summary>
/// A per-user notification (submission scored, competition concluded, model promoted, …). Delivered
/// in-app via a bell + feed; created by domain services as events happen.
/// </summary>
public class Notification : AuditableEntity
{
    public string UserId { get; set; } = default!;
    public string Type { get; set; } = default!;      // submission-scored, competition-concluded, model-promoted, …
    public string Title { get; set; } = default!;
    public string Message { get; set; } = default!;
    public string? LinkUrl { get; set; }              // in-app route, e.g. "/compete"
    public DateTime? ReadUtc { get; set; }
}
