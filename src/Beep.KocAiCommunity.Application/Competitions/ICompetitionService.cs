using Beep.KocAiCommunity.Application.Localization;
using Beep.KocAiCommunity.Contracts.Workflow;
using Beep.KocAiCommunity.Domain.Competitions;
using Beep.KocAiCommunity.Domain.Organization;

namespace Beep.KocAiCommunity.Application.Competitions;

/// <summary>Raised when a competition action is not permitted (visibility, quota, state).</summary>
public sealed class CompetitionException : Exception, IUserFacingMessage
{
    /// <summary>
    /// A message the member will read. Pass the English with <c>{0}</c> placeholders and the values
    /// separately — never an interpolated string, or the sentence cannot be looked up for translation.
    /// </summary>
    public CompetitionException(string template, params object[] args)
        : base(UserFacingMessage.Format(template, args))
    {
        Template = template;
        TemplateArgs = args;
    }

    public string Template { get; }

    public object[] TemplateArgs { get; }
}

/// <summary>
/// Raised when a user is not authorized to create a competition at the requested level
/// (no creator grant, or the requested scope exceeds their granted maximum). Maps to HTTP 403.
/// </summary>
public sealed class CompetitionAccessException : Exception, IUserFacingMessage
{
    /// <summary>
    /// A message the member will read. Pass the English with <c>{0}</c> placeholders and the values
    /// separately — never an interpolated string, or the sentence cannot be looked up for translation.
    /// </summary>
    public CompetitionAccessException(string template, params object[] args)
        : base(UserFacingMessage.Format(template, args))
    {
        Template = template;
        TemplateArgs = args;
    }

    public string Template { get; }

    public object[] TemplateArgs { get; }
}

/// <summary>A leaderboard row with the entrant's display name resolved for UI use.</summary>
public sealed record NamedLeaderboardEntry(int Rank, string UserId, string DisplayName, double Score);

/// <summary>Arena stats for a competition: distinct competitors, total submissions, and the host's display name.</summary>
public sealed record CompetitionStats(int ParticipantCount, int SubmissionCount, string HostName);

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

    /// <summary>Pins one competition as THE landing-page hero (clears the flag on any other). Platform admin only.</summary>
    Task SetFeaturedAsync(Guid competitionId, CancellationToken ct = default);

    /// <summary>Set (or clear, with blanks) the 1st/2nd/3rd podium prizes shown for a competition.</summary>
    Task SetPrizesAsync(Guid competitionId, string? first, string? second, string? third, CancellationToken ct = default);

    /// <summary>
    /// Store the web-relative path of the competition's hero image (the creator, or a platform admin).
    /// The image file itself is written to the web app's wwwroot; only the path is persisted here.
    /// Pass null/blank to clear it.
    /// </summary>
    Task SetHeroImagePathAsync(string userId, bool isPlatformAdmin, Guid competitionId, string? path, CancellationToken ct = default);

    /// <summary>The currently featured competition (the admin-pinned landing-page hero), or null.</summary>
    Task<Competition?> GetFeaturedAsync(CancellationToken ct = default);

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
    /// <summary>
    /// Runs a participant's graph on the competition's own data and records the score.
    /// <inheritdoc cref="SubmitAsync" path="/summary/para"/>
    /// </summary>
    Task<Submission> SubmitPipelineAsync(string userId, Guid competitionId, WorkflowDefinition definition, string? idempotencyKey = null, CancellationToken ct = default);

    Task<IReadOnlyList<Competition>> BrowseVisibleAsync(string userId, CancellationToken ct = default);

    /// <summary>
    /// Active, company-wide competitions for the signed-out landing preview (featured first). No user scope
    /// is applied — only Company-visible competitions are returned, so nothing team/group-private leaks.
    /// </summary>
    Task<IReadOnlyList<Competition>> BrowsePublicAsync(CancellationToken ct = default);

    /// <summary>
    /// Every company-wide competition whatever its status, for signed-out browsing of the arena and the
    /// leaderboards. <see cref="BrowsePublicAsync"/> is the landing preview and shows only what is running;
    /// somebody browsing the arena wants to see what has already been run too — a concluded competition
    /// with a finished board is the most interesting thing here, not the least.
    /// </summary>
    Task<IReadOnlyList<Competition>> BrowsePublicAllAsync(CancellationToken ct = default);

    /// <summary>
    /// One competition, but only if a signed-out visitor is allowed to see it. Returns null for anything
    /// team/group/directorate-scoped or in a disabled category, so holding the id is not enough to read a
    /// private competition. This is the leak rule for every anonymous read — keep it in one place.
    /// </summary>
    Task<Competition?> GetPublicAsync(Guid competitionId, CancellationToken ct = default);

    Task<Competition?> GetAsync(Guid competitionId, CancellationToken ct = default);

    // ---- Categories ----
    // Grouping by KOC operational domain. The list is data, owned by the platform admin: disabling a
    // category hides every competition in it from browsing and from direct links, without deleting
    // anything, so a theme can be staged before launch or retired afterwards.

    /// <summary>Categories, ordered. <paramref name="includeDisabled"/> is for the admin console only.</summary>
    Task<IReadOnlyList<CompetitionCategory>> ListCategoriesAsync(bool includeDisabled, CancellationToken ct = default);

    /// <summary>Creates or updates a category by its code, returning the saved row.</summary>
    Task<CompetitionCategory> UpsertCategoryAsync(
        string actorUserId, string code, string name, string description, string icon, bool isEnabled, int orderNo, CancellationToken ct = default);

    /// <summary>
    /// Removes a category. Refused while competitions still reference it — reassign or disable instead,
    /// so a delete can never orphan a challenge into an unrecognised code.
    /// </summary>
    Task DeleteCategoryAsync(string actorUserId, string code, CancellationToken ct = default);

    /// <summary>Assigns a competition to a category, or clears it when <paramref name="code"/> is null.</summary>
    Task SetCompetitionCategoryAsync(string actorUserId, Guid competitionId, string? code, CancellationToken ct = default);

    /// <summary>Arena stats for a set of competitions, computed in one pass (no per-competition queries).</summary>
    Task<IReadOnlyDictionary<Guid, CompetitionStats>> GetStatsAsync(IReadOnlyCollection<Guid> competitionIds, CancellationToken ct = default);

    /// <summary>
    /// Scores a prediction file against the hidden key and updates the leaderboard.
    /// <para>
    /// <paramref name="idempotencyKey"/> makes a retry safe. Submissions are quota-limited, so a client
    /// that resends a request it is not sure was received — anything queueing work offline — would
    /// otherwise have to choose between losing the work and spending someone's quota twice. Given the
    /// same key, the submission already recorded is returned unchanged and no quota is consumed.
    /// </para>
    /// </summary>
    Task<Submission> SubmitAsync(string userId, Guid competitionId, Stream predictions, string fileName, string? idempotencyKey = null, CancellationToken ct = default);

    Task<IReadOnlyList<LeaderboardEntry>> GetLeaderboardAsync(Guid competitionId, CancellationToken ct = default);

    /// <summary>Leaderboard with each entrant's community display name resolved (falls back to the id).</summary>
    Task<IReadOnlyList<NamedLeaderboardEntry>> GetLeaderboardNamedAsync(Guid competitionId, string board, CancellationToken ct = default);
    Task<IReadOnlyList<Submission>> GetMySubmissionsAsync(string userId, Guid competitionId, CancellationToken ct = default);

    /// <summary>
    /// Opens a participant-visible dataset (<c>which</c> = "training" or "evaluation") for download.
    /// The hidden answer key is never downloadable. Returns null when the dataset is not set.
    /// </summary>
    Task<Stream?> OpenDatasetAsync(string userId, Guid competitionId, string which, CancellationToken ct = default);
}
