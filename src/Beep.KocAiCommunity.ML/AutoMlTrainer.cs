using System.Globalization;
using System.Text.Json;
using Beep.KocAiCommunity.Application.Common;
using Beep.KocAiCommunity.Application.ML;
using Microsoft.ML;
using Microsoft.ML.AutoML;
using Microsoft.ML.Data;

namespace Beep.KocAiCommunity.ML;

/// <summary>ML.NET AutoML trainer for binary, multiclass, and regression tasks. Deterministic seed.</summary>
public sealed class AutoMlTrainer : IMlTrainer
{
    private sealed class NullProgress<T> : IProgress<T>
    {
        public static readonly NullProgress<T> Instance = new();
        public void Report(T value) { }
    }

    // The winning model plus the input schema needed to save it and score against it later.
    private sealed record TrainRun(TrainingResult Result, ITransformer Model, DataViewSchema Schema);

    public Task<TrainingResult> TrainAsync(MlTaskType task, Stream csv, string labelColumn, int maxSeconds, CancellationToken ct = default) =>
        TrainWithTrialsAsync(task, csv, labelColumn, maxSeconds, NullProgress<TrialReport>.Instance, ct);

    public async Task<TrainingResult> TrainWithTrialsAsync(MlTaskType task, Stream csv, string labelColumn, int maxSeconds, IProgress<TrialReport> trials, CancellationToken ct = default)
    {
        var tempPath = await SpillAsync(csv, ct);
        try
        {
            return await Task.Run(() => TrainCore(task, tempPath, labelColumn, maxSeconds, trials).Result, ct);
        }
        finally
        {
            TryDelete(tempPath);
        }
    }

    public Task<CapturedModel> TrainAndCaptureAsync(MlTaskType task, Stream csv, string labelColumn, int maxSeconds, CancellationToken ct = default) =>
        TrainAndCaptureAsync(task, csv, labelColumn, maxSeconds, NullProgress<TrialReport>.Instance, ct);

    public async Task<CapturedModel> TrainAndCaptureAsync(MlTaskType task, Stream csv, string labelColumn, int maxSeconds, IProgress<TrialReport> trials, CancellationToken ct = default)
    {
        var tempPath = await SpillAsync(csv, ct);
        try
        {
            return await Task.Run(() =>
            {
                var run = TrainCore(task, tempPath, labelColumn, maxSeconds, trials);
                var ml = new MLContext(seed: 1);
                using var ms = new MemoryStream();
                ml.Model.Save(run.Model, run.Schema, ms);
                var stats = ComputeFeatureStats(tempPath, labelColumn);
                return new CapturedModel(run.Result, ms.ToArray(), stats);
            }, ct);
        }
        finally
        {
            TryDelete(tempPath);
        }
    }

    private static async Task<string> SpillAsync(Stream csv, CancellationToken ct)
    {
        var tempPath = Path.Combine(Path.GetTempPath(), $"koc-train-{Guid.NewGuid():N}.csv");
        await using var file = File.Create(tempPath);
        await csv.CopyToAsync(file, ct);
        return tempPath;
    }

    private static void TryDelete(string path)
    {
        try { File.Delete(path); } catch (IOException) { /* best effort */ }
    }

    // The platform's conventional row-key column. AutoML has no id role (it treats every non-label column
    // as a feature), so — unlike the node-graph executor, which excludes the id via FeatureNames — it would
    // otherwise train on a sequential/target-correlated identifier and leak. Keep this aligned with the
    // platform default (Competition.IdColumn, PipelineContext id handling) which is likewise "id".
    private const string IdColumnName = "id";

    private static TrainRun TrainCore(MlTaskType task, string path, string labelColumn, int maxSeconds, IProgress<TrialReport> trials)
    {
        var ml = new MLContext(seed: 1);
        var columns = ml.Auto().InferColumns(path, labelColumnName: labelColumn, groupColumns: false);
        IgnoreColumn(columns.ColumnInformation, IdColumnName);
        var loader = ml.Data.CreateTextLoader(columns.TextLoaderOptions);
        var data = loader.Load(path);
        var split = ml.Data.TrainTestSplit(data, testFraction: 0.25, seed: 1);
        var seconds = (uint)Math.Max(1, maxSeconds);
        var rowCount = CountDataRows(path);

        return task switch
        {
            MlTaskType.Regression => Regression(ml, columns, split, seconds, labelColumn, rowCount, trials),
            MlTaskType.MulticlassClassification => Multiclass(ml, columns, split, seconds, labelColumn, rowCount, trials),
            _ => Binary(ml, columns, split, seconds, labelColumn, rowCount, trials),
        };
    }

    // Drops a column from AutoML's inferred feature set so it is neither featurized nor scored. No-op when
    // the column isn't a feature (the dataset has no such column, or it's the label).
    private static void IgnoreColumn(ColumnInformation info, string name)
    {
        var wasFeature = info.NumericColumnNames.Remove(name)
            | info.CategoricalColumnNames.Remove(name)
            | info.TextColumnNames.Remove(name);
        if (wasFeature)
        {
            info.IgnoredColumnNames.Add(name);
        }
    }

    // A trial-progress bridge: forwards each AutoML RunDetail to the caller's non-blocking reporter.
    private sealed class TrialBridge<TMetrics>(string metricName, Func<TMetrics, double> extract, IProgress<TrialReport> sink) : IProgress<RunDetail<TMetrics>>
        where TMetrics : class
    {
        private int _counter;

        public void Report(RunDetail<TMetrics> value)
        {
            if (value.ValidationMetrics is null)
            {
                return; // a failed trial contributes no metric
            }

            var n = Interlocked.Increment(ref _counter);
            sink.Report(new TrialReport(n, value.TrainerName, metricName, extract(value.ValidationMetrics), value.RuntimeInSeconds));
        }
    }

    private static TrainRun Binary(MLContext ml, ColumnInferenceResults columns, DataOperationsCatalog.TrainTestData split, uint seconds, string label, long rows, IProgress<TrialReport> trials)
    {
        var experiment = ml.Auto().CreateBinaryClassificationExperiment(new BinaryExperimentSettings
        {
            MaxExperimentTimeInSeconds = seconds,
            OptimizingMetric = BinaryClassificationMetric.Accuracy,
        });
        var handler = new TrialBridge<BinaryClassificationMetrics>("Accuracy", m => m.Accuracy, trials);
        var result = experiment.Execute(split.TrainSet, columns.ColumnInformation, preFeaturizer: null, progressHandler: handler);
        var metrics = ml.BinaryClassification.EvaluateNonCalibrated(result.BestRun.Model.Transform(split.TestSet), labelColumnName: label);
        var tr = new TrainingResult("BinaryClassification", result.BestRun.TrainerName,
            "Accuracy", metrics.Accuracy, "AUC", metrics.AreaUnderRocCurve, rows);
        return new TrainRun(tr, result.BestRun.Model, split.TrainSet.Schema);
    }

    private static TrainRun Multiclass(MLContext ml, ColumnInferenceResults columns, DataOperationsCatalog.TrainTestData split, uint seconds, string label, long rows, IProgress<TrialReport> trials)
    {
        var experiment = ml.Auto().CreateMulticlassClassificationExperiment(new MulticlassExperimentSettings
        {
            MaxExperimentTimeInSeconds = seconds,
            OptimizingMetric = MulticlassClassificationMetric.MicroAccuracy,
        });
        var handler = new TrialBridge<MulticlassClassificationMetrics>("MicroAccuracy", m => m.MicroAccuracy, trials);
        var result = experiment.Execute(split.TrainSet, columns.ColumnInformation, preFeaturizer: null, progressHandler: handler);
        var metrics = ml.MulticlassClassification.Evaluate(result.BestRun.Model.Transform(split.TestSet), labelColumnName: label);
        var tr = new TrainingResult("MulticlassClassification", result.BestRun.TrainerName,
            "MicroAccuracy", metrics.MicroAccuracy, "MacroAccuracy", metrics.MacroAccuracy, rows);
        return new TrainRun(tr, result.BestRun.Model, split.TrainSet.Schema);
    }

    private static TrainRun Regression(MLContext ml, ColumnInferenceResults columns, DataOperationsCatalog.TrainTestData split, uint seconds, string label, long rows, IProgress<TrialReport> trials)
    {
        var experiment = ml.Auto().CreateRegressionExperiment(new RegressionExperimentSettings
        {
            MaxExperimentTimeInSeconds = seconds,
            OptimizingMetric = RegressionMetric.RSquared,
        });
        var handler = new TrialBridge<RegressionMetrics>("RSquared", m => m.RSquared, trials);
        var result = experiment.Execute(split.TrainSet, columns.ColumnInformation, preFeaturizer: null, progressHandler: handler);
        var metrics = ml.Regression.Evaluate(result.BestRun.Model.Transform(split.TestSet), labelColumnName: label);
        var tr = new TrainingResult("Regression", result.BestRun.TrainerName,
            "RSquared", metrics.RSquared, "RMSE", metrics.RootMeanSquaredError, rows);
        return new TrainRun(tr, result.BestRun.Model, split.TrainSet.Schema);
    }

    // Counts data records (header excluded) with the RFC-4180 codec, so a field with an embedded newline
    // counts as one row rather than inflating the total the way physical-line counting would.
    private static long CountDataRows(string path)
    {
        using var reader = new StreamReader(path);
        long rows = 0;
        var first = true;
        foreach (var _ in KocCsv.ParseRecords(reader))
        {
            if (first) { first = false; continue; }
            rows++;
        }

        return rows;
    }

    /// <summary>
    /// Computes count/mean/min/max for every numeric feature column (label excluded) from the raw
    /// CSV using the RFC-4180 codec (so quoted/comma-bearing fields don't shift the columns and
    /// misattribute the stats). Used as the drift baseline. Non-numeric columns are skipped.
    /// </summary>
    private static string ComputeFeatureStats(string path, string labelColumn)
    {
        using var reader = new StreamReader(path);
        string[]? names = null;
        long[] count = [];
        double[] sum = [];
        double[] min = [];
        double[] max = [];
        long rows = 0;

        foreach (var record in KocCsv.ParseRecords(reader))
        {
            if (names is null)
            {
                names = record;
                count = new long[names.Length];
                sum = new double[names.Length];
                min = new double[names.Length];
                max = new double[names.Length];
                for (var i = 0; i < names.Length; i++)
                {
                    min[i] = double.PositiveInfinity;
                    max[i] = double.NegativeInfinity;
                }

                continue;
            }

            rows++;
            for (var i = 0; i < names.Length && i < record.Length; i++)
            {
                if (double.TryParse(record[i], NumberStyles.Any, CultureInfo.InvariantCulture, out var v))
                {
                    count[i]++;
                    sum[i] += v;
                    if (v < min[i]) { min[i] = v; }
                    if (v > max[i]) { max[i] = v; }
                }
            }
        }

        if (names is null)
        {
            return JsonSerializer.Serialize(new FeatureStatsDoc(0, new Dictionary<string, FeatureStat>()));
        }

        var features = new Dictionary<string, FeatureStat>();
        for (var i = 0; i < names.Length; i++)
        {
            // Exclude the label and the row-key id (never features) from the drift baseline, matching the
            // columns AutoML actually trains on.
            if (string.Equals(names[i], labelColumn, StringComparison.OrdinalIgnoreCase)
                || string.Equals(names[i], IdColumnName, StringComparison.OrdinalIgnoreCase)
                || count[i] == 0)
            {
                continue; // skip the label and non-numeric columns
            }

            features[names[i]] = new FeatureStat(count[i], sum[i] / count[i], min[i], max[i]);
        }

        return JsonSerializer.Serialize(new FeatureStatsDoc(rows, features));
    }

    private sealed record FeatureStat(long Count, double Mean, double Min, double Max);
    private sealed record FeatureStatsDoc(long RowCount, Dictionary<string, FeatureStat> Features);
}
