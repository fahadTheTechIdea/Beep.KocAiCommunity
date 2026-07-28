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
    public async Task<PipelineExecutionResult> ExecuteAsync(WorkflowDefinition definition, string? labelColumn, MlTaskType? task, Stream csv, int maxSeconds, IReadOnlyDictionary<Guid, Stream>? secondaryDatasets = null, CancellationToken ct = default)
    {
        var compiled = WorkflowCompiler.Compile(definition);
        if (!compiled.IsValid)
        {
            return new PipelineExecutionResult(false, null, null, 0,
                [new NodeExecutionResult("", "compile", "failed", string.Join(" ", compiled.Errors))]);
        }

        var label = ResolveLabel(definition, labelColumn);
        var id = TrainCfg(definition, "idColumn"); // exclude the id from features in preview iff the graph declares one
        var resolvedTask = ResolveTask(definition, task);
        var secondary = await ReadSecondaryAsync(secondaryDatasets, ct);
        var path = await SpillAsync(csv, ct);
        try
        {
            return await Task.Run(() => Run(definition, compiled.Order, label, id, resolvedTask, path, secondary, ct), ct);
        }
        finally
        {
            Cleanup(path);
        }
    }

    public async Task<string> PredictAsync(WorkflowDefinition definition, string? labelColumn, string? idColumn, MlTaskType? task, Stream trainingCsv, Stream evaluationCsv, IReadOnlyDictionary<Guid, Stream>? secondaryDatasets = null, CancellationToken ct = default)
    {
        var compiled = WorkflowCompiler.Compile(definition);
        if (!compiled.IsValid)
        {
            throw new InvalidOperationException($"Pipeline is not valid: {string.Join(" ", compiled.Errors)}");
        }

        var label = ResolveLabel(definition, labelColumn);
        var id = ResolveId(definition, idColumn);
        var resolvedTask = ResolveTask(definition, task);
        var secondary = await ReadSecondaryAsync(secondaryDatasets, ct);
        var trainPath = await SpillAsync(trainingCsv, ct);
        var evalPath = await SpillAsync(evaluationCsv, ct);
        try
        {
            return await Task.Run(() => Predict(definition, compiled.Order, label, id, resolvedTask, trainPath, evalPath, secondary, ct), ct);
        }
        finally
        {
            Cleanup(trainPath);
            Cleanup(evalPath);
        }
    }

    // The graph is the single source of truth for the control facts: label/id/task default to what the Train
    // node declares (targetColumn/idColumn/task), so a saved WorkflowDefinition drives itself end to end. An
    // explicit argument (e.g. a competition's authoritative values) overrides.
    internal static string ResolveLabel(WorkflowDefinition definition, string? overrideValue) =>
        !string.IsNullOrWhiteSpace(overrideValue) ? overrideValue : TrainCfg(definition, "targetColumn") ?? "label";

    internal static string ResolveId(WorkflowDefinition definition, string? overrideValue) =>
        !string.IsNullOrWhiteSpace(overrideValue) ? overrideValue : TrainCfg(definition, "idColumn") ?? "id";

    internal static MlTaskType ResolveTask(WorkflowDefinition definition, MlTaskType? overrideValue) =>
        overrideValue ?? (Enum.TryParse<MlTaskType>(TrainCfg(definition, "task"), out var t) ? t : MlTaskType.BinaryClassification);

    private static string? TrainCfg(WorkflowDefinition definition, string key) =>
        definition.Nodes.FirstOrDefault(n => string.Equals(n.Kind, "train", StringComparison.OrdinalIgnoreCase))?.Config is { } c
        && c.TryGetValue(key, out var v) && !string.IsNullOrWhiteSpace(v) ? v : null;

    private PipelineExecutionResult Run(WorkflowDefinition definition, IReadOnlyList<string> order, string labelColumn, string? idColumn, MlTaskType task, string path, IReadOnlyDictionary<Guid, byte[]> secondary, CancellationToken ct = default)
    {
        var byId = definition.Nodes.ToDictionary(n => n.Id, StringComparer.Ordinal);
        using var ctx = new PipelineContext
        {
            Ml = new MLContext(seed: 1),
            Task = task,
            Mode = PipelineMode.Execute,
            LabelColumn = labelColumn,
            IdColumn = idColumn,
            SecondaryDatasets = secondary,
        };

        var primaryMetric = task switch
        {
            MlTaskType.Regression => "RSquared",
            MlTaskType.MulticlassClassification => "MicroAccuracy",
            MlTaskType.AnomalyDetection => "AUC",
            _ => "Accuracy",
        };

        var table = PipelineTable.FromCsvFile(path);
        var rowCount = table.RowCount; // the dataset's row count, recorded on the run
        foreach (var nodeId in order)
        {
            ct.ThrowIfCancellationRequested(); // bound runaway graphs at node boundaries
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
                return new PipelineExecutionResult(false, ctx.Algorithm, primaryMetric, ctx.PrimaryValue, ctx.Results, rowCount);
            }

            ctx.Results.Add(result.Status);
            if (result.Status.Status == "failed")
            {
                return new PipelineExecutionResult(false, ctx.Algorithm, primaryMetric, ctx.PrimaryValue, ctx.Results, rowCount);
            }

            if (result.Output is not null)
            {
                table = result.Output;
            }
        }

        return new PipelineExecutionResult(true, ctx.Algorithm, primaryMetric, ctx.PrimaryValue, ctx.Results, rowCount);
    }

    private string Predict(WorkflowDefinition definition, IReadOnlyList<string> order, string labelColumn, string idColumn, MlTaskType task, string trainPath, string evalPath, IReadOnlyDictionary<Guid, byte[]> secondary, CancellationToken ct = default)
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
            ct.ThrowIfCancellationRequested(); // bound runaway graphs at node boundaries
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
                TransformReplay(var transformer) => PipelineTable.FromMlView(transformer.Transform(evalTable.LoadIntoMl(ctx.Ml, idColumn, forceTextColumn: idColumn)), ctx.TempFiles),
                DataReplay(var dnode, var handler) => handler.Execute(ctx, dnode, evalTable).Output ?? evalTable,
                _ => evalTable,
            };
        }

        // The id column must survive the pipeline: ReadColumn falls back to column 0 when the named id is
        // absent, so a replayed step that dropped or renamed the id would silently emit the wrong ids with
        // a matching row count (slipping past the count guard below). Fail loudly instead.
        if (!evalTable.HasColumn(idColumn))
        {
            throw new InvalidOperationException(
                $"The id column '{idColumn}' is no longer in the evaluation data after the pipeline ran. A "
                + "replayed column step (drop-columns, a SQL SELECT that omits the id, or renaming it) removed "
                + "it, so predictions can't be aligned to the answer key. Keep the id column through the pipeline.");
        }

        // Read the ids from the SAME (possibly replayed) table that produces the predictions, so id and
        // prediction stay row-aligned even when a replayed step changed the eval rows.
        var ids = MlModelOps.ReadColumn(evalTable.CsvPath, idColumn);
        var scored = ctx.Model.Transform(evalTable.LoadIntoMl(ctx.Ml, idColumn, forceTextColumn: idColumn));

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

        // A fan-out join (a merged dataset with duplicate keys) multiplies evaluation rows, so the same id
        // appears more than once. Counts still match, so the alignment guard above passes — but the scorers
        // align by id and would silently keep only the last prediction per id. Fail loudly instead: the
        // submission must carry exactly one prediction per evaluation id.
        var duplicateIds = ids
            .GroupBy(x => x, StringComparer.OrdinalIgnoreCase)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key)
            .Take(5)
            .ToList();
        if (duplicateIds.Count > 0)
        {
            throw new InvalidOperationException(
                "The submission has duplicate ids, so a merge/join multiplied evaluation rows: "
                + string.Join(", ", duplicateIds)
                + ". Join on a unique key (or de-duplicate the merged dataset) so each id yields exactly one prediction.");
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
                // The primary 'dataset' source node's datasetId is the main input (opened as the training
                // stream by the caller), not a secondary to resolve here — skip it.
                if (string.Equals(descriptor.Kind, "dataset", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

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
