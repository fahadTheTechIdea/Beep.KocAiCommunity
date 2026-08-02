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

    /// <summary>
    /// Copies a CSV into the workspace and returns it as a dataset.
    /// <para>
    /// Until this existed the only way to get data into the desktop Studio was to know about
    /// <c>%LOCALAPPDATA%\KocStudio\datasets</c> and copy files there by hand — which meant the designer
    /// opened with nothing to run on and no way to say so.
    /// </para>
    /// <para>
    /// A name that is already taken gets a numeric suffix rather than overwriting: two people's
    /// <c>data.csv</c> are not the same file, and silently replacing one would lose it.
    /// </para>
    /// </summary>
    public async Task<DatasetDto> ImportAsync(Stream content, string fileName, CancellationToken ct = default)
    {
        var safe = SafeCsvName(fileName);
        string destination;

        lock (_gate)
        {
            destination = UniquePath(safe);
            // Claim the name inside the lock so two imports cannot resolve to the same path.
            File.Create(destination).Dispose();
        }

        await using (var file = File.Create(destination))
        {
            await content.CopyToAsync(file, ct);
        }

        lock (_gate)
        {
            var index = LoadIndex();
            var name = Path.GetFileName(destination);
            if (!index.TryGetValue(name, out var id))
            {
                id = Guid.NewGuid();
                index[name] = id;
                SaveIndex(index);
            }

            return new DatasetDto(id, Path.GetFileNameWithoutExtension(name), "Local file",
                "Local", "Internal", "local", "local", HasFile: true);
        }
    }

    /// <summary>
    /// Removes a dataset's file and its index entry. Returns false when it is already gone.
    /// <para>
    /// The id is not reused: a workflow that still references it will fail to resolve the dataset and
    /// say so, which is better than silently binding to whatever file took its place.
    /// </para>
    /// </summary>
    public bool Delete(Guid datasetId)
    {
        lock (_gate)
        {
            var index = LoadIndex();
            var entry = index.FirstOrDefault(e => e.Value == datasetId);
            if (entry.Key is null)
            {
                return false;
            }

            var path = Path.Combine(workspace.DatasetsPath, entry.Key);
            if (File.Exists(path))
            {
                File.Delete(path);
            }

            index.Remove(entry.Key);
            SaveIndex(index);
            return true;
        }
    }

    /// <summary>The first few lines of a dataset, for a preview before it is used in a pipeline.</summary>
    public async Task<(IReadOnlyList<string> Header, IReadOnlyList<string[]> Rows, int SampledLines)> PeekAsync(
        Guid datasetId, int rows = 8, CancellationToken ct = default)
    {
        var path = PathFor(datasetId);
        if (path is null)
        {
            return ([], [], 0);
        }

        using var reader = new StreamReader(path);
        var headerLine = await reader.ReadLineAsync(ct);
        if (headerLine is null)
        {
            return ([], [], 0);
        }

        var header = SplitCsvLine(headerLine);
        var sample = new List<string[]>();
        var read = 0;

        while (sample.Count < rows && await reader.ReadLineAsync(ct) is { } line)
        {
            read++;
            if (line.Length > 0)
            {
                sample.Add(SplitCsvLine(line));
            }
        }

        return (header, sample, read);
    }

    /// <summary>The folder the files live in, so the UI can offer to open it.</summary>
    public string FolderPath => workspace.DatasetsPath;

    /// <summary>Keeps an imported name to a plain CSV file name inside the workspace.</summary>
    private static string SafeCsvName(string fileName)
    {
        var stem = Path.GetFileNameWithoutExtension(fileName);
        foreach (var bad in Path.GetInvalidFileNameChars())
        {
            stem = stem.Replace(bad, '-');
        }

        stem = stem.Trim();
        return (stem.Length == 0 ? "dataset" : stem) + ".csv";
    }

    /// <summary>Adds " (2)", " (3)" … until the name is free. Caller holds the lock.</summary>
    private string UniquePath(string fileName)
    {
        var stem = Path.GetFileNameWithoutExtension(fileName);
        var candidate = Path.Combine(workspace.DatasetsPath, fileName);

        for (var n = 2; File.Exists(candidate); n++)
        {
            candidate = Path.Combine(workspace.DatasetsPath, $"{stem} ({n}).csv");
        }

        return candidate;
    }

    /// <summary>Comma-splitting that respects double quotes — enough for a preview, not a parser.</summary>
    private static string[] SplitCsvLine(string line)
    {
        var cells = new List<string>();
        var cell = new System.Text.StringBuilder();
        var quoted = false;

        foreach (var ch in line)
        {
            if (ch == '"')
            {
                quoted = !quoted;
            }
            else if (ch == ',' && !quoted)
            {
                cells.Add(cell.ToString());
                cell.Clear();
            }
            else
            {
                cell.Append(ch);
            }
        }

        cells.Add(cell.ToString());
        return [.. cells];
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

    /// <summary>
    /// True when the last <see cref="LoadIndex"/> found the index unreadable and started over. The
    /// launch check reads this so a rebuilt index is reported rather than passing unnoticed — the
    /// visible symptom is that every dataset has a new id, which breaks saved workflows.
    /// </summary>
    public bool IndexWasRebuilt { get; private set; }

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
        catch (Exception)
        {
            // Keep the unreadable file rather than overwriting it: the ids in it are the only link
            // between a saved workflow and its dataset, and someone may be able to salvage them.
            TryPreserveCorruptIndex();
            IndexWasRebuilt = true;
            return new(StringComparer.OrdinalIgnoreCase);
        }
    }

    private void TryPreserveCorruptIndex()
    {
        try
        {
            var backup = $"{_indexPath}.corrupt-{DateTime.UtcNow:yyyyMMddHHmmss}";
            if (!File.Exists(backup))
            {
                File.Copy(_indexPath, backup);
            }
        }
        catch (Exception)
        {
            // Best effort. Failing to keep a copy must not stop the app from starting.
        }
    }

    private void SaveIndex(Dictionary<string, Guid> index) =>
        File.WriteAllText(_indexPath, JsonSerializer.Serialize(index));
}
