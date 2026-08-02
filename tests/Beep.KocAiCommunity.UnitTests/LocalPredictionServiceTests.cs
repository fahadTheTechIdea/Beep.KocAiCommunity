using Beep.KocAiCommunity.Application.ML;
using Beep.KocAiCommunity.Desktop.Local;
using Beep.KocAiCommunity.ML;
using FluentAssertions;
using Xunit;

namespace Beep.KocAiCommunity.UnitTests;

/// <summary>
/// Predicting against a kept model.
/// <para>
/// The interesting case is the ordinary one: somebody points a CSV at a model and it is missing a
/// column. That has to come back naming the column, not as a framework exception several layers down.
/// </para>
/// </summary>
[Collection(MlTrainingCollection.Name)]
public sealed class LocalPredictionServiceTests(TrainedModelFixture trained) : IAsyncLifetime
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "koc-predict-" + Guid.NewGuid().ToString("N"));
    private LocalModelStore _store = default!;
    private LocalPredictionService _predictions = default!;
    private CountingPool _pool = default!;
    private LocalModelVersion _model = default!;

    /// <summary>Wraps the real pool so the caching claim can be checked rather than assumed.</summary>
    private sealed class CountingPool(IPredictionPool inner) : IPredictionPool
    {
        public int Loads { get; private set; }

        public Task<InferenceResult> PredictAsync(
            Guid modelVersionId, Func<CancellationToken, Task<byte[]>> modelLoader, string labelColumn,
            IReadOnlyList<IReadOnlyDictionary<string, string>> rows, CancellationToken ct = default) =>
            inner.PredictAsync(modelVersionId, c => { Loads++; return modelLoader(c); }, labelColumn, rows, ct);

        public void Evict(Guid modelVersionId) => inner.Evict(modelVersionId);
    }

    public async Task InitializeAsync()
    {
        var workspace = new LocalWorkspace { RootPath = _root };
        workspace.EnsureCreated();
        _store = new LocalModelStore(workspace);
        _pool = new CountingPool(new AutoMlPredictionPool());
        _predictions = new LocalPredictionService(_store, _pool);

        _model = await _store.RegisterAsync("ESP failure", trained.ModelBytes, new LocalModelVersion
        {
            Id = Guid.Empty,
            Name = "ESP failure",
            Version = 0,
            CreatedUtc = default,
            MlNetVersion = "",
            Task = "BinaryClassification",
            TargetColumn = TrainedModelFixture.TargetColumn,
        });
    }

    [Fact]
    public async Task A_single_row_gets_a_prediction()
    {
        var prediction = await _predictions.PredictAsync(
            _model, new Dictionary<string, string> { ["x1"] = "8", ["x2"] = "8" });

        prediction.Should().NotBeNull();
        LocalPredictionService.Describe(prediction!).Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task A_file_is_scored_into_a_new_file_beside_it()
    {
        var input = Path.Combine(_root, "to-score.csv");
        await File.WriteAllTextAsync(input, "x1,x2\n8,8\n1,1\n2,0\n");

        var result = await _predictions.PredictFileAsync(_model, input);

        result.Succeeded.Should().BeTrue();
        result.RowCount.Should().Be(3);
        result.OutputPath.Should().Be(Path.Combine(_root, "to-score-predictions.csv"));

        var written = await File.ReadAllLinesAsync(result.OutputPath!);
        written[0].Should().Be("x1,x2,prediction");
        written.Should().HaveCount(4, "a header and one line per input row");
        written.Skip(1).Should().OnlyContain(line => line.Split(',').Length == 3);
    }

    [Fact]
    public async Task The_input_file_is_left_alone()
    {
        // Overwriting somebody's data with a scored copy would be its own incident.
        var input = Path.Combine(_root, "precious.csv");
        const string original = "x1,x2\n8,8\n";
        await File.WriteAllTextAsync(input, original);

        await _predictions.PredictFileAsync(_model, input);

        (await File.ReadAllTextAsync(input)).Should().Be(original);
    }

    [Fact]
    public async Task A_file_missing_a_feature_names_the_column()
    {
        var input = Path.Combine(_root, "incomplete.csv");
        await File.WriteAllTextAsync(input, "x1,pressure\n8,120\n");

        var result = await _predictions.PredictFileAsync(_model, input);

        result.Succeeded.Should().BeFalse();
        result.MissingColumns.Should().BeEquivalentTo(["x2"]);
        File.Exists(Path.Combine(_root, "incomplete-predictions.csv")).Should().BeFalse("nothing was scored");
    }

    [Fact]
    public async Task Extra_columns_are_no_obstacle()
    {
        // Real files carry ids, timestamps and notes. Only the missing ones matter.
        var input = Path.Combine(_root, "extra.csv");
        await File.WriteAllTextAsync(input, "well,x1,x2,notes\nBG-114,8,8,routine\n");

        var result = await _predictions.PredictFileAsync(_model, input);

        result.Succeeded.Should().BeTrue();
        (await File.ReadAllLinesAsync(result.OutputPath!))[0].Should().Be("well,x1,x2,notes,prediction");
    }

    [Fact]
    public async Task A_header_only_file_scores_nothing_rather_than_writing_an_empty_result()
    {
        var input = Path.Combine(_root, "empty.csv");
        await File.WriteAllTextAsync(input, "x1,x2\n");

        var result = await _predictions.PredictFileAsync(_model, input);

        result.Succeeded.Should().BeFalse();
        result.MissingColumns.Should().BeEmpty("nothing was wrong with it — there was just nothing in it");
    }

    [Fact]
    public async Task The_model_is_loaded_once_and_then_cached()
    {
        // Loading per prediction would be slow and pointless — the whole reason the pool exists.
        await _predictions.PredictAsync(_model, new Dictionary<string, string> { ["x1"] = "8", ["x2"] = "8" });
        await _predictions.PredictAsync(_model, new Dictionary<string, string> { ["x1"] = "1", ["x2"] = "1" });
        await _predictions.PredictAsync(_model, new Dictionary<string, string> { ["x1"] = "2", ["x2"] = "0" });

        _pool.Loads.Should().Be(1);
    }

    [Fact]
    public async Task Predicting_against_a_deleted_model_says_which_one_is_missing()
    {
        _store.Delete(_model.Id).Should().BeTrue();
        _predictions.Forget(_model.Id);

        var act = () => _predictions.PredictAsync(_model, new Dictionary<string, string> { ["x1"] = "8", ["x2"] = "8" });

        (await act.Should().ThrowAsync<InvalidOperationException>())
            .And.Message.Should().Contain(_model.Name);
    }

    public Task DisposeAsync()
    {
        try { Directory.Delete(_root, recursive: true); } catch (IOException) { /* best effort */ }
        return Task.CompletedTask;
    }
}
