using Beep.KocAiCommunity.Domain.Datasets;

namespace Beep.KocAiCommunity.Application.Datasets;

/// <summary>A dataset version with its files, inferred schema, and profile columns.</summary>
public sealed record DatasetVersionDetail(
    DatasetVersion Version,
    IReadOnlyList<DatasetFile> Files,
    IReadOnlyList<DatasetSchemaColumn> Schema,
    DatasetProfile? Profile,
    IReadOnlyList<DatasetProfileColumn> ProfileColumns);

/// <summary>An opened dataset file: its bytes plus what's needed to serve it.</summary>
public sealed record DatasetDownload(Stream Content, string FileName, string ContentType);

/// <summary>
/// Manages a dataset's versioned contents: upload/import files into a draft, infer schema + profile,
/// publish (freeze), and download (classification-enforced). Uploads are owner/admin only; reads honor
/// dataset visibility.
/// </summary>
public interface IDatasetContentService
{
    /// <summary>Uploads a CSV into the dataset's draft (opening a new draft if the latest is published).</summary>
    Task<DatasetVersion> UploadCsvAsync(string userId, bool isAdmin, Guid datasetId, Stream content, string fileName, string contentType, CancellationToken ct = default);

    /// <summary>Imports a CSV from a URL (SSRF-guarded) into the dataset's draft.</summary>
    Task<DatasetVersion> ImportFromUrlAsync(string userId, bool isAdmin, Guid datasetId, string url, CancellationToken ct = default);

    Task<IReadOnlyList<DatasetVersion>> ListVersionsAsync(string userId, Guid datasetId, CancellationToken ct = default);
    Task<DatasetVersionDetail?> GetVersionAsync(string userId, Guid datasetId, int versionNumber, CancellationToken ct = default);

    Task<DatasetVersion> PublishVersionAsync(string userId, bool isAdmin, Guid datasetId, int versionNumber, CancellationToken ct = default);
    Task<DatasetVersion> ArchiveVersionAsync(string userId, bool isAdmin, Guid datasetId, int versionNumber, CancellationToken ct = default);

    /// <summary>Downloads a file, enforcing classification (Confidential/Restricted → owner or admin only).</summary>
    Task<DatasetDownload> DownloadFileAsync(string userId, bool isAdmin, Guid fileId, CancellationToken ct = default);
}
