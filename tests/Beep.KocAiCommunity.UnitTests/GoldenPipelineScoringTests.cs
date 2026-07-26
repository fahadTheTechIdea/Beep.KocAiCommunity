using System.Text;
using Beep.KocAiCommunity.Application.Competitions;
using Beep.KocAiCommunity.Application.ML;
using Beep.KocAiCommunity.Contracts.Workflow;
using Beep.KocAiCommunity.Infrastructure.Competitions;
using Beep.KocAiCommunity.ML.Nodes;
using FluentAssertions;
using Xunit;

namespace Beep.KocAiCommunity.UnitTests;

/// <summary>
/// The correctness assurance layer: golden end-to-end fixtures that run the WHOLE competition path —
/// execute the pipeline graph → emit an id,prediction submission → score it against the answer key — with
/// adversarial data that used to break silently (zero-padded ids, quoted comma-bearing ids, 1/0 vs
/// true/false labels). Each asserts the id survives every crossing AND joins 1:1 to the key, so a real
/// model scores perfectly. If any pipeline/scoring fix regresses, one of these fails loudly.
/// </summary>
public class GoldenPipelineScoringTests
{
    private static PluginNodeExecutor NewExecutor()
    {
        var handlers = typeof(PluginNodeExecutor).Assembly.GetTypes()
            .Where(t => t is { IsAbstract: false, IsInterface: false } && typeof(IPipelineNodeHandler).IsAssignableFrom(t))
            .Select(t => (IPipelineNodeHandler)Activator.CreateInstance(t)!);
        return new PluginNodeExecutor(new PluginNodeRegistry(handlers));
    }

    private static MemoryStream Csv(string text) => new(Encoding.UTF8.GetBytes(text));

    // dataset → normalize (a column transform that replays on the eval set) → split → train → evaluate.
    private static WorkflowDefinition Pipeline() => new()
    {
        Name = "golden",
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

    // Separable training data (high features → the positive class, low → the negative) with an id column.
    private static string TrainCsv(string positive, string negative)
    {
        var sb = new StringBuilder("id,x1,x2,label\n");
        for (var i = 0; i < 60; i++)
        {
            sb.Append($"tr{i}a,{8 + (i % 3)},{8 + ((i / 3) % 3)},{positive}\n");
            sb.Append($"tr{i}b,{i % 3},{(i / 3) % 3},{negative}\n");
        }

        return sb.ToString();
    }

    private static async Task<double> PredictAndScoreAsync(string labelColumn, string trainCsv, string evalCsv, string answerKey, IScoringPlugin scorer, MlTaskType task)
    {
        var submission = await NewExecutor().PredictAsync(Pipeline(), labelColumn, "id", task, Csv(trainCsv), Csv(evalCsv));
        return await scorer.ScoreAsync(Csv(submission), Csv(answerKey), "id");
    }

    [Fact]
    public async Task Zero_padded_ids_survive_the_whole_path_and_score_perfectly()
    {
        // e-ids are zero-padded and all-numeric — the exact shape a naive sniffer collapses to 7 / 13,
        // which would then fail to join the answer key. Row 007 is clearly positive, 013 clearly negative.
        var eval = "id,x1,x2\n007,9,9\n013,0,0\n";
        var key = "id,label\n007,true\n013,false\n";

        var score = await PredictAndScoreAsync("label", TrainCsv("true", "false"), eval, key, new AccuracyScorer(), MlTaskType.BinaryClassification);

        score.Should().Be(1.0, "the model separates the rows AND the zero-padded ids join the key intact");
    }

    [Fact]
    public async Task One_zero_labels_round_trip_and_score_perfectly()
    {
        // Titanic-style: labels are 1/0, so the submission must echo 1/0 (not true/false) to match the key.
        var eval = "id,x1,x2\ne1,9,9\ne2,0,0\n";
        var key = "id,label\ne1,1\ne2,0\n";

        var score = await PredictAndScoreAsync("label", TrainCsv("1", "0"), eval, key, new AccuracyScorer(), MlTaskType.BinaryClassification);

        score.Should().Be(1.0);
    }

    [Fact]
    public async Task A_quoted_comma_bearing_id_survives_the_whole_path()
    {
        // The id contains a comma, so it MUST be RFC-4180-quoted at every hop (eval load, transform replay,
        // submission emit, scoring read). A naive split would shear it and lose the join.
        var eval = "id,x1,x2\n\"W,1\",9,9\n\"W,2\",0,0\n";
        var key = "id,label\n\"W,1\",true\n\"W,2\",false\n";

        var score = await PredictAndScoreAsync("label", TrainCsv("true", "false"), eval, key, new AccuracyScorer(), MlTaskType.BinaryClassification);

        score.Should().Be(1.0);
    }

    [Fact]
    public async Task Titanic_feature_engineering_pipeline_submits_and_scores()
    {
        // A real Titanic feature-engineering pipeline — derive family_size (compute-column), add
        // fare_per_person (SQL), bin age/fare, one-hot sex/embarked, normalize — then split/train/evaluate.
        // This is exactly what /submit-pipeline runs server-side, so it proves those nodes work end-to-end
        // for a competition submission: the engineered features replay onto the eval set and a valid
        // id,prediction (1/0) submission comes out, one row per eval id.
        var def = new WorkflowDefinition
        {
            Name = "titanic-fe",
            Nodes =
            [
                new() { Id = "d", Kind = "dataset" },
                new() { Id = "fam", Kind = "compute-column", Config = new Dictionary<string, string> { ["output"] = "family_size", ["inputs"] = "sibsp,parch", ["expression"] = "(a, b) => a + b" } },
                new() { Id = "fpp", Kind = "sql", Config = new Dictionary<string, string> { ["sql"] = "SELECT *, fare / (family_size + 1) AS fare_per_person FROM working" } },
                new() { Id = "bin", Kind = "binning", Config = new Dictionary<string, string> { ["bins"] = "5" } },
                new() { Id = "oh", Kind = "one-hot" },
                new() { Id = "nz", Kind = "normalize" },
                new() { Id = "sp", Kind = "split" },
                new() { Id = "tr", Kind = "train", Config = new Dictionary<string, string> { ["algorithm"] = "fasttree" } },
                new() { Id = "ev", Kind = "evaluate" },
            ],
            Edges =
            [
                new("d", "fam"), new("fam", "fpp"), new("fpp", "bin"), new("bin", "oh"),
                new("oh", "nz"), new("nz", "sp"), new("sp", "tr"), new("tr", "ev"),
            ],
        };

        // Titanic-shaped training data with the real columns; clear signal: female OR 1st class survives.
        var train = new StringBuilder("id,pclass,sex,age,sibsp,parch,fare,embarked,survived\n");
        for (var i = 0; i < 120; i++)
        {
            var female = i % 2 == 0;
            var pclass = (i % 3) + 1;
            var embarked = new[] { "S", "C", "Q" }[i % 3];
            var survived = female || pclass == 1 ? 1 : 0;
            train.Append($"t{i},{pclass},{(female ? "female" : "male")},{20 + (i % 50)},{i % 3},{i % 2},{10 + (i % 90)},{embarked},{survived}\n");
        }

        // Evaluation set — id + the same features, no label.
        const string eval = "id,pclass,sex,age,sibsp,parch,fare,embarked\n"
            + "007,1,female,30,0,0,80,S\n"   // female + 1st → survives
            + "013,3,male,40,0,0,10,S\n";    // male + 3rd → not

        var submission = await NewExecutor().PredictAsync(def, "survived", "id", MlTaskType.BinaryClassification, Csv(train.ToString()), Csv(eval));

        var lines = submission.Trim().Split('\n');
        lines[0].Should().Be("id,prediction");
        lines.Should().HaveCount(3); // header + one row per eval id
        lines[1].Should().Be("007,1", "the engineered features replay onto the eval set, ids survive, and 1/0 is echoed");
        lines[2].Should().Be("013,0");
    }

    [Theory]
    [InlineData("pca")]
    [InlineData("feature-selection")]
    [InlineData("featurize-text")]
    [InlineData("binning")]
    public async Task Titanic_transform_node_runs_end_to_end_in_a_submission(string transform)
    {
        // Each of these transforms is a column-shaping step that must replay onto the eval set at predict.
        // Build a minimal Titanic pipeline around each one and run it through PredictAsync (the submit path);
        // assert a valid, id-aligned 1/0 submission comes out — proof the node works in a real submission.
        Dictionary<string, string>? cfg = transform switch
        {
            "pca" => new() { ["rank"] = "2" },
            "feature-selection" => new() { ["count"] = "1" },
            "binning" => new() { ["bins"] = "5" },
            _ => null, // featurize-text takes no config
        };

        var def = new WorkflowDefinition
        {
            Name = transform,
            Nodes =
            [
                new() { Id = "d", Kind = "dataset" },
                new() { Id = "fe", Kind = transform, Config = cfg },
                new() { Id = "oh", Kind = "one-hot" }, // no-op after featurize-text (it consumed the text cols)
                new() { Id = "nz", Kind = "normalize" },
                new() { Id = "sp", Kind = "split" },
                new() { Id = "tr", Kind = "train", Config = new Dictionary<string, string> { ["algorithm"] = "fasttree" } },
                new() { Id = "ev", Kind = "evaluate" },
            ],
            Edges = [new("d", "fe"), new("fe", "oh"), new("oh", "nz"), new("nz", "sp"), new("sp", "tr"), new("tr", "ev")],
        };

        var train = new StringBuilder("id,pclass,sex,age,sibsp,parch,fare,embarked,survived\n");
        for (var i = 0; i < 120; i++)
        {
            var female = i % 2 == 0;
            var pclass = (i % 3) + 1;
            var embarked = new[] { "S", "C", "Q" }[i % 3];
            train.Append($"t{i},{pclass},{(female ? "female" : "male")},{20 + (i % 50)},{i % 3},{i % 2},{10 + (i % 90)},{embarked},{(female || pclass == 1 ? 1 : 0)}\n");
        }

        const string eval = "id,pclass,sex,age,sibsp,parch,fare,embarked\ne1,1,female,30,0,0,80,S\ne2,3,male,40,0,0,10,S\n";

        var submission = await NewExecutor().PredictAsync(def, "survived", "id", MlTaskType.BinaryClassification, Csv(train.ToString()), Csv(eval));

        var lines = submission.Trim().Split('\n');
        lines[0].Should().Be("id,prediction");
        lines.Should().HaveCount(3, $"'{transform}' must yield one prediction per eval id");
        lines.Skip(1).Select(l => l.Split(',')[0]).Should().BeEquivalentTo(["e1", "e2"], "the ids survive the transform + replay");
        lines.Skip(1).Should().OnlyContain(l => l.EndsWith(",1") || l.EndsWith(",0"), "valid binary predictions in the label's own tokens");
    }

    [Fact]
    public async Task Regression_ids_survive_and_score_with_low_rmse()
    {
        // A linear regression target with ids; a good model + intact ids → the submission joins the key and
        // RMSE is small (real learning, not a mis-joined wild miss).
        var train = new StringBuilder("id,choke,tubing,oil_rate\n");
        for (var i = 0; i < 120; i++)
        {
            var choke = 1 + (i % 8);
            var tubing = 20 + (i % 25);
            train.Append($"r{i},{choke},{tubing},{(40 * choke) + (6 * tubing)}\n");
        }

        var eval = "id,choke,tubing\n0042,3,30\n0007,5,25\n";
        var key = "id,oil_rate\n0042,300\n0007,350\n"; // 40*3+6*30, 40*5+6*25

        var score = await PredictAndScoreAsync("oil_rate", train.ToString(), eval, key, new RmseScorer(), MlTaskType.Regression);

        score.Should().BeLessThan(150, "the zero-padded ids join the key, so RMSE reflects real error, not a mis-join");
    }
}
