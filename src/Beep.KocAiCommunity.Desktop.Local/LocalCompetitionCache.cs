using System.Text.Json;

namespace Beep.KocAiCommunity.Desktop.Local;

/// <summary>Something read from the cache, and how old it is.</summary>
public sealed record CachedValue<T>(T Value, DateTime FetchedUtc)
{
    public TimeSpan Age => DateTime.UtcNow - FetchedUtc;
}

/// <summary>
/// The last successful answer from the API, kept so the app has something to show when the network is
/// not there.
/// <para>
/// Every read carries its age, and the UI is expected to show it. A three-day-old leaderboard presented
/// as current is worse than no leaderboard — it is the same number a person would act on, with none of
/// the reasons not to.
/// </para>
/// <para>
/// Disposable by design: deleting the folder costs a refresh and nothing else, and it holds nothing the
/// platform would not serve this user anyway.
/// </para>
/// </summary>
public sealed class LocalCompetitionCache(LocalWorkspace workspace)
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { WriteIndented = true };

    public string FolderPath => Path.Combine(workspace.RootPath, "cache");

    private sealed record Envelope<T>(DateTime FetchedUtc, T Value);

    /// <summary>Stores a successful fetch. A cache that cannot be written is not an error worth raising.</summary>
    public async Task WriteAsync<T>(string key, T value, CancellationToken ct = default)
    {
        try
        {
            Directory.CreateDirectory(FolderPath);
            var json = JsonSerializer.Serialize(new Envelope<T>(DateTime.UtcNow, value), Json);
            await File.WriteAllTextAsync(PathFor(key), json, ct);
        }
        catch (Exception)
        {
            // Losing the cache costs a refresh next time the network is there. It must never cost the
            // call that just succeeded.
        }
    }

    /// <summary>The last stored value and its age, or null if there is none or it cannot be read.</summary>
    public async Task<CachedValue<T>?> ReadAsync<T>(string key, CancellationToken ct = default)
    {
        var path = PathFor(key);
        if (!File.Exists(path))
        {
            return null;
        }

        try
        {
            var envelope = JsonSerializer.Deserialize<Envelope<T>>(await File.ReadAllTextAsync(path, ct), Json);
            return envelope is null ? null : new CachedValue<T>(envelope.Value, envelope.FetchedUtc);
        }
        catch (Exception)
        {
            // A damaged cache entry is indistinguishable from none, and treating it as none is right:
            // the alternative is showing a person half a leaderboard.
            return null;
        }
    }

    /// <summary>
    /// Returns the live value, caching it — or the cached one if the call fails.
    /// <para>
    /// The failure is swallowed on purpose: the caller gets a value plus its age, and decides what to
    /// tell the user. What it must never do is return stale data with no way to tell.
    /// </para>
    /// </summary>
    public async Task<CachedValue<T>?> ThroughAsync<T>(
        string key, Func<CancellationToken, Task<T?>> fetch, CancellationToken ct = default)
    {
        try
        {
            if (await fetch(ct) is { } fresh)
            {
                await WriteAsync(key, fresh, ct);
                return new CachedValue<T>(fresh, DateTime.UtcNow);
            }
        }
        catch (Exception)
        {
            // Offline, or the API is down. Fall through to whatever was last seen.
        }

        return await ReadAsync<T>(key, ct);
    }

    public void Clear()
    {
        try
        {
            if (Directory.Exists(FolderPath))
            {
                Directory.Delete(FolderPath, recursive: true);
            }
        }
        catch (IOException)
        {
            // Best effort — it is a cache.
        }
    }

    /// <summary>The key list, so a competition id cannot write outside the cache folder.</summary>
    public static string CompetitionsKey => "competitions";

    public static string CompetitionKey(Guid id) => $"competition-{id:N}";

    private string PathFor(string key)
    {
        var safe = new string([.. key.Where(c => char.IsLetterOrDigit(c) || c is '-' or '_')]);
        return Path.Combine(FolderPath, $"{(safe.Length == 0 ? "entry" : safe)}.json");
    }
}
