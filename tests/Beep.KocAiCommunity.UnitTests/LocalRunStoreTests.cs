using System.Text;
using Beep.KocAiCommunity.Desktop.Local;
using FluentAssertions;
using Xunit;

namespace Beep.KocAiCommunity.UnitTests;

/// <summary>
/// Run history as files in the workspace.
/// <para>
/// The property worth pinning is the honest one: a run records the dataset's content hash, so history
/// can say "this is no longer reproducible" instead of quietly implying it still is.
/// </para>
/// </summary>
public sealed class LocalRunStoreTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "koc-runs-" + Guid.NewGuid().ToString("N"));
    private readonly LocalRunStore _store;

    public LocalRunStoreTests()
    {
        var workspace = new LocalWorkspace { RootPath = _root };
        workspace.EnsureCreated();
        _store = new LocalRunStore(workspace);
    }

    private static LocalRun Run(string id, LocalRunOutcome outcome = LocalRunOutcome.Completed) => new()
    {
        Id = id,
        StartedUtc = new DateTime(2026, 8, 2, 14, 33, 7, DateTimeKind.Utc),
        DatasetId = Guid.Empty.ToString(),
        DatasetName = "esp-readings.csv",
        Task = "BinaryClassification",
        TargetColumn = "failed",
        Outcome = outcome,
        PrimaryMetric = "Accuracy",
        PrimaryValue = 0.91,
    };

    [Fact]
    public async Task A_saved_run_reads_back_with_its_metrics()
    {
        await _store.SaveAsync(Run("20260802-143307-abc123"), model: [1, 2, 3], trialLog: "trial 1");

        var loaded = _store.Get("20260802-143307-abc123");

        loaded.Should().NotBeNull();
        loaded!.PrimaryValue.Should().Be(0.91);
        loaded.TargetColumn.Should().Be("failed");
        loaded.HasModel.Should().BeTrue();
        _store.ReadTrialLog(loaded.Id).Should().Be("trial 1");
    }

    [Fact]
    public async Task A_run_that_produced_no_model_says_so()
    {
        // A stopped run is still worth keeping — it is the record of an attempt that did not work.
        await _store.SaveAsync(Run("20260802-150000-def456", LocalRunOutcome.Stopped), model: null, trialLog: null);

        var loaded = _store.Get("20260802-150000-def456");

        loaded!.HasModel.Should().BeFalse();
        loaded.Outcome.Should().Be(LocalRunOutcome.Stopped);
    }

    [Fact]
    public async Task Runs_list_newest_first()
    {
        await _store.SaveAsync(Run("20260801-090000-aaaaaa"), null, null);
        await _store.SaveAsync(Run("20260802-090000-bbbbbb"), null, null);
        await _store.SaveAsync(Run("20260803-090000-cccccc"), null, null);

        _store.List().Select(r => r.Id).Should()
            .ContainInOrder("20260803-090000-cccccc", "20260802-090000-bbbbbb", "20260801-090000-aaaaaa");
    }

    [Fact]
    public async Task One_unreadable_run_does_not_hide_the_others()
    {
        // Truncated JSON — a crash mid-write, or a file someone opened and saved. The rest of the
        // history must still be readable, because that is when someone most wants to look at it.
        await _store.SaveAsync(Run("20260802-090000-good11"), null, null);
        var broken = Path.Combine(_root, "runs", "20260802-100000-bad222");
        Directory.CreateDirectory(broken);
        await File.WriteAllTextAsync(Path.Combine(broken, "run.json"), "{ not json", Encoding.UTF8);

        var runs = _store.List();

        runs.Should().ContainSingle().Which.Id.Should().Be("20260802-090000-good11");
    }

    [Fact]
    public async Task Pruning_a_model_keeps_the_record()
    {
        // Models are almost all of the disk cost; the metrics are what someone comes back to read.
        await _store.SaveAsync(Run("20260802-090000-prune1"), model: [9, 9, 9], trialLog: null);

        _store.PruneModel("20260802-090000-prune1").Should().BeTrue();

        _store.Get("20260802-090000-prune1").Should().NotBeNull().And
            .Match<LocalRun>(r => !r.HasModel && r.PrimaryValue == 0.91);
        (await _store.ReadModelAsync("20260802-090000-prune1")).Should().BeNull();
    }

    [Fact]
    public async Task A_dataset_edited_after_the_run_no_longer_matches_its_hash()
    {
        // This is the whole reason the hash is stored. Same file name, same id, different contents —
        // and re-running it will not give the same answer.
        var csv = Path.Combine(_root, "readings.csv");
        await File.WriteAllTextAsync(csv, "id,failed\n1,0\n");
        var before = await LocalRunStore.HashFileAsync(csv);

        await File.WriteAllTextAsync(csv, "id,failed\n1,0\n2,1\n");
        var after = await LocalRunStore.HashFileAsync(csv);

        before.Should().NotBeNullOrEmpty();
        after.Should().NotBe(before);
    }

    [Fact]
    public async Task An_unchanged_dataset_hashes_the_same_twice()
    {
        var csv = Path.Combine(_root, "stable.csv");
        await File.WriteAllTextAsync(csv, "id,failed\n1,0\n");

        (await LocalRunStore.HashFileAsync(csv)).Should().Be(await LocalRunStore.HashFileAsync(csv));
    }

    [Fact]
    public async Task A_missing_dataset_hashes_to_nothing_rather_than_a_wrong_value()
    {
        // Absent has to be distinguishable from changed, or the history warns about the wrong thing.
        (await LocalRunStore.HashFileAsync(Path.Combine(_root, "gone.csv"))).Should().BeNull();
        (await LocalRunStore.HashFileAsync(null)).Should().BeNull();
    }

    [Fact]
    public void Run_ids_sort_chronologically()
    {
        // The list ordering is the folder name. If ids stopped sorting, history would silently shuffle.
        var earlier = LocalRunStore.NewId(new DateTime(2026, 8, 2, 9, 0, 0, DateTimeKind.Utc));
        var later = LocalRunStore.NewId(new DateTime(2026, 8, 2, 14, 0, 0, DateTimeKind.Utc));

        string.CompareOrdinal(earlier, later).Should().BeNegative();
    }

    [Fact]
    public async Task Deleting_a_run_removes_it_from_the_history()
    {
        await _store.SaveAsync(Run("20260802-090000-gone11"), model: [1], trialLog: "x");

        _store.Delete("20260802-090000-gone11").Should().BeTrue();

        _store.Get("20260802-090000-gone11").Should().BeNull();
        _store.Delete("20260802-090000-gone11").Should().BeFalse("it is already gone");
    }

    public void Dispose()
    {
        try { Directory.Delete(_root, recursive: true); } catch (IOException) { /* best effort */ }
    }
}
