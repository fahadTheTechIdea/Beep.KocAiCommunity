using System.Text;
using Beep.KocAiCommunity.Application.ML;
using Beep.KocAiCommunity.Application.Workflow;
using Beep.KocAiCommunity.Contracts.Workflow;
using Beep.KocAiCommunity.Workflow;
using Microsoft.ML;
using Microsoft.ML.AutoML;

namespace Beep.KocAiCommunity.ML.Nodes;

/// <summary>
/// Executes a workflow by dispatching each node (in topological order) to its registered
/// <see cref="IPipelineNodeHandler"/> over a shared <see cref="PipelineContext"/>. Replaces the
/// monolithic switch-based executor; behavior is preserved for the ML handlers.
/// </summary>
public sealed class PluginNodeExecutor(PluginNodeRegistry registry) : IPipelineExecutor
{
    public async Task<PipelineExecutionResult> ExecuteAsync(WorkflowDefinition definition, string labelColumn, MlTaskType task, Stream csv, int maxSeconds, CancellationToken ct = default)
    {
        var compiled = WorkflowCompiler.Compile(definition);
        if (!compiled.IsValid)
        {
            return new PipelineExecutionResult(false, null, null, 0,
                [new NodeExecutionResult("", "compile", "failed", string.Join(" ", compiled.Errors))]);
        }

        var tempPath = await SpillAsync(csv, ct);
        try
        {
            return await Task.Run(() => Run(definition, compiled.Order, labelColumn, task, tempPath), ct);
        }
        finally
        {
            Cleanup(tempPath);
        }
    }

    public async Task<string> PredictAsync(WorkflowDefinition definition, string labelColumn, string idColumn, MlTaskType task, Stream trainingCsv, Stream evaluationCsv, CancellationToken ct = default)
    {
        var compiled = WorkflowCompiler.Compile(definition);
        if (!compiled.IsValid)
        {
            throw new InvalidOperationException($"Pipeline is not valid: {string.Join(" ", compiled.Errors)}");
        }

        var trainPath = await SpillAsync(trainingCsv, ct);
        var evalPath = await SpillAsync(evaluationCsv, ct);
        try
        {
            return await Task.Run(() => Predict(definition, compiled.Order, labelColumn, idColumn, task, trainPath, evalPath), ct);
        }
        finally
        {
            Cleanup(trainPath);
            Cleanup(evalPath);
        }
    }

    private PipelineExecutionResult Run(WorkflowDefinition definition, IReadOnlyList<string> order, string labelColumn, MlTaskType task, string path)
    {
        var ml = new MLContext(seed: 1);
        var byId = definition.Nodes.ToDictionary(n => n.Id, StringComparer.Ordinal);

        var columns = ml.Auto().InferColumns(path, labelColumnName: labelColumn, groupColumns: false);
        var full = ml.Data.CreateTextLoader(columns.TextLoaderOptions).Load(path);

        var featureCols = full.Schema.Select(c => c.Name).Where(n => n != labelColumn).ToList();
        var splitFraction = ReadSplitFraction(byId, order);
        var split = ml.Data.TrainTestSplit(full, testFraction: splitFraction, seed: 1);

        var ctx = new PipelineContext
        {
            Ml = ml,
            Task = task,
            Mode = PipelineMode.Execute,
            LabelColumn = labelColumn,
            SourceRowCount = PipelineContext.Count(full),
            SplitFraction = splitFraction,
            TrainView = split.TrainSet,
            TestView = split.TestSet,
            FeatureColumns = featureCols,
        };

        var primaryMetric = task switch
        {
            MlTaskType.Regression => "RSquared",
            MlTaskType.MulticlassClassification => "MicroAccuracy",
            _ => "Accuracy",
        };

        foreach (var nodeId in order)
        {
            var node = byId[nodeId];
            NodeExecutionResult result;
            try
            {
                var handler = registry.Handler(node.Kind)
                    ?? throw new InvalidOperationException($"No handler for node kind '{node.Kind}'.");
                result = handler.ExecuteAsync(ctx, node, CancellationToken.None).GetAwaiter().GetResult();
            }
            catch (Exception ex)
            {
                ctx.Results.Add(new NodeExecutionResult(nodeId, node.Kind, "failed", ex.Message));
                return new PipelineExecutionResult(false, ctx.Algorithm, primaryMetric, ctx.PrimaryValue, ctx.Results);
            }

            ctx.Results.Add(result);
            if (result.Status == "failed")
            {
                return new PipelineExecutionResult(false, ctx.Algorithm, primaryMetric, ctx.PrimaryValue, ctx.Results);
            }
        }

        return new PipelineExecutionResult(true, ctx.Algorithm, primaryMetric, ctx.PrimaryValue, ctx.Results);
    }

    private string Predict(WorkflowDefinition definition, IReadOnlyList<string> order, string labelColumn, string idColumn, MlTaskType task, string trainPath, string evalPath)
    {
        var ml = new MLContext(seed: 1);
        var byId = definition.Nodes.ToDictionary(n => n.Id, StringComparer.Ordinal);

        var columns = ml.Auto().InferColumns(trainPath, labelColumnName: labelColumn, groupColumns: false);
        var full = ml.Data.CreateTextLoader(columns.TextLoaderOptions).Load(trainPath);

        // The id column is not a feature — it must never leak into the model.
        var featureCols = full.Schema.Select(c => c.Name).Where(n => n != labelColumn && n != idColumn).ToList();

        var ctx = new PipelineContext
        {
            Ml = ml,
            Task = task,
            Mode = PipelineMode.Predict,
            LabelColumn = labelColumn,
            IdColumn = idColumn,
            SourceRowCount = PipelineContext.Count(full),
            TrainView = full,
            FeatureColumns = featureCols,
        };

        foreach (var nodeId in order)
        {
            var node = byId[nodeId];
            var handler = registry.Handler(node.Kind)
                ?? throw new InvalidOperationException($"No handler for node kind '{node.Kind}'.");
            handler.ExecuteAsync(ctx, node, CancellationToken.None).GetAwaiter().GetResult();
        }

        if (ctx.Model is null)
        {
            throw new InvalidOperationException("Pipeline has no train node, so it cannot produce predictions.");
        }

        // Ids are read straight from the evaluation file so their formatting and order are preserved.
        var ids = MlModelOps.ReadColumn(evalPath, idColumn);
        var evalColumns = ml.Auto().InferColumns(evalPath, labelColumnName: idColumn, groupColumns: false);
        var evalData = ml.Data.CreateTextLoader(evalColumns.TextLoaderOptions).Load(evalPath);
        var scored = ctx.Model.Transform(ctx.ApplyPreprocessors(evalData));
        var predictions = MlModelOps.ReadPredictions(scored, task);

        var count = Math.Min(ids.Count, predictions.Count);
        var sb = new StringBuilder("id,prediction\n");
        for (var i = 0; i < count; i++)
        {
            sb.Append(ids[i]).Append(',').Append(predictions[i]).Append('\n');
        }

        return sb.ToString();
    }

    private static double ReadSplitFraction(IReadOnlyDictionary<string, WorkflowNode> byId, IEnumerable<string> order)
    {
        foreach (var id in order)
        {
            if (byId[id].Kind.Equals("split", StringComparison.OrdinalIgnoreCase))
            {
                return Math.Clamp(PipelineContext.ReadDouble(PipelineContext.Cfg(byId[id], "testFraction"), 0.25), 0.05, 0.9);
            }
        }

        return 0.25;
    }

    private static async Task<string> SpillAsync(Stream source, CancellationToken ct)
    {
        var path = Path.Combine(Path.GetTempPath(), $"koc-pipe-{Guid.NewGuid():N}.csv");
        await using var file = File.Create(path);
        await source.CopyToAsync(file, ct);
        return path;
    }

    private static void Cleanup(string path)
    {
        try { File.Delete(path); } catch (IOException) { /* best effort */ }
    }
}
