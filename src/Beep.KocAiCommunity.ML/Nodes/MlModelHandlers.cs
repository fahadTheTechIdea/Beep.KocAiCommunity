using Beep.KocAiCommunity.Application.ML;
using Beep.KocAiCommunity.Contracts.Workflow;
using Microsoft.ML;
using static Beep.KocAiCommunity.ML.Nodes.NodeParam;
using static Beep.KocAiCommunity.ML.Nodes.PipelineContext;

namespace Beep.KocAiCommunity.ML.Nodes;

// Source / split / model / evaluate handlers. The split tags rows with the fold marker; train fits on
// the train fold and evaluate scores the test fold. All are terminal or pass-through (Output = input).

public sealed class DatasetHandler : IPipelineNodeHandler
{
    public NodeDescriptor Descriptor { get; } = new("dataset", "Source", "Dataset",
        "The input rows flowing into the pipeline (e.g. well headers, sensor readings).",
        PortKind.None, PortKind.Table, []);

    public NodeResult Execute(PipelineContext ctx, WorkflowNode node, PipelineTable input) =>
        new(new NodeExecutionResult(node.Id, node.Kind, "done", $"{input.RowCount} rows · {ctx.FeatureNames(input).Count()} columns"), input);
}

public sealed class SplitHandler : IPipelineNodeHandler
{
    public NodeDescriptor Descriptor { get; } = new("split", "Split", "Train/test split",
        "Hold out a fraction of rows for honest evaluation. Place before the model.", PortKind.Table, PortKind.Table,
        [P("testFraction", "Test fraction", NodeParameterType.Number, def: "0.25")]);

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

public sealed class TrainHandler : IPipelineNodeHandler
{
    public NodeDescriptor Descriptor { get; } = new("train", "Model", "Train model",
        "Fit a model on the training features (ESP failure, production rate, …).", PortKind.Table, PortKind.Model,
        [P("algorithm", "Algorithm", NodeParameterType.Select, def: "sdca", options: ["sdca", "lbfgs", "fasttree", "fastforest"])]);

    public NodeResult Execute(PipelineContext ctx, WorkflowNode node, PipelineTable input)
    {
        ctx.RequireLabel(node, input);
        var full = input.LoadIntoMl(ctx.Ml, ctx.LabelColumn);
        var features = NumericFeatures(full.Schema, ctx.FeatureNames(input));
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
        [P("clusters", "Clusters", NodeParameterType.Number, def: "3")]);

    public NodeResult Execute(PipelineContext ctx, WorkflowNode node, PipelineTable input)
    {
        if (ctx.Mode == PipelineMode.Predict)
        {
            return new NodeResult(new NodeExecutionResult(node.Id, node.Kind, "skipped", "not used for prediction"), input);
        }

        var full = input.LoadIntoMl(ctx.Ml, ctx.LabelColumn);
        var features = NumericFeatures(full.Schema, ctx.FeatureNames(input));
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
        [P("folds", "Folds", NodeParameterType.Number, def: "5")]);

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
        var features = NumericFeatures(full.Schema, ctx.FeatureNames(input));
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
        "Apply the trained model to the held-out set.", PortKind.Model, PortKind.Table, []);

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
        "Compute metrics on the held-out set.", PortKind.Table, PortKind.Metrics, []);

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
        if (ctx.Task == MlTaskType.Regression)
        {
            var m = ml.Regression.Evaluate(scored, labelColumnName: ctx.LabelColumn);
            ctx.PrimaryValue = m.RSquared;
            status = new NodeExecutionResult(node.Id, node.Kind, "done", $"R² {m.RSquared:0.###} · RMSE {m.RootMeanSquaredError:0.###}");
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
