using Beep.KocAiCommunity.Application.ML;
using Beep.KocAiCommunity.Contracts.Workflow;
using Microsoft.ML;
using static Beep.KocAiCommunity.ML.Nodes.NodeParam;
using static Beep.KocAiCommunity.ML.Nodes.PipelineContext;

namespace Beep.KocAiCommunity.ML.Nodes;

// Source / split / model / evaluate handlers. These branch on ctx.Mode: in Predict mode the model
// trains on the full set and the split/score/evaluate/cluster/cross-validate nodes are no-ops.

public sealed class DatasetHandler : IPipelineNodeHandler
{
    public NodeEngine Engine => NodeEngine.Source;
    public NodeDescriptor Descriptor { get; } = new("dataset", "Source", "Dataset",
        "The input rows flowing into the pipeline (e.g. well headers, sensor readings).",
        PortKind.None, PortKind.Table, []);

    public Task<NodeExecutionResult> ExecuteAsync(PipelineContext ctx, WorkflowNode node, CancellationToken ct) =>
        Task.FromResult(new NodeExecutionResult(node.Id, node.Kind, "done", $"{ctx.SourceRowCount} rows · {ctx.FeatureColumns.Count} columns"));
}

public sealed class SplitHandler : IPipelineNodeHandler
{
    public NodeEngine Engine => NodeEngine.Ml;
    public NodeDescriptor Descriptor { get; } = new("split", "Split", "Train/test split",
        "Hold out a fraction of rows for honest evaluation. Place before the model.", PortKind.Table, PortKind.Table,
        [P("testFraction", "Test fraction", NodeParameterType.Number, def: "0.25")]);

    public Task<NodeExecutionResult> ExecuteAsync(PipelineContext ctx, WorkflowNode node, CancellationToken ct)
    {
        if (ctx.Mode == PipelineMode.Predict)
        {
            return Task.FromResult(new NodeExecutionResult(node.Id, node.Kind, "done", "trained on the full set for prediction"));
        }

        return Task.FromResult(new NodeExecutionResult(node.Id, node.Kind, "done",
            $"train {Count(ctx.TrainView)} · test {Count(ctx.TestView!)} ({ctx.SplitFraction:0.##} held out)"));
    }
}

public sealed class TrainHandler : IPipelineNodeHandler
{
    public NodeEngine Engine => NodeEngine.Ml;
    public NodeDescriptor Descriptor { get; } = new("train", "Model", "Train model",
        "Fit a model on the training features (ESP failure, production rate, …).", PortKind.Table, PortKind.Model,
        [P("algorithm", "Algorithm", NodeParameterType.Select, def: "sdca", options: ["sdca", "lbfgs", "fasttree", "fastforest"])]);

    public Task<NodeExecutionResult> ExecuteAsync(PipelineContext ctx, WorkflowNode node, CancellationToken ct)
    {
        var trainFeatures = ctx.NumericFeatures();
        if (trainFeatures.Length == 0)
        {
            if (ctx.Mode == PipelineMode.Predict)
            {
                throw new InvalidOperationException("Pipeline has no usable feature columns to train on.");
            }

            return Task.FromResult(new NodeExecutionResult(node.Id, node.Kind, "failed", "no usable feature columns"));
        }

        (ctx.Model, ctx.Algorithm, ctx.LabelMap) = MlModelOps.FitModel(ctx.Ml, ctx.Task, node, ctx.LabelColumn, ctx.TrainView, trainFeatures);
        return Task.FromResult(new NodeExecutionResult(node.Id, node.Kind, "done", $"{ctx.Algorithm} · {trainFeatures.Length} features"));
    }
}

public sealed class ClusterHandler : IPipelineNodeHandler
{
    public NodeEngine Engine => NodeEngine.Ml;
    public NodeDescriptor Descriptor { get; } = new("cluster", "Model", "Cluster (k-means)",
        "Unsupervised grouping — no label needed (e.g. well-log facies).", PortKind.Table, PortKind.Model,
        [P("clusters", "Clusters", NodeParameterType.Number, def: "3")]);

    public Task<NodeExecutionResult> ExecuteAsync(PipelineContext ctx, WorkflowNode node, CancellationToken ct)
    {
        if (ctx.Mode == PipelineMode.Predict)
        {
            return Task.FromResult(new NodeExecutionResult(node.Id, node.Kind, "skipped", "not used for prediction"));
        }

        var clusterFeatures = ctx.NumericFeatures();
        if (clusterFeatures.Length == 0)
        {
            return Task.FromResult(new NodeExecutionResult(node.Id, node.Kind, "skipped", "no numeric features"));
        }

        var k = Math.Clamp((int)ReadDouble(Cfg(node, "clusters"), 3), 2, 20);
        var clusterModel = ctx.Ml.Transforms.Concatenate("Features", clusterFeatures)
            .Append(ctx.Ml.Clustering.Trainers.KMeans("Features", numberOfClusters: k))
            .Fit(ctx.TrainView);
        var clustered = clusterModel.Transform(ctx.TrainView);
        var cm = ctx.Ml.Clustering.Evaluate(clustered, scoreColumnName: "Score", featureColumnName: "Features");
        return Task.FromResult(new NodeExecutionResult(node.Id, node.Kind, "done", $"{k} clusters · avg distance {cm.AverageDistance:0.###} · DBI {cm.DaviesBouldinIndex:0.###}"));
    }
}

public sealed class CrossValidateHandler : IPipelineNodeHandler
{
    public NodeEngine Engine => NodeEngine.Ml;
    public NodeDescriptor Descriptor { get; } = new("cross-validate", "Model", "Cross-validate",
        "K-fold validation for a more honest metric.", PortKind.Table, PortKind.Metrics,
        [P("folds", "Folds", NodeParameterType.Number, def: "5")]);

    public Task<NodeExecutionResult> ExecuteAsync(PipelineContext ctx, WorkflowNode node, CancellationToken ct)
    {
        if (ctx.Mode == PipelineMode.Predict)
        {
            return Task.FromResult(new NodeExecutionResult(node.Id, node.Kind, "skipped", "not used for prediction"));
        }

        var ml = ctx.Ml;
        var trainFeatures = ctx.NumericFeatures();
        var folds = Math.Clamp((int)ReadDouble(Cfg(node, "folds"), 5), 2, 10);

        if (ctx.Task == MlTaskType.MulticlassClassification)
        {
            var (mcTrainer, mcName) = MlModelOps.MulticlassTrainer(ml, node);
            var mcEst = ml.Transforms.Conversion.MapValueToKey("Label", ctx.LabelColumn)
                .Append(ml.Transforms.Concatenate("Features", trainFeatures))
                .Append(mcTrainer);
            var cv = ml.MulticlassClassification.CrossValidate(ctx.TrainView, mcEst, numberOfFolds: folds, labelColumnName: "Label");
            return Task.FromResult(new NodeExecutionResult(node.Id, node.Kind, "done", $"{folds}-fold {mcName} · mean micro-acc {cv.Average(r => r.Metrics.MicroAccuracy):0.###}"));
        }

        var (trainer, name) = MlModelOps.Trainer(ml, ctx.Task, node, ctx.LabelColumn);
        var est = ml.Transforms.Concatenate("Features", trainFeatures).Append(trainer);
        if (ctx.Task == MlTaskType.Regression)
        {
            var cv = ml.Regression.CrossValidate(ctx.TrainView, est, numberOfFolds: folds, labelColumnName: ctx.LabelColumn);
            return Task.FromResult(new NodeExecutionResult(node.Id, node.Kind, "done", $"{folds}-fold {name} · mean R² {cv.Average(r => r.Metrics.RSquared):0.###}"));
        }

        var cvb = ml.BinaryClassification.CrossValidateNonCalibrated(ctx.TrainView, est, numberOfFolds: folds, labelColumnName: ctx.LabelColumn);
        return Task.FromResult(new NodeExecutionResult(node.Id, node.Kind, "done", $"{folds}-fold {name} · mean accuracy {cvb.Average(r => r.Metrics.Accuracy):0.###}"));
    }
}

public sealed class ScoreHandler : IPipelineNodeHandler
{
    public NodeEngine Engine => NodeEngine.Ml;
    public NodeDescriptor Descriptor { get; } = new("score", "Evaluate", "Score",
        "Apply the trained model to the held-out set.", PortKind.Model, PortKind.Table, []);

    public Task<NodeExecutionResult> ExecuteAsync(PipelineContext ctx, WorkflowNode node, CancellationToken ct)
    {
        if (ctx.Mode == PipelineMode.Predict)
        {
            return Task.FromResult(new NodeExecutionResult(node.Id, node.Kind, "skipped", "handled by the prediction step"));
        }

        if (ctx.Model is null)
        {
            return Task.FromResult(new NodeExecutionResult(node.Id, node.Kind, "skipped", "no trained model upstream"));
        }

        var scored = ctx.Model.Transform(ctx.ApplyPreprocessors(ctx.TestView!));
        return Task.FromResult(new NodeExecutionResult(node.Id, node.Kind, "done", $"{Count(scored)} rows scored"));
    }
}

public sealed class EvaluateHandler : IPipelineNodeHandler
{
    public NodeEngine Engine => NodeEngine.Ml;
    public NodeDescriptor Descriptor { get; } = new("evaluate", "Evaluate", "Evaluate",
        "Compute metrics on the held-out set.", PortKind.Table, PortKind.Metrics, []);

    public Task<NodeExecutionResult> ExecuteAsync(PipelineContext ctx, WorkflowNode node, CancellationToken ct)
    {
        if (ctx.Mode == PipelineMode.Predict)
        {
            return Task.FromResult(new NodeExecutionResult(node.Id, node.Kind, "skipped", "handled by the prediction step"));
        }

        if (ctx.Model is null)
        {
            return Task.FromResult(new NodeExecutionResult(node.Id, node.Kind, "skipped", "no trained model upstream"));
        }

        var ml = ctx.Ml;
        var prepared = ctx.ApplyPreprocessors(ctx.TestView!);
        var withLabel = ctx.LabelMap is null ? prepared : ctx.LabelMap.Transform(prepared);
        var scored = ctx.Model.Transform(withLabel);
        NodeExecutionResult result;
        if (ctx.Task == MlTaskType.Regression)
        {
            var m = ml.Regression.Evaluate(scored, labelColumnName: ctx.LabelColumn);
            ctx.PrimaryValue = m.RSquared;
            result = new NodeExecutionResult(node.Id, node.Kind, "done", $"R² {m.RSquared:0.###} · RMSE {m.RootMeanSquaredError:0.###}");
        }
        else if (ctx.Task == MlTaskType.MulticlassClassification)
        {
            var m = ml.MulticlassClassification.Evaluate(scored, labelColumnName: "Label");
            ctx.PrimaryValue = m.MicroAccuracy;
            result = new NodeExecutionResult(node.Id, node.Kind, "done", $"MicroAcc {m.MicroAccuracy:0.###} · MacroAcc {m.MacroAccuracy:0.###}");
        }
        else
        {
            var m = ml.BinaryClassification.EvaluateNonCalibrated(scored, labelColumnName: ctx.LabelColumn);
            ctx.PrimaryValue = m.Accuracy;
            result = new NodeExecutionResult(node.Id, node.Kind, "done", $"Accuracy {m.Accuracy:0.###} · AUC {m.AreaUnderRocCurve:0.###}");
        }

        return Task.FromResult(result);
    }
}
