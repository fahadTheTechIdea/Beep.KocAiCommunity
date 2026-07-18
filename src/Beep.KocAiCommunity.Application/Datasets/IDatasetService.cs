using Beep.KocAiCommunity.Domain.Common;
using Beep.KocAiCommunity.Domain.Datasets;
using Beep.KocAiCommunity.Domain.Organization;

namespace Beep.KocAiCommunity.Application.Datasets;

/// <summary>Raised when a dataset action is not permitted (e.g. an invalid visibility choice).</summary>
public sealed class DatasetException(string message) : Exception(message);

public interface IDatasetService
{
    Task<Dataset> CreateAsync(
        string userId,
        string name,
        string description,
        VisibilityScope scope,
        KocDataClassification classification,
        string domain,
        string? tags,
        CancellationToken ct = default);

    Task<IReadOnlyList<Dataset>> BrowseVisibleAsync(string userId, CancellationToken ct = default);
    Task<Dataset?> GetVisibleAsync(string userId, Guid datasetId, CancellationToken ct = default);
}
