using Beep.KocAiCommunity.Domain.Common;
using Beep.KocAiCommunity.Domain.Storage;

namespace Beep.KocAiCommunity.Application.Storage;

/// <summary>Thrown when an upload violates the configured guards (size, extension).</summary>
public sealed class ArtifactValidationException(string message) : Exception(message);

/// <summary>
/// Governs artifact uploads: validates against <see cref="ArtifactUploadOptions"/>, computes a
/// SHA-256, stores the bytes via the low-level store, and records (or reuses, by hash) an
/// <see cref="ArtifactReference"/>.
/// </summary>
public interface IArtifactService
{
    Task<ArtifactReference> SaveAsync(
        Stream content,
        string logicalPath,
        string contentType,
        KocDataClassification classification,
        CancellationToken ct = default);

    Task<Stream> OpenReadAsync(Guid artifactReferenceId, CancellationToken ct = default);
}
