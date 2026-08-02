using System.Text;
using System.Text.Json;
using Beep.KocAiCommunity.Desktop.Local;
using FluentAssertions;
using Xunit;

namespace Beep.KocAiCommunity.UnitTests;

/// <summary>
/// Getting a file into the workspace, and what the workspace remembers about it afterwards.
/// <para>
/// The through-line: <b>the id must survive everything</b>. Saved workflows reference datasets by id,
/// so a rename, an index upgrade, or a re-listing that minted new ones would quietly break every
/// pipeline on the machine — with no error, because a workflow pointing at a missing dataset simply
/// stops resolving.
/// </para>
/// </summary>
public sealed class LocalDatasetImportTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "koc-import-" + Guid.NewGuid().ToString("N"));
    private readonly LocalWorkspace _workspace;
    private readonly LocalDatasetStore _store;

    public LocalDatasetImportTests()
    {
        _workspace = new LocalWorkspace { RootPath = _root };
        _workspace.EnsureCreated();
        _store = new LocalDatasetStore(_workspace);
    }

    private static Stream Bytes(string text, Encoding? encoding = null) =>
        new MemoryStream((encoding ?? new UTF8Encoding(false)).GetBytes(text));

    private string ReadStored(Guid id) => File.ReadAllText(_store.PathFor(id)!, Encoding.UTF8);

    [Fact]
    public async Task A_semicolon_file_imports_with_the_right_columns()
    {
        var csv = "well;pressure;failed\nBG-114;120;0\nBG-115;340;1\nBG-116;210;0\n";

        var dataset = await _store.ImportAsync(Bytes(csv), "arabic-excel-export.csv");

        var (header, rows, _) = await _store.PeekAsync(dataset.Id);
        header.Should().BeEquivalentTo(["well", "pressure", "failed"], o => o.WithStrictOrdering());
        rows[0].Should().BeEquivalentTo(["BG-114", "120", "0"], o => o.WithStrictOrdering());
    }

    [Fact]
    public async Task A_windows_1256_file_imports_with_readable_arabic_column_names()
    {
        // The stored copy is UTF-8, so everything downstream reads it without knowing where it came from.
        var arabic = CsvFormatDetector.Arabic();
        var csv = "البئر,الضغط\nBG-114,120\n";

        var dataset = await _store.ImportAsync(Bytes(csv, arabic), "legacy-export.csv");

        var (header, _, _) = await _store.PeekAsync(dataset.Id);
        header.Should().BeEquivalentTo(["البئر", "الضغط"], o => o.WithStrictOrdering());
        ReadStored(dataset.Id).Should().Contain("الضغط");
    }

    [Fact]
    public async Task A_semicolon_file_is_stored_as_a_comma_file()
    {
        // Converting on the way in rather than recording the delimiter: the node engine, AutoML and
        // every scorer read commas, and teaching all of them about delimiters means missing one.
        var dataset = await _store.ImportAsync(Bytes("a;b\n1;2\n"), "semi.csv");

        ReadStored(dataset.Id).Should().Contain("a,b").And.NotContain(";");
    }

    [Fact]
    public async Task A_quoted_field_survives_the_conversion()
    {
        var dataset = await _store.ImportAsync(Bytes("well;notes\nBG-114;\"routine, cleared\"\n"), "quoted.csv");

        var (_, rows, _) = await _store.PeekAsync(dataset.Id);
        rows[0][1].Should().Be("routine, cleared", "the comma is data, and re-quoting must keep it that way");
    }

    [Fact]
    public async Task Staging_reports_what_it_found_without_committing_anything()
    {
        var staged = await _store.StageAsync(Bytes("a;b\n1;2\n"), "probe.csv");

        staged.Format.Delimiter.Should().Be(';');
        staged.Header.Should().BeEquivalentTo(["a", "b"]);
        _store.List().Should().BeEmpty("staging must not put anything in the workspace");
    }

    [Fact]
    public async Task Restaging_under_a_different_delimiter_re_reads_the_columns()
    {
        // What the override does: the user says "that's wrong" and sees the result immediately.
        var staged = await _store.StageAsync(Bytes("a;b\n1;2\n"), "probe.csv");

        var asCommas = _store.Restage(staged, staged.Format with { Delimiter = ',' });

        asCommas.Header.Should().BeEquivalentTo(["a;b"], "read as commas, the whole header is one column");
    }

    [Fact]
    public async Task A_file_with_no_line_breaks_is_refused_rather_than_read()
    {
        // A single-line 200 MB export, or a binary someone renamed. Parsing it would build one field
        // the size of the file.
        var oneLine = new string('x', LocalDatasetStore.MaxLineBytes + 1024);

        var staged = await _store.StageAsync(Bytes(oneLine), "not-really-a-csv.csv");

        staged.CanCommit.Should().BeFalse();
        staged.Problem.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task A_small_file_with_no_trailing_newline_is_fine()
    {
        // The guard is about runaway lines, not about short files that happen to lack a final break.
        var staged = await _store.StageAsync(Bytes("a,b\n1,2"), "tiny.csv");

        staged.CanCommit.Should().BeTrue();
    }

    [Fact]
    public async Task Renaming_keeps_the_id_and_the_file_still_resolves()
    {
        var dataset = await _store.ImportAsync(Bytes("a,b\n1,2\n"), "raw-export.csv");

        _store.Rename(dataset.Id, "ESP readings").Should().BeTrue();

        var listed = _store.List().Should().ContainSingle().Subject;
        listed.Id.Should().Be(dataset.Id, "workflows reference this and a rename must not break them");
        listed.Name.Should().Be("ESP readings");
        _store.PathFor(dataset.Id).Should().NotBeNull();
        File.ReadAllText(_store.PathFor(dataset.Id)!).Should().Contain("a,b");
    }

    [Fact]
    public async Task Renaming_onto_a_taken_name_does_not_overwrite_the_other_file()
    {
        await _store.ImportAsync(Bytes("x\n1\n"), "taken.csv");
        var second = await _store.ImportAsync(Bytes("y\n2\n"), "other.csv");

        _store.Rename(second.Id, "taken").Should().BeTrue();

        _store.List().Should().HaveCount(2);
        File.ReadAllText(_store.PathFor(second.Id)!).Should().Contain("y");
    }

    [Fact]
    public async Task The_list_puts_recently_used_datasets_first()
    {
        var first = await _store.ImportAsync(Bytes("a\n1\n"), "aaa.csv");
        await _store.ImportAsync(Bytes("b\n2\n"), "bbb.csv");

        // Alphabetically 'aaa' already leads, so use the second one to prove recency actually sorts.
        _store.MarkUsed(_store.List().Single(d => d.Name == "bbb").Id);

        _store.List().Select(d => d.Name).Should().ContainInOrder("bbb", "aaa");
        first.Id.Should().NotBe(Guid.Empty);
    }

    [Fact]
    public async Task An_old_index_upgrades_without_losing_a_single_id()
    {
        // The ids in a v1 index are the only link between a saved workflow and its data. An upgrade
        // that dropped them would break every workflow on the machine, silently.
        var dataset = await _store.ImportAsync(Bytes("a,b\n1,2\n"), "legacy.csv");
        var indexPath = Path.Combine(_workspace.DatasetsPath, ".index.json");
        var fileName = Path.GetFileName(_store.PathFor(dataset.Id)!);

        await File.WriteAllTextAsync(indexPath,
            JsonSerializer.Serialize(new Dictionary<string, Guid> { [fileName] = dataset.Id }));

        var reopened = new LocalDatasetStore(_workspace);

        reopened.List().Should().ContainSingle().Which.Id.Should().Be(dataset.Id);
        reopened.IndexWasRebuilt.Should().BeFalse("a v1 index is understood, not discarded");
    }

    [Fact]
    public async Task An_upgraded_index_can_then_record_recency()
    {
        var dataset = await _store.ImportAsync(Bytes("a\n1\n"), "legacy.csv");
        var indexPath = Path.Combine(_workspace.DatasetsPath, ".index.json");
        var fileName = Path.GetFileName(_store.PathFor(dataset.Id)!);
        await File.WriteAllTextAsync(indexPath,
            JsonSerializer.Serialize(new Dictionary<string, Guid> { [fileName] = dataset.Id }));

        var reopened = new LocalDatasetStore(_workspace);
        reopened.MarkUsed(dataset.Id);

        var saved = LocalDatasetIndex.Parse(await File.ReadAllTextAsync(indexPath));
        saved.Datasets[fileName].LastUsedUtc.Should().NotBeNull();
        saved.Datasets[fileName].Id.Should().Be(dataset.Id);
    }

    [Fact]
    public async Task A_profile_is_cached_and_recomputed_when_the_file_changes()
    {
        var dataset = await _store.ImportAsync(Bytes("x,y\n1,10\n2,20\n3,30\n"), "profile-me.csv");

        var first = await _store.ProfileAsync(dataset.Id);
        first!.TotalRows.Should().Be(3);
        first.Columns.Single(c => c.Name == "y").Max.Should().Be(30);

        var cachePath = Path.Combine(_workspace.DatasetsPath, ".profile-me.csv.profile.json");
        File.Exists(cachePath).Should().BeTrue("the second look should not re-read the file");

        // Editing outside the app must not leave a profile describing what the file used to hold.
        var path = _store.PathFor(dataset.Id)!;
        await File.WriteAllTextAsync(path, "x,y\n1,10\n2,20\n3,30\n4,999\n");
        File.SetLastWriteTimeUtc(path, DateTime.UtcNow.AddSeconds(5));

        var second = await _store.ProfileAsync(dataset.Id);
        second!.TotalRows.Should().Be(4);
        second.Columns.Single(c => c.Name == "y").Max.Should().Be(999);
    }

    [Fact]
    public async Task The_row_estimate_lands_within_ten_percent_on_a_uniform_file()
    {
        // It exists to fill in a caption without reading the file; it is labelled approximate wherever
        // it is shown, and the profile gives the true count to anyone who asks for detail.
        var csv = new StringBuilder("well,pressure,failed\n");
        for (var i = 0; i < 5_000; i++)
        {
            csv.Append($"BG-{i:D4},{100 + (i % 400)},{i % 2}\n");
        }

        var dataset = await _store.ImportAsync(Bytes(csv.ToString()), "many.csv");

        var estimate = await _store.EstimateRowsAsync(dataset.Id);

        estimate.Should().BeInRange(4_500, 5_500);
    }

    [Fact]
    public async Task An_empty_file_estimates_zero_rather_than_dividing_by_nothing()
    {
        var dataset = await _store.ImportAsync(Bytes("a,b\n"), "header-only.csv");

        (await _store.EstimateRowsAsync(dataset.Id)).Should().Be(0);
    }

    [Fact]
    public async Task Deleting_a_dataset_takes_its_cached_profile_with_it()
    {
        var dataset = await _store.ImportAsync(Bytes("x\n1\n"), "temporary.csv");
        await _store.ProfileAsync(dataset.Id);

        _store.Delete(dataset.Id).Should().BeTrue();

        Directory.EnumerateFiles(_workspace.DatasetsPath, "*.profile.json").Should().BeEmpty();
    }

    public void Dispose()
    {
        try { Directory.Delete(_root, recursive: true); } catch (IOException) { /* best effort */ }
    }
}
