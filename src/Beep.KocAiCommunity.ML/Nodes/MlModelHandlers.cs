using System.Globalization;
using Beep.KocAiCommunity.Application.Common;
using Beep.KocAiCommunity.Application.ML;
using Beep.KocAiCommunity.Contracts.Workflow;
using Microsoft.ML;
using static Beep.KocAiCommunity.ML.Nodes.PipelineContext;

namespace Beep.KocAiCommunity.ML.Nodes;

// Source / split / model / evaluate handlers. The split tags rows with the fold marker; train fits on
// the train fold and evaluate scores the test fold. All are terminal or pass-through (Output = input).

public sealed class DatasetHandler : IPipelineNodeHandler
{
    public NodeDescriptor Descriptor { get; } = new("dataset", "Source", "Dataset",
        "The input rows flowing into the pipeline (e.g. well headers, sensor readings).",
        PortKind.None, PortKind.Table, new DatasetParameters().Describe());

    public NodeResult Execute(PipelineContext ctx, WorkflowNode node, PipelineTable input) =>
        new(new NodeExecutionResult(node.Id, node.Kind, "done", $"{input.RowCount} rows · {ctx.FeatureNames(input).Count()} columns"), input);
}

public sealed class SplitHandler : IPipelineNodeHandler
{
    public NodeDescriptor Descriptor { get; } = new("split", "Split", "Train/test split",
        "Hold out a fraction of rows for honest evaluation. Place before the model.", PortKind.Table, PortKind.Table,
        new SplitParameters().Describe());

    public NodeResult Execute(PipelineContext ctx, WorkflowNode node, PipelineTable input)
    {
        if (ctx.Mode == PipelineMode.Predict)
        {
            return new NodeResult(new NodeExecutionResult(node.Id, node.Kind, "done", "trained on the full set for prediction"), input);
        }

        ctx.HasSplit = true; // a fold marker now exists; downstream nodes must not drop it (guards leakage)
        var fraction = Math.Clamp(ReadDouble(Cfg(node, "testFraction"), 0.25), 0.05, 0.9);
        var full = input.LoadIntoMl(ctx.Ml, ctx.LabelColumn);
        var split = ctx.Ml.Data.TrainTestSplit(full, testFraction: fraction, seed: 1);

        var outPath = ctx.NewTemp();
        using (var sw = new StreamWriter(outPath))
        {
            MlCsv.Write(split.TrainSet, sw, writeHeader: true, extra: (FoldColumn, "0"));
            MlCsv.Write(split.TestSet, sw, writeHeader: false, extra: (FoldColumn, "1"));
        }

        var output = PipelineTable.FromCsvFile(outPath);
        return new NodeResult(new NodeExecutionResult(node.Id, node.Kind, "done", $"{output.RowCount} rows · {fraction:0.##} held out for test"), output);
    }
}

public sealed class TimeSplitHandler : IPipelineNodeHandler
{
    public NodeDescriptor Descriptor { get; } = new("time-split", "Split", "Chronological split",
        "For time-series forecasting: order by a time column and hold out the most-recent rows, so the "
        + "model is trained on the past and evaluated on the future (no leakage). Place before the model.",
        PortKind.Table, PortKind.Table, new TimeSplitParameters().Describe());

    public NodeResult Execute(PipelineContext ctx, WorkflowNode node, PipelineTable input)
    {
        if (ctx.Mode == PipelineMode.Predict)
        {
            return new NodeResult(new NodeExecutionResult(node.Id, node.Kind, "done", "trained on the full history for prediction"), input);
        }

        var orderColumn = Cfg(node, "orderColumn") ?? string.Empty;
        var col = input.Columns.ToList().IndexOf(orderColumn);
        if (string.IsNullOrWhiteSpace(orderColumn) || col < 0)
        {
            return new NodeResult(new NodeExecutionResult(node.Id, node.Kind, "failed",
                string.IsNullOrWhiteSpace(orderColumn) ? "pick the time/order column" : $"column '{orderColumn}' not found"), input);
        }

        var fraction = Math.Clamp(ReadDouble(Cfg(node, "testFraction"), 0.25), 0.05, 0.9);

        // Read every row, then order it in time. The most-recent `fraction` becomes the test fold so the
        // model never sees the future — a random split would leak later observations into training.
        string[]? header = null;
        var rows = new List<string[]>();
        using (var reader = new StreamReader(input.CsvPath))
        {
            foreach (var record in KocCsv.ParseRecords(reader))
            {
                if (header is null)
                {
                    header = record;
                }
                else
                {
                    rows.Add(record);
                }
            }
        }

        if (header is null || rows.Count < 2)
        {
            return new NodeResult(new NodeExecutionResult(node.Id, node.Kind, "failed", "need at least two rows to split"), input);
        }

        var ordered = OrderChronologically(rows, col);
        var trainCount = Math.Clamp((int)Math.Round(ordered.Count * (1 - fraction)), 1, ordered.Count - 1);

        var outPath = ctx.NewTemp();
        using (var sw = new StreamWriter(outPath))
        {
            sw.WriteLine(KocCsv.WriteRow([.. header, FoldColumn]));
            for (var i = 0; i < ordered.Count; i++)
            {
                sw.WriteLine(KocCsv.WriteRow([.. ordered[i], i < trainCount ? "0" : "1"]));
            }
        }

        ctx.HasSplit = true; // a fold marker now exists; downstream nodes must not drop it (guards leakage)
        var output = PipelineTable.FromCsvFile(outPath);
        return new NodeResult(new NodeExecutionResult(node.Id, node.Kind, "done",
            $"{trainCount} past rows train · {ordered.Count - trainCount} most-recent held out (by {orderColumn})"), output);
    }

    // Sort ascending by the order column, choosing one homogeneous key type (date → number → text) so we
    // never compare mixed types. Blank keys sort first (treated as the earliest).
    private static List<string[]> OrderChronologically(List<string[]> rows, int col)
    {
        string Value(string[] r) => col < r.Length ? r[col] : string.Empty;
        var values = rows.Select(Value).Where(v => !string.IsNullOrWhiteSpace(v)).ToList();

        bool AllParse(Func<string, bool> tryParse) => values.Count > 0 && values.All(tryParse);

        if (AllParse(v => DateTime.TryParse(v, CultureInfo.InvariantCulture, DateTimeStyles.None, out _)))
        {
            return [.. rows.OrderBy(r => DateTime.TryParse(Value(r), CultureInfo.InvariantCulture, DateTimeStyles.None, out var d) ? d : DateTime.MinValue)];
        }

        if (AllParse(v => double.TryParse(v, NumberStyles.Any, CultureInfo.InvariantCulture, out _)))
        {
            return [.. rows.OrderBy(r => double.TryParse(Value(r), NumberStyles.Any, CultureInfo.InvariantCulture, out var n) ? n : double.MinValue)];
        }

        return [.. rows.OrderBy(Value, StringComparer.Ordinal)];
    }
}

public sealed class TrainHandler : IPipelineNodeHandler
{
    public NodeDescriptor Descriptor { get; } = new("train", "Model", "Train model",
        "Fit a model on the training features (ESP failure, production rate, …).", PortKind.Table, PortKind.Model,
        new TrainParameters().Describe());

    public NodeResult Execute(PipelineContext ctx, WorkflowNode node, PipelineTable input)
    {
        // Anomaly detection is unsupervised — it learns "normal" from the features alone, so it needs no
        // label to train (a label, if present, is ground truth used only by the evaluate node).
        if (ctx.Task != MlTaskType.AnomalyDetection)
        {
            ctx.RequireLabel(node, input);
        }

        var full = input.LoadIntoMl(ctx.Ml, ctx.LabelColumn);
        var features = SelectFeatures(node, NumericFeatures(full.Schema, ctx.FeatureNames(input)));
        if (features.Length == 0)
        {
            if (ctx.Mode == PipelineMode.Predict)
            {
                throw new InvalidOperationException("Pipeline has no usable feature columns to train on.");
            }

            return new NodeResult(new NodeExecutionResult(node.Id, node.Kind, "failed", "no usable feature columns"), input);
        }

        (ctx.Model, ctx.Algorithm, ctx.LabelMap) = MlModelOps.FitModel(ctx.Ml, ctx.Task, node, ctx.LabelColumn, ctx.FoldTrainView(full), features);
        return new NodeResult(new NodeExecutionResult(node.Id, node.Kind, "done", $"{ctx.Algorithm} · {features.Length} features"), input);
    }
}

public sealed class ClusterHandler : IPipelineNodeHandler
{
    public NodeDescriptor Descriptor { get; } = new("cluster", "Model", "Cluster (k-means)",
        "Unsupervised grouping — no label needed (e.g. well-log facies).", PortKind.Table, PortKind.Model,
        new ClusterParameters().Describe());

    public NodeResult Execute(PipelineContext ctx, WorkflowNode node, PipelineTable input)
    {
        if (ctx.Mode == PipelineMode.Predict)
        {
            return new NodeResult(new NodeExecutionResult(node.Id, node.Kind, "skipped", "not used for prediction"), input);
        }

        var full = input.LoadIntoMl(ctx.Ml, ctx.LabelColumn);
        var features = SelectFeatures(node, NumericFeatures(full.Schema, ctx.FeatureNames(input)));
        if (features.Length == 0)
        {
            return new NodeResult(new NodeExecutionResult(node.Id, node.Kind, "skipped", "no numeric features"), input);
        }

        var k = Math.Clamp((int)ReadDouble(Cfg(node, "clusters"), 3), 2, 20);
        var trainView = ctx.FoldTrainView(full);
        var clusterModel = ctx.Ml.Transforms.Concatenate("Features", features)
            .Append(ctx.Ml.Clustering.Trainers.KMeans("Features", numberOfClusters: k))
            .Fit(trainView);
        var cm = ctx.Ml.Clustering.Evaluate(clusterModel.Transform(trainView), scoreColumnName: "Score", featureColumnName: "Features");
        return new NodeResult(new NodeExecutionResult(node.Id, node.Kind, "done", $"{k} clusters · avg distance {cm.AverageDistance:0.###} · DBI {cm.DaviesBouldinIndex:0.###}"), input);
    }
}

public sealed class CrossValidateHandler : IPipelineNodeHandler
{
    public NodeDescriptor Descriptor { get; } = new("cross-validate", "Model", "Cross-validate",
        "K-fold validation for a more honest metric.", PortKind.Table, PortKind.Metrics,
        new CrossValidateParameters().Describe());

    public NodeResult Execute(PipelineContext ctx, WorkflowNode node, PipelineTable input)
    {
        if (ctx.Mode == PipelineMode.Predict)
        {
            return new NodeResult(new NodeExecutionResult(node.Id, node.Kind, "skipped", "not used for prediction"), input);
        }

        ctx.RequireLabel(node, input);
        var ml = ctx.Ml;
        var full = input.LoadIntoMl(ml, ctx.LabelColumn);
        var trainView = ctx.FoldTrainView(full);
        var features = SelectFeatures(node, NumericFeatures(full.Schema, ctx.FeatureNames(input)));
        var folds = Math.Clamp((int)ReadDouble(Cfg(node, "folds"), 5), 2, 10);

        NodeExecutionResult status;
        if (ctx.Task == MlTaskType.MulticlassClassification)
        {
            var (mcTrainer, mcName) = MlModelOps.MulticlassTrainer(ml, node);
            var mcEst = ml.Transforms.Conversion.MapValueToKey("Label", ctx.LabelColumn)
                .Append(ml.Transforms.Concatenate("Features", features)).Append(mcTrainer);
            var cv = ml.MulticlassClassification.CrossValidate(trainView, mcEst, numberOfFolds: folds, labelColumnName: "Label");
            status = new NodeExecutionResult(node.Id, node.Kind, "done", $"{folds}-fold {mcName} · mean micro-acc {cv.Average(r => r.Metrics.MicroAccuracy):0.###}");
        }
        else
        {
            var (trainer, name) = MlModelOps.Trainer(ml, ctx.Task, node, ctx.LabelColumn);
            var est = ml.Transforms.Concatenate("Features", features).Append(trainer);
            if (ctx.Task == MlTaskType.Regression)
            {
                var cv = ml.Regression.CrossValidate(trainView, est, numberOfFolds: folds, labelColumnName: ctx.LabelColumn);
                status = new NodeExecutionResult(node.Id, node.Kind, "done", $"{folds}-fold {name} · mean R² {cv.Average(r => r.Metrics.RSquared):0.###}");
            }
            else
            {
                var cv = ml.BinaryClassification.CrossValidateNonCalibrated(trainView, est, numberOfFolds: folds, labelColumnName: ctx.LabelColumn);
                status = new NodeExecutionResult(node.Id, node.Kind, "done", $"{folds}-fold {name} · mean accuracy {cv.Average(r => r.Metrics.Accuracy):0.###}");
            }
        }

        return new NodeResult(status, input);
    }
}

public sealed class ScoreHandler : IPipelineNodeHandler
{
    public NodeDescriptor Descriptor { get; } = new("score", "Evaluate", "Score",
        "Apply the trained model to the held-out set.", PortKind.Model, PortKind.Table, new ScoreParameters().Describe());

    public NodeResult Execute(PipelineContext ctx, WorkflowNode node, PipelineTable input)
    {
        if (ctx.Mode == PipelineMode.Predict || ctx.Model is null)
        {
            return new NodeResult(new NodeExecutionResult(node.Id, node.Kind, "skipped", ctx.Model is null ? "no trained model upstream" : "handled by the prediction step"), input);
        }

        var full = input.LoadIntoMl(ctx.Ml, ctx.LabelColumn);
        var scored = ctx.Model.Transform(ctx.FoldTestView(full));
        var count = scored.GetRowCount() ?? scored.Preview(int.MaxValue).RowView.Length;
        return new NodeResult(new NodeExecutionResult(node.Id, node.Kind, "done", $"{count} rows scored"), input);
    }
}

public sealed class EvaluateHandler : IPipelineNodeHandler
{
    public NodeDescriptor Descriptor { get; } = new("evaluate", "Evaluate", "Evaluate",
        "Compute metrics on the held-out set.", PortKind.Table, PortKind.Metrics, new EvaluateParameters().Describe());

    public NodeResult Execute(PipelineContext ctx, WorkflowNode node, PipelineTable input)
    {
        if (ctx.Mode == PipelineMode.Predict || ctx.Model is null)
        {
            return new NodeResult(new NodeExecutionResult(node.Id, node.Kind, "skipped", ctx.Model is null ? "no trained model upstream" : "handled by the prediction step"), input);
        }

        ctx.RequireLabel(node, input);
        var ml = ctx.Ml;
        var full = input.LoadIntoMl(ml, ctx.LabelColumn);
        var testView = ctx.FoldTestView(full);
        var withLabel = ctx.LabelMap is null ? testView : ctx.LabelMap.Transform(testView);
        var scored = ctx.Model.Transform(withLabel);

        NodeExecutionResult status;
        if (ctx.Task == MlTaskType.AnomalyDetection)
        {
            // Rank the continuous anomaly Score against the ground-truth label (rare positives). AUC is the
            // right metric here — accuracy is meaningless when almost every row is "normal".
            var rows = MlModelOps.ReadScoreAndLabel(scored, ctx.LabelColumn);
            var flagged = rows.Count(r => r.Positive);
            if (flagged == 0 || flagged == rows.Count)
            {
                ctx.PrimaryValue = 0.5;
                status = new NodeExecutionResult(node.Id, node.Kind, "done", $"no labelled anomalies in the held-out set ({rows.Count} rows)");
            }
            else
            {
                var auc = RocAuc.Compute(rows);
                ctx.PrimaryValue = auc;
                status = new NodeExecutionResult(node.Id, node.Kind, "done", $"AUC {auc:0.###} · {flagged} anomalies of {rows.Count}");
            }
        }
        else if (ctx.Task == MlTaskType.Regression)
        {
            var m = ml.Regression.Evaluate(scored, labelColumnName: ctx.LabelColumn);
            ctx.PrimaryValue = m.RSquared;
            status = new NodeExecutionResult(node.Id, node.Kind, "done", $"R² {m.RSquared:0.###} · RMSE {m.RootMeanSquaredError:0.###} · MAE {m.MeanAbsoluteError:0.###}");
        }
        else if (ctx.Task == MlTaskType.MulticlassClassification)
        {
            var m = ml.MulticlassClassification.Evaluate(scored, labelColumnName: "Label");
            ctx.PrimaryValue = m.MicroAccuracy;
            status = new NodeExecutionResult(node.Id, node.Kind, "done", $"MicroAcc {m.MicroAccuracy:0.###} · MacroAcc {m.MacroAccuracy:0.###}");
        }
        else
        {
            var m = ml.BinaryClassification.EvaluateNonCalibrated(scored, labelColumnName: ctx.LabelColumn);
            ctx.PrimaryValue = m.Accuracy;
            status = new NodeExecutionResult(node.Id, node.Kind, "done", $"Accuracy {m.Accuracy:0.###} · AUC {m.AreaUnderRocCurve:0.###}");
        }

        return new NodeResult(status, input);
    }
}
