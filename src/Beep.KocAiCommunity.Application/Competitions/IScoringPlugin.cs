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

    /// <summary>The ML task types this scorer is valid for (guards mismatched competition setup).</summary>
    IReadOnlyCollection<string> SupportedTasks { get; }

    /// <summary>
    /// Scores predictions against the hidden answer key, aligned <b>by id</b>. Both CSVs are
    /// <c>{idColumn},value</c> with an optional header; <paramref name="idColumn"/> is the competition's
    /// configured id column, used to detect and skip that header.
    /// </summary>
    Task<double> ScoreAsync(Stream predictions, Stream answerKey, string idColumn = "id", CancellationToken ct = default);
}

/// <summary>Resolves the registered scorer for a competition's <c>ScorerCode</c>.</summary>
public interface IScorerRegistry
{
    IScoringPlugin Resolve(string scorerCode);
}
