namespace Beep.KocAiCommunity.ML.Nodes;

/// <summary>
/// Central allocation and cleanup of the pipeline's scratch CSV files. Every temp file the executor
/// writes shares one prefix so a startup sweep can reclaim any orphaned by a process crash — the
/// normal path deletes them promptly when the run's <see cref="PipelineContext"/> is disposed.
/// </summary>
public static class PipelineTemp
{
    private const string Prefix = "koc-pipe-";

    /// <summary>A fresh, uniquely-named scratch CSV path (not created on disk until written).</summary>
    public static string New() => Path.Combine(Path.GetTempPath(), $"{Prefix}{Guid.NewGuid():N}.csv");

    /// <summary>Best-effort delete of a single scratch file (never throws).</summary>
    public static void Delete(string path)
    {
        try { File.Delete(path); } catch { /* best effort — swept later if it lingers */ }
    }

    /// <summary>
    /// Reclaims scratch files older than <paramref name="olderThan"/> — a safety net for files a
    /// crashed run couldn't delete. Safe to call at startup; never throws.
    /// </summary>
    public static int SweepOrphans(TimeSpan olderThan)
    {
        var swept = 0;
        DateTime cutoff;
        try { cutoff = DateTime.UtcNow - olderThan; }
        catch { return 0; }

        try
        {
            foreach (var file in Directory.EnumerateFiles(Path.GetTempPath(), $"{Prefix}*.csv"))
            {
                try
                {
                    if (File.GetLastWriteTimeUtc(file) < cutoff)
                    {
                        File.Delete(file);
                        swept++;
                    }
                }
                catch { /* skip files in use */ }
            }
        }
        catch { /* temp dir unreadable — ignore */ }

        return swept;
    }
}
