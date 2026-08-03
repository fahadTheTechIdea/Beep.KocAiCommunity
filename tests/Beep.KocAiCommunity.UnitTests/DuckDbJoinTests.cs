using System.Text;
using Beep.KocAiCommunity.Application.ML;
using Beep.KocAiCommunity.Contracts.Workflow;
using Beep.KocAiCommunity.ML.Nodes;
using FluentAssertions;
using Xunit;

namespace Beep.KocAiCommunity.UnitTests;

/// <summary>Merging a second dataset into a workflow — join columns and append rows via DuckDB.</summary>
// Serialised with every other AutoML test. Training gets a fixed wall-clock budget, so when
// several of these run at once they starve each other of cores, complete fewer trials, and fail
// on a worse model than the same test produces alone — which reads as flakiness rather than as
// contention. Slower in total, and honest.
[Collection(MlTrainingCollection.Name)]
public class DuckDbJoinTests
{
    private static PluginNodeExecutor NewExecutor()
    {
        var handlers = typeof(PluginNodeExecutor).Assembly.GetTypes()
            .Where(t => t is { IsAbstract: false, IsInterface: false } && typeof(IPipelineNodeHandler).IsAssignableFrom(t))
            .Select(t => (IPipelineNodeHandler)Activator.CreateInstance(t)!);
        return new PluginNodeExecutor(new PluginNodeRegistry(handlers));
    }

    private static MemoryStream Csv(string text) => new(Encoding.UTF8.GetBytes(text));

    [Fact]
    public async Task Join_dataset_brings_in_columns_from_a_second_dataset()
    {
        var second = Guid.NewGuid();
        // Primary: a key + one weak feature + label. The joined column 'signal' is what makes it separable.
        var primary = new StringBuilder("key,x1,label\n");
        var secondary = new StringBuilder("key,signal\n");
        for (var i = 0; i < 60; i++)
        {
            primary.Append($"k{i},{i % 2},{(i % 2 == 0 ? "true" : "false")}\n");
            secondary.Append($"k{i},{(i % 2 == 0 ? 9 : 0)}\n");
        }

        var def = new WorkflowDefinition
        {
            Name = "join",
            Nodes =
            [
                new() { Id = "d", Kind = "dataset" },
                new() { Id = "j", Kind = "join-dataset", Config = new Dictionary<string, string> { ["datasetId"] = second.ToString(), ["on"] = "key" } },
                new() { Id = "drop", Kind = "drop-columns", Config = new Dictionary<string, string> { ["columns"] = "key" } },
                new() { Id = "sp", Kind = "split" },
                new() { Id = "tr", Kind = "train" },
                new() { Id = "ev", Kind = "evaluate" },
            ],
            Edges = [new("d", "j"), new("j", "drop"), new("drop", "sp"), new("sp", "tr"), new("tr", "ev")],
        };

        var secondaryMap = new Dictionary<Guid, Stream> { [second] = Csv(secondary.ToString()) };
        var result = await NewExecutor().ExecuteAsync(def, "label", MlTaskType.BinaryClassification, Csv(primary.ToString()), 5, secondaryMap);

        var failed = result.Nodes.FirstOrDefault(n => n.Status is not "done" and not "skipped");
        result.Success.Should().BeTrue($"but node '{failed?.Kind}' {failed?.Status}: {failed?.Detail}");
        result.Nodes.Single(n => n.Kind == "join-dataset").Detail.Should().Contain("signal");
        // The joined 'signal' column makes the data separable → strong accuracy.
        result.PrimaryValue.Should().BeGreaterThan(0.9);
    }

    [Fact]
    public async Task Join_with_an_unresolvable_dataset_fails_loudly()
    {
        // The referenced dataset id isn't attached to the run. The join must fail the run rather than
        // silently skip and train on the primary alone (a wrong, quietly-degraded result).
        var def = new WorkflowDefinition
        {
            Name = "missing-join",
            Nodes =
            [
                new() { Id = "d", Kind = "dataset" },
                new() { Id = "j", Kind = "join-dataset", Config = new Dictionary<string, string> { ["datasetId"] = Guid.NewGuid().ToString(), ["on"] = "key" } },
                new() { Id = "sp", Kind = "split" },
                new() { Id = "tr", Kind = "train" },
            ],
            Edges = [new("d", "j"), new("j", "sp"), new("sp", "tr")],
        };

        var primary = "key,x1,label\nk0,0,false\nk1,1,true\n";
        var result = await NewExecutor().ExecuteAsync(def, "label", MlTaskType.BinaryClassification, Csv(primary), 5);

        result.Success.Should().BeFalse();
        result.Nodes.Single(n => n.Kind == "join-dataset").Status.Should().Be("failed");
    }

    [Fact]
    public async Task A_fan_out_join_that_duplicates_evaluation_ids_fails_loudly_at_predict()
    {
        // The merged dataset has DUPLICATE keys, so a left join multiplies rows — every evaluation id then
        // appears more than once in the submission. Row-count alignment still holds, so that guard passes;
        // this must be caught separately, because the scorers align by id and would keep only the last
        // prediction per id (a silently wrong score). The run must fail with a clear message instead.
        var second = Guid.NewGuid();
        var train = new StringBuilder("id,key,x1,label\n");
        for (var i = 0; i < 60; i++)
        {
            train.Append($"tr{i},{(i % 2 == 0 ? "k0" : "k1")},{i % 2},{(i % 2 == 0 ? "true" : "false")}\n");
        }

        // Two rows per key → any joined row fans out 2x.
        var secondary = "key,signal\nk0,9\nk0,8\nk1,0\nk1,1\n";
        var eval = "id,key,x1\ne1,k0,0\ne2,k1,0\n";

        var def = new WorkflowDefinition
        {
            Name = "fan-out-join",
            Nodes =
            [
                new() { Id = "d", Kind = "dataset" },
                new() { Id = "j", Kind = "join-dataset", Config = new Dictionary<string, string> { ["datasetId"] = second.ToString(), ["on"] = "key" } },
                new() { Id = "drop", Kind = "drop-columns", Config = new Dictionary<string, string> { ["columns"] = "key" } },
                new() { Id = "sp", Kind = "split" },
                new() { Id = "tr", Kind = "train" },
                new() { Id = "ev", Kind = "evaluate" },
            ],
            Edges = [new("d", "j"), new("j", "drop"), new("drop", "sp"), new("sp", "tr"), new("tr", "ev")],
        };

        var secondaryMap = new Dictionary<Guid, Stream> { [second] = Csv(secondary) };

        await FluentActions
            .Awaiting(() => NewExecutor().PredictAsync(def, "label", "id", MlTaskType.BinaryClassification, Csv(train.ToString()), Csv(eval), secondaryMap))
            .Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*duplicate ids*");
    }

    [Fact]
    public async Task Union_dataset_appends_rows_from_a_second_dataset()
    {
        var second = Guid.NewGuid();
        var primary = "x1,x2,label\n9,9,true\n0,0,false\n8,8,true\n1,1,false\n";
        var extra = new StringBuilder("x1,x2,label\n");
        for (var i = 0; i < 40; i++)
        {
            extra.Append($"{7 + (i % 3)},{7 + (i % 3)},true\n{i % 3},{i % 3},false\n");
        }

        var def = new WorkflowDefinition
        {
            Name = "union",
            Nodes =
            [
                new() { Id = "d", Kind = "dataset" },
                new() { Id = "u", Kind = "union-dataset", Config = new Dictionary<string, string> { ["datasetId"] = second.ToString() } },
                new() { Id = "sp", Kind = "split" },
                new() { Id = "tr", Kind = "train" },
                new() { Id = "ev", Kind = "evaluate" },
            ],
            Edges = [new("d", "u"), new("u", "sp"), new("sp", "tr"), new("tr", "ev")],
        };

        var secondaryMap = new Dictionary<Guid, Stream> { [second] = Csv(extra.ToString()) };
        var result = await NewExecutor().ExecuteAsync(def, "label", MlTaskType.BinaryClassification, Csv(primary), 5, secondaryMap);

        var failed = result.Nodes.FirstOrDefault(n => n.Status is not "done" and not "skipped");
        result.Success.Should().BeTrue($"but node '{failed?.Kind}' {failed?.Status}: {failed?.Detail}");
        // 4 primary rows + 80 appended (40 × 2) = 84.
        result.Nodes.Single(n => n.Kind == "union-dataset").Detail.Should().Contain("84 rows");
    }

    [Fact]
    public async Task Union_with_a_dataset_missing_the_label_fails_loudly()
    {
        // The appended dataset has no label column, so UNION ALL BY NAME would give its rows a null label
        // and train them as a phantom class. Must fail loudly rather than silently pollute training.
        var second = Guid.NewGuid();
        const string primary = "x1,x2,label\n9,9,true\n0,0,false\n8,8,true\n1,1,false\n";
        const string extra = "x1,x2\n7,7\n2,2\n"; // no label column

        var def = new WorkflowDefinition
        {
            Name = "union-no-label",
            Nodes =
            [
                new() { Id = "d", Kind = "dataset" },
                new() { Id = "u", Kind = "union-dataset", Config = new Dictionary<string, string> { ["datasetId"] = second.ToString() } },
                new() { Id = "sp", Kind = "split" },
                new() { Id = "tr", Kind = "train" },
            ],
            Edges = [new("d", "u"), new("u", "sp"), new("sp", "tr")],
        };

        var secondaryMap = new Dictionary<Guid, Stream> { [second] = Csv(extra) };
        var result = await NewExecutor().ExecuteAsync(def, "label", MlTaskType.BinaryClassification, Csv(primary), 5, secondaryMap);

        result.Success.Should().BeFalse();
        result.Nodes.Single(n => n.Kind == "union-dataset").Status.Should().Be("failed");
    }
}
