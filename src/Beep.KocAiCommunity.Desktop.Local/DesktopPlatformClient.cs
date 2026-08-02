using Beep.KocAiCommunity.Application.Competitions;
using Beep.KocAiCommunity.Application.Experiments;
using Beep.KocAiCommunity.Client;
using Beep.KocAiCommunity.Contracts.Competitions;
using Beep.KocAiCommunity.Contracts.Experiments;

namespace Beep.KocAiCommunity.Desktop.Local;

/// <summary>
/// KOC Studio's own connection to the platform database.
/// <para>
/// There is no API website between the desktop and the data any more. This calls the same application
/// services the website's endpoints call, in this process, against the shared database — so a
/// workstation reads competitions and records a submission without a second site in the middle.
/// </para>
/// <para>
/// It covers only what the desktop actually asks for. Everything else inherits the base's guarded
/// fallback and throws by name, which is the honest outcome: a desktop build that quietly returned
/// nothing would look like an empty platform rather than a missing implementation.
/// </para>
/// <para>
/// <b>The authorization consequence, stated plainly:</b> on a workstation this process decides who the
/// user is. The website checks a token it issued; here the identity is the signed-in Windows account,
/// taken as a KOC employee. That is inherent to reading the database directly from a laptop, and it is
/// why the desktop exposes no administrative surface — see <c>docs/DESKTOP_DIRECT_DATABASE.md</c>.
/// </para>
/// </summary>
public sealed class DesktopPlatformClient(
    ICompetitionService competitions,
    IExperimentService experiments,
    IScorerRegistry scorers,
    SignedInUser me) : RemoteFallbackKocApiClient(remote: null)
{
    private string UserId => me.Current?.UserId
        ?? throw new InvalidOperationException("No signed-in Windows user — KOC Studio cannot identify you.");

    // ---- Competitions ----

    public override async Task<IReadOnlyList<CompetitionDto>> GetCompetitionsAsync(CancellationToken ct = default)
    {
        var visible = await competitions.BrowseVisibleAsync(UserId, ct);
        var stats = await competitions.GetStatsAsync([.. visible.Select(c => c.Id)], ct);
        return [.. visible.Select(c => ToDto(c, stats.GetValueOrDefault(c.Id)))];
    }

    public override async Task<CompetitionDto?> GetCompetitionAsync(Guid competitionId, CancellationToken ct = default)
    {
        var competition = await competitions.GetAsync(competitionId, ct);
        if (competition is null)
        {
            return null;
        }

        var stats = await competitions.GetStatsAsync([competitionId], ct);
        return ToDto(competition, stats.GetValueOrDefault(competitionId));
    }

    public override async Task<string?> GetCompetitionDataAsync(Guid competitionId, string which, CancellationToken ct = default)
    {
        await using var stream = await competitions.OpenDatasetAsync(UserId, competitionId, which, ct);
        if (stream is null)
        {
            return null;
        }

        using var reader = new StreamReader(stream);
        return await reader.ReadToEndAsync(ct);
    }

    public override async Task<IReadOnlyList<LeaderboardEntryDto>> GetLeaderboardAsync(
        Guid competitionId, CancellationToken ct = default)
    {
        var named = await competitions.GetLeaderboardNamedAsync(competitionId, "live", ct);
        return [.. named.Select(e => new LeaderboardEntryDto(e.Rank, e.UserId, e.DisplayName, e.Score))];
    }

    public override async Task<IReadOnlyList<SubmissionResultDto>> GetMySubmissionsAsync(
        Guid competitionId, CancellationToken ct = default)
    {
        var mine = await competitions.GetMySubmissionsAsync(UserId, competitionId, ct);
        return [.. mine.Select(s => new SubmissionResultDto(s.Id, s.Score, s.Status, s.SubmittedUtc))];
    }

    public override async Task<SubmissionResultDto?> SubmitAsync(
        Guid competitionId, Stream csv, string fileName, string? idempotencyKey = null, CancellationToken ct = default)
    {
        var submission = await competitions.SubmitAsync(UserId, competitionId, csv, fileName, idempotencyKey, ct);
        return new SubmissionResultDto(submission.Id, submission.Score, submission.Status, submission.SubmittedUtc);
    }

    public override async Task<SubmissionResultDto?> SubmitPipelineAsync(
        Guid competitionId, Contracts.Workflow.WorkflowDefinition definition,
        string? idempotencyKey = null, CancellationToken ct = default)
    {
        var submission = await competitions.SubmitPipelineAsync(UserId, competitionId, definition, idempotencyKey, ct);
        return new SubmissionResultDto(submission.Id, submission.Score, submission.Status, submission.SubmittedUtc);
    }

    // ---- Experiments ----

    public override Task<IReadOnlyList<ExperimentDto>> GetExperimentsAsync(CancellationToken ct = default) =>
        experiments.ListForOwnerAsync(UserId, ct);

    public override async Task<ExperimentDto?> CreateExperimentAsync(
        CreateExperimentRequest request, CancellationToken ct = default) =>
        await experiments.CreateAsync(UserId, request, ct);

    public override Task<IReadOnlyList<RunDto>> GetExperimentRunsAsync(Guid experimentId, CancellationToken ct = default) =>
        experiments.ListRunsAsync(experimentId, ct);

    public override Task<IReadOnlyList<ComparisonRowDto>> GetExperimentCompareAsync(
        Guid experimentId, CancellationToken ct = default) =>
        experiments.CompareAsync(experimentId, ct);

    /// <summary>
    /// The competition's headline metric, resolved the same way the website resolves it, so a
    /// competition does not read as "rmse" here and "RMSE" there.
    /// </summary>
    private CompetitionDto ToDto(Domain.Competitions.Competition c, CompetitionStats? stats)
    {
        var scorer = scorers.Resolve(c.ScorerCode);
        var metric = scorer.Code.Equals("rmse", StringComparison.OrdinalIgnoreCase) ? "RMSE"
            : scorer.Code.Equals("accuracy", StringComparison.OrdinalIgnoreCase) ? "Accuracy"
            : scorer.Code.Equals("auc", StringComparison.OrdinalIgnoreCase) ? "AUC"
            : scorer.Code;

        return new CompetitionDto(
            c.Id, c.Title, c.Description, c.Status, c.VisibilityScope.ToString(), c.RevealUtc,
            c.AnswerKeyArtifactId is not null,
            c.TrainingDatasetArtifactId is not null && c.EvaluationArtifactId is not null,
            c.LabelColumn, c.IdColumn, c.TaskType, c.RecommendedTrackId,
            ParticipantCount: stats?.ParticipantCount ?? 0,
            SubmissionCount: stats?.SubmissionCount ?? 0,
            QuotaPerDay: c.SubmissionQuotaPerDay,
            MetricName: metric,
            HigherIsBetter: scorer.HigherIsBetter,
            CreatedUtc: c.CreatedUtc);
    }
}
