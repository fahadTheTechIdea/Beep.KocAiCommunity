using System.Text.Json;

namespace Beep.KocAiCommunity.Desktop.Local;

/// <summary>What the workspace remembers about one local CSV, beyond the file itself.</summary>
public sealed record LocalDatasetEntry
{
    /// <summary>Stable across restarts and across a rename — saved workflows reference it.</summary>
    public required Guid Id { get; init; }

    /// <summary>
    /// When a pipeline last ran against it. Sorting by this keeps the file someone is working on at the
    /// top, instead of alphabetising it away from them.
    /// </summary>
    public DateTime? LastUsedUtc { get; init; }

    /// <summary>The file's name as imported, before any conversion or collision suffix.</summary>
    public string? OriginalName { get; init; }
}

/// <summary>
/// The dataset index on disk.
/// <para>
/// It was a bare <c>Dictionary&lt;string, Guid&gt;</c>, which had nowhere to record anything else. This
/// reads both shapes: the ids in an old index are the only link between a saved workflow and its data,
/// so an upgrade that dropped them would quietly break every workflow on the machine.
/// </para>
/// </summary>
public sealed record LocalDatasetIndex
{
    public int Version { get; init; } = 2;

    public Dictionary<string, LocalDatasetEntry> Datasets { get; init; } = new(StringComparer.OrdinalIgnoreCase);

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { WriteIndented = true };

    /// <summary>Parses either index format. Throws if it is neither — the caller preserves and rebuilds.</summary>
    public static LocalDatasetIndex Parse(string json)
    {
        using var document = JsonDocument.Parse(json);

        // Version 2: an object per dataset.
        if (document.RootElement.ValueKind == JsonValueKind.Object
            && document.RootElement.TryGetProperty("datasets", out _))
        {
            return JsonSerializer.Deserialize<LocalDatasetIndex>(json, Json) ?? new LocalDatasetIndex();
        }

        // Version 1: name → guid, and nothing else.
        var legacy = JsonSerializer.Deserialize<Dictionary<string, Guid>>(json)
                     ?? throw new JsonException("The dataset index is not in a shape this version understands.");

        return new LocalDatasetIndex
        {
            Datasets = legacy.ToDictionary(
                e => e.Key,
                e => new LocalDatasetEntry { Id = e.Value },
                StringComparer.OrdinalIgnoreCase),
        };
    }

    public string ToJson() => JsonSerializer.Serialize(this, Json);
}
