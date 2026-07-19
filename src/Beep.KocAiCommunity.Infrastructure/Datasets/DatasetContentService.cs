using Beep.KocAiCommunity.Application.Authorization;
using Beep.KocAiCommunity.Application.Datasets;
using Beep.KocAiCommunity.Application.Storage;
using Beep.KocAiCommunity.Domain.Common;
using Beep.KocAiCommunity.Domain.Datasets;
using Beep.KocAiCommunity.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Beep.KocAiCommunity.Infrastructure.Datasets;

/// <summary>
/// Versioned dataset contents: upload/import CSVs into a draft, infer schema + a reproducible profile,
/// publish (freeze), and download with classification enforcement. Uploads require owner/admin; reads
/// honor dataset visibility.
/// </summary>
public sealed class DatasetContentService(
    KocDbContext db,
    IVisibilityEvaluator visibility,
    IArtifactService artifacts) : IDatasetContentService
{
    private const long MaxImportBytes = 200L * 1024 * 1024;
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(30) };

    public async Task<DatasetVersion> UploadCsvAsync(string userId, bool isAdmin, Guid datasetId, Stream content, string fileName, string contentType, CancellationToken ct = default)
    {
        var dataset = await RequireEditableAsync(userId, isAdmin, datasetId, ct);
        using var buffer = await BufferAsync(content, ct);
        return await IngestAsync(userId, dataset, buffer, fileName, string.IsNullOrWhiteSpace(contentType) ? "text/csv" : contentType, ct);
    }

    public async Task<DatasetVersion> ImportFromUrlAsync(string userId, bool isAdmin, Guid datasetId, string url, CancellationToken ct = default)
    {
        var dataset = await RequireEditableAsync(userId, isAdmin, datasetId, ct);

        var block = UrlImportGuard.Check(url);
        if (block != UrlBlockReason.None)
        {
            throw new DatasetException(block switch
            {
                UrlBlockReason.BadScheme => "Only http(s) URLs can be imported.",
                UrlBlockReason.BadHost => "The import host could not be resolved.",
                _ => "That URL resolves to a private or internal address and is blocked.",
            });
        }

        using var response = await Http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct);
        response.EnsureSuccessStatusCode();
        await using var stream = await response.Content.ReadAsStreamAsync(ct);
        using var buffer = await BufferAsync(stream, ct);

        var name = Path.GetFileName(new Uri(url).AbsolutePath);
        if (string.IsNullOrWhiteSpace(name) || !name.Contains('.'))
        {
            name = "import.csv";
        }

        return await IngestAsync(userId, dataset, buffer, name, "text/csv", ct);
    }

    public async Task<IReadOnlyList<DatasetVersion>> ListVersionsAsync(string userId, Guid datasetId, CancellationToken ct = default)
    {
        await RequireVisibleAsync(userId, datasetId, ct);
        return await db.Set<DatasetVersion>().AsNoTracking()
            .Where(v => v.DatasetId == datasetId)
            .OrderByDescending(v => v.VersionNumber)
            .ToListAsync(ct);
    }

    public async Task<DatasetVersionDetail?> GetVersionAsync(string userId, Guid datasetId, int versionNumber, CancellationToken ct = default)
    {
        var dataset = await db.Set<Dataset>().AsNoTracking().FirstOrDefaultAsync(d => d.Id == datasetId, ct);
        if (dataset is null || !await CanSeeAsync(userId, dataset, ct))
        {
            return null;
        }

        var version = await db.Set<DatasetVersion>().AsNoTracking().FirstOrDefaultAsync(v => v.DatasetId == datasetId && v.VersionNumber == versionNumber, ct);
        if (version is null)
        {
            return null;
        }

        var files = await db.Set<DatasetFile>().AsNoTracking().Where(f => f.DatasetVersionId == version.Id).ToListAsync(ct);
        var schema = await db.Set<DatasetSchemaColumn>().AsNoTracking().Where(s => s.DatasetVersionId == version.Id).OrderBy(s => s.Ordinal).ToListAsync(ct);
        var profile = await db.Set<DatasetProfile>().AsNoTracking().FirstOrDefaultAsync(p => p.DatasetVersionId == version.Id, ct);
        var columns = profile is null
            ? new List<DatasetProfileColumn>()
            : await db.Set<DatasetProfileColumn>().AsNoTracking().Where(c => c.DatasetProfileId == profile.Id).ToListAsync(ct);

        return new DatasetVersionDetail(version, files, schema, profile, columns);
    }

    public async Task<DatasetVersion> PublishVersionAsync(string userId, bool isAdmin, Guid datasetId, int versionNumber, CancellationToken ct = default)
    {
        await RequireEditableAsync(userId, isAdmin, datasetId, ct);
        var version = await RequireVersionAsync(datasetId, versionNumber, ct);
        if (version.Status != "draft")
        {
            throw new DatasetException("Only a draft version can be published.");
        }

        if (!await db.Set<DatasetFile>().AnyAsync(f => f.DatasetVersionId == version.Id, ct))
        {
            throw new DatasetException("Add a file before publishing this version.");
        }

        version.Status = "published";
        version.PublishedUtc = DateTime.UtcNow;
        version.PublishedByUserId = userId;
        version.LastModifiedByUserId = userId;
        version.LastModifiedUtc = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return version;
    }

    public async Task<DatasetVersion> ArchiveVersionAsync(string userId, bool isAdmin, Guid datasetId, int versionNumber, CancellationToken ct = default)
    {
        await RequireEditableAsync(userId, isAdmin, datasetId, ct);
        var version = await RequireVersionAsync(datasetId, versionNumber, ct);
        if (version.Status != "published")
        {
            throw new DatasetException("Only a published version can be archived.");
        }

        version.Status = "archived";
        version.LastModifiedByUserId = userId;
        version.LastModifiedUtc = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return version;
    }

    public async Task<DatasetDownload> DownloadFileAsync(string userId, bool isAdmin, Guid fileId, CancellationToken ct = default)
    {
        var file = await db.Set<DatasetFile>().AsNoTracking().FirstOrDefaultAsync(f => f.Id == fileId, ct)
            ?? throw new DatasetException("File not found.");
        var version = await db.Set<DatasetVersion>().AsNoTracking().FirstOrDefaultAsync(v => v.Id == file.DatasetVersionId, ct)
            ?? throw new DatasetException("File not found.");
        var dataset = await db.Set<Dataset>().AsNoTracking().FirstOrDefaultAsync(d => d.Id == version.DatasetId, ct)
            ?? throw new DatasetException("File not found.");

        if (!await CanSeeAsync(userId, dataset, ct))
        {
            throw new DatasetException("This dataset is not visible to you.");
        }

        // Classification gate: Confidential/Restricted downloads require the owner or a platform admin.
        if (dataset.Classification >= KocDataClassification.Confidential && !isAdmin && dataset.OwnerUserId != userId)
        {
            throw new DatasetException($"This dataset is classified {dataset.Classification}; downloading it requires explicit permission.");
        }

        var stream = await artifacts.OpenReadAsync(file.ArtifactReferenceId, ct);
        return new DatasetDownload(stream, Path.GetFileName(file.LogicalPath), file.ContentType);
    }

    // Stores the CSV into the dataset's draft version and (re)computes its schema + profile.
    private async Task<DatasetVersion> IngestAsync(string userId, Dataset dataset, MemoryStream buffer, string fileName, string contentType, CancellationToken ct)
    {
        var version = await OpenDraftAsync(userId, dataset, ct);
        await ClearVersionContentAsync(version.Id, ct);

        var safeName = Path.GetFileName(fileName);
        if (string.IsNullOrWhiteSpace(safeName))
        {
            safeName = "data.csv";
        }

        buffer.Position = 0;
        var reference = await artifacts.SaveAsync(buffer, $"datasets/{dataset.Id:N}/v{version.VersionNumber}/{safeName}", contentType, dataset.Classification, ct);

        buffer.Position = 0;
        var result = CsvProfiler.Profile(buffer);

        db.Set<DatasetFile>().Add(new DatasetFile
        {
            DatasetVersionId = version.Id,
            ArtifactReferenceId = reference.Id,
            LogicalPath = safeName,
            ContentType = contentType,
            SizeBytes = reference.SizeBytes,
            RowCount = result.TotalRows,
            CreatedByUserId = userId,
            CreatedUtc = DateTime.UtcNow,
        });

        var ordinal = 0;
        foreach (var col in result.Columns)
        {
            db.Set<DatasetSchemaColumn>().Add(new DatasetSchemaColumn
            {
                DatasetVersionId = version.Id,
                Ordinal = ordinal++,
                ColumnName = col.Name,
                DataType = col.DataType,
                Nullable = col.Nullable,
                CreatedByUserId = userId,
                CreatedUtc = DateTime.UtcNow,
            });
        }

        var profile = new DatasetProfile
        {
            DatasetVersionId = version.Id,
            SampledRows = result.SampledRows,
            TotalRows = result.TotalRows,
            GeneratedUtc = DateTime.UtcNow,
            CreatedByUserId = userId,
            CreatedUtc = DateTime.UtcNow,
        };
        db.Set<DatasetProfile>().Add(profile);
        foreach (var col in result.Columns)
        {
            db.Set<DatasetProfileColumn>().Add(new DatasetProfileColumn
            {
                DatasetProfileId = profile.Id,
                ColumnName = col.Name,
                NullCount = col.NullCount,
                DistinctCount = col.DistinctCount,
                Min = col.Min,
                Max = col.Max,
                Mean = col.Mean,
                CreatedByUserId = userId,
                CreatedUtc = DateTime.UtcNow,
            });
        }

        version.TotalSizeBytes = reference.SizeBytes;
        version.Sha256 = reference.Sha256;
        version.LastModifiedByUserId = userId;
        version.LastModifiedUtc = DateTime.UtcNow;

        // Keep the dataset's single-file pointer in sync for the existing training path.
        dataset.FileArtifactId = reference.Id;
        dataset.LastModifiedByUserId = userId;
        dataset.LastModifiedUtc = DateTime.UtcNow;

        await db.SaveChangesAsync(ct);
        return version;
    }

    private async Task<DatasetVersion> OpenDraftAsync(string userId, Dataset dataset, CancellationToken ct)
    {
        var latest = await db.Set<DatasetVersion>()
            .Where(v => v.DatasetId == dataset.Id)
            .OrderByDescending(v => v.VersionNumber)
            .FirstOrDefaultAsync(ct);
        if (latest is { Status: "draft" })
        {
            return latest;
        }

        var next = (latest?.VersionNumber ?? 0) + 1;
        var version = new DatasetVersion
        {
            DatasetId = dataset.Id,
            VersionNumber = next,
            Status = "draft",
            CreatedByUserId = userId,
            CreatedUtc = DateTime.UtcNow,
        };
        db.Set<DatasetVersion>().Add(version);
        dataset.LatestVersionNumber = next;
        return version;
    }

    private async Task ClearVersionContentAsync(Guid versionId, CancellationToken ct)
    {
        await db.Set<DatasetFile>().Where(f => f.DatasetVersionId == versionId).ExecuteDeleteAsync(ct);
        await db.Set<DatasetSchemaColumn>().Where(s => s.DatasetVersionId == versionId).ExecuteDeleteAsync(ct);
        var profiles = await db.Set<DatasetProfile>().Where(p => p.DatasetVersionId == versionId).Select(p => p.Id).ToListAsync(ct);
        if (profiles.Count > 0)
        {
            await db.Set<DatasetProfileColumn>().Where(c => profiles.Contains(c.DatasetProfileId)).ExecuteDeleteAsync(ct);
            await db.Set<DatasetProfile>().Where(p => p.DatasetVersionId == versionId).ExecuteDeleteAsync(ct);
        }
    }

    private static async Task<MemoryStream> BufferAsync(Stream source, CancellationToken ct)
    {
        var buffer = new MemoryStream();
        await source.CopyToAsync(buffer, ct);
        if (buffer.Length > MaxImportBytes)
        {
            buffer.Dispose();
            throw new DatasetException($"The file exceeds the {MaxImportBytes / (1024 * 1024)} MB import limit.");
        }

        buffer.Position = 0;
        return buffer;
    }

    private async Task<Dataset> RequireEditableAsync(string userId, bool isAdmin, Guid datasetId, CancellationToken ct)
    {
        var dataset = await db.Set<Dataset>().FirstOrDefaultAsync(d => d.Id == datasetId, ct)
            ?? throw new DatasetException("Dataset not found.");
        if (!isAdmin && dataset.OwnerUserId != userId)
        {
            throw new DatasetException("Only the dataset owner or a platform admin can change it.");
        }

        return dataset;
    }

    private async Task RequireVisibleAsync(string userId, Guid datasetId, CancellationToken ct)
    {
        var dataset = await db.Set<Dataset>().AsNoTracking().FirstOrDefaultAsync(d => d.Id == datasetId, ct)
            ?? throw new DatasetException("Dataset not found.");
        if (!await CanSeeAsync(userId, dataset, ct))
        {
            throw new DatasetException("This dataset is not visible to you.");
        }
    }

    private async Task<DatasetVersion> RequireVersionAsync(Guid datasetId, int versionNumber, CancellationToken ct) =>
        await db.Set<DatasetVersion>().FirstOrDefaultAsync(v => v.DatasetId == datasetId && v.VersionNumber == versionNumber, ct)
            ?? throw new DatasetException("Dataset version not found.");

    private async Task<bool> CanSeeAsync(string userId, Dataset dataset, CancellationToken ct) =>
        dataset.OwnerUserId == userId
        || await visibility.CanSeeAsync(userId, dataset.VisibilityScope, dataset.VisibilityOrgUnitId, ct);
}
