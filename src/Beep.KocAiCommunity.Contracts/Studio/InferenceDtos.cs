namespace Beep.KocAiCommunity.Contracts.Studio;

/// <summary>An online inference request: one record as a column→value map.</summary>
public sealed record InferRequest(IReadOnlyDictionary<string, string> Input);

/// <summary>A batch inference request: many records.</summary>
public sealed record BatchInferRequest(IReadOnlyList<IReadOnlyDictionary<string, string>> Rows);

/// <summary>One scored record. Fields are populated per task (classification/binary/regression/multiclass).</summary>
public sealed record PredictionDto(
    string? PredictedLabel,
    double? Probability,
    double? Score,
    IReadOnlyList<double>? Scores);

/// <summary>The scored predictions for a request, in input order.</summary>
public sealed record InferResponseDto(IReadOnlyList<PredictionDto> Predictions);

/// <summary>An audit record of one served inference call.</summary>
public sealed record InferenceLogDto(
    Guid Id, string CallerUserId, string Endpoint, int RowCount, int LatencyMs, DateTime CalledUtc, bool Success, string? Error);

/// <summary>A batch to compare against the model's training baseline.</summary>
public sealed record DriftRequest(IReadOnlyList<IReadOnlyDictionary<string, string>> Rows);

/// <summary>One feature's drift signal.</summary>
public sealed record FeatureDriftDto(string Feature, double BaselineMean, double BatchMean, double MeanShift, bool Drifted);

/// <summary>A drift report for a batch against the training baseline.</summary>
public sealed record DriftReportDto(long BaselineRows, int BatchRows, IReadOnlyList<FeatureDriftDto> Features, bool AnyDrift);
