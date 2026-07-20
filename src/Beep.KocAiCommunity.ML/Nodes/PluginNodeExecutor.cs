using System.Text;
using Beep.KocAiCommunity.Application.ML;
using Beep.KocAiCommunity.Application.Workflow;
using Beep.KocAiCommunity.Contracts.Workflow;
using Beep.KocAiCommunity.Workflow;
using Microsoft.ML;

namespace Beep.KocAiCommunity.ML.Nodes;

/// <summary>
/// Executes a workflow by threading one uniform <see cref="PipelineTable"/> through every node's
/// handler in topological order. DuckDB and ML.NET nodes speak the same table contract, so they are
/// interchangeable and freely ordered. Column-shaping steps are recorded and replayed on the
/// evaluation set at predict time; row-shaping steps are not.
/// </summary>
public sealed class PluginNodeExecutor(PluginNodeRegistry registry) : IPipelineExecutor
{
    public async Task<PipelineExecutionResult> ExecuteAsync(WorkflowDefinition definition, string labelColumn, MlTaskType task, Stream csv, int maxSeconds, IReadOnlyDictionary<Guid, Stream>? secondaryDatasets = null, CancellationToken ct = default)
    {
        var compiled = WorkflowCompiler.Compile(definition);
        if (!compiled.IsValid)
        {
            return new PipelineExecutionResult(false, null, null, 0,
                [new NodeExecutionResult("", "compile", "failed", string.Join(" ", compiled.Errors))]);
        }

        var secondary = await ReadSecondaryAsync(secondaryDatasets, ct);
        var path = await SpillAsync(csv, ct);
        try
        {
            return await Task.Run(() => Run(definition, compiled.Order, labelColumn, task, path, secondary), ct);
        }
        finally
        {
            Cleanup(path);
        }
    }

    public async Task<string> PredictAsync(WorkflowDefinition definition, string labelColumn, string idColumn, MlTaskType task, Stream trainingCsv, Stream evaluationCsv, IReadOnlyDictionary<Guid, Stream>? secondaryDatasets = null, CancellationToken ct = default)
    {
        var compiled = WorkflowCompiler.Compile(definition);
        if (!compiled.IsValid)
        {
            throw new InvalidOperationException($"Pipeline is not valid: {string.Join(" ", compiled.Errors)}");
        }

        var secondary = await ReadSecondaryAsync(secondaryDatasets, ct);
        var trainPath = await SpillAsync(trainingCsv, ct);
        var evalPath = await SpillAsync(evaluationCsv, ct);
        try
        {
            return await Task.Run(() => Predict(definition, compiled.Order, labelColumn, idColumn, task, trainPath, evalPath, secondary), ct);
        }
        finally
        {
            Cleanup(trainPath);
            Cleanup(evalPath);
        }
    }

    private PipelineExecutionResult Run(WorkflowDefinition definition, IReadOnlyList<string> order, string labelColumn, MlTaskType task, string path, IReadOnlyDictionary<Guid, byte[]> secondary)
    {
        var byId = definition.Nodes.ToDictionary(n => n.Id, StringComparer.Ordinal);
        using var ctx = new PipelineContext
        {
            Ml = new MLContext(seed: 1),
            Task = task,
            Mode = PipelineMode.Execute,
            LabelColumn = labelColumn,
            SecondaryDatasets = secondary,
        };

        var primaryMetric = task switch
        {
            MlTaskType.Regression => "RSquared",
            MlTaskType.MulticlassClassification => "MicroAccuracy",
            _ => "Accuracy",
        };

        var table = PipelineTable.FromCsvFile(path);
        foreach (var nodeId in order)
        {
            var node = byId[nodeId];
            NodeResult result;
            try
            {
                result = HandlerFor(node).Execute(ctx, node, table);
            }
            catch (Exception ex)
            {
                ctx.Results.Add(new NodeExecutionResult(nodeId, node.Kind, "failed", ex.Message));
                return new PipelineExecutionResult(false, ctx.Algorithm, primaryMetric, ctx.PrimaryValue, ctx.Results);
            }

            ctx.Results.Add(result.Status);
            if (result.Status.Status == "failed")
            {
                return new PipelineExecutionResult(false, ctx.Algorithm, primaryMetric, ctx.PrimaryValue, ctx.Results);
            }

            if (result.Output is not null)
            {
                table = result.Output;
            }
        }

        return new PipelineExecutionResult(true, ctx.Algorithm, primaryMetric, ctx.PrimaryValue, ctx.Results);
    }

    private string Predict(WorkflowDefinition definition, IReadOnlyList<string> order, string labelColumn, string idColumn, MlTaskType task, string trainPath, string evalPath, IReadOnlyDictionary<Guid, byte[]> secondary)
    {
        var byId = definition.Nodes.ToDictionary(n => n.Id, StringComparer.Ordinal);
        using var ctx = new PipelineContext
        {
            Ml = new MLContext(seed: 1),
            Task = task,
            Mode = PipelineMode.Predict,
            LabelColumn = labelColumn,
            IdColumn = idColumn,
            SecondaryDatasets = secondary,
        };

        // Train pass: run the graph on the full training set, building the model and recording the
        // column-shaping steps (fitted transformers + deterministic SQL) to replay on the eval set.
        var table = PipelineTable.FromCsvFile(trainPath);
        foreach (var nodeId in order)
        {
            var node = byId[nodeId];
            var result = HandlerFor(node).Execute(ctx, node, table);
            if (result.Output is not null)
            {
                table = result.Output;
            }

            if (result.Replay is not null)
            {
                ctx.Steps.Add(result.Replay);
            }
        }

        if (ctx.Model is null)
        {
            throw new InvalidOperationException("Pipeline has no train node, so it cannot produce predictions.");
        }

        // Replay the recorded steps on the evaluation set, then apply the trained model.
        var evalTable = PipelineTable.FromCsvFile(evalPath);
        foreach (var step in ctx.Steps)
        {
            evalTable = step switch
            {
                TransformReplay(var transformer) => PipelineTable.FromMlView(transformer.Transform(evalTable.LoadIntoMl(ctx.Ml, idColumn)), ctx.TempFiles),
                DataReplay(var dnode, var handler) => handler.Execute(ctx, dnode, evalTable).Output ?? evalTable,
                _ => evalTable,
            };
        }

        var ids = MlModelOps.ReadColumn(evalPath, idColumn);
        var scored = ctx.Model.Transform(evalTable.LoadIntoMl(ctx.Ml, idColumn));
        var predictions = MlModelOps.ReadPredictions(scored, task);

        var count = Math.Min(ids.Count, predictions.Count);
        var sb = new StringBuilder("id,prediction\n");
        for (var i = 0; i < count; i++)
        {
            sb.Append(ids[i]).Append(',').Append(predictions[i]).Append('\n');
        }

        return sb.ToString();
    }

    private IPipelineNodeHandler HandlerFor(WorkflowNode node)
        => registry.Handler(node.Kind) ?? throw new InvalidOperationException($"No handler for node kind '{node.Kind}'.");

    private static async Task<IReadOnlyDictionary<Guid, byte[]>> ReadSecondaryAsync(IReadOnlyDictionary<Guid, Stream>? secondary, CancellationToken ct)
    {
        if (secondary is null || secondary.Count == 0)
        {
            return new Dictionary<Guid, byte[]>();
        }

        var map = new Dictionary<Guid, byte[]>();
        foreach (var (id, stream) in secondary)
        {
            using var ms = new MemoryStream();
            await stream.CopyToAsync(ms, ct);
            map[id] = ms.ToArray();
        }

        return map;
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
