namespace Beep.KocAiCommunity.Application.ML;

/// <summary>
/// One scored record. Fields are populated as the task produces them: <see cref="PredictedLabel"/>
/// for classification, <see cref="Probability"/> for binary, <see cref="Score"/> for a scalar score
/// (or the regression prediction), and <see cref="Scores"/> for a multiclass per-class score vector.
/// </summary>
public sealed record Prediction(
    string? PredictedLabel,
    double? Probability,
    double? Score,
    IReadOnlyList<double>? Scores);

/// <summary>The result of scoring a batch of input rows through a model version.</summary>
public sealed record InferenceResult(IReadOnlyList<Prediction> Predictions);

/// <summary>
/// A thread-safe, hot-reloadable pool of loaded models keyed by model-version id. Because platform
/// models are trained on arbitrary user CSV schemas, ML.NET's compile-time-typed
/// <c>PredictionEnginePool&lt;TSrc,TDst&gt;</c> cannot be used; this pool caches the loaded
/// <c>ITransformer</c> and scores rows against the model's own input schema dynamically.
/// The concrete implementation lives in the ML project and is registered by the executable hosts.
/// </summary>
public interface IPredictionPool
{
    /// <summary>
    /// Scores <paramref name="rows"/> (each a column→value map) against the cached model for
    /// <paramref name="modelVersionId"/>, loading it via <paramref name="modelLoader"/> on a cache miss.
    /// The <paramref name="labelColumn"/> is filled with a type-appropriate placeholder when absent.
    /// </summary>
    Task<InferenceResult> PredictAsync(
        Guid modelVersionId,
        Func<CancellationToken, Task<byte[]>> modelLoader,
        string labelColumn,
        IReadOnlyList<IReadOnlyDictionary<string, string>> rows,
        CancellationToken ct = default);

    /// <summary>Drops a model version from the cache (call on rollback/retire/delete).</summary>
    void Evict(Guid modelVersionId);
}
