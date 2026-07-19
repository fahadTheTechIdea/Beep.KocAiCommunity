using Beep.KocAiCommunity.Domain.Common;

namespace Beep.KocAiCommunity.Domain.Studio;

/// <summary>
/// One inference call against a model version. Captures the caller, the endpoint (online/batch),
/// end-to-end latency, row count, and outcome — the audit trail for served predictions.
/// </summary>
public class ModelInferenceLog : AuditableEntity
{
    public Guid ModelVersionId { get; set; }
    public string CallerUserId { get; set; } = default!;
    public string Endpoint { get; set; } = "online";   // online, batch
    public int RowCount { get; set; }
    public int LatencyMs { get; set; }
    public DateTime CalledUtc { get; set; }
    public bool Success { get; set; }
    public string? Error { get; set; }
}
