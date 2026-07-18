using Beep.KocAiCommunity.Domain.Common;

namespace Beep.KocAiCommunity.Domain.Storage;

/// <summary>
/// Metadata for a stored artifact. The bytes live in an <c>IArtifactStore</c> under
/// <see cref="StorageKey"/>; this row is the governed record (hash, size, classification).
/// Deduplicated by <see cref="Sha256"/>.
/// </summary>
public class ArtifactReference : AuditableEntity
{
    public string StorageKey { get; set; } = default!;
    public string LogicalPath { get; set; } = default!;
    public string ContentType { get; set; } = "application/octet-stream";
    public long SizeBytes { get; set; }
    public string Sha256 { get; set; } = default!;
    public KocDataClassification Classification { get; set; } = KocDataClassification.Internal;
}
