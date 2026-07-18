using System.Security.Cryptography;
using Beep.KocAiCommunity.Application.Abstractions;
using Beep.KocAiCommunity.Application.Storage;
using Beep.KocAiCommunity.Domain.Common;
using Beep.KocAiCommunity.Domain.Storage;
using Beep.KocAiCommunity.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Beep.KocAiCommunity.Infrastructure.Storage;

/// <summary>
/// Governs uploads: validates size/extension, computes a SHA-256, deduplicates by hash, stores
/// the bytes via <see cref="IArtifactStore"/>, and records an <see cref="ArtifactReference"/>.
/// </summary>
public sealed class ArtifactService(
    KocDbContext db,
    IArtifactStore store,
    IOptions<ArtifactUploadOptions> options) : IArtifactService
{
    private readonly ArtifactUploadOptions _options = options.Value;

    public async Task<ArtifactReference> SaveAsync(
        Stream content,
        string logicalPath,
        string contentType,
        KocDataClassification classification,
        CancellationToken ct = default)
    {
        ValidateExtension(logicalPath);

        // Buffer to compute hash and size, enforcing the size cap during the copy.
        using var buffer = new MemoryStream();
        await CopyWithLimitAsync(content, buffer, _options.MaxSizeBytes, ct);
        buffer.Position = 0;

        var sha256 = Convert.ToHexStringLower(await SHA256.HashDataAsync(buffer, ct));
        buffer.Position = 0;

        // Deduplicate: identical bytes reuse the existing reference.
        var existing = await db.Set<ArtifactReference>()
            .FirstOrDefaultAsync(a => a.Sha256 == sha256, ct);
        if (existing is not null)
        {
            return existing;
        }

        var storageKey = $"{sha256[..2]}/{sha256}";
        await store.SaveAsync(storageKey, buffer, ct);

        var reference = new ArtifactReference
        {
            StorageKey = storageKey,
            LogicalPath = logicalPath,
            ContentType = string.IsNullOrWhiteSpace(contentType) ? "application/octet-stream" : contentType,
            SizeBytes = buffer.Length,
            Sha256 = sha256,
            Classification = classification,
            CreatedUtc = DateTime.UtcNow,
        };

        db.Add(reference);
        await db.SaveChangesAsync(ct);
        return reference;
    }

    public async Task<Stream> OpenReadAsync(Guid artifactReferenceId, CancellationToken ct = default)
    {
        var reference = await db.Set<ArtifactReference>()
            .FirstOrDefaultAsync(a => a.Id == artifactReferenceId, ct)
            ?? throw new InvalidOperationException($"Artifact {artifactReferenceId} not found.");

        return await store.OpenReadAsync(reference.StorageKey, ct);
    }

    private void ValidateExtension(string logicalPath)
    {
        if (_options.AllowedExtensions.Count == 0)
        {
            return;
        }

        var ext = Path.GetExtension(logicalPath);
        if (!_options.AllowedExtensions.Contains(ext))
        {
            throw new ArtifactValidationException($"File extension '{ext}' is not permitted.");
        }
    }

    private static async Task CopyWithLimitAsync(Stream source, Stream destination, long maxBytes, CancellationToken ct)
    {
        var bufferArray = new byte[81920];
        long total = 0;
        int read;
        while ((read = await source.ReadAsync(bufferArray, ct)) > 0)
        {
            total += read;
            if (total > maxBytes)
            {
                throw new ArtifactValidationException($"Upload exceeds the {maxBytes}-byte limit.");
            }

            await destination.WriteAsync(bufferArray.AsMemory(0, read), ct);
        }
    }
}
