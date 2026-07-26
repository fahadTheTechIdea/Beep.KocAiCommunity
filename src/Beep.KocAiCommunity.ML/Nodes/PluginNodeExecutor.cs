using System.Text;
using Beep.KocAiCommunity.Application.Common;
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
                var handler = HandlerFor(node);
                ValidateNodeInputs(handler.Descriptor, node, table, ctx);
                result = handler.Execute(ctx, node, table);
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
            var handler = HandlerFor(node);
            ValidateNodeInputs(handler.Descriptor, node, table, ctx);
            var result = handler.Execute(ctx, node, table);
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

        // Read the ids from the SAME (possibly replayed) table that produces the predictions, so id and
        // prediction stay row-aligned even when a replayed step changed the eval rows. If a step dropped
        // the id column or the counts diverge, fail loudly rather than silently mis-pair with Math.Min.
        var ids = MlModelOps.ReadColumn(evalTable.CsvPath, idColumn);
        var scored = ctx.Model.Transform(evalTable.LoadIntoMl(ctx.Ml, idColumn));

        // For binary tasks, echo the training label's own token convention (1/0, yes/no, …) so the
        // submission matches the competition's answer key rather than a hardcoded true/false.
        var binaryTokens = task == MlTaskType.BinaryClassification
            ? MlModelOps.BinaryLabelTokens(trainPath, labelColumn)
            : null;
        var predictions = MlModelOps.ReadPredictions(scored, task, binaryTokens);

        if (ids.Count != predictions.Count)
        {
            throw new InvalidOperationException(
                $"Prediction alignment failed: {ids.Count} ids but {predictions.Count} predictions. "
                + "A row-changing step (e.g. a filtering SQL node) must preserve the id column so predictions stay aligned.");
        }

        var sb = new StringBuilder("id,prediction\n");
        for (var i = 0; i < ids.Count; i++)
        {
            sb.Append(KocCsv.QuoteField(ids[i])).Append(',').Append(KocCsv.QuoteField(predictions[i])).Append('\n');
        }

        return sb.ToString();
    }

    private IPipelineNodeHandler HandlerFor(WorkflowNode node)
        => registry.Handler(node.Kind) ?? throw new InvalidOperationException($"No handler for node kind '{node.Kind}'.");

    /// <summary>
    /// Fails a node loudly when its runtime-scoped parameters don't resolve — a <c>Columns</c> value that
    /// names a column not in the table it operates on, or a <c>Dataset</c> value that can't be loaded.
    /// These slip past the static (publish-time) validator, which can't see the flowing data, and would
    /// otherwise be silently dropped (wrong result) or silently skipped. Blank values are left to the
    /// handler's own "blank = all / no-op" semantics.
    /// </summary>
    private static void ValidateNodeInputs(NodeDescriptor descriptor, WorkflowNode node, PipelineTable table, PipelineContext ctx)
    {
        foreach (var p in descriptor.Parameters)
        {
            var raw = PipelineContext.Cfg(node, p.Name);
            if (string.IsNullOrWhiteSpace(raw))
            {
                continue;
            }

            if (p.Type == NodeParameterType.Dataset)
            {
                if (!Guid.TryParse(raw, out var id) || ctx.SecondaryTable(id) is null)
                {
                    throw new InvalidOperationException(
                        $"Node '{node.Kind}': the dataset selected for '{p.DisplayName}' could not be loaded. "
                        + "Attach the dataset to the run, or clear the selection.");
                }

                continue;
            }

            if (p.Type == NodeParameterType.Columns)
            {
                var scope = ColumnScope(descriptor.Kind, p.Name, node, table, ctx);
                var missing = PipelineContext.SplitList(raw).Where(c => !scope.Contains(c)).ToList();
                if (missing.Count > 0)
                {
                    throw new InvalidOperationException(
                        $"Node '{node.Kind}': column(s) not found for '{p.DisplayName}': {string.Join(", ", missing)}. "
                        + $"Available columns: {string.Join(", ", scope)}.");
                }
            }
        }
    }

    /// <summary>The columns a <c>Columns</c> parameter is checked against — normally the input table, but
    /// the join node's column picker names columns of the <em>joined</em> dataset, so it resolves there.</summary>
    private static IReadOnlyCollection<string> ColumnScope(string kind, string paramName, WorkflowNode node, PipelineTable table, PipelineContext ctx)
    {
        if (kind == "join-dataset" && paramName == "columns"
            && Guid.TryParse(PipelineContext.Cfg(node, "datasetId"), out var id)
            && ctx.SecondaryTable(id) is { } joined)
        {
            return [.. ctx.Duck.Columns(joined)];
        }

        return [.. table.Columns];
    }

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
        var path = PipelineTemp.New();
        await using var file = File.Create(path);
        await source.CopyToAsync(file, ct);
        return path;
    }

    private static void Cleanup(string path) => PipelineTemp.Delete(path);
}
