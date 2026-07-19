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

/// <summary>One completed AutoML trial, reported live as training progresses.</summary>
public sealed record TrialReport(int TrialNumber, string TrainerName, string MetricName, double MetricValue, double RuntimeSeconds);

/// <summary>
/// A training outcome that also carries the serialized ML.NET model (.zip bytes) and a drift
/// baseline (per-feature numeric stats), so the run can be registered and later served for inference.
/// </summary>
public sealed record CapturedModel(TrainingResult Result, byte[] ModelBytes, string FeatureStatsJson);

/// <summary>Trains a model from a CSV using ML.NET AutoML, time-boxed.</summary>
public interface IMlTrainer
{
    Task<TrainingResult> TrainAsync(MlTaskType task, Stream csv, string labelColumn, int maxSeconds, CancellationToken ct = default);

    /// <summary>
    /// Same as <see cref="TrainAsync"/> but reports each trial to <paramref name="trials"/> as it
    /// completes. The reporter must be non-blocking — training never waits on the consumer.
    /// </summary>
    Task<TrainingResult> TrainWithTrialsAsync(MlTaskType task, Stream csv, string labelColumn, int maxSeconds, IProgress<TrialReport> trials, CancellationToken ct = default);

    /// <summary>
    /// Trains and returns the winning model serialized to bytes plus a drift baseline, so it can be
    /// stored in the registry and served. The model bytes are a self-contained ML.NET .zip.
    /// </summary>
    Task<CapturedModel> TrainAndCaptureAsync(MlTaskType task, Stream csv, string labelColumn, int maxSeconds, CancellationToken ct = default);
}
