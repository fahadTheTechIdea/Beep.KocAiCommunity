using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Beep.KocAiCommunity.Desktop.Local;

/// <summary>Why a run ended. Distinguishing these is the difference between a result and a guess.</summary>
public enum LocalRunOutcome
{
    /// <summary>Ran to its time budget and produced a model.</summary>
    Completed,

    /// <summary>The user stopped it. A best-so-far model may still exist.</summary>
    Stopped,

    /// <summary>The memory watchdog ended it.</summary>
    OutOfBudget,

    /// <summary>It threw. <see cref="LocalRun.Error"/> says what.</summary>
    Failed,
}

/// <summary>
/// One recorded training run.
/// <para>
/// <see cref="DatasetHash"/> is the point of this record. A run whose data has since changed is not
/// reproducible, and a history that quietly implies otherwise is worse than no history — so the hash
/// is stored and checked, and the UI says when it no longer matches.
/// </para>
/// </summary>
public sealed record LocalRun
{
    public required string Id { get; init; }
    public required DateTime StartedUtc { get; init; }
    public double DurationSeconds { get; init; }
    public LocalRunOutcome Outcome { get; init; }

    public required string DatasetId { get; init; }
    public required string DatasetName { get; init; }

    /// <summary>Content hash of the CSV as it was when this ran.</summary>
    public string? DatasetHash { get; init; }

    public required string Task { get; init; }
    public required string TargetColumn { get; init; }

    public string? Algorithm { get; init; }
    public string? PrimaryMetric { get; init; }
    public double? PrimaryValue { get; init; }
    public string? SecondaryMetric { get; init; }
    public double? SecondaryValue { get; init; }
    public long RowCount { get; init; }

    public int TrialsCompleted { get; init; }
    public int LimitSeconds { get; init; }
    public int LimitMemoryMb { get; init; }

    public string? Error { get; init; }

    /// <summary>True when a model was captured and is still on disk.</summary>
    [JsonIgnore]
    public bool HasModel { get; init; }
}

/// <summary>
/// Training runs as folders in the workspace — no database.
/// <para>
/// The workspace is already the unit of backup and the thing a user can copy to another machine. Adding
/// SQLite would create a second thing to migrate, corrupt and explain.
/// </para>
/// </summary>
public sealed class LocalRunStore(LocalWorkspace workspace)
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { WriteIndented = true };

    private string RunsPath => Path.Combine(workspace.RootPath, "runs");

    /// <summary>Newest first. A run whose folder is unreadable is skipped rather than failing the list.</summary>
    public IReadOnlyList<LocalRun> List(int take = 50)
    {
        Directory.CreateDirectory(RunsPath);

        var runs = new List<LocalRun>();
        foreach (var dir in Directory.EnumerateDirectories(RunsPath).OrderByDescending(d => d))
        {
            if (Read(dir) is { } run)
            {
                runs.Add(run);
            }

            if (runs.Count >= take)
            {
                break;
            }
        }

        return runs;
    }

    public LocalRun? Get(string id) => Read(Path.Combine(RunsPath, id));

    /// <summary>Writes the record, and the model beside it when one was produced.</summary>
    public async Task SaveAsync(LocalRun run, byte[]? model, string? trialLog, CancellationToken ct = default)
    {
        var dir = Path.Combine(RunsPath, run.Id);
        Directory.CreateDirectory(dir);

        await File.WriteAllTextAsync(Path.Combine(dir, "run.json"), JsonSerializer.Serialize(run, Json), ct);

        if (model is { Length: > 0 })
        {
            await File.WriteAllBytesAsync(Path.Combine(dir, "model.zip"), model, ct);
        }

        if (!string.IsNullOrWhiteSpace(trialLog))
        {
            await File.WriteAllTextAsync(Path.Combine(dir, "log.txt"), trialLog, ct);
        }
    }

    /// <summary>The saved model's bytes, or null when the run produced none or it has been pruned.</summary>
    public async Task<byte[]?> ReadModelAsync(string id, CancellationToken ct = default)
    {
        var path = Path.Combine(RunsPath, id, "model.zip");
        return File.Exists(path) ? await File.ReadAllBytesAsync(path, ct) : null;
    }

    public string? ReadTrialLog(string id)
    {
        var path = Path.Combine(RunsPath, id, "log.txt");
        return File.Exists(path) ? File.ReadAllText(path) : null;
    }

    public bool Delete(string id)
    {
        var dir = Path.Combine(RunsPath, id);
        if (!Directory.Exists(dir))
        {
            return false;
        }

        Directory.Delete(dir, recursive: true);
        return true;
    }

    /// <summary>
    /// Drops the model file but keeps the record. Models are the bulk of the disk cost, and the metrics
    /// are what someone actually comes back to look at.
    /// </summary>
    public bool PruneModel(string id)
    {
        var path = Path.Combine(RunsPath, id, "model.zip");
        if (!File.Exists(path))
        {
            return false;
        }

        File.Delete(path);
        return true;
    }

    /// <summary>Total bytes under the runs folder, for the Settings disk report.</summary>
    public long TotalBytes()
    {
        Directory.CreateDirectory(RunsPath);
        return new DirectoryInfo(RunsPath)
            .EnumerateFiles("*", SearchOption.AllDirectories)
            .Sum(f => f.Length);
    }

    /// <summary>A folder name that sorts chronologically — the list ordering depends on it.</summary>
    public static string NewId(DateTime startedUtc) =>
        $"{startedUtc:yyyyMMdd-HHmmss}-{Guid.NewGuid().ToString("N")[..6]}";

    /// <summary>Content hash of a dataset file, so a later run can tell whether the data moved under it.</summary>
    public static async Task<string?> HashFileAsync(string? path, CancellationToken ct = default)
    {
        if (path is null || !File.Exists(path))
        {
            return null;
        }

        try
        {
            await using var stream = File.OpenRead(path);
            var hash = await SHA256.HashDataAsync(stream, ct);
            return Convert.ToHexString(hash)[..16];
        }
        catch (Exception)
        {
            // A hash we could not take is better reported as absent than as wrong.
            return null;
        }
    }

    private static LocalRun? Read(string dir)
    {
        var path = Path.Combine(dir, "run.json");
        if (!File.Exists(path))
        {
            return null;
        }

        try
        {
            var run = JsonSerializer.Deserialize<LocalRun>(File.ReadAllText(path), Json);
            return run is null ? null : run with { HasModel = File.Exists(Path.Combine(dir, "model.zip")) };
        }
        catch (Exception)
        {
            // One unreadable run must not hide the rest of the history.
            return null;
        }
    }
}
