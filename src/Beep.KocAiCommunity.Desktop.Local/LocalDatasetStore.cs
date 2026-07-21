using System.Text.Json;
using Beep.KocAiCommunity.Contracts.Datasets;

namespace Beep.KocAiCommunity.Desktop.Local;

/// <summary>
/// Local CSV files as datasets. Each file in the workspace <c>datasets/</c> folder is a dataset;
/// a persisted index gives every file a stable <see cref="Guid"/> so saved workflows (join/union
/// nodes reference a dataset id) keep resolving across restarts.
/// </summary>
public sealed class LocalDatasetStore(LocalWorkspace workspace)
{
    private readonly string _indexPath = Path.Combine(workspace.DatasetsPath, ".index.json");
    private readonly Lock _gate = new();

    public IReadOnlyList<DatasetDto> List()
    {
        lock (_gate)
        {
            var index = LoadIndex();
            var changed = false;
            var result = new List<DatasetDto>();

            foreach (var file in Directory.EnumerateFiles(workspace.DatasetsPath, "*.csv"))
            {
                var name = Path.GetFileName(file);
                if (!index.TryGetValue(name, out var id))
                {
                    id = Guid.NewGuid();
                    index[name] = id;
                    changed = true;
                }

                result.Add(new DatasetDto(id, Path.GetFileNameWithoutExtension(name), "Local file",
                    "Local", "Internal", "local", "local", HasFile: true));
            }

            if (changed)
            {
                SaveIndex(index);
            }

            return result;
        }
    }

    /// <summary>The absolute path of the CSV backing <paramref name="datasetId"/>, or null.</summary>
    public string? PathFor(Guid datasetId)
    {
        lock (_gate)
        {
            foreach (var (name, id) in LoadIndex())
            {
                if (id == datasetId)
                {
                    var path = Path.Combine(workspace.DatasetsPath, name);
                    return File.Exists(path) ? path : null;
                }
            }

            return null;
        }
    }

    private Dictionary<string, Guid> LoadIndex()
    {
        if (!File.Exists(_indexPath))
        {
            return new(StringComparer.OrdinalIgnoreCase);
        }

        try
        {
            return JsonSerializer.Deserialize<Dictionary<string, Guid>>(File.ReadAllText(_indexPath))
                   ?? new(StringComparer.OrdinalIgnoreCase);
        }
        catch
        {
            return new(StringComparer.OrdinalIgnoreCase);
        }
    }

    private void SaveIndex(Dictionary<string, Guid> index) =>
        File.WriteAllText(_indexPath, JsonSerializer.Serialize(index));
}
