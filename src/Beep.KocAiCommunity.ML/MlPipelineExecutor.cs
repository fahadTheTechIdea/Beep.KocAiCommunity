using System.Globalization;
using System.Text;
using Beep.KocAiCommunity.Application.ML;
using Beep.KocAiCommunity.Application.Workflow;
using Beep.KocAiCommunity.Contracts.Workflow;
using Beep.KocAiCommunity.Workflow;
using Microsoft.ML;
using Microsoft.ML.AutoML;
using Microsoft.ML.Data;
using Microsoft.ML.Transforms;

namespace Beep.KocAiCommunity.ML;

/// <summary>
/// A real node-by-node ML.NET pipeline runtime. Each node performs an actual step — load, select
/// columns, sample, encode categoricals, replace missing values, normalize, split, train (with a
/// chosen algorithm), cross-validate, score, evaluate — and reports its status and a summary.
/// The catalog is deliberately broad so competitors can build materially different pipelines and
/// therefore earn different scores. The same pipeline can be replayed to predict a competition's
/// evaluation set for a leaderboard submission.
/// </summary>
public sealed class MlPipelineExecutor : IPipelineExecutor
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

    private static PipelineExecutionResult Run(WorkflowDefinition definition, IReadOnlyList<string> order, string labelColumn, MlTaskType task, string path)
    {
        var ml = new MLContext(seed: 1);
        var byId = definition.Nodes.ToDictionary(n => n.Id, n => n, StringComparer.Ordinal);
        var results = new List<NodeExecutionResult>();

        var columns = ml.Auto().InferColumns(path, labelColumnName: labelColumn, groupColumns: false);
        var loader = ml.Data.CreateTextLoader(columns.TextLoaderOptions);
        var full = loader.Load(path);

        var featureCols = full.Schema.Select(c => c.Name).Where(n => n != labelColumn).ToList();
        var splitFraction = ReadSplitFraction(byId, order);
        var split = ml.Data.TrainTestSplit(full, testFraction: splitFraction, seed: 1);
        IDataView train = split.TrainSet;

        var preprocessors = new List<ITransformer>();
        ITransformer? model = null;
        ITransformer? labelMap = null;
        string? algorithm = null;
        var primaryMetric = task switch
        {
            MlTaskType.Regression => "RSquared",
            MlTaskType.MulticlassClassification => "MicroAccuracy",
            _ => "Accuracy",
        };
        double primaryValue = 0;

        foreach (var nodeId in order)
        {
            var node = byId[nodeId];
            var kind = node.Kind.ToLowerInvariant();
            try
            {
                var featureResult = TryFeatureNode(ml, kind, node, nodeId, ref train, ref featureCols, preprocessors);
                if (featureResult is not null)
                {
                    results.Add(featureResult);
                    continue;
                }

                switch (kind)
                {
                    case "dataset":
                        results.Add(new NodeExecutionResult(nodeId, kind, "done", $"{Count(full)} rows · {featureCols.Count} columns"));
                        break;

                    case "split":
                        results.Add(new NodeExecutionResult(nodeId, kind, "done", $"train {Count(split.TrainSet)} · test {Count(split.TestSet)} ({splitFraction:0.##} held out)"));
                        break;

                    case "train":
                        {
                            var trainFeatures = NumericFeatures(train.Schema, featureCols);
                            if (trainFeatures.Length == 0)
                            {
                                results.Add(new NodeExecutionResult(nodeId, kind, "failed", "no usable feature columns"));
                                return new PipelineExecutionResult(false, algorithm, primaryMetric, primaryValue, results);
                            }

                            (model, algorithm, labelMap) = FitModel(ml, task, node, labelColumn, train, trainFeatures);
                            results.Add(new NodeExecutionResult(nodeId, kind, "done", $"{algorithm} · {trainFeatures.Length} features"));
                            break;
                        }

                    case "cluster":
                        {
                            var clusterFeatures = NumericFeatures(train.Schema, featureCols);
                            if (clusterFeatures.Length == 0)
                            {
                                results.Add(new NodeExecutionResult(nodeId, kind, "skipped", "no numeric features"));
                                break;
                            }

                            var k = Math.Clamp((int)ReadDouble(Cfg(node, "clusters"), 3), 2, 20);
                            var clusterModel = ml.Transforms.Concatenate("Features", clusterFeatures)
                                .Append(ml.Clustering.Trainers.KMeans("Features", numberOfClusters: k))
                                .Fit(train);
                            var clustered = clusterModel.Transform(train);
                            var cm = ml.Clustering.Evaluate(clustered, scoreColumnName: "Score", featureColumnName: "Features");
                            results.Add(new NodeExecutionResult(nodeId, kind, "done", $"{k} clusters · avg distance {cm.AverageDistance:0.###} · DBI {cm.DaviesBouldinIndex:0.###}"));
                            break;
                        }

                    case "cross-validate":
                        {
                            var trainFeatures = NumericFeatures(train.Schema, featureCols);
                            var folds = Math.Clamp((int)ReadDouble(Cfg(node, "folds"), 5), 2, 10);

                            if (task == MlTaskType.MulticlassClassification)
                            {
                                var (mcTrainer, mcName) = MulticlassTrainer(ml, node);
                                var mcEst = ml.Transforms.Conversion.MapValueToKey("Label", labelColumn)
                                    .Append(ml.Transforms.Concatenate("Features", trainFeatures))
                                    .Append(mcTrainer);
                                var cv = ml.MulticlassClassification.CrossValidate(train, mcEst, numberOfFolds: folds, labelColumnName: "Label");
                                results.Add(new NodeExecutionResult(nodeId, kind, "done", $"{folds}-fold {mcName} · mean micro-acc {cv.Average(r => r.Metrics.MicroAccuracy):0.###}"));
                                break;
                            }

                            var (trainer, name) = Trainer(ml, task, node, labelColumn);
                            var est = ml.Transforms.Concatenate("Features", trainFeatures).Append(trainer);
                            if (task == MlTaskType.Regression)
                            {
                                var cv = ml.Regression.CrossValidate(train, est, numberOfFolds: folds, labelColumnName: labelColumn);
                                results.Add(new NodeExecutionResult(nodeId, kind, "done", $"{folds}-fold {name} · mean R² {cv.Average(r => r.Metrics.RSquared):0.###}"));
                            }
                            else
                            {
                                var cv = ml.BinaryClassification.CrossValidateNonCalibrated(train, est, numberOfFolds: folds, labelColumnName: labelColumn);
                                results.Add(new NodeExecutionResult(nodeId, kind, "done", $"{folds}-fold {name} · mean accuracy {cv.Average(r => r.Metrics.Accuracy):0.###}"));
                            }
                            break;
                        }

                    case "score":
                        if (model is null)
                        {
                            results.Add(new NodeExecutionResult(nodeId, kind, "skipped", "no trained model upstream"));
                        }
                        else
                        {
                            var scored = model.Transform(ApplyPreprocessors(preprocessors, split.TestSet));
                            results.Add(new NodeExecutionResult(nodeId, kind, "done", $"{Count(scored)} rows scored"));
                        }
                        break;

                    case "evaluate":
                        if (model is null)
                        {
                            results.Add(new NodeExecutionResult(nodeId, kind, "skipped", "no trained model upstream"));
                        }
                        else
                        {
                            // Multiclass evaluation needs the true label as a key column ("Label"); the
                            // model itself is label-free, so map the test label here.
                            var prepared = ApplyPreprocessors(preprocessors, split.TestSet);
                            var withLabel = labelMap is null ? prepared : labelMap.Transform(prepared);
                            var scored = model.Transform(withLabel);
                            if (task == MlTaskType.Regression)
                            {
                                var m = ml.Regression.Evaluate(scored, labelColumnName: labelColumn);
                                primaryValue = m.RSquared;
                                results.Add(new NodeExecutionResult(nodeId, kind, "done", $"R² {m.RSquared:0.###} · RMSE {m.RootMeanSquaredError:0.###}"));
                            }
                            else if (task == MlTaskType.MulticlassClassification)
                            {
                                var m = ml.MulticlassClassification.Evaluate(scored, labelColumnName: "Label");
                                primaryValue = m.MicroAccuracy;
                                results.Add(new NodeExecutionResult(nodeId, kind, "done", $"MicroAcc {m.MicroAccuracy:0.###} · MacroAcc {m.MacroAccuracy:0.###}"));
                            }
                            else
                            {
                                var m = ml.BinaryClassification.EvaluateNonCalibrated(scored, labelColumnName: labelColumn);
                                primaryValue = m.Accuracy;
                                results.Add(new NodeExecutionResult(nodeId, kind, "done", $"Accuracy {m.Accuracy:0.###} · AUC {m.AreaUnderRocCurve:0.###}"));
                            }
                        }
                        break;

                    default:
                        results.Add(new NodeExecutionResult(nodeId, kind, "skipped", "unknown node"));
                        break;
                }
            }
            catch (Exception ex)
            {
                results.Add(new NodeExecutionResult(nodeId, kind, "failed", ex.Message));
                return new PipelineExecutionResult(false, algorithm, primaryMetric, primaryValue, results);
            }
        }

        return new PipelineExecutionResult(true, algorithm, primaryMetric, primaryValue, results);
    }

    private static string Predict(WorkflowDefinition definition, IReadOnlyList<string> order, string labelColumn, string idColumn, MlTaskType task, string trainPath, string evalPath)
    {
        var ml = new MLContext(seed: 1);
        var byId = definition.Nodes.ToDictionary(n => n.Id, n => n, StringComparer.Ordinal);

        var columns = ml.Auto().InferColumns(trainPath, labelColumnName: labelColumn, groupColumns: false);
        var loader = ml.Data.CreateTextLoader(columns.TextLoaderOptions);
        var full = loader.Load(trainPath);

        // The id column is not a feature — it must never leak into the model.
        var featureCols = full.Schema.Select(c => c.Name).Where(n => n != labelColumn && n != idColumn).ToList();
        IDataView train = full;
        var preprocessors = new List<ITransformer>();
        ITransformer? model = null;

        foreach (var nodeId in order)
        {
            var node = byId[nodeId];
            var kind = node.Kind.ToLowerInvariant();
            if (TryFeatureNode(ml, kind, node, nodeId, ref train, ref featureCols, preprocessors) is not null)
            {
                continue;
            }

            if (kind == "train")
            {
                var trainFeatures = NumericFeatures(train.Schema, featureCols);
                if (trainFeatures.Length == 0)
                {
                    throw new InvalidOperationException("Pipeline has no usable feature columns to train on.");
                }

                (model, _, _) = FitModel(ml, task, node, labelColumn, train, trainFeatures);
            }
            // split / evaluate / score / cross-validate / dataset are no-ops when generating predictions.
        }

        if (model is null)
        {
            throw new InvalidOperationException("Pipeline has no train node, so it cannot produce predictions.");
        }

        // Ids are read straight from the evaluation file so their formatting and order are preserved.
        var ids = ReadColumn(evalPath, idColumn);
        var evalColumns = ml.Auto().InferColumns(evalPath, labelColumnName: idColumn, groupColumns: false);
        var evalData = ml.Data.CreateTextLoader(evalColumns.TextLoaderOptions).Load(evalPath);
        var scored = model.Transform(ApplyPreprocessors(preprocessors, evalData));
        var predictions = ReadPredictions(scored, task);

        var count = Math.Min(ids.Count, predictions.Count);
        var sb = new StringBuilder("id,prediction\n");
        for (var i = 0; i < count; i++)
        {
            sb.Append(ids[i]).Append(',').Append(predictions[i]).Append('\n');
        }
        return sb.ToString();
    }

    /// <summary>Applies a feature-engineering node (mutating the working set) or returns null if the kind is not one.</summary>
    private static NodeExecutionResult? TryFeatureNode(MLContext ml, string kind, WorkflowNode node, string nodeId, ref IDataView train, ref List<string> featureCols, List<ITransformer> preprocessors)
    {
        switch (kind)
        {
            case "select-columns":
                {
                    var keep = SplitList(Cfg(node, "columns"));
                    if (keep.Count == 0)
                    {
                        return new NodeExecutionResult(nodeId, kind, "skipped", "no columns configured");
                    }

                    var drop = featureCols.Where(c => !keep.Contains(c)).ToArray();
                    if (drop.Length > 0)
                    {
                        var t = ml.Transforms.DropColumns(drop).Fit(train);
                        train = t.Transform(train);
                        preprocessors.Add(t);
                    }

                    featureCols = featureCols.Where(keep.Contains).ToList();
                    return new NodeExecutionResult(nodeId, kind, "done", $"kept {featureCols.Count}: {string.Join(", ", featureCols)}");
                }

            case "drop-columns":
                {
                    var drop = SplitList(Cfg(node, "columns"));
                    if (drop.Count == 0)
                    {
                        return new NodeExecutionResult(nodeId, kind, "skipped", "no columns configured");
                    }

                    var toDrop = featureCols.Where(drop.Contains).ToArray();
                    if (toDrop.Length > 0)
                    {
                        var t = ml.Transforms.DropColumns(toDrop).Fit(train);
                        train = t.Transform(train);
                        preprocessors.Add(t);
                    }

                    featureCols = featureCols.Where(c => !drop.Contains(c)).ToList();
                    return new NodeExecutionResult(nodeId, kind, "done", $"dropped {toDrop.Length}: {string.Join(", ", toDrop)}");
                }

            case "sample":
                {
                    var fraction = ReadDouble(Cfg(node, "fraction"), 0.5);
                    var before = Count(train);
                    var take = Math.Max(1, (long)(before * fraction));
                    train = ml.Data.TakeRows(ml.Data.ShuffleRows(train, seed: 1), take);
                    return new NodeExecutionResult(nodeId, kind, "done", $"{Count(train)} of {before} rows ({fraction:0.##})");
                }

            case "filter-rows":
                {
                    var column = Cfg(node, "column");
                    if (string.IsNullOrWhiteSpace(column))
                    {
                        return new NodeExecutionResult(nodeId, kind, "skipped", "no column configured");
                    }

                    var min = ReadDouble(Cfg(node, "min"), double.NegativeInfinity);
                    var max = ReadDouble(Cfg(node, "max"), double.PositiveInfinity);
                    var before = Count(train);
                    // Row filter applies to the working (training) set only — evaluation rows are never dropped.
                    train = ml.Data.FilterRowsByColumn(train, column, lowerBound: min, upperBound: max);
                    return new NodeExecutionResult(nodeId, kind, "done", $"{Count(train)} of {before} rows kept");
                }

            case "standardize":
                {
                    var numeric = NumericFeatures(train.Schema, featureCols);
                    if (numeric.Length == 0)
                    {
                        return new NodeExecutionResult(nodeId, kind, "skipped", "no numeric columns");
                    }

                    var t = ml.Transforms.NormalizeMeanVariance([.. numeric.Select(c => new InputOutputColumnPair(c))]).Fit(train);
                    train = t.Transform(train);
                    preprocessors.Add(t);
                    return new NodeExecutionResult(nodeId, kind, "done", "standardized (mean/variance)");
                }

            case "one-hot":
                {
                    var textCols = TextFeatures(train.Schema, featureCols);
                    if (textCols.Length == 0)
                    {
                        return new NodeExecutionResult(nodeId, kind, "skipped", "no categorical columns");
                    }

                    var t = ml.Transforms.Categorical.OneHotEncoding([.. textCols.Select(c => new InputOutputColumnPair(c))]).Fit(train);
                    train = t.Transform(train);
                    preprocessors.Add(t);
                    return new NodeExecutionResult(nodeId, kind, "done", $"encoded {textCols.Length}: {string.Join(", ", textCols)}");
                }

            case "replace-missing":
                {
                    var numeric = NumericFeatures(train.Schema, featureCols);
                    if (numeric.Length == 0)
                    {
                        return new NodeExecutionResult(nodeId, kind, "skipped", "no numeric columns");
                    }

                    var mode = (Cfg(node, "mode") ?? "mean").ToLowerInvariant() switch
                    {
                        "min" or "minimum" => MissingValueReplacingEstimator.ReplacementMode.Minimum,
                        "max" or "maximum" => MissingValueReplacingEstimator.ReplacementMode.Maximum,
                        _ => MissingValueReplacingEstimator.ReplacementMode.Mean,
                    };
                    var t = ml.Transforms.ReplaceMissingValues([.. numeric.Select(c => new InputOutputColumnPair(c))], replacementMode: mode).Fit(train);
                    train = t.Transform(train);
                    preprocessors.Add(t);
                    return new NodeExecutionResult(nodeId, kind, "done", $"missing values → {mode}");
                }

            case "normalize":
                return NumericNormalizer(nodeId, kind, ref train, featureCols, preprocessors,
                    (cols, data) => ml.Transforms.NormalizeMinMax(cols).Fit(data), "min-max normalized");

            case "log-normalize":
                return NumericNormalizer(nodeId, kind, ref train, featureCols, preprocessors,
                    (cols, data) => ml.Transforms.NormalizeLogMeanVariance(cols).Fit(data), "log mean-variance");

            case "robust-scale":
                return NumericNormalizer(nodeId, kind, ref train, featureCols, preprocessors,
                    (cols, data) => ml.Transforms.NormalizeRobustScaling(cols).Fit(data), "robust-scaled (median/IQR)");

            case "binning":
                {
                    var bins = Math.Clamp((int)ReadDouble(Cfg(node, "bins"), 10), 2, 255);
                    return NumericNormalizer(nodeId, kind, ref train, featureCols, preprocessors,
                        (cols, data) => ml.Transforms.NormalizeBinning(cols, maximumBinCount: bins).Fit(data), $"binned into ≤{bins}");
                }

            case "hash-encode":
                {
                    var textCols = TextFeatures(train.Schema, featureCols);
                    if (textCols.Length == 0)
                    {
                        return new NodeExecutionResult(nodeId, kind, "skipped", "no categorical columns");
                    }

                    var t = ml.Transforms.Categorical.OneHotHashEncoding([.. textCols.Select(c => new InputOutputColumnPair(c))]).Fit(train);
                    train = t.Transform(train);
                    preprocessors.Add(t);
                    return new NodeExecutionResult(nodeId, kind, "done", $"hash-encoded {textCols.Length}: {string.Join(", ", textCols)}");
                }

            case "featurize-text":
                {
                    var textCols = TextFeatures(train.Schema, featureCols);
                    if (textCols.Length == 0)
                    {
                        return new NodeExecutionResult(nodeId, kind, "skipped", "no text columns");
                    }

                    IEstimator<ITransformer>? est = null;
                    foreach (var c in textCols)
                    {
                        var step = ml.Transforms.Text.FeaturizeText(c, c);
                        est = est is null ? step : est.Append(step);
                    }

                    var t = est!.Fit(train);
                    train = t.Transform(train);
                    preprocessors.Add(t);
                    return new NodeExecutionResult(nodeId, kind, "done", $"featurized text: {string.Join(", ", textCols)}");
                }

            case "pca":
                {
                    var numeric = NumericFeatures(train.Schema, featureCols);
                    if (numeric.Length < 2)
                    {
                        return new NodeExecutionResult(nodeId, kind, "skipped", "need ≥2 numeric columns");
                    }

                    var rank = Math.Clamp((int)ReadDouble(Cfg(node, "rank"), 2), 1, numeric.Length);
                    var t = ml.Transforms.Concatenate("__PcaIn", numeric)
                        .Append(ml.Transforms.ProjectToPrincipalComponents("Pca", "__PcaIn", rank: rank))
                        .Fit(train);
                    train = t.Transform(train);
                    preprocessors.Add(t);
                    featureCols = ["Pca"];
                    return new NodeExecutionResult(nodeId, kind, "done", $"PCA → {rank} components");
                }

            case "feature-selection":
                {
                    var numeric = NumericFeatures(train.Schema, featureCols);
                    if (numeric.Length == 0)
                    {
                        return new NodeExecutionResult(nodeId, kind, "skipped", "no numeric columns");
                    }

                    var count = Math.Max(1, (int)ReadDouble(Cfg(node, "count"), 1));
                    var t = ml.Transforms.Concatenate("__FsIn", numeric)
                        .Append(ml.Transforms.FeatureSelection.SelectFeaturesBasedOnCount("Fs", "__FsIn", count: count))
                        .Fit(train);
                    train = t.Transform(train);
                    preprocessors.Add(t);
                    featureCols = ["Fs"];
                    return new NodeExecutionResult(nodeId, kind, "done", $"selected features (count ≥ {count})");
                }

            // ---- Prepare (data-management) ----
            case "rename-column":
                {
                    var from = Cfg(node, "from");
                    var to = Cfg(node, "to");
                    if (string.IsNullOrWhiteSpace(from) || string.IsNullOrWhiteSpace(to))
                    {
                        return new NodeExecutionResult(nodeId, kind, "skipped", "'from' and 'to' are required");
                    }

                    if (!featureCols.Contains(from))
                    {
                        return new NodeExecutionResult(nodeId, kind, "skipped", $"column '{from}' not found");
                    }

                    var t = ml.Transforms.CopyColumns(to, from).Append(ml.Transforms.DropColumns(from)).Fit(train);
                    train = t.Transform(train);
                    preprocessors.Add(t);
                    featureCols = featureCols.Select(c => c == from ? to : c).ToList();
                    return new NodeExecutionResult(nodeId, kind, "done", $"{from} → {to}");
                }

            case "convert-numeric":
                {
                    var wanted = SplitList(Cfg(node, "columns"));
                    var target = (wanted.Count > 0 ? featureCols.Where(wanted.Contains) : TextFeatures(train.Schema, featureCols)).ToArray();
                    if (target.Length == 0)
                    {
                        return new NodeExecutionResult(nodeId, kind, "skipped", "no columns to convert");
                    }

                    var t = ml.Transforms.Conversion.ConvertType([.. target.Select(c => new InputOutputColumnPair(c))], DataKind.Single).Fit(train);
                    train = t.Transform(train);
                    preprocessors.Add(t);
                    return new NodeExecutionResult(nodeId, kind, "done", $"cast to number: {string.Join(", ", target)}");
                }

            case "compute-column":
                {
                    var output = Cfg(node, "output");
                    var expression = Cfg(node, "expression");
                    var inputs = SplitList(Cfg(node, "inputs")).Where(featureCols.Contains).ToArray();
                    if (string.IsNullOrWhiteSpace(output) || string.IsNullOrWhiteSpace(expression))
                    {
                        return new NodeExecutionResult(nodeId, kind, "skipped", "'output' name and 'expression' are required");
                    }

                    if (inputs.Length == 0)
                    {
                        return new NodeExecutionResult(nodeId, kind, "skipped", "no valid input columns");
                    }

                    // Inputs must be numeric for the expression math; cast first, then compute.
                    var cast = ml.Transforms.Conversion.ConvertType([.. inputs.Select(c => new InputOutputColumnPair(c))], DataKind.Single);
                    var t = cast.Append(ml.Transforms.Expression(output, expression, inputs)).Fit(train);
                    train = t.Transform(train);
                    preprocessors.Add(t);
                    if (!featureCols.Contains(output))
                    {
                        featureCols = [.. featureCols, output];
                    }

                    return new NodeExecutionResult(nodeId, kind, "done", $"{output} = {expression}  [{string.Join(", ", inputs)}]");
                }

            case "combine-columns":
                {
                    var wanted = SplitList(Cfg(node, "columns"));
                    var target = (wanted.Count > 0 ? featureCols.Where(wanted.Contains) : NumericFeatures(train.Schema, featureCols)).ToArray();
                    if (target.Length < 2)
                    {
                        return new NodeExecutionResult(nodeId, kind, "skipped", "need ≥2 columns to combine");
                    }

                    var t = ml.Transforms.Concatenate("Combined", target).Fit(train);
                    train = t.Transform(train);
                    preprocessors.Add(t);
                    featureCols = [.. featureCols.Where(c => !target.Contains(c)), "Combined"];
                    return new NodeExecutionResult(nodeId, kind, "done", $"combined {target.Length} → Combined");
                }

            case "lp-normalize":
                {
                    var numeric = NumericFeatures(train.Schema, featureCols);
                    if (numeric.Length == 0)
                    {
                        return new NodeExecutionResult(nodeId, kind, "skipped", "no numeric columns");
                    }

                    var t = ml.Transforms.Concatenate("__LpIn", numeric)
                        .Append(ml.Transforms.NormalizeLpNorm("LpNorm", "__LpIn"))
                        .Fit(train);
                    train = t.Transform(train);
                    preprocessors.Add(t);
                    featureCols = ["LpNorm"];
                    return new NodeExecutionResult(nodeId, kind, "done", "Lp-normalized feature vector");
                }

            case "global-contrast":
                {
                    var numeric = NumericFeatures(train.Schema, featureCols);
                    if (numeric.Length == 0)
                    {
                        return new NodeExecutionResult(nodeId, kind, "skipped", "no numeric columns");
                    }

                    var t = ml.Transforms.Concatenate("__GcnIn", numeric)
                        .Append(ml.Transforms.NormalizeGlobalContrast("Gcn", "__GcnIn"))
                        .Fit(train);
                    train = t.Transform(train);
                    preprocessors.Add(t);
                    featureCols = ["Gcn"];
                    return new NodeExecutionResult(nodeId, kind, "done", "global-contrast normalized");
                }

            // ---- Shape (row operations; training set only, like sample/filter) ----
            case "take-rows":
                {
                    var n = Math.Max(1, (int)ReadDouble(Cfg(node, "count"), 1000));
                    var before = Count(train);
                    train = ml.Data.TakeRows(train, n);
                    return new NodeExecutionResult(nodeId, kind, "done", $"kept first {Count(train)} of {before}");
                }

            case "shuffle":
                {
                    train = ml.Data.ShuffleRows(train, seed: 1);
                    return new NodeExecutionResult(nodeId, kind, "done", "rows shuffled");
                }

            default:
                return null;
        }
    }

    /// <summary>
    /// Fits a model over the given features. For multiclass the label→key mapping is returned
    /// <em>separately</em> (LabelMap) rather than baked into the model, so the model applies to
    /// label-less evaluation data at prediction time; evaluation re-applies LabelMap to the test set.
    /// </summary>
    private static (ITransformer Model, string Algorithm, ITransformer? LabelMap) FitModel(MLContext ml, MlTaskType task, WorkflowNode node, string label, IDataView train, string[] features)
    {
        if (task == MlTaskType.MulticlassClassification)
        {
            var (trainer, name) = MulticlassTrainer(ml, node);
            var labelMap = ml.Transforms.Conversion.MapValueToKey("Label", label).Fit(train);
            var keyed = labelMap.Transform(train);
            // Predict on features only; map the predicted key back to its original class string.
            var model = ml.Transforms.Concatenate("Features", features)
                .Append(trainer)
                .Append(ml.Transforms.Conversion.MapKeyToValue("PredictedLabelValue", "PredictedLabel"))
                .Fit(keyed);
            return (model, name, labelMap);
        }

        var (t, n) = Trainer(ml, task, node, label);
        var p = ml.Transforms.Concatenate("Features", features).Append(t);
        return (p.Fit(train), n, null);
    }

    private static (IEstimator<ITransformer> Trainer, string Name) MulticlassTrainer(MLContext ml, WorkflowNode node)
    {
        const string label = "Label";
        const string features = "Features";
        var l2 = HpFloat(node, "l2");
        return Algo(node) switch
        {
            "lbfgs" => (ml.MulticlassClassification.Trainers.LbfgsMaximumEntropy(label, features, l2Regularization: l2 ?? 1f), "LbfgsMaximumEntropy"),
            "naivebayes" => (ml.MulticlassClassification.Trainers.NaiveBayes(label, features), "NaiveBayes"),
            _ => (ml.MulticlassClassification.Trainers.SdcaMaximumEntropy(labelColumnName: label, featureColumnName: features, l2Regularization: l2), "SdcaMaximumEntropy"),
        };
    }

    // Shared body for the per-column numeric normalizers (min-max / log / robust / binning).
    private static NodeExecutionResult NumericNormalizer(string nodeId, string kind, ref IDataView train, List<string> featureCols, List<ITransformer> preprocessors, Func<InputOutputColumnPair[], IDataView, ITransformer> fit, string detail)
    {
        var numeric = NumericFeatures(train.Schema, featureCols);
        if (numeric.Length == 0)
        {
            return new NodeExecutionResult(nodeId, kind, "skipped", "no numeric columns");
        }

        var t = fit([.. numeric.Select(c => new InputOutputColumnPair(c))], train);
        train = t.Transform(train);
        preprocessors.Add(t);
        return new NodeExecutionResult(nodeId, kind, "done", detail);
    }

    private static IDataView ApplyPreprocessors(IEnumerable<ITransformer> preprocessors, IDataView data)
    {
        foreach (var t in preprocessors)
        {
            data = t.Transform(data);
        }
        return data;
    }

    private static List<string> ReadPredictions(IDataView scored, MlTaskType task)
    {
        var list = new List<string>();
        if (task == MlTaskType.Regression)
        {
            var col = scored.Schema["Score"];
            using var cursor = scored.GetRowCursor([col]);
            var getter = cursor.GetGetter<float>(col);
            float value = 0;
            while (cursor.MoveNext())
            {
                getter(ref value);
                list.Add(value.ToString(CultureInfo.InvariantCulture));
            }
        }
        else if (task == MlTaskType.MulticlassClassification)
        {
            // The model's final transform emits the predicted class as its original string label.
            var col = scored.Schema["PredictedLabelValue"];
            using var cursor = scored.GetRowCursor([col]);
            var getter = cursor.GetGetter<ReadOnlyMemory<char>>(col);
            ReadOnlyMemory<char> value = default;
            while (cursor.MoveNext())
            {
                getter(ref value);
                list.Add(value.ToString());
            }
        }
        else
        {
            var col = scored.Schema["PredictedLabel"];
            using var cursor = scored.GetRowCursor([col]);
            var getter = cursor.GetGetter<bool>(col);
            bool value = false;
            while (cursor.MoveNext())
            {
                getter(ref value);
                list.Add(value ? "true" : "false");
            }
        }
        return list;
    }

    private static List<string> ReadColumn(string path, string columnName)
    {
        var values = new List<string>();
        using var reader = new StreamReader(path);
        var header = reader.ReadLine();
        if (header is null)
        {
            return values;
        }

        var cols = header.Split(',');
        var index = Array.FindIndex(cols, c => c.Trim().Equals(columnName, StringComparison.OrdinalIgnoreCase));
        if (index < 0)
        {
            index = 0;
        }

        string? line;
        while ((line = reader.ReadLine()) is not null)
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            var parts = line.Split(',');
            values.Add(index < parts.Length ? parts[index].Trim() : string.Empty);
        }
        return values;
    }

    private static (IEstimator<ITransformer> Trainer, string Name) Trainer(MLContext ml, MlTaskType task, WorkflowNode node, string label)
    {
        const string features = "Features";
        const int minLeaf = 10;
        var algo = Algo(node);
        var trees = HpInt(node, "trees", 100);
        var leaves = HpInt(node, "leaves", 20);
        var learningRate = ReadDouble(Cfg(node, "learningRate"), 0.2);
        var l2 = HpFloat(node, "l2");

        if (task == MlTaskType.Regression)
        {
            return algo switch
            {
                "fasttree" => (ml.Regression.Trainers.FastTree(label, features, numberOfLeaves: leaves, numberOfTrees: trees, minimumExampleCountPerLeaf: minLeaf, learningRate: learningRate), "FastTreeRegression"),
                "fastforest" => (ml.Regression.Trainers.FastForest(label, features, numberOfLeaves: leaves, numberOfTrees: trees, minimumExampleCountPerLeaf: minLeaf), "FastForestRegression"),
                "lbfgs" => (ml.Regression.Trainers.LbfgsPoissonRegression(label, features, l2Regularization: l2 ?? 1f), "LbfgsPoissonRegression"),
                _ => (ml.Regression.Trainers.Sdca(labelColumnName: label, featureColumnName: features, l2Regularization: l2), "SdcaRegression"),
            };
        }

        return algo switch
        {
            "fasttree" => (ml.BinaryClassification.Trainers.FastTree(label, features, numberOfLeaves: leaves, numberOfTrees: trees, minimumExampleCountPerLeaf: minLeaf, learningRate: learningRate), "FastTreeBinary"),
            "fastforest" => (ml.BinaryClassification.Trainers.FastForest(label, features, numberOfLeaves: leaves, numberOfTrees: trees, minimumExampleCountPerLeaf: minLeaf), "FastForestBinary"),
            "lbfgs" => (ml.BinaryClassification.Trainers.LbfgsLogisticRegression(label, features, l2Regularization: l2 ?? 1f), "LbfgsLogisticRegression"),
            "perceptron" => (ml.BinaryClassification.Trainers.AveragedPerceptron(label, features), "AveragedPerceptron"),
            _ => (ml.BinaryClassification.Trainers.SdcaLogisticRegression(labelColumnName: label, featureColumnName: features, l2Regularization: l2), "SdcaLogisticRegression"),
        };
    }

    private static string[] NumericFeatures(DataViewSchema schema, IEnumerable<string> featureCols)
    {
        var set = featureCols.ToHashSet(StringComparer.Ordinal);
        // In-place transforms leave the prior column as a hidden entry with the same name; skip those
        // so a feature is never selected twice.
        return schema.Where(c => !c.IsHidden && set.Contains(c.Name) && IsNumeric(c.Type)).Select(c => c.Name).ToArray();
    }

    private static string[] TextFeatures(DataViewSchema schema, IEnumerable<string> featureCols)
    {
        var set = featureCols.ToHashSet(StringComparer.Ordinal);
        return schema.Where(c => !c.IsHidden && set.Contains(c.Name) && ItemType(c.Type) == TextDataViewType.Instance).Select(c => c.Name).ToArray();
    }

    private static bool IsNumeric(DataViewType type) => ItemType(type) == NumberDataViewType.Single;

    private static DataViewType ItemType(DataViewType type) => (type as VectorDataViewType)?.ItemType ?? type;

    private static double ReadSplitFraction(IReadOnlyDictionary<string, WorkflowNode> byId, IEnumerable<string> order)
    {
        foreach (var id in order)
        {
            if (byId[id].Kind.Equals("split", StringComparison.OrdinalIgnoreCase))
            {
                return Math.Clamp(ReadDouble(Cfg(byId[id], "testFraction"), 0.25), 0.05, 0.9);
            }
        }
        return 0.25;
    }

    private static string Algo(WorkflowNode node) => (Cfg(node, "algorithm") ?? "sdca").ToLowerInvariant();

    private static string? Cfg(WorkflowNode node, string key)
        => node.Config is not null && node.Config.TryGetValue(key, out var v) ? v : null;

    private static double ReadDouble(string? raw, double fallback)
        => double.TryParse(raw, NumberStyles.Any, CultureInfo.InvariantCulture, out var v) ? v : fallback;

    private static int HpInt(WorkflowNode node, string key, int fallback)
        => int.TryParse(Cfg(node, key), NumberStyles.Integer, CultureInfo.InvariantCulture, out var v) && v > 0 ? v : fallback;

    // Nullable so trainers can fall back to their own ML.NET defaults when unset.
    private static float? HpFloat(WorkflowNode node, string key)
        => double.TryParse(Cfg(node, key), NumberStyles.Any, CultureInfo.InvariantCulture, out var v) && v > 0 ? (float)v : null;

    private static HashSet<string> SplitList(string? raw)
        => (raw ?? string.Empty).Split([',', ';', ' '], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToHashSet(StringComparer.Ordinal);

    private static long Count(IDataView view) => view.GetRowCount() ?? view.Preview(int.MaxValue).RowView.Length;

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
