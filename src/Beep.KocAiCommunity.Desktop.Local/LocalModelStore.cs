using System.Text.Json;
using Beep.KocAiCommunity.ML;

namespace Beep.KocAiCommunity.Desktop.Local;

/// <summary>
/// A model an engineer decided to keep, and where it came from.
/// <para>
/// This carries enough to answer "what is this and where did it come from" without the run it came from
/// still existing. A registry that pointed back at run folders would gut itself the first time somebody
/// cleared their run history.
/// </para>
/// </summary>
public sealed record LocalModelVersion
{
    /// <summary>Stable per version, and the key the prediction pool caches on.</summary>
    public required Guid Id { get; init; }

    public required string Name { get; init; }
    public required int Version { get; init; }
    public required DateTime CreatedUtc { get; init; }

    public required string Task { get; init; }
    public required string TargetColumn { get; init; }

    /// <summary>The columns the model itself says it needs, read from its schema when it was kept.</summary>
    public IReadOnlyList<ModelInputColumn> Inputs { get; init; } = [];

    public string? Algorithm { get; init; }
    public string? PrimaryMetric { get; init; }
    public double? PrimaryValue { get; init; }
    public string? SecondaryMetric { get; init; }
    public double? SecondaryValue { get; init; }

    public string? SourceRunId { get; init; }
    public string? DatasetName { get; init; }
    public string? DatasetHash { get; init; }

    /// <summary>
    /// The ML.NET that wrote this. A bundle from a newer runtime is refused on import — loading it
    /// anyway produces a crash a long way from its cause.
    /// </summary>
    public required string MlNetVersion { get; init; }

    /// <summary>Feature columns — everything the model takes that is not the label.</summary>
    public IEnumerable<ModelInputColumn> Features =>
        Inputs.Where(c => !string.Equals(c.Name, TargetColumn, StringComparison.OrdinalIgnoreCase));
}

/// <summary>
/// Kept models, as folders in the workspace.
/// <para>
/// Deliberately not the run history. A run is an experiment; a model is a thing you decided to keep, and
/// registering <em>copies</em> the file so that pruning runs cannot gut the registry. On a single
/// engineer's machine there is no audience to protect, so there is no approval workflow here — that is a
/// platform concern, and two-person approval on your own laptop is theatre.
/// </para>
/// </summary>
public sealed class LocalModelStore(LocalWorkspace workspace)
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { WriteIndented = true };

    public string FolderPath => Path.Combine(workspace.RootPath, "models");

    /// <summary>Every kept version, newest first.</summary>
    public IReadOnlyList<LocalModelVersion> List()
    {
        Directory.CreateDirectory(FolderPath);

        return [.. Directory.EnumerateDirectories(FolderPath)
            .SelectMany(nameDir => Directory.EnumerateDirectories(nameDir))
            .Select(Read)
            .OfType<LocalModelVersion>()
            .OrderByDescending(m => m.CreatedUtc)
            .ThenByDescending(m => m.Version)];
    }

    /// <summary>The highest version kept under a name, or null if the name is unused.</summary>
    public LocalModelVersion? Latest(string name) =>
        List().Where(m => m.Name == name).MaxBy(m => m.Version);

    public LocalModelVersion? Get(Guid id) => List().FirstOrDefault(m => m.Id == id);

    /// <summary>
    /// Keeps a model under a name, as the next version.
    /// <para>
    /// Versions are integers and are never reused — the highest that has ever existed plus one, not the
    /// count of what is on disk, so deleting v2 does not make the next save silently overwrite it.
    /// </para>
    /// </summary>
    public async Task<LocalModelVersion> RegisterAsync(
        string name, byte[] modelBytes, LocalModelVersion details, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("A model needs a name.", nameof(name));
        }

        if (modelBytes.Length == 0)
        {
            throw new ArgumentException("There is no model to keep.", nameof(modelBytes));
        }

        var safeName = SafeName(name);
        var nameDirectory = Path.Combine(FolderPath, safeName);
        Directory.CreateDirectory(nameDirectory);

        var version = NextVersion(nameDirectory);
        var directory = Path.Combine(nameDirectory, $"v{version}");
        Directory.CreateDirectory(directory);

        var kept = details with
        {
            Id = Guid.NewGuid(),
            Name = safeName,
            Version = version,
            CreatedUtc = DateTime.UtcNow,
            MlNetVersion = CurrentMlNetVersion,
            // Read from the model itself rather than from whatever the caller believed.
            Inputs = ModelSchema.Read(modelBytes),
        };

        await File.WriteAllBytesAsync(Path.Combine(directory, "model.zip"), modelBytes, ct);
        await File.WriteAllTextAsync(Path.Combine(directory, "model.json"), JsonSerializer.Serialize(kept, Json), ct);
        await File.WriteAllTextAsync(Path.Combine(nameDirectory, "latest.txt"), version.ToString(), ct);

        return kept;
    }

    /// <summary>Writes a version straight from an imported bundle, keeping its own recorded lineage.</summary>
    public async Task<LocalModelVersion> AdoptAsync(
        LocalModelVersion imported, byte[] modelBytes, CancellationToken ct = default)
    {
        var nameDirectory = Path.Combine(FolderPath, SafeName(imported.Name));
        Directory.CreateDirectory(nameDirectory);

        var version = NextVersion(nameDirectory);
        var directory = Path.Combine(nameDirectory, $"v{version}");
        Directory.CreateDirectory(directory);

        // A new id and a local version number; everything else — metrics, lineage, the ML.NET that
        // wrote it — is the bundle's and is preserved. Rewriting those would erase where it came from.
        var adopted = imported with
        {
            Id = Guid.NewGuid(),
            Name = SafeName(imported.Name),
            Version = version,
        };

        await File.WriteAllBytesAsync(Path.Combine(directory, "model.zip"), modelBytes, ct);
        await File.WriteAllTextAsync(Path.Combine(directory, "model.json"), JsonSerializer.Serialize(adopted, Json), ct);
        await File.WriteAllTextAsync(Path.Combine(nameDirectory, "latest.txt"), version.ToString(), ct);

        return adopted;
    }

    public async Task<byte[]?> ReadModelAsync(Guid id, CancellationToken ct = default)
    {
        if (DirectoryFor(id) is not { } directory)
        {
            return null;
        }

        var path = Path.Combine(directory, "model.zip");
        return File.Exists(path) ? await File.ReadAllBytesAsync(path, ct) : null;
    }

    public bool Delete(Guid id)
    {
        if (DirectoryFor(id) is not { } directory)
        {
            return false;
        }

        Directory.Delete(directory, recursive: true);

        // A name whose last version is gone should not leave an empty folder behind pretending to be
        // a model with no versions.
        var nameDirectory = Path.GetDirectoryName(directory)!;
        if (!Directory.EnumerateDirectories(nameDirectory).Any())
        {
            Directory.Delete(nameDirectory, recursive: true);
        }

        return true;
    }

    /// <summary>Bytes on disk for one version, for the size shown against it.</summary>
    public long SizeOf(Guid id) =>
        DirectoryFor(id) is { } directory
            ? new DirectoryInfo(directory).EnumerateFiles().Sum(f => f.Length)
            : 0;

    public long TotalBytes()
    {
        Directory.CreateDirectory(FolderPath);
        return new DirectoryInfo(FolderPath).EnumerateFiles("*", SearchOption.AllDirectories).Sum(f => f.Length);
    }

    /// <summary>The ML.NET this host runs, stamped into everything it writes.</summary>
    public static string CurrentMlNetVersion =>
        typeof(Microsoft.ML.MLContext).Assembly.GetName().Version?.ToString() ?? "0.0.0.0";

    /// <summary>
    /// A name that is safe as a folder. Model names are user input and go straight into a path, so a
    /// name of "../../windows" must not be able to write outside the workspace.
    /// </summary>
    public static string SafeName(string name)
    {
        var cleaned = new string([.. name.Trim()
            .Select(c => Path.GetInvalidFileNameChars().Contains(c) || c is '.' or ' ' ? '-' : c)]);

        cleaned = cleaned.Trim('-');
        return string.IsNullOrEmpty(cleaned) ? "model" : cleaned;
    }

    private int NextVersion(string nameDirectory)
    {
        // The highest that has ever existed, not the count on disk: 'latest.txt' survives a deletion,
        // so deleting v2 cannot make the next save reuse its number.
        var highest = 0;
        var marker = Path.Combine(nameDirectory, "latest.txt");
        if (File.Exists(marker) && int.TryParse(File.ReadAllText(marker), out var recorded))
        {
            highest = recorded;
        }

        foreach (var directory in Directory.EnumerateDirectories(nameDirectory))
        {
            var folder = Path.GetFileName(directory);
            if (folder.StartsWith('v') && int.TryParse(folder[1..], out var existing) && existing > highest)
            {
                highest = existing;
            }
        }

        return highest + 1;
    }

    private string? DirectoryFor(Guid id)
    {
        Directory.CreateDirectory(FolderPath);

        return Directory.EnumerateDirectories(FolderPath)
            .SelectMany(Directory.EnumerateDirectories)
            .FirstOrDefault(d => Read(d)?.Id == id);
    }

    private static LocalModelVersion? Read(string directory)
    {
        var path = Path.Combine(directory, "model.json");
        if (!File.Exists(path))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<LocalModelVersion>(File.ReadAllText(path), Json);
        }
        catch (Exception)
        {
            // One unreadable manifest must not hide the rest of the registry.
            return null;
        }
    }
}
