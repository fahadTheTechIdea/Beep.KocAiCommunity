using Beep.KocAiCommunity.Desktop.Local;
using FluentAssertions;
using Xunit;

namespace Beep.KocAiCommunity.UnitTests;

/// <summary>
/// The local registry: models an engineer decided to keep.
/// <para>
/// It is deliberately not the run history. Registering copies the file, so clearing runs cannot gut the
/// registry — and versions are never reused, so a deleted v2 cannot be silently overwritten by the next
/// save under the same name.
/// </para>
/// <para>
/// These use the collection's real trained model, because the manifest's input list is read out of the
/// model's own schema rather than from whatever the caller believed. A fake byte array would not
/// exercise that.
/// </para>
/// </summary>
[Collection(MlTrainingCollection.Name)]
public sealed class LocalModelStoreTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "koc-models-" + Guid.NewGuid().ToString("N"));
    private readonly LocalModelStore _store;
    private readonly byte[] _model;

    public LocalModelStoreTests(TrainedModelFixture trained)
    {
        var workspace = new LocalWorkspace { RootPath = _root };
        workspace.EnsureCreated();
        _store = new LocalModelStore(workspace);
        _model = trained.ModelBytes;
    }

    private LocalModelVersion Details(string name) => new()
    {
        Id = Guid.Empty,
        Name = name,
        Version = 0,
        CreatedUtc = default,
        MlNetVersion = "",
        Task = "BinaryClassification",
        TargetColumn = "label",
        Algorithm = "LightGbm",
        PrimaryMetric = "Accuracy",
        PrimaryValue = 0.93,
        SourceRunId = "20260802-143307-abc123",
        DatasetName = "esp-readings.csv",
        DatasetHash = "ABCD1234ABCD1234",
    };

    [Fact]
    public async Task A_kept_model_reads_back_with_its_lineage()
    {
        var kept = await _store.RegisterAsync("ESP failure", _model, Details("ESP failure"));

        kept.Version.Should().Be(1);
        kept.Id.Should().NotBe(Guid.Empty);

        var loaded = _store.Get(kept.Id);
        loaded.Should().NotBeNull();
        loaded!.SourceRunId.Should().Be("20260802-143307-abc123");
        loaded.DatasetHash.Should().Be("ABCD1234ABCD1234");
        loaded.PrimaryValue.Should().Be(0.93);
        loaded.MlNetVersion.Should().NotBeNullOrEmpty("the runtime that wrote it decides what can read it");
    }

    [Fact]
    public async Task The_input_list_comes_from_the_model_not_from_the_caller()
    {
        // Recording features at training time and trusting them later drifts the moment anything about
        // featurization changes. The saved model already knows.
        var kept = await _store.RegisterAsync("ESP failure", _model, Details("ESP failure"));

        kept.Inputs.Select(c => c.Name).Should().Contain(["x1", "x2", "label"]);
        kept.Features.Select(f => f.Name).Should().BeEquivalentTo(["x1", "x2"], "the label is not an input to ask for");
        kept.Inputs.Single(c => c.Name == "x1").IsNumeric.Should().BeTrue();
    }

    [Fact]
    public async Task Keeping_the_same_name_twice_makes_v2_and_v1_still_reads()
    {
        var first = await _store.RegisterAsync("ESP failure", _model, Details("ESP failure"));
        var second = await _store.RegisterAsync("ESP failure", _model, Details("ESP failure"));

        second.Version.Should().Be(2);
        _store.Get(first.Id).Should().NotBeNull("v1 is still there");
        (await _store.ReadModelAsync(first.Id)).Should().NotBeNull();
        _store.Latest("ESP-failure")!.Version.Should().Be(2);
    }

    [Fact]
    public async Task A_deleted_version_number_is_never_reused()
    {
        // Reusing it would mean a new model quietly answering to a number someone already wrote down.
        await _store.RegisterAsync("ESP failure", _model, Details("ESP failure"));
        var second = await _store.RegisterAsync("ESP failure", _model, Details("ESP failure"));

        _store.Delete(second.Id).Should().BeTrue();

        var third = await _store.RegisterAsync("ESP failure", _model, Details("ESP failure"));
        third.Version.Should().Be(3);
    }

    [Fact]
    public async Task A_name_cannot_write_outside_the_workspace()
    {
        // Model names are user input and go straight into a path.
        var kept = await _store.RegisterAsync("../../windows/system32", _model, Details("x"));

        kept.Name.Should().NotContain("..").And.NotContain("/").And.NotContain("\\");
        Directory.Exists(Path.Combine(_store.FolderPath, kept.Name)).Should().BeTrue();
    }

    [Fact]
    public async Task An_empty_name_still_lands_somewhere_sensible()
    {
        var kept = await _store.RegisterAsync("...", _model, Details("x"));

        kept.Name.Should().Be("model");
    }

    [Fact]
    public async Task Deleting_the_last_version_leaves_no_empty_entry_behind()
    {
        var kept = await _store.RegisterAsync("ESP failure", _model, Details("ESP failure"));

        _store.Delete(kept.Id).Should().BeTrue();

        _store.List().Should().BeEmpty();
        Directory.Exists(Path.Combine(_store.FolderPath, "ESP-failure")).Should().BeFalse();
    }

    [Fact]
    public async Task A_kept_model_survives_its_source_run_being_deleted()
    {
        // The reason registering copies rather than references. Clearing run history must not gut the
        // registry.
        var workspace = new LocalWorkspace { RootPath = _root };
        var runs = new LocalRunStore(workspace);
        await runs.SaveAsync(new LocalRun
        {
            Id = "20260802-143307-abc123",
            StartedUtc = DateTime.UtcNow,
            DatasetId = Guid.Empty.ToString(),
            DatasetName = "esp-readings.csv",
            Task = "BinaryClassification",
            TargetColumn = "label",
        }, _model, "log");

        var kept = await _store.RegisterAsync("ESP failure", _model, Details("ESP failure"));
        runs.Delete("20260802-143307-abc123").Should().BeTrue();

        (await _store.ReadModelAsync(kept.Id)).Should().NotBeNull();
        _store.Get(kept.Id)!.SourceRunId.Should().Be("20260802-143307-abc123",
            "the lineage is still recorded even though the run is gone");
    }

    [Fact]
    public async Task Registering_nothing_is_refused()
    {
        await FluentActions.Awaiting(() => _store.RegisterAsync("ESP", [], Details("ESP")))
            .Should().ThrowAsync<ArgumentException>();

        await FluentActions.Awaiting(() => _store.RegisterAsync("  ", _model, Details("ESP")))
            .Should().ThrowAsync<ArgumentException>();
    }

    public void Dispose()
    {
        try { Directory.Delete(_root, recursive: true); } catch (IOException) { /* best effort */ }
    }
}
