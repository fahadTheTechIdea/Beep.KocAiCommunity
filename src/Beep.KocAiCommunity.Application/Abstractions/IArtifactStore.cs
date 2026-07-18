namespace Beep.KocAiCommunity.Application.Abstractions;

/// <summary>
/// Pluggable artifact storage (local filesystem in dev, Azure Blob in production).
/// Implementations live in Infrastructure. Defined here so services stay storage-agnostic.
/// </summary>
public interface IArtifactStore
{
    Task<string> SaveAsync(string logicalPath, Stream content, CancellationToken ct = default);
    Task<Stream> OpenReadAsync(string reference, CancellationToken ct = default);
    Task<bool> DeleteAsync(string reference, CancellationToken ct = default);
}
