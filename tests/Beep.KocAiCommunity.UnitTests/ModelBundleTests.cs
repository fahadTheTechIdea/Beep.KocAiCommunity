using System.IO.Compression;
using System.Text.Json;
using Beep.KocAiCommunity.Desktop.Local;
using FluentAssertions;
using Xunit;

namespace Beep.KocAiCommunity.UnitTests;

/// <summary>
/// The <c>.kocmodel</c> file — how a model gets from one machine to another.
/// <para>
/// The refusals matter more than the happy path. A bundle this host cannot honestly load has to be
/// turned away with a reason: loading it anyway produces a crash a long way from its cause, which is
/// the failure this check exists to prevent.
/// </para>
/// </summary>
[Collection(MlTrainingCollection.Name)]
public sealed class ModelBundleTests(TrainedModelFixture trained) : IAsyncLifetime
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "koc-bundle-" + Guid.NewGuid().ToString("N"));
    private readonly byte[] _model = trained.ModelBytes;
    private LocalModelStore _store = default!;
    private LocalModelVersion _kept = default!;

    public async Task InitializeAsync()
    {
        Directory.CreateDirectory(_root);
        var workspace = new LocalWorkspace { RootPath = _root };
        workspace.EnsureCreated();
        _store = new LocalModelStore(workspace);

        _kept = await _store.RegisterAsync("ESP failure", _model, new LocalModelVersion
        {
            Id = Guid.Empty,
            Name = "ESP failure",
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
        });
    }

    [Fact]
    public async Task A_bundle_round_trips_with_its_lineage()
    {
        var path = Path.Combine(_root, ModelBundle.SuggestedFileName(_kept));

        await ModelBundle.WriteAsync(path, _kept, _model);
        var read = await ModelBundle.ReadAsync(path);

        read.Accepted.Should().BeTrue(read.Refusal);
        read.ModelBytes.Should().Equal(_model);
        read.Manifest!.SourceRunId.Should().Be("20260802-143307-abc123");
        read.Manifest.PrimaryValue.Should().Be(0.93);
    }

    [Fact]
    public async Task An_imported_bundle_becomes_a_local_version_and_keeps_where_it_came_from()
    {
        var path = Path.Combine(_root, "shared.kocmodel");
        await ModelBundle.WriteAsync(path, _kept, _model);
        var read = await ModelBundle.ReadAsync(path);

        var adopted = await _store.AdoptAsync(read.Manifest!, read.ModelBytes!);

        adopted.Version.Should().Be(2, "the local registry numbers it, not the sender");
        adopted.Id.Should().NotBe(_kept.Id);
        adopted.SourceRunId.Should().Be("20260802-143307-abc123", "erasing the lineage would lose where it came from");
        adopted.PrimaryValue.Should().Be(0.93);
        (await _store.ReadModelAsync(adopted.Id)).Should().Equal(_model);
    }

    [Fact]
    public async Task A_bundle_from_a_newer_runtime_is_refused_by_name()
    {
        var path = Path.Combine(_root, "from-the-future.kocmodel");
        await ModelBundle.WriteAsync(path, _kept with { MlNetVersion = "99.0.0.0" }, _model);

        var read = await ModelBundle.ReadAsync(path);

        read.Accepted.Should().BeFalse();
        read.Refusal.Should().Contain("99.0.0.0").And.Contain("newer");
    }

    [Fact]
    public async Task An_older_bundle_is_accepted()
    {
        // Only newer is refused. Refusing older too would strand every model anyone made last month.
        var path = Path.Combine(_root, "older.kocmodel");
        await ModelBundle.WriteAsync(path, _kept with { MlNetVersion = "1.0.0.0" }, _model);

        (await ModelBundle.ReadAsync(path)).Accepted.Should().BeTrue();
    }

    [Fact]
    public async Task A_zip_that_is_not_a_model_bundle_is_refused()
    {
        var path = Path.Combine(_root, "holiday-photos.kocmodel");
        await using (var file = File.Create(path))
        using (var archive = new ZipArchive(file, ZipArchiveMode.Create))
        {
            archive.CreateEntry("beach.jpg");
        }

        var read = await ModelBundle.ReadAsync(path);

        read.Accepted.Should().BeFalse();
        read.Refusal.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task A_file_that_is_not_a_zip_at_all_is_refused_without_throwing()
    {
        var path = Path.Combine(_root, "notes.kocmodel");
        await File.WriteAllTextAsync(path, "this is just some text");

        var read = await ModelBundle.ReadAsync(path);

        read.Accepted.Should().BeFalse();
        read.Refusal.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task A_bundle_with_an_unparseable_version_is_still_accepted()
    {
        // Refusing on a malformed field would block a model that may well load fine.
        var path = Path.Combine(_root, "odd-version.kocmodel");
        await ModelBundle.WriteAsync(path, _kept with { MlNetVersion = "preview-3" }, _model);

        (await ModelBundle.ReadAsync(path)).Accepted.Should().BeTrue();
    }

    [Fact]
    public async Task The_manifest_is_readable_json_rather_than_something_only_we_can_open()
    {
        // Somebody will want to know what a colleague sent them without installing anything.
        var path = Path.Combine(_root, "readable.kocmodel");
        await ModelBundle.WriteAsync(path, _kept, _model);

        using var archive = ZipFile.OpenRead(path);
        await using var entry = archive.GetEntry("model.json")!.Open();
        using var reader = new StreamReader(entry);

        JsonDocument.Parse(await reader.ReadToEndAsync())
            .RootElement.GetProperty("name").GetString().Should().Be("ESP-failure");
    }

    public Task DisposeAsync()
    {
        try { Directory.Delete(_root, recursive: true); } catch (IOException) { /* best effort */ }
        return Task.CompletedTask;
    }
}
