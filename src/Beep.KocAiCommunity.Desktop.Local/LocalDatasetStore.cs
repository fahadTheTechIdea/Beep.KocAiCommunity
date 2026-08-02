using System.Text;
using System.Text.Json;
using Beep.KocAiCommunity.Application.Common;
using Beep.KocAiCommunity.Application.Datasets;
using Beep.KocAiCommunity.Contracts.Datasets;

namespace Beep.KocAiCommunity.Desktop.Local;

/// <summary>A file waiting to be imported, and what it appears to be.</summary>
/// <param name="StagedPath">Where the raw bytes are parked until the user commits or cancels.</param>
/// <param name="FileName">The name it came in under.</param>
/// <param name="Format">The detected encoding and delimiter — a guess the user can override.</param>
/// <param name="Header">Columns as they parse under <paramref name="Format"/>.</param>
/// <param name="Rows">A few parsed rows, so a wrong guess is visible rather than described.</param>
/// <param name="Problem">Set when the file cannot be imported at all; nothing else is meaningful then.</param>
public sealed record StagedImport(
    string StagedPath,
    string FileName,
    CsvFormat Format,
    IReadOnlyList<string> Header,
    IReadOnlyList<string[]> Rows,
    string? Problem = null)
{
    public bool CanCommit => Problem is null && Header.Count > 0;
}

/// <summary>
/// Local CSV files as datasets. Each file in the workspace <c>datasets/</c> folder is a dataset;
/// a persisted index gives every file a stable <see cref="Guid"/> so saved workflows (join/union
/// nodes reference a dataset id) keep resolving across restarts.
/// </summary>
public sealed class LocalDatasetStore(LocalWorkspace workspace)
{
    private readonly string _indexPath = Path.Combine(workspace.DatasetsPath, ".index.json");
    private readonly Lock _gate = new();

    /// <summary>
    /// A line this long with no break means the file is not row-oriented — a single-line 200 MB export,
    /// or a binary someone renamed. Reading on would allocate the lot.
    /// </summary>
    public const int MaxLineBytes = 1024 * 1024;

    /// <summary>Lines sampled to estimate a row count without reading the file.</summary>
    private const int RowEstimateSampleLines = 200;

    /// <summary>Most recently used first, then alphabetically — the file being worked on stays on top.</summary>
    public IReadOnlyList<DatasetDto> List()
    {
        lock (_gate)
        {
            var index = LoadIndex();
            var changed = false;
            var result = new List<(DatasetDto Dto, DateTime? LastUsed)>();

            foreach (var file in Directory.EnumerateFiles(workspace.DatasetsPath, "*.csv"))
            {
                var name = Path.GetFileName(file);
                if (!index.Datasets.TryGetValue(name, out var entry))
                {
                    entry = new LocalDatasetEntry { Id = Guid.NewGuid() };
                    index.Datasets[name] = entry;
                    changed = true;
                }

                result.Add((new DatasetDto(entry.Id, Path.GetFileNameWithoutExtension(name), "Local file",
                    "Local", "Internal", "local", "local", HasFile: true), entry.LastUsedUtc));
            }

            if (changed)
            {
                SaveIndex(index);
            }

            return [.. result
                .OrderByDescending(r => r.LastUsed ?? DateTime.MinValue)
                .ThenBy(r => r.Dto.Name, StringComparer.OrdinalIgnoreCase)
                .Select(r => r.Dto)];
        }
    }

    /// <summary>
    /// Parks a file and reports what it appears to be, without committing it.
    /// <para>
    /// Detection is a guess, and a guess that acts silently is the worst of both worlds: a
    /// semicolon-separated export used to import as one column named after the whole header row, and
    /// the failure surfaced much later in the designer. Staging lets the guess be shown, with parsed
    /// columns beside it, before anything is kept.
    /// </para>
    /// </summary>
    public async Task<StagedImport> StageAsync(Stream content, string fileName, CancellationToken ct = default)
    {
        Directory.CreateDirectory(workspace.TempPath);
        var staged = Path.Combine(workspace.TempPath, $"import-{Guid.NewGuid():N}.csv");

        await using (var file = File.Create(staged))
        {
            await content.CopyToAsync(file, ct);
        }

        await using (var probe = File.OpenRead(staged))
        {
            if (!await LooksRowOrientedAsync(probe, ct))
            {
                return new StagedImport(staged, fileName, new CsvFormat(Encoding.UTF8, ',', false), [], [],
                    "This file has no line breaks in its first megabyte, so it is not a row-per-line CSV.");
            }
        }

        await using var stream = File.OpenRead(staged);
        var format = await CsvFormatDetector.DetectAsync(stream, ct);
        var (header, rows) = PreviewStaged(staged, format);

        return new StagedImport(staged, fileName, format, header, rows);
    }

    /// <summary>Re-reads a staged file under a different encoding or delimiter, for the override.</summary>
    public StagedImport Restage(StagedImport staged, CsvFormat format)
    {
        var (header, rows) = PreviewStaged(staged.StagedPath, format);
        return staged with { Format = format, Header = header, Rows = rows };
    }

    /// <summary>
    /// Commits a staged file into the workspace, converted to UTF-8 with commas.
    /// <para>
    /// Converting rather than recording the original format is deliberate: the node engine, AutoML and
    /// every scorer read comma-separated UTF-8. Storing a semicolon file as-is would mean teaching all
    /// of them what a dataset's delimiter is, and getting one of them wrong.
    /// </para>
    /// <para>
    /// A name that is already taken gets a numeric suffix rather than overwriting: two people's
    /// <c>data.csv</c> are not the same file, and silently replacing one would lose it.
    /// </para>
    /// </summary>
    public async Task<DatasetDto> CommitAsync(StagedImport staged, CancellationToken ct = default)
    {
        var safe = SafeCsvName(staged.FileName);
        string destination;

        lock (_gate)
        {
            destination = UniquePath(safe);
            // Claim the name inside the lock so two imports cannot resolve to the same path.
            File.Create(destination).Dispose();
        }

        try
        {
            await ConvertAsync(staged.StagedPath, destination, staged.Format, ct);
        }
        catch (Exception)
        {
            // Do not leave the claimed empty file behind pretending to be a dataset.
            TryDelete(destination);
            throw;
        }
        finally
        {
            TryDelete(staged.StagedPath);
        }

        lock (_gate)
        {
            var index = LoadIndex();
            var name = Path.GetFileName(destination);
            if (!index.Datasets.TryGetValue(name, out var entry))
            {
                entry = new LocalDatasetEntry { Id = Guid.NewGuid(), OriginalName = staged.FileName };
                index.Datasets[name] = entry;
                SaveIndex(index);
            }

            return new DatasetDto(entry.Id, Path.GetFileNameWithoutExtension(name), "Local file",
                "Local", "Internal", "local", "local", HasFile: true);
        }
    }

    /// <summary>Stage and commit in one step, taking the detection as offered. Used where there is no UI.</summary>
    public async Task<DatasetDto> ImportAsync(Stream content, string fileName, CancellationToken ct = default)
    {
        var staged = await StageAsync(content, fileName, ct);
        if (!staged.CanCommit)
        {
            TryDelete(staged.StagedPath);
            throw new InvalidOperationException(staged.Problem ?? "That file has no columns.");
        }

        return await CommitAsync(staged, ct);
    }

    /// <summary>
    /// Renames a dataset, keeping its id.
    /// <para>
    /// The id must survive: workflows reference it, and a rename that minted a new one would break every
    /// pipeline built on the file.
    /// </para>
    /// </summary>
    public bool Rename(Guid datasetId, string newName)
    {
        lock (_gate)
        {
            var index = LoadIndex();
            var entry = index.Datasets.FirstOrDefault(e => e.Value.Id == datasetId);
            if (entry.Key is null)
            {
                return false;
            }

            var target = SafeCsvName(newName);
            if (string.Equals(target, entry.Key, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            var source = Path.Combine(workspace.DatasetsPath, entry.Key);
            var destination = UniquePath(target);
            if (!File.Exists(source))
            {
                return false;
            }

            File.Move(source, destination);
            TryDelete(ProfilePathFor(entry.Key));

            index.Datasets.Remove(entry.Key);
            index.Datasets[Path.GetFileName(destination)] = entry.Value;
            SaveIndex(index);
            return true;
        }
    }

    /// <summary>Records that a pipeline ran against this dataset, so it sorts to the top.</summary>
    public void MarkUsed(Guid datasetId)
    {
        lock (_gate)
        {
            var index = LoadIndex();
            var entry = index.Datasets.FirstOrDefault(e => e.Value.Id == datasetId);
            if (entry.Key is null)
            {
                return;
            }

            index.Datasets[entry.Key] = entry.Value with { LastUsedUtc = DateTime.UtcNow };
            SaveIndex(index);
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
            var entry = index.Datasets.FirstOrDefault(e => e.Value.Id == datasetId);
            if (entry.Key is null)
            {
                return false;
            }

            var path = Path.Combine(workspace.DatasetsPath, entry.Key);
            if (File.Exists(path))
            {
                File.Delete(path);
            }

            TryDelete(ProfilePathFor(entry.Key));
            index.Datasets.Remove(entry.Key);
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
        var records = KocCsv.ParseRecords(reader).GetEnumerator();
        if (!records.MoveNext())
        {
            return ([], [], 0);
        }

        var header = records.Current;
        var sample = new List<string[]>();
        var read = 0;

        while (sample.Count < rows && records.MoveNext())
        {
            ct.ThrowIfCancellationRequested();
            read++;
            sample.Add(records.Current);
        }

        return (header, sample, read);
    }

    /// <summary>
    /// Column types, nulls, distincts and ranges — computed once and cached beside the file.
    /// <para>
    /// On demand rather than on import: a 200 MB file should not hold up the import button for detail
    /// nobody has asked for yet. The cache is invalidated by the file's write time, so editing a file
    /// outside the app does not leave a profile describing what it used to contain.
    /// </para>
    /// </summary>
    public async Task<CsvProfileResult?> ProfileAsync(Guid datasetId, CancellationToken ct = default)
    {
        var path = PathFor(datasetId);
        if (path is null)
        {
            return null;
        }

        var cachePath = ProfilePathFor(Path.GetFileName(path));
        var writtenUtc = File.GetLastWriteTimeUtc(path);

        if (ReadCachedProfile(cachePath, writtenUtc) is { } cached)
        {
            return cached;
        }

        // Profiling streams, but it is still work; keep it off whichever thread asked.
        var profile = await Task.Run(() =>
        {
            using var stream = File.OpenRead(path);
            return CsvProfiler.Profile(stream);
        }, ct);

        TryWriteCachedProfile(cachePath, writtenUtc, profile);
        return profile;
    }

    /// <summary>
    /// Rows, guessed from the file's size and the length of its first lines.
    /// <para>
    /// Counting them means reading the file, and this number exists to fill in a card. It is labelled
    /// as approximate wherever it is shown; <see cref="ProfileAsync"/> gives the true count to anyone
    /// who has asked for detail.
    /// </para>
    /// </summary>
    public async Task<long?> EstimateRowsAsync(Guid datasetId, CancellationToken ct = default)
    {
        var path = PathFor(datasetId);
        if (path is null)
        {
            return null;
        }

        var length = new FileInfo(path).Length;
        if (length == 0)
        {
            return 0;
        }

        using var reader = new StreamReader(path);
        long sampledBytes = 0;
        var sampledLines = 0;

        // The header is not a row, and it is usually longer than one — measure it, then leave it out.
        var headerLine = await reader.ReadLineAsync(ct);
        var headerBytes = headerLine is null ? 0 : Encoding.UTF8.GetByteCount(headerLine) + 1;

        while (sampledLines < RowEstimateSampleLines && await reader.ReadLineAsync(ct) is { } line)
        {
            sampledBytes += Encoding.UTF8.GetByteCount(line) + 1;
            sampledLines++;
        }

        if (sampledLines == 0 || sampledBytes == 0)
        {
            return 0;
        }

        var averageLine = (double)sampledBytes / sampledLines;
        return (long)Math.Round(Math.Max(0, length - headerBytes) / averageLine);
    }

    /// <summary>The folder the files live in, so the UI can offer to open it.</summary>
    public string FolderPath => workspace.DatasetsPath;

    /// <summary>The absolute path of the CSV backing <paramref name="datasetId"/>, or null.</summary>
    public string? PathFor(Guid datasetId)
    {
        lock (_gate)
        {
            foreach (var (name, entry) in LoadIndex().Datasets)
            {
                if (entry.Id == datasetId)
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

    /// <summary>Reads the head of a staged file under a given format, without loading all of it.</summary>
    private static (IReadOnlyList<string> Header, IReadOnlyList<string[]> Rows) PreviewStaged(
        string path, CsvFormat format, int rows = 8)
    {
        try
        {
            using var reader = new StreamReader(path, format.Encoding);
            using var records = KocCsv.ParseRecords(reader, format.Delimiter).GetEnumerator();
            if (!records.MoveNext())
            {
                return ([], []);
            }

            var header = records.Current;
            var sample = new List<string[]>();
            while (sample.Count < rows && records.MoveNext())
            {
                sample.Add(records.Current);
            }

            return (header, sample);
        }
        catch (Exception)
        {
            return ([], []);
        }
    }

    /// <summary>Rewrites a staged file as UTF-8 with commas, streaming a record at a time.</summary>
    private static async Task ConvertAsync(string source, string destination, CsvFormat format, CancellationToken ct)
    {
        using var reader = new StreamReader(source, format.Encoding);
        await using var writer = new StreamWriter(destination, append: false, new UTF8Encoding(false));

        foreach (var record in KocCsv.ParseRecords(reader, format.Delimiter))
        {
            ct.ThrowIfCancellationRequested();
            await writer.WriteLineAsync(KocCsv.WriteRow(record));
        }
    }

    /// <summary>
    /// True if a line break turns up inside the first megabyte. A file without one is not row-oriented,
    /// and parsing it would build a single field the size of the file.
    /// </summary>
    private static async Task<bool> LooksRowOrientedAsync(Stream stream, CancellationToken ct)
    {
        var buffer = new byte[64 * 1024];
        var scanned = 0;

        while (scanned < MaxLineBytes)
        {
            var read = await stream.ReadAsync(buffer, ct);
            if (read == 0)
            {
                // The whole file fits inside the cap, so nothing can run away.
                return true;
            }

            if (buffer.AsSpan(0, read).IndexOfAny((byte)'\n', (byte)'\r') >= 0)
            {
                return true;
            }

            scanned += read;
        }

        return false;
    }

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

    private string ProfilePathFor(string csvFileName) =>
        Path.Combine(workspace.DatasetsPath, $".{csvFileName}.profile.json");

    private sealed record CachedProfile(DateTime SourceWrittenUtc, CsvProfileResult Profile);

    private static CsvProfileResult? ReadCachedProfile(string path, DateTime writtenUtc)
    {
        if (!File.Exists(path))
        {
            return null;
        }

        try
        {
            var cached = JsonSerializer.Deserialize<CachedProfile>(File.ReadAllText(path));
            return cached is not null && cached.SourceWrittenUtc == writtenUtc ? cached.Profile : null;
        }
        catch (Exception)
        {
            return null; // a stale or damaged cache just means profiling again
        }
    }

    private static void TryWriteCachedProfile(string path, DateTime writtenUtc, CsvProfileResult profile)
    {
        try
        {
            File.WriteAllText(path, JsonSerializer.Serialize(new CachedProfile(writtenUtc, profile)));
        }
        catch (Exception)
        {
            // A cache that cannot be written costs a few seconds next time, and nothing else.
        }
    }

    private static void TryDelete(string path)
    {
        try { File.Delete(path); } catch (IOException) { /* best effort */ }
    }

    private LocalDatasetIndex LoadIndex()
    {
        if (!File.Exists(_indexPath))
        {
            return new LocalDatasetIndex();
        }

        try
        {
            return LocalDatasetIndex.Parse(File.ReadAllText(_indexPath));
        }
        catch (Exception)
        {
            // Keep the unreadable file rather than overwriting it: the ids in it are the only link
            // between a saved workflow and its dataset, and someone may be able to salvage them.
            TryPreserveCorruptIndex();
            IndexWasRebuilt = true;
            return new LocalDatasetIndex();
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

    private void SaveIndex(LocalDatasetIndex index) => File.WriteAllText(_indexPath, index.ToJson());
}
