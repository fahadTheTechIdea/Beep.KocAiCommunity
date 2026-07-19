using Beep.KocAiCommunity.Application.ML;
using Beep.KocAiCommunity.Domain.Studio;

namespace Beep.KocAiCommunity.Application.Studio;

/// <summary>Raised when an inference request cannot be served (missing/unservable model, authz).</summary>
public sealed class InferenceException(string message) : Exception(message);

/// <summary>One feature's drift signal: the baseline mean versus the batch mean and the shift.</summary>
public sealed record FeatureDrift(string Feature, double BaselineMean, double BatchMean, double MeanShift, bool Drifted);

/// <summary>A drift report for a scored batch against the model's training baseline.</summary>
public sealed record DriftReport(long BaselineRows, int BatchRows, IReadOnlyList<FeatureDrift> Features, bool AnyDrift);

/// <summary>
/// Serves predictions from registered model versions: dynamic-schema scoring through the prediction
/// pool, per-call audit logging, classification/authz enforcement, and drift comparison.
/// </summary>
public interface IInferenceService
{
    /// <summary>
    /// Scores one or more input rows against a model version and records an inference log. Production
    /// versions are servable to any employee; non-production versions only to the owner or an admin.
    /// </summary>
    Task<InferenceResult> InferAsync(
        string callerUserId,
        bool isPlatformAdmin,
        Guid modelVersionId,
        string endpoint,
        IReadOnlyList<IReadOnlyDictionary<string, string>> rows,
        CancellationToken ct = default);

    /// <summary>Recent inference logs for a model version (owner/admin only), newest first.</summary>
    Task<IReadOnlyList<ModelInferenceLog>> GetLogsAsync(string callerUserId, bool isPlatformAdmin, Guid modelVersionId, int take = 50, CancellationToken ct = default);

    /// <summary>Compares a batch's numeric feature means against the model's training baseline.</summary>
    Task<DriftReport> ComputeDriftAsync(
        string callerUserId,
        bool isPlatformAdmin,
        Guid modelVersionId,
        IReadOnlyList<IReadOnlyDictionary<string, string>> rows,
        CancellationToken ct = default);
}
