using Beep.KocAiCommunity.Application.Abstractions;

namespace Beep.KocAiCommunity.Infrastructure.Storage;

/// <summary>
/// Local filesystem artifact store for development and tests. The Azure Blob provider
/// and classification/size guards are added in Phase 03.
/// </summary>
public sealed class LocalFileArtifactStore(string rootPath) : IArtifactStore
{
    private readonly string _rootPath = rootPath;

    public async Task<string> SaveAsync(string logicalPath, Stream content, CancellationToken ct = default)
    {
        var full = Path.Combine(_rootPath, logicalPath);
        Directory.CreateDirectory(Path.GetDirectoryName(full)!);
        await using var file = File.Create(full);
        await content.CopyToAsync(file, ct);
        return logicalPath;
    }

    public Task<Stream> OpenReadAsync(string reference, CancellationToken ct = default)
    {
        Stream stream = File.OpenRead(Path.Combine(_rootPath, reference));
        return Task.FromResult(stream);
    }

    public Task<bool> DeleteAsync(string reference, CancellationToken ct = default)
    {
        var full = Path.Combine(_rootPath, reference);
        if (!File.Exists(full))
        {
            return Task.FromResult(false);
        }

        File.Delete(full);
        return Task.FromResult(true);
    }
}
