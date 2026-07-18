using Beep.KocAiCommunity.Application.ML;
using Microsoft.ML;
using Microsoft.ML.AutoML;

namespace Beep.KocAiCommunity.ML;

/// <summary>ML.NET AutoML trainer for binary, multiclass, and regression tasks. Deterministic seed.</summary>
public sealed class AutoMlTrainer : IMlTrainer
{
    public async Task<TrainingResult> TrainAsync(MlTaskType task, Stream csv, string labelColumn, int maxSeconds, CancellationToken ct = default)
    {
        var tempPath = Path.Combine(Path.GetTempPath(), $"koc-train-{Guid.NewGuid():N}.csv");
        await using (var file = File.Create(tempPath))
        {
            await csv.CopyToAsync(file, ct);
        }

        try
        {
            return await Task.Run(() => TrainCore(task, tempPath, labelColumn, maxSeconds), ct);
        }
        finally
        {
            try { File.Delete(tempPath); } catch (IOException) { /* best effort */ }
        }
    }

    private static TrainingResult TrainCore(MlTaskType task, string path, string labelColumn, int maxSeconds)
    {
        var ml = new MLContext(seed: 1);
        var columns = ml.Auto().InferColumns(path, labelColumnName: labelColumn, groupColumns: false);
        var loader = ml.Data.CreateTextLoader(columns.TextLoaderOptions);
        var data = loader.Load(path);
        var split = ml.Data.TrainTestSplit(data, testFraction: 0.25, seed: 1);
        var seconds = (uint)Math.Max(1, maxSeconds);
        var rowCount = File.ReadLines(path).Skip(1).LongCount();

        return task switch
        {
            MlTaskType.Regression => Regression(ml, columns, split, seconds, labelColumn, rowCount),
            MlTaskType.MulticlassClassification => Multiclass(ml, columns, split, seconds, labelColumn, rowCount),
            _ => Binary(ml, columns, split, seconds, labelColumn, rowCount),
        };
    }

    private static TrainingResult Binary(MLContext ml, ColumnInferenceResults columns, DataOperationsCatalog.TrainTestData split, uint seconds, string label, long rows)
    {
        var experiment = ml.Auto().CreateBinaryClassificationExperiment(new BinaryExperimentSettings
        {
            MaxExperimentTimeInSeconds = seconds,
            OptimizingMetric = BinaryClassificationMetric.Accuracy,
        });
        var result = experiment.Execute(split.TrainSet, columns.ColumnInformation);
        var metrics = ml.BinaryClassification.EvaluateNonCalibrated(result.BestRun.Model.Transform(split.TestSet), labelColumnName: label);
        return new TrainingResult("BinaryClassification", result.BestRun.TrainerName,
            "Accuracy", metrics.Accuracy, "AUC", metrics.AreaUnderRocCurve, rows);
    }

    private static TrainingResult Multiclass(MLContext ml, ColumnInferenceResults columns, DataOperationsCatalog.TrainTestData split, uint seconds, string label, long rows)
    {
        var experiment = ml.Auto().CreateMulticlassClassificationExperiment(new MulticlassExperimentSettings
        {
            MaxExperimentTimeInSeconds = seconds,
            OptimizingMetric = MulticlassClassificationMetric.MicroAccuracy,
        });
        var result = experiment.Execute(split.TrainSet, columns.ColumnInformation);
        var metrics = ml.MulticlassClassification.Evaluate(result.BestRun.Model.Transform(split.TestSet), labelColumnName: label);
        return new TrainingResult("MulticlassClassification", result.BestRun.TrainerName,
            "MicroAccuracy", metrics.MicroAccuracy, "MacroAccuracy", metrics.MacroAccuracy, rows);
    }

    private static TrainingResult Regression(MLContext ml, ColumnInferenceResults columns, DataOperationsCatalog.TrainTestData split, uint seconds, string label, long rows)
    {
        var experiment = ml.Auto().CreateRegressionExperiment(new RegressionExperimentSettings
        {
            MaxExperimentTimeInSeconds = seconds,
            OptimizingMetric = RegressionMetric.RSquared,
        });
        var result = experiment.Execute(split.TrainSet, columns.ColumnInformation);
        var metrics = ml.Regression.Evaluate(result.BestRun.Model.Transform(split.TestSet), labelColumnName: label);
        return new TrainingResult("Regression", result.BestRun.TrainerName,
            "RSquared", metrics.RSquared, "RMSE", metrics.RootMeanSquaredError, rows);
    }
}
