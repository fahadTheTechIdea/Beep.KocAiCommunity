namespace Beep.KocAiCommunity.Application.Competitions;

/// <summary>
/// A trusted, server-side scorer. Plugins are registered in code — user-uploaded scoring scripts
/// are never executed. Scores a prediction stream against a hidden answer-key stream.
/// </summary>
public interface IScoringPlugin
{
    /// <summary>Stable code referenced by <c>Competition.ScorerCode</c> (e.g. "accuracy").</summary>
    string Code { get; }

    /// <summary>True when a higher score is better (drives leaderboard ordering).</summary>
    bool HigherIsBetter { get; }

    Task<double> ScoreAsync(Stream predictions, Stream answerKey, CancellationToken ct = default);
}

/// <summary>Resolves the registered scorer for a competition's <c>ScorerCode</c>.</summary>
public interface IScorerRegistry
{
    IScoringPlugin Resolve(string scorerCode);
}
