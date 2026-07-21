using Beep.KocAiCommunity.Contracts.Workflow;
using Beep.KocAiCommunity.Domain.Competitions;
using Beep.KocAiCommunity.Domain.Organization;

namespace Beep.KocAiCommunity.Application.Competitions;

/// <summary>Raised when a competition action is not permitted (visibility, quota, state).</summary>
public sealed class CompetitionException(string message) : Exception(message);

/// <summary>
/// Raised when a user is not authorized to create a competition at the requested level
/// (no creator grant, or the requested scope exceeds their granted maximum). Maps to HTTP 403.
/// </summary>
public sealed class CompetitionAccessException(string message) : Exception(message);

/// <summary>A leaderboard row with the entrant's display name resolved for UI use.</summary>
public sealed record NamedLeaderboardEntry(int Rank, string UserId, string DisplayName, double Score);

public interface ICompetitionService
{
    /// <summary>
    /// Creates a competition. The caller must hold a <c>CompetitionCreatorGrant</c> whose max scope
    /// covers <paramref name="scope"/>, or be a platform admin (<paramref name="isPlatformAdmin"/>);
    /// otherwise a <see cref="CompetitionAccessException"/> is thrown.
    /// </summary>
    Task<Competition> CreateAsync(
        string userId,
        bool isPlatformAdmin,
        string title,
        string description,
        VisibilityScope scope,
        Guid visibilityOrgUnitId,
        DateTime? revealUtc,
        int quotaPerDay,
        string scorerCode,
        CancellationToken ct = default);

    /// <summary>
    /// The widest audience level this user may create a competition at: <c>Company</c> for a platform
    /// admin, else their active creator grant's max scope, else <c>null</c> (not allowed to create).
    /// </summary>
    Task<VisibilityScope?> GetMaxCreateScopeAsync(string userId, bool isPlatformAdmin, CancellationToken ct = default);

    /// <summary>Uploads (or replaces) the hidden answer key. Creator only.</summary>
    Task SetAnswerKeyAsync(string userId, Guid competitionId, Stream answerKey, CancellationToken ct = default);

    /// <summary>Moves a competition through its lifecycle (draft → active → concluded). Creator only.</summary>
    Task SetStatusAsync(string userId, Guid competitionId, string status, CancellationToken ct = default);

    /// <summary>Sets (or clears) the reveal time that unlocks the final leaderboard. Creator only.</summary>
    Task SetRevealAsync(string userId, Guid competitionId, DateTime? revealUtc, CancellationToken ct = default);

    /// <summary>
    /// Uploads the participant-visible training data and evaluation feature set, plus the column names
    /// a pipeline runner needs (label, id, task). Creator only.
    /// </summary>
    Task SetDatasetsAsync(
        string userId, Guid competitionId, Stream trainingData, Stream evaluationData,
        string labelColumn, string idColumn, string taskType, CancellationToken ct = default);

    /// <summary>
    /// Runs a participant's pipeline definition on the competition's authoritative training and
    /// evaluation data (so no one can tamper with the inputs), scores the predictions against the
    /// hidden key, and updates the leaderboard. This turns a Studio pipeline into a submission.
    /// </summary>
    Task<Submission> SubmitPipelineAsync(string userId, Guid competitionId, WorkflowDefinition definition, CancellationToken ct = default);

    Task<IReadOnlyList<Competition>> BrowseVisibleAsync(string userId, CancellationToken ct = default);
    Task<Competition?> GetAsync(Guid competitionId, CancellationToken ct = default);

    /// <summary>Scores a prediction file against the hidden key and updates the leaderboard.</summary>
    Task<Submission> SubmitAsync(string userId, Guid competitionId, Stream predictions, string fileName, CancellationToken ct = default);

    Task<IReadOnlyList<LeaderboardEntry>> GetLeaderboardAsync(Guid competitionId, CancellationToken ct = default);

    /// <summary>Leaderboard with each entrant's community display name resolved (falls back to the id).</summary>
    Task<IReadOnlyList<NamedLeaderboardEntry>> GetLeaderboardNamedAsync(Guid competitionId, CancellationToken ct = default);
    Task<IReadOnlyList<Submission>> GetMySubmissionsAsync(string userId, Guid competitionId, CancellationToken ct = default);

    /// <summary>
    /// Opens a participant-visible dataset (<c>which</c> = "training" or "evaluation") for download.
    /// The hidden answer key is never downloadable. Returns null when the dataset is not set.
    /// </summary>
    Task<Stream?> OpenDatasetAsync(string userId, Guid competitionId, string which, CancellationToken ct = default);
}
