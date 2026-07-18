namespace Beep.KocAiCommunity.Application.ML;

/// <summary>The kind of ML task AutoML should solve.</summary>
public enum MlTaskType
{
    BinaryClassification,
    MulticlassClassification,
    Regression,
}

/// <summary>
/// The outcome of a training run: the winning algorithm and two task-appropriate metrics
/// (e.g. Accuracy/AUC for binary, MicroAccuracy/MacroAccuracy for multiclass, R²/RMSE for regression).
/// </summary>
public sealed record TrainingResult(
    string Task,
    string Algorithm,
    string PrimaryMetric,
    double PrimaryValue,
    string SecondaryMetric,
    double SecondaryValue,
    long RowCount);

/// <summary>Trains a model from a CSV using ML.NET AutoML, time-boxed.</summary>
public interface IMlTrainer
{
    Task<TrainingResult> TrainAsync(MlTaskType task, Stream csv, string labelColumn, int maxSeconds, CancellationToken ct = default);
}
