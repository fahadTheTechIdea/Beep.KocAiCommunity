using System.Text;
using Beep.KocAiCommunity.Application.ML;
using Beep.KocAiCommunity.Contracts.Workflow;
using Beep.KocAiCommunity.ML;
using Beep.KocAiCommunity.ML.Nodes;
using FluentAssertions;
using Xunit;

namespace Beep.KocAiCommunity.UnitTests;

public class MlPipelineExecutorTests
{
    // Builds the plug-and-play executor with every registered handler (mirrors DI) so these tests are
    // the parity oracle for the migrated node engine.
    private static PluginNodeExecutor NewExecutor()
    {
        var handlers = typeof(PluginNodeExecutor).Assembly.GetTypes()
            .Where(t => t is { IsAbstract: false, IsInterface: false } && typeof(IPipelineNodeHandler).IsAssignableFrom(t))
            .Select(t => (IPipelineNodeHandler)Activator.CreateInstance(t)!);
        return new PluginNodeExecutor(new PluginNodeRegistry(handlers));
    }

    [Fact]
    public async Task Dropping_the_fold_marker_after_split_is_rejected_not_silently_leaked()
    {
        // split creates the fold marker; a SQL node then projects only features+label, dropping it.
        // Training must fail loudly (data leakage) rather than silently train == test.
        var def = new WorkflowDefinition
        {
            Name = "leaky",
            Nodes =
            [
                new() { Id = "d", Kind = "dataset" },
                new() { Id = "sp", Kind = "split" },
                new() { Id = "q", Kind = "sql", Config = new Dictionary<string, string> { ["sql"] = "SELECT x1, x2, label FROM working" } },
                new() { Id = "tr", Kind = "train" },
            ],
            Edges = [new("d", "sp"), new("sp", "q"), new("q", "tr")],
        };

        var sb = new StringBuilder("x1,x2,label\n");
        for (var i = 0; i < 40; i++)
        {
            sb.Append($"{7 + (i % 3)},{7 + ((i / 3) % 3)},true\n");
            sb.Append($"{i % 3},{(i / 3) % 3},false\n");
        }
        using var csv = new MemoryStream(Encoding.UTF8.GetBytes(sb.ToString()));

        var result = await NewExecutor().ExecuteAsync(def, "label", MlTaskType.BinaryClassification, csv, 5);

        result.Success.Should().BeFalse();
        result.Nodes.Should().Contain(n => n.Kind == "train" && n.Status != "done");
    }

    [Fact]
    public async Task Executes_each_node_and_trains_a_model()
    {
        var def = new WorkflowDefinition
        {
            Name = "pipeline",
            Nodes =
            [
                new() { Id = "d", Kind = "dataset" },
                new() { Id = "rm", Kind = "replace-missing" },
                new() { Id = "nz", Kind = "normalize" },
                new() { Id = "sp", Kind = "split" },
                new() { Id = "tr", Kind = "train" },
                new() { Id = "ev", Kind = "evaluate" },
            ],
            Edges = [new("d", "rm"), new("rm", "nz"), new("nz", "sp"), new("sp", "tr"), new("tr", "ev")],
        };

        var sb = new StringBuilder("x1,x2,label\n");
        for (var i = 0; i < 60; i++)
        {
            sb.Append($"{7 + (i % 3)},{7 + ((i / 3) % 3)},true\n");
            sb.Append($"{i % 3},{(i / 3) % 3},false\n");
        }
        using var csv = new MemoryStream(Encoding.UTF8.GetBytes(sb.ToString()));

        var result = await NewExecutor().ExecuteAsync(def, "label", MlTaskType.BinaryClassification, csv, 5);

        var failed = result.Nodes.FirstOrDefault(n => n.Status is not "done" and not "skipped");
        result.Success.Should().BeTrue($"but node '{failed?.Kind}' {failed?.Status}: {failed?.Detail}");
        result.Nodes.Should().HaveCount(6);
        result.Nodes.Should().OnlyContain(n => n.Status == "done");
        result.Nodes.Single(n => n.Kind == "train").Detail.Should().Contain("Sdca");
        result.PrimaryMetric.Should().Be("Accuracy");
        result.PrimaryValue.Should().BeGreaterThan(0.8);
    }

    [Fact]
    public async Task Runs_data_management_nodes_end_to_end()
    {
        // rename → cast → label-encode a category → combine → shuffle → take → split → train → evaluate.
        var def = new WorkflowDefinition
        {
            Name = "prep",
            Nodes =
            [
                new() { Id = "d", Kind = "dataset" },
                new() { Id = "rn", Kind = "rename-column", Config = new Dictionary<string, string> { ["from"] = "x1", ["to"] = "pressure" } },
                new() { Id = "cn", Kind = "convert-numeric", Config = new Dictionary<string, string> { ["columns"] = "pressure" } },
                new() { Id = "cmp", Kind = "compute-column", Config = new Dictionary<string, string> { ["output"] = "ratio", ["inputs"] = "pressure,x2", ["expression"] = "(p, x) => p / (x + 1)" } },
                new() { Id = "cc", Kind = "combine-columns" },
                new() { Id = "sh", Kind = "shuffle" },
                new() { Id = "tk", Kind = "take-rows", Config = new Dictionary<string, string> { ["count"] = "100" } },
                new() { Id = "sp", Kind = "split" },
                new() { Id = "tr", Kind = "train" },
                new() { Id = "ev", Kind = "evaluate" },
            ],
            Edges = [new("d", "rn"), new("rn", "cn"), new("cn", "cmp"), new("cmp", "cc"), new("cc", "sh"), new("sh", "tk"), new("tk", "sp"), new("sp", "tr"), new("tr", "ev")],
        };

        var sb = new StringBuilder("x1,x2,label\n");
        for (var i = 0; i < 60; i++)
        {
            sb.Append($"{7 + (i % 3)},{7 + ((i / 3) % 3)},true\n");
            sb.Append($"{i % 3},{(i / 3) % 3},false\n");
        }
        using var csv = new MemoryStream(Encoding.UTF8.GetBytes(sb.ToString()));

        var result = await NewExecutor().ExecuteAsync(def, "label", MlTaskType.BinaryClassification, csv, 5);

        var failed = result.Nodes.FirstOrDefault(n => n.Status is not "done" and not "skipped");
        result.Success.Should().BeTrue($"but node '{failed?.Kind}' {failed?.Status}: {failed?.Detail}");
        result.Nodes.Single(n => n.Kind == "rename-column").Status.Should().Be("done");
        result.Nodes.Single(n => n.Kind == "compute-column").Status.Should().Be("done");
        result.Nodes.Single(n => n.Kind == "combine-columns").Status.Should().Be("done");
        result.PrimaryValue.Should().BeGreaterThan(0.8);
    }

    [Fact]
    public async Task Runs_expanded_catalog_with_per_node_config()
    {
        // A richer pipeline: pick columns, sub-sample, normalize, split 30% out, train FastTree,
        // cross-validate 4-fold, score, evaluate — all driven by per-node config.
        var def = new WorkflowDefinition
        {
            Name = "expanded",
            Nodes =
            [
                new() { Id = "d", Kind = "dataset" },
                new() { Id = "sc", Kind = "select-columns", Config = new Dictionary<string, string> { ["columns"] = "x1,x2" } },
                new() { Id = "sm", Kind = "sample", Config = new Dictionary<string, string> { ["fraction"] = "0.9" } },
                new() { Id = "nz", Kind = "normalize" },
                new() { Id = "sp", Kind = "split", Config = new Dictionary<string, string> { ["testFraction"] = "0.3" } },
                new() { Id = "tr", Kind = "train", Config = new Dictionary<string, string> { ["algorithm"] = "fasttree" } },
                new() { Id = "cv", Kind = "cross-validate", Config = new Dictionary<string, string> { ["folds"] = "4" } },
                new() { Id = "sco", Kind = "score" },
                new() { Id = "ev", Kind = "evaluate" },
            ],
            Edges =
            [
                new("d", "sc"), new("sc", "sm"), new("sm", "nz"), new("nz", "sp"),
                new("sp", "tr"), new("tr", "cv"), new("cv", "sco"), new("sco", "ev"),
            ],
        };

        var sb = new StringBuilder("x1,x2,noise,label\n");
        for (var i = 0; i < 80; i++)
        {
            sb.Append($"{7 + (i % 3)},{7 + ((i / 3) % 3)},{i % 5},true\n");
            sb.Append($"{i % 3},{(i / 3) % 3},{i % 5},false\n");
        }
        using var csv = new MemoryStream(Encoding.UTF8.GetBytes(sb.ToString()));

        var result = await NewExecutor().ExecuteAsync(def, "label", MlTaskType.BinaryClassification, csv, 10);

        var failed = result.Nodes.FirstOrDefault(n => n.Status is "failed");
        result.Success.Should().BeTrue($"but node '{failed?.Kind}' failed: {failed?.Detail}");
        result.Nodes.Should().OnlyContain(n => n.Status == "done");
        result.Nodes.Single(n => n.Kind == "select-columns").Detail.Should().Contain("kept 2");
        result.Nodes.Single(n => n.Kind == "train").Detail.Should().Contain("FastTree");
        result.Nodes.Single(n => n.Kind == "cross-validate").Detail.Should().Contain("4-fold");
        result.Nodes.Single(n => n.Kind == "score").Detail.Should().Contain("scored");
        result.PrimaryValue.Should().BeGreaterThan(0.8);
    }

    [Fact]
    public async Task Predicts_evaluation_set_as_a_submission_csv()
    {
        var def = new WorkflowDefinition
        {
            Name = "submission",
            Nodes =
            [
                new() { Id = "d", Kind = "dataset" },
                new() { Id = "nz", Kind = "normalize" },
                new() { Id = "sp", Kind = "split" },
                new() { Id = "tr", Kind = "train" },
                new() { Id = "ev", Kind = "evaluate" },
            ],
            Edges = [new("d", "nz"), new("nz", "sp"), new("sp", "tr"), new("tr", "ev")],
        };

        // Training data carries an id column that must be excluded from the features.
        var train = new StringBuilder("id,x1,x2,label\n");
        for (var i = 0; i < 60; i++)
        {
            train.Append($"tr{i}a,{7 + (i % 3)},{7 + ((i / 3) % 3)},true\n");
            train.Append($"tr{i}b,{i % 3},{(i / 3) % 3},false\n");
        }
        using var trainCsv = new MemoryStream(Encoding.UTF8.GetBytes(train.ToString()));

        // Evaluation set: id + features, NO label. Rows 1 & 3 are clearly high (true), row 2 low (false).
        var eval = "id,x1,x2\ne1,9,9\ne2,0,0\ne3,8,7\n";
        using var evalCsv = new MemoryStream(Encoding.UTF8.GetBytes(eval));

        var csv = await NewExecutor().PredictAsync(def, "label", "id", MlTaskType.BinaryClassification, trainCsv, evalCsv);

        var lines = csv.Trim().Split('\n');
        lines[0].Should().Be("id,prediction");
        lines.Should().HaveCount(4); // header + 3 rows
        lines[1].Should().Be("e1,true");
        lines[2].Should().Be("e2,false");
        lines[3].Should().StartWith("e3,");
    }

    [Fact]
    public async Task Naming_a_missing_column_fails_loudly_instead_of_silently_dropping_it()
    {
        // select-columns names a column that isn't in the data — a typo. It must fail the run rather than
        // silently drop the unknown name and carry on with a wrong column set.
        var def = new WorkflowDefinition
        {
            Name = "typo",
            Nodes =
            [
                new() { Id = "d", Kind = "dataset" },
                new() { Id = "sc", Kind = "select-columns", Config = new Dictionary<string, string> { ["columns"] = "x1,presure" } },
                new() { Id = "sp", Kind = "split" },
                new() { Id = "tr", Kind = "train" },
            ],
            Edges = [new("d", "sc"), new("sc", "sp"), new("sp", "tr")],
        };

        var sb = new StringBuilder("x1,x2,label\n");
        for (var i = 0; i < 20; i++)
        {
            sb.Append($"{7 + (i % 3)},{7 + ((i / 3) % 3)},true\n{i % 3},{(i / 3) % 3},false\n");
        }
        using var csv = new MemoryStream(Encoding.UTF8.GetBytes(sb.ToString()));

        var result = await NewExecutor().ExecuteAsync(def, "label", MlTaskType.BinaryClassification, csv, 5);

        result.Success.Should().BeFalse();
        var failed = result.Nodes.Single(n => n.Kind == "select-columns");
        failed.Status.Should().Be("failed");
        failed.Detail.Should().Contain("presure");
    }

    [Fact]
    public async Task Predict_preserves_zero_padded_numeric_ids_across_the_transform_crossing()
    {
        // A normalize node records a TransformReplay, so the eval table round-trips through the ML.NET
        // crossing at predict time. The id "007" must come back as "007" — not retyped to the number 7 —
        // or it would no longer join to the competition answer key.
        var def = new WorkflowDefinition
        {
            Name = "id-preserve",
            Nodes =
            [
                new() { Id = "d", Kind = "dataset" },
                new() { Id = "nz", Kind = "normalize" },
                new() { Id = "sp", Kind = "split" },
                new() { Id = "tr", Kind = "train" },
                new() { Id = "ev", Kind = "evaluate" },
            ],
            Edges = [new("d", "nz"), new("nz", "sp"), new("sp", "tr"), new("tr", "ev")],
        };

        var train = new StringBuilder("id,x1,x2,label\n");
        for (var i = 0; i < 60; i++)
        {
            train.Append($"tr{i}a,{7 + (i % 3)},{7 + ((i / 3) % 3)},true\n");
            train.Append($"tr{i}b,{i % 3},{(i / 3) % 3},false\n");
        }
        using var trainCsv = new MemoryStream(Encoding.UTF8.GetBytes(train.ToString()));

        // Zero-padded, all-numeric ids — the exact shape a naive type sniffer collapses to 7 / 13.
        var eval = "id,x1,x2\n007,9,9\n013,0,0\n";
        using var evalCsv = new MemoryStream(Encoding.UTF8.GetBytes(eval));

        var csv = await NewExecutor().PredictAsync(def, "label", "id", MlTaskType.BinaryClassification, trainCsv, evalCsv);

        var lines = csv.Trim().Split('\n');
        lines[0].Should().Be("id,prediction");
        lines[1].Should().Be("007,true");
        lines[2].Should().Be("013,false");
    }

    [Fact]
    public async Task Predict_preserves_zero_padded_numeric_ids_across_the_duckdb_crossing()
    {
        // A SQL node replays on the eval set (a DuckDB DataReplay), so the id round-trips through the
        // DuckDB CSV crossing. The id column is pinned to text there too, so "007" is not collapsed to 7.
        var def = new WorkflowDefinition
        {
            Name = "id-preserve-duck",
            Nodes =
            [
                new() { Id = "d", Kind = "dataset" },
                new() { Id = "q", Kind = "sql", Config = new Dictionary<string, string> { ["sql"] = "SELECT *, x1 + x2 AS total FROM working" } },
                new() { Id = "sp", Kind = "split" },
                new() { Id = "tr", Kind = "train" },
                new() { Id = "ev", Kind = "evaluate" },
            ],
            Edges = [new("d", "q"), new("q", "sp"), new("sp", "tr"), new("tr", "ev")],
        };

        var train = new StringBuilder("id,x1,x2,label\n");
        for (var i = 0; i < 60; i++)
        {
            train.Append($"tr{i}a,{7 + (i % 3)},{7 + ((i / 3) % 3)},true\n");
            train.Append($"tr{i}b,{i % 3},{(i / 3) % 3},false\n");
        }
        using var trainCsv = new MemoryStream(Encoding.UTF8.GetBytes(train.ToString()));

        var eval = "id,x1,x2\n007,9,9\n013,0,0\n";
        using var evalCsv = new MemoryStream(Encoding.UTF8.GetBytes(eval));

        var csv = await NewExecutor().PredictAsync(def, "label", "id", MlTaskType.BinaryClassification, trainCsv, evalCsv);

        var lines = csv.Trim().Split('\n');
        lines[0].Should().Be("id,prediction");
        lines[1].Should().Be("007,true");
        lines[2].Should().Be("013,false");
    }

    [Fact]
    public async Task Binary_predictions_echo_the_training_labels_token_convention()
    {
        // The training label uses 1/0 (a Titanic-style key), so the submission must come back as 1/0 —
        // not a hardcoded true/false.
        var def = new WorkflowDefinition
        {
            Name = "submission-1-0",
            Nodes =
            [
                new() { Id = "d", Kind = "dataset" },
                new() { Id = "nz", Kind = "normalize" },
                new() { Id = "sp", Kind = "split" },
                new() { Id = "tr", Kind = "train" },
                new() { Id = "ev", Kind = "evaluate" },
            ],
            Edges = [new("d", "nz"), new("nz", "sp"), new("sp", "tr"), new("tr", "ev")],
        };

        var train = new StringBuilder("id,x1,x2,label\n");
        for (var i = 0; i < 60; i++)
        {
            train.Append($"tr{i}a,{7 + (i % 3)},{7 + ((i / 3) % 3)},1\n");
            train.Append($"tr{i}b,{i % 3},{(i / 3) % 3},0\n");
        }
        using var trainCsv = new MemoryStream(Encoding.UTF8.GetBytes(train.ToString()));
        var eval = "id,x1,x2\ne1,9,9\ne2,0,0\n";
        using var evalCsv = new MemoryStream(Encoding.UTF8.GetBytes(eval));

        var csv = await NewExecutor().PredictAsync(def, "label", "id", MlTaskType.BinaryClassification, trainCsv, evalCsv);

        var lines = csv.Trim().Split('\n');
        lines[0].Should().Be("id,prediction");
        lines[1].Should().Be("e1,1");
        lines[2].Should().Be("e2,0");
    }

    [Fact]
    public async Task Executes_and_evaluates_a_multiclass_model()
    {
        var def = new WorkflowDefinition
        {
            Name = "multiclass",
            Nodes =
            [
                new() { Id = "d", Kind = "dataset" },
                new() { Id = "nz", Kind = "normalize" },
                new() { Id = "sp", Kind = "split" },
                new() { Id = "tr", Kind = "train" },
                new() { Id = "ev", Kind = "evaluate" },
            ],
            Edges = [new("d", "nz"), new("nz", "sp"), new("sp", "tr"), new("tr", "ev")],
        };

        using var csv = new MemoryStream(Encoding.UTF8.GetBytes(MulticlassCsv(withId: false)));

        var result = await NewExecutor().ExecuteAsync(def, "grade", MlTaskType.MulticlassClassification, csv, 10);

        var failed = result.Nodes.FirstOrDefault(n => n.Status is "failed");
        result.Success.Should().BeTrue($"but node '{failed?.Kind}' failed: {failed?.Detail}");
        result.Nodes.Single(n => n.Kind == "train").Detail.Should().Contain("MaximumEntropy");
        result.PrimaryMetric.Should().Be("MicroAccuracy");
        result.PrimaryValue.Should().BeGreaterThan(0.8);
    }

    [Fact]
    public async Task Predicts_multiclass_labels_as_a_submission_csv()
    {
        var def = new WorkflowDefinition
        {
            Name = "multiclass-submission",
            Nodes =
            [
                new() { Id = "d", Kind = "dataset" },
                new() { Id = "nz", Kind = "normalize" },
                new() { Id = "sp", Kind = "split" },
                new() { Id = "tr", Kind = "train" },
                new() { Id = "ev", Kind = "evaluate" },
            ],
            Edges = [new("d", "nz"), new("nz", "sp"), new("sp", "tr"), new("tr", "ev")],
        };

        using var trainCsv = new MemoryStream(Encoding.UTF8.GetBytes(MulticlassCsv(withId: true)));
        // One clearly-separated row per class: low → a, mid → b, high → c.
        var eval = "id,x1,x2\ne0,0,0\ne1,5,5\ne2,10,10\n";
        using var evalCsv = new MemoryStream(Encoding.UTF8.GetBytes(eval));

        var csv = await NewExecutor().PredictAsync(def, "grade", "id", MlTaskType.MulticlassClassification, trainCsv, evalCsv);

        var lines = csv.Trim().Split('\n');
        lines[0].Should().Be("id,prediction");
        lines.Should().HaveCount(4);
        lines[1].Should().Be("e0,a");
        lines[2].Should().Be("e1,b");
        lines[3].Should().Be("e2,c");
    }

    [Fact]
    public async Task Honours_per_algorithm_hyperparameters()
    {
        var def = new WorkflowDefinition
        {
            Name = "tuned",
            Nodes =
            [
                new() { Id = "d", Kind = "dataset" },
                new() { Id = "sp", Kind = "split" },
                new()
                {
                    Id = "tr",
                    Kind = "train",
                    Config = new Dictionary<string, string>
                    {
                        ["algorithm"] = "fasttree",
                        ["trees"] = "30",
                        ["leaves"] = "8",
                        ["learningRate"] = "0.3",
                    },
                },
                new() { Id = "ev", Kind = "evaluate" },
            ],
            Edges = [new("d", "sp"), new("sp", "tr"), new("tr", "ev")],
        };

        var sb = new StringBuilder("x1,x2,label\n");
        for (var i = 0; i < 60; i++)
        {
            sb.Append($"{7 + (i % 3)},{7 + ((i / 3) % 3)},true\n");
            sb.Append($"{i % 3},{(i / 3) % 3},false\n");
        }
        using var csv = new MemoryStream(Encoding.UTF8.GetBytes(sb.ToString()));

        var result = await NewExecutor().ExecuteAsync(def, "label", MlTaskType.BinaryClassification, csv, 10);

        var failed = result.Nodes.FirstOrDefault(n => n.Status is "failed");
        result.Success.Should().BeTrue($"but node '{failed?.Kind}' failed: {failed?.Detail}");
        result.Nodes.Single(n => n.Kind == "train").Detail.Should().Contain("FastTree");
        result.PrimaryValue.Should().BeGreaterThan(0.8);
    }

    [Fact]
    public async Task Runs_data_management_nodes()
    {
        // drop-columns + filter-rows + standardize alongside the model steps.
        var def = new WorkflowDefinition
        {
            Name = "data-mgmt",
            Nodes =
            [
                new() { Id = "d", Kind = "dataset" },
                new() { Id = "dc", Kind = "drop-columns", Config = new Dictionary<string, string> { ["columns"] = "noise" } },
                new() { Id = "fr", Kind = "filter-rows", Config = new Dictionary<string, string> { ["column"] = "x1", ["min"] = "-100", ["max"] = "100" } },
                new() { Id = "st", Kind = "standardize" },
                new() { Id = "sp", Kind = "split" },
                new() { Id = "tr", Kind = "train" },
                new() { Id = "ev", Kind = "evaluate" },
            ],
            Edges = [new("d", "dc"), new("dc", "fr"), new("fr", "st"), new("st", "sp"), new("sp", "tr"), new("tr", "ev")],
        };

        var sb = new StringBuilder("x1,x2,noise,label\n");
        for (var i = 0; i < 80; i++)
        {
            sb.Append($"{7 + (i % 3)},{7 + ((i / 3) % 3)},{i % 5},true\n");
            sb.Append($"{i % 3},{(i / 3) % 3},{i % 5},false\n");
        }
        using var csv = new MemoryStream(Encoding.UTF8.GetBytes(sb.ToString()));

        var result = await NewExecutor().ExecuteAsync(def, "label", MlTaskType.BinaryClassification, csv, 10);

        var failed = result.Nodes.FirstOrDefault(n => n.Status is "failed");
        result.Success.Should().BeTrue($"but node '{failed?.Kind}' failed: {failed?.Detail}");
        result.Nodes.Single(n => n.Kind == "drop-columns").Detail.Should().Contain("noise");
        result.Nodes.Single(n => n.Kind == "standardize").Status.Should().Be("done");
        result.PrimaryValue.Should().BeGreaterThan(0.8);
    }

    [Theory]
    [InlineData("binning")]
    [InlineData("log-normalize")]
    [InlineData("robust-scale")]
    public async Task Runs_scaling_variant_nodes(string scaler)
    {
        var def = new WorkflowDefinition
        {
            Name = scaler,
            Nodes =
            [
                new() { Id = "d", Kind = "dataset" },
                new() { Id = "sc", Kind = scaler },
                new() { Id = "sp", Kind = "split" },
                new() { Id = "tr", Kind = "train" },
                new() { Id = "ev", Kind = "evaluate" },
            ],
            Edges = [new("d", "sc"), new("sc", "sp"), new("sp", "tr"), new("tr", "ev")],
        };

        var sb = new StringBuilder("x1,x2,label\n");
        for (var i = 0; i < 80; i++)
        {
            sb.Append($"{7 + (i % 3)},{7 + ((i / 3) % 3)},true\n");
            sb.Append($"{i % 3},{(i / 3) % 3},false\n");
        }
        using var csv = new MemoryStream(Encoding.UTF8.GetBytes(sb.ToString()));

        var result = await NewExecutor().ExecuteAsync(def, "label", MlTaskType.BinaryClassification, csv, 10);

        var failed = result.Nodes.FirstOrDefault(n => n.Status is "failed");
        result.Success.Should().BeTrue($"but node '{failed?.Kind}' failed: {failed?.Detail}");
        result.Nodes.Single(n => n.Kind == scaler).Status.Should().Be("done");
        result.PrimaryValue.Should().BeGreaterThan(0.8);
    }

    [Fact]
    public async Task Runs_pca_then_trains()
    {
        var def = new WorkflowDefinition
        {
            Name = "pca",
            Nodes =
            [
                new() { Id = "d", Kind = "dataset" },
                new() { Id = "pc", Kind = "pca", Config = new Dictionary<string, string> { ["rank"] = "2" } },
                new() { Id = "sp", Kind = "split" },
                new() { Id = "tr", Kind = "train" },
                new() { Id = "ev", Kind = "evaluate" },
            ],
            Edges = [new("d", "pc"), new("pc", "sp"), new("sp", "tr"), new("tr", "ev")],
        };

        var sb = new StringBuilder("x1,x2,x3,label\n");
        for (var i = 0; i < 80; i++)
        {
            sb.Append($"{7 + (i % 3)},{7 + ((i / 3) % 3)},{6 + (i % 2)},true\n");
            sb.Append($"{i % 3},{(i / 3) % 3},{i % 2},false\n");
        }
        using var csv = new MemoryStream(Encoding.UTF8.GetBytes(sb.ToString()));

        var result = await NewExecutor().ExecuteAsync(def, "label", MlTaskType.BinaryClassification, csv, 10);

        var failed = result.Nodes.FirstOrDefault(n => n.Status is "failed");
        result.Success.Should().BeTrue($"but node '{failed?.Kind}' failed: {failed?.Detail}");
        result.Nodes.Single(n => n.Kind == "pca").Detail.Should().Contain("2 components");
        result.PrimaryValue.Should().BeGreaterThan(0.7);
    }

    [Fact]
    public async Task Runs_kmeans_clustering_without_a_train_node()
    {
        var def = new WorkflowDefinition
        {
            Name = "cluster",
            Nodes =
            [
                new() { Id = "d", Kind = "dataset" },
                new() { Id = "cl", Kind = "cluster", Config = new Dictionary<string, string> { ["clusters"] = "3" } },
            ],
            Edges = [new("d", "cl")],
        };

        var sb = new StringBuilder("x1,x2,label\n");
        for (var i = 0; i < 90; i++)
        {
            foreach (var b in new[] { 0, 6, 12 })
            {
                sb.Append($"{b + (i % 2)},{b + ((i / 2) % 2)},g\n");
            }
        }
        using var csv = new MemoryStream(Encoding.UTF8.GetBytes(sb.ToString()));

        var result = await NewExecutor().ExecuteAsync(def, "label", MlTaskType.MulticlassClassification, csv, 10);

        var failed = result.Nodes.FirstOrDefault(n => n.Status is "failed");
        result.Success.Should().BeTrue($"but node '{failed?.Kind}' failed: {failed?.Detail}");
        result.Nodes.Single(n => n.Kind == "cluster").Detail.Should().Contain("3 clusters");
    }

    [Fact]
    public async Task Runs_text_featurization()
    {
        var def = new WorkflowDefinition
        {
            Name = "text",
            Nodes =
            [
                new() { Id = "d", Kind = "dataset" },
                new() { Id = "ft", Kind = "featurize-text" },
                new() { Id = "sp", Kind = "split" },
                new() { Id = "tr", Kind = "train" },
                new() { Id = "ev", Kind = "evaluate" },
            ],
            Edges = [new("d", "ft"), new("ft", "sp"), new("sp", "tr"), new("tr", "ev")],
        };

        var sb = new StringBuilder("note,label\n");
        for (var i = 0; i < 80; i++)
        {
            sb.Append("high pressure vibration alarm,true\n");
            sb.Append("normal steady reading nominal,false\n");
        }
        using var csv = new MemoryStream(Encoding.UTF8.GetBytes(sb.ToString()));

        var result = await NewExecutor().ExecuteAsync(def, "label", MlTaskType.BinaryClassification, csv, 10);

        var failed = result.Nodes.FirstOrDefault(n => n.Status is "failed");
        result.Success.Should().BeTrue($"but node '{failed?.Kind}' failed: {failed?.Detail}");
        result.Nodes.Single(n => n.Kind == "featurize-text").Status.Should().Be("done");
        result.PrimaryValue.Should().BeGreaterThan(0.8);
    }

    [Fact]
    public async Task FastTree_training_is_reproducible_run_to_run()
    {
        // FastTree's native builder is multi-threaded and its floating-point reductions vary run-to-run;
        // pinning NumberOfThreads = 1 (plus the fixed seed) must make the fitted model — and therefore the
        // reported metric — identical across two independent runs of the same pipeline.
        var def = new WorkflowDefinition
        {
            Name = "determinism",
            Nodes =
            [
                new() { Id = "d", Kind = "dataset" },
                new() { Id = "sp", Kind = "split" },
                new() { Id = "tr", Kind = "train", Config = new Dictionary<string, string> { ["algorithm"] = "fasttree" } },
                new() { Id = "ev", Kind = "evaluate" },
            ],
            Edges = [new("d", "sp"), new("sp", "tr"), new("tr", "ev")],
        };

        static string Data()
        {
            var sb = new StringBuilder("x1,x2,x3,label\n");
            for (var i = 0; i < 120; i++)
            {
                sb.Append($"{7 + (i % 5)},{6 + ((i / 5) % 4)},{5 + (i % 3)},true\n");
                sb.Append($"{i % 5},{(i / 5) % 4},{i % 3},false\n");
            }
            return sb.ToString();
        }

        var first = await NewExecutor().ExecuteAsync(def, "label", MlTaskType.BinaryClassification, new MemoryStream(Encoding.UTF8.GetBytes(Data())), 10);
        var second = await NewExecutor().ExecuteAsync(def, "label", MlTaskType.BinaryClassification, new MemoryStream(Encoding.UTF8.GetBytes(Data())), 10);

        first.Success.Should().BeTrue();
        second.Success.Should().BeTrue();
        first.Nodes.Single(n => n.Kind == "train").Detail.Should().Contain("FastTree");
        second.PrimaryValue.Should().Be(first.PrimaryValue, "one pinned thread + fixed seed must be bit-for-bit reproducible");
    }

    [Fact]
    public async Task Dropping_the_label_before_train_fails_loudly()
    {
        // A drop-columns node removes the label. Training must fail with a clear message rather than
        // silently fall back to the first column as the label and report a meaningless metric.
        var def = new WorkflowDefinition
        {
            Name = "no-label",
            Nodes =
            [
                new() { Id = "d", Kind = "dataset" },
                new() { Id = "dc", Kind = "drop-columns", Config = new Dictionary<string, string> { ["columns"] = "label" } },
                new() { Id = "sp", Kind = "split" },
                new() { Id = "tr", Kind = "train" },
            ],
            Edges = [new("d", "dc"), new("dc", "sp"), new("sp", "tr")],
        };

        var sb = new StringBuilder("x1,x2,label\n");
        for (var i = 0; i < 20; i++)
        {
            sb.Append($"{7 + (i % 3)},{7 + ((i / 3) % 3)},true\n{i % 3},{(i / 3) % 3},false\n");
        }
        using var csv = new MemoryStream(Encoding.UTF8.GetBytes(sb.ToString()));

        var result = await NewExecutor().ExecuteAsync(def, "label", MlTaskType.BinaryClassification, csv, 5);

        result.Success.Should().BeFalse();
        var train = result.Nodes.Single(n => n.Kind == "train");
        train.Status.Should().Be("failed");
        train.Detail.Should().Contain("label");
    }

    [Fact]
    public async Task Dropping_the_id_before_prediction_fails_loudly()
    {
        // A replayed drop-columns removes the id, so at predict the eval set has no id column. Prediction
        // must fail loudly (predictions can't be aligned to the answer key) rather than emit column 0 as a
        // bogus id with a matching row count.
        var def = new WorkflowDefinition
        {
            Name = "drop-id",
            Nodes =
            [
                new() { Id = "d", Kind = "dataset" },
                new() { Id = "dc", Kind = "drop-columns", Config = new Dictionary<string, string> { ["columns"] = "id" } },
                new() { Id = "sp", Kind = "split" },
                new() { Id = "tr", Kind = "train" },
                new() { Id = "ev", Kind = "evaluate" },
            ],
            Edges = [new("d", "dc"), new("dc", "sp"), new("sp", "tr"), new("tr", "ev")],
        };

        var train = new StringBuilder("id,x1,x2,label\n");
        for (var i = 0; i < 60; i++)
        {
            train.Append($"tr{i}a,{7 + (i % 3)},{7 + ((i / 3) % 3)},true\n");
            train.Append($"tr{i}b,{i % 3},{(i / 3) % 3},false\n");
        }
        using var trainCsv = new MemoryStream(Encoding.UTF8.GetBytes(train.ToString()));
        var eval = "id,x1,x2\ne1,9,9\ne2,0,0\n";
        using var evalCsv = new MemoryStream(Encoding.UTF8.GetBytes(eval));

        await NewExecutor()
            .Invoking(x => x.PredictAsync(def, "label", "id", MlTaskType.BinaryClassification, trainCsv, evalCsv))
            .Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*id*");
    }

    [Fact]
    public async Task Building_a_feature_from_the_label_is_rejected_as_leakage()
    {
        // compute-column takes an explicit input list (not filtered through FeatureNames), so using the
        // label as an input would encode the target into a feature. That must fail loudly, not train on it.
        var def = new WorkflowDefinition
        {
            Name = "leaky-feature",
            Nodes =
            [
                new() { Id = "d", Kind = "dataset" },
                new() { Id = "cc", Kind = "compute-column", Config = new Dictionary<string, string> { ["output"] = "cheat", ["inputs"] = "x1,label", ["expression"] = "(a, b) => a + b" } },
                new() { Id = "sp", Kind = "split" },
                new() { Id = "tr", Kind = "train" },
            ],
            Edges = [new("d", "cc"), new("cc", "sp"), new("sp", "tr")],
        };

        var sb = new StringBuilder("x1,x2,label\n");
        for (var i = 0; i < 20; i++)
        {
            sb.Append($"{7 + (i % 3)},{7 + ((i / 3) % 3)},1\n{i % 3},{(i / 3) % 3},0\n");
        }
        using var csv = new MemoryStream(Encoding.UTF8.GetBytes(sb.ToString()));

        var result = await NewExecutor().ExecuteAsync(def, "label", MlTaskType.BinaryClassification, csv, 5);

        result.Success.Should().BeFalse();
        var cc = result.Nodes.Single(n => n.Kind == "compute-column");
        cc.Status.Should().Be("failed");
        cc.Detail.Should().Contain("leakage");
    }

    [Fact]
    public async Task Take_rows_after_split_is_rejected_it_would_drop_the_test_fold()
    {
        // split lays out train rows then test rows; take-rows(N) after it would keep only train rows,
        // leaving evaluate with no held-out set. Must fail loudly.
        var def = new WorkflowDefinition
        {
            Name = "sample-after-split",
            Nodes =
            [
                new() { Id = "d", Kind = "dataset" },
                new() { Id = "sp", Kind = "split" },
                new() { Id = "tk", Kind = "take-rows", Config = new Dictionary<string, string> { ["count"] = "10" } },
                new() { Id = "tr", Kind = "train" },
                new() { Id = "ev", Kind = "evaluate" },
            ],
            Edges = [new("d", "sp"), new("sp", "tk"), new("tk", "tr"), new("tr", "ev")],
        };

        var sb = new StringBuilder("x1,x2,label\n");
        for (var i = 0; i < 40; i++)
        {
            sb.Append($"{7 + (i % 3)},{7 + ((i / 3) % 3)},true\n{i % 3},{(i / 3) % 3},false\n");
        }
        using var csv = new MemoryStream(Encoding.UTF8.GetBytes(sb.ToString()));

        var result = await NewExecutor().ExecuteAsync(def, "label", MlTaskType.BinaryClassification, csv, 5);

        result.Success.Should().BeFalse();
        var tk = result.Nodes.Single(n => n.Kind == "take-rows");
        tk.Status.Should().Be("failed");
        tk.Detail.Should().Contain("split");
    }

    // Three separable clusters → classes a/b/c.
    private static string MulticlassCsv(bool withId)
    {
        var sb = new StringBuilder(withId ? "id,x1,x2,grade\n" : "x1,x2,grade\n");
        var clusters = new[] { (0, "a"), (5, "b"), (10, "c") };
        for (var i = 0; i < 40; i++)
        {
            foreach (var (baseVal, grade) in clusters)
            {
                var prefix = withId ? $"r{i}{grade}," : string.Empty;
                sb.Append($"{prefix}{baseVal + (i % 2)},{baseVal + ((i / 2) % 2)},{grade}\n");
            }
        }
        return sb.ToString();
    }
}
