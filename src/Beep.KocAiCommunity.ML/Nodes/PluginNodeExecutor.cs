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
    public async Task<PipelineExecutionResult> ExecuteAsync(WorkflowDefinition definition, string labelColumn, MlTaskType task, Stream csv, int maxSeconds, IReadOnlyDictionary<Guid, Stream>? secondaryDatasets = null, CancellationToken ct = default)
    {
        var compiled = WorkflowCompiler.Compile(definition);
        if (!compiled.IsValid)
        {
            return new PipelineExecutionResult(false, null, null, 0,
                [new NodeExecutionResult("", "compile", "failed", string.Join(" ", compiled.Errors))]);
        }

        var secondary = await ReadSecondaryAsync(secondaryDatasets, ct);
        var tempPath = await SpillAsync(csv, ct);
        try
        {
            return await Task.Run(() => Run(definition, compiled.Order, labelColumn, task, tempPath, secondary), ct);
        }
        finally
        {
            Cleanup(tempPath);
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

    private PipelineExecutionResult Run(WorkflowDefinition definition, IReadOnlyList<string> order, string labelColumn, MlTaskType task, string path, IReadOnlyDictionary<Guid, byte[]> secondary)
    {
        var byId = definition.Nodes.ToDictionary(n => n.Id, StringComparer.Ordinal);
        using var ctx = NewContext(definition, order, byId, labelColumn, null, task, PipelineMode.Execute, path, secondary);

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
                var handler = HandlerFor(node);
                Cross(ctx, handler.Engine);
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

    private string Predict(WorkflowDefinition definition, IReadOnlyList<string> order, string labelColumn, string idColumn, MlTaskType task, string trainPath, string evalPath, IReadOnlyDictionary<Guid, byte[]> secondary)
    {
        var byId = definition.Nodes.ToDictionary(n => n.Id, StringComparer.Ordinal);
        using var ctx = NewContext(definition, order, byId, labelColumn, idColumn, task, PipelineMode.Predict, trainPath, secondary);

        foreach (var nodeId in order)
        {
            var node = byId[nodeId];
            var handler = HandlerFor(node);
            Cross(ctx, handler.Engine);
            handler.ExecuteAsync(ctx, node, CancellationToken.None).GetAwaiter().GetResult();
        }

        if (ctx.Model is null)
        {
            throw new InvalidOperationException("Pipeline has no train node, so it cannot produce predictions.");
        }

        // The eval set runs through the same DuckDB data-prep, then the ML preprocessors + model.
        var evalPrepared = PrepareEvalData(ctx, definition, order, byId, idColumn, evalPath, secondary);
        var ids = MlModelOps.ReadColumn(evalPrepared, idColumn);
        var evalColumns = ctx.Ml.Auto().InferColumns(evalPrepared, labelColumnName: idColumn, groupColumns: false);
        var evalData = ctx.Ml.Data.CreateTextLoader(evalColumns.TextLoaderOptions).Load(evalPrepared);
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

    // ---- Context setup + engine crossing ----

    private PipelineContext NewContext(WorkflowDefinition definition, IReadOnlyList<string> order, IReadOnlyDictionary<string, WorkflowNode> byId,
        string labelColumn, string? idColumn, MlTaskType task, PipelineMode mode, string path, IReadOnlyDictionary<Guid, byte[]> secondary)
    {
        var ctx = new PipelineContext
        {
            Ml = new MLContext(seed: 1),
            Task = task,
            Mode = mode,
            LabelColumn = labelColumn,
            IdColumn = idColumn,
            SplitFraction = ReadSplitFraction(byId, order),
            SourcePath = path,
        };

        // A pipeline needs DuckDB only if it has a Duck-engine node; otherwise stay ML-native (parity).
        var usesDuck = order.Any(id => registry.Handler(byId[id].Kind)?.Engine == NodeEngine.Duck);
        if (usesDuck)
        {
            ctx.Duck = new DuckDbSession();
            RegisterSecondaryTables(ctx, secondary);
            EnsureDuck(ctx);   // load the source into the DuckDB working table up front
        }
        else
        {
            EnsureMl(ctx);     // eager load + split, exactly as before
        }

        return ctx;
    }

    private IPipelineNodeHandler HandlerFor(WorkflowNode node)
        => registry.Handler(node.Kind) ?? throw new InvalidOperationException($"No handler for node kind '{node.Kind}'.");

    private void Cross(PipelineContext ctx, NodeEngine engine)
    {
        switch (engine)
        {
            case NodeEngine.Ml: EnsureMl(ctx); break;
            case NodeEngine.Duck: EnsureDuck(ctx); break;
            default: break; // Source: no representation needed
        }
    }

    private void EnsureMl(PipelineContext ctx)
    {
        if (ctx.Current == DataLocation.Ml)
        {
            return;
        }

        // Materialize from the DuckDB working table (post data-prep) if it exists, else the source CSV.
        string csvPath;
        if (ctx.Current == DataLocation.Duck && ctx.Duck is not null)
        {
            csvPath = TempCsv(ctx);
            ctx.Duck.ExportCsv(PipelineContext.WorkingTable, csvPath);
        }
        else
        {
            csvPath = ctx.SourcePath;
        }

        var ml = ctx.Ml;
        // DuckDB prep may have dropped the label (e.g. an unsupervised group-by → cluster). Infer with
        // the label only when it is still present; otherwise every column is a candidate feature.
        var header = File.ReadLines(csvPath).First().Split(',').Select(h => h.Trim()).ToList();
        var hasLabel = header.Contains(ctx.LabelColumn);
        var inferLabel = hasLabel ? ctx.LabelColumn : header[0];
        var columns = ml.Auto().InferColumns(csvPath, labelColumnName: inferLabel, groupColumns: false);
        var full = ml.Data.CreateTextLoader(columns.TextLoaderOptions).Load(csvPath);

        var featureCols = full.Schema.Select(c => c.Name)
            .Where(n => n != ctx.IdColumn && (!hasLabel || n != ctx.LabelColumn))
            .ToList();
        SetSourceMetaIfUnset(ctx, PipelineContext.Count(full), featureCols);
        ctx.FeatureColumns = featureCols;

        if (ctx.Mode == PipelineMode.Execute)
        {
            var split = ml.Data.TrainTestSplit(full, testFraction: ctx.SplitFraction, seed: 1);
            ctx.TrainView = split.TrainSet;
            ctx.TestView = split.TestSet;
        }
        else
        {
            ctx.TrainView = full;
        }

        ctx.Current = DataLocation.Ml;
    }

    private void EnsureDuck(PipelineContext ctx)
    {
        if (ctx.Current == DataLocation.Duck)
        {
            return;
        }

        var duck = ctx.Duck ??= new DuckDbSession();
        if (ctx.Current == DataLocation.Ml)
        {
            throw new InvalidOperationException("DuckDB/SQL nodes must run before the ML.NET modelling nodes (they are the data-prep front-end).");
        }

        duck.LoadCsv(ctx.SourcePath, PipelineContext.WorkingTable);
        SetSourceMetaIfUnset(ctx, duck.RowCount(PipelineContext.WorkingTable),
            [.. duck.Columns(PipelineContext.WorkingTable).Where(c => c != ctx.LabelColumn && c != ctx.IdColumn)]);
        ctx.Current = DataLocation.Duck;
    }

    private static void SetSourceMetaIfUnset(PipelineContext ctx, long rowCount, List<string> featureCols)
    {
        if (ctx.SourceRowCount == 0)
        {
            ctx.SourceRowCount = rowCount;
        }

        if (ctx.FeatureColumns.Count == 0)
        {
            ctx.FeatureColumns = featureCols;
        }
    }

    private void RegisterSecondaryTables(PipelineContext ctx, IReadOnlyDictionary<Guid, byte[]> secondary)
    {
        if (ctx.Duck is null || secondary.Count == 0)
        {
            return;
        }

        var map = new Dictionary<Guid, string>();
        var i = 0;
        foreach (var (id, bytes) in secondary)
        {
            var tempCsv = TempCsv(ctx);
            File.WriteAllBytes(tempCsv, bytes);
            var table = $"ds_{i++}";
            ctx.Duck.LoadCsv(tempCsv, table);
            map[id] = table;
        }

        ctx.SecondaryTables = map;
    }

    // Runs the eval set through the DuckDB data-prep nodes (deterministic), returning a CSV path.
    private string PrepareEvalData(PipelineContext ctx, WorkflowDefinition definition, IReadOnlyList<string> order,
        IReadOnlyDictionary<string, WorkflowNode> byId, string idColumn, string evalPath, IReadOnlyDictionary<Guid, byte[]> secondary)
    {
        var duckNodes = order.Select(id => byId[id]).Where(n => HandlerFor(n).Engine == NodeEngine.Duck).ToList();
        if (duckNodes.Count == 0)
        {
            return evalPath;
        }

        using var evalDuck = new DuckDbSession();
        var evalCtx = new PipelineContext
        {
            Ml = ctx.Ml,
            Task = ctx.Task,
            Mode = PipelineMode.Predict,
            LabelColumn = ctx.LabelColumn,
            IdColumn = idColumn,
            Duck = evalDuck,
            SourcePath = evalPath,
        };
        RegisterSecondaryTables(evalCtx, secondary);
        evalDuck.LoadCsv(evalPath, PipelineContext.WorkingTable);
        evalCtx.Current = DataLocation.Duck;

        foreach (var node in duckNodes)
        {
            HandlerFor(node).ExecuteAsync(evalCtx, node, CancellationToken.None).GetAwaiter().GetResult();
        }

        var outPath = Path.Combine(Path.GetTempPath(), $"koc-eval-{Guid.NewGuid():N}.csv");
        ctx.TempFiles.Add(outPath);
        evalDuck.ExportCsv(PipelineContext.WorkingTable, outPath);
        return outPath;
    }

    private static string TempCsv(PipelineContext ctx)
    {
        var path = Path.Combine(Path.GetTempPath(), $"koc-cross-{Guid.NewGuid():N}.csv");
        ctx.TempFiles.Add(path);
        return path;
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
