using System.Net;
using System.Net.Http.Json;
using Beep.KocAiCommunity.Contracts.Admin;
using Beep.KocAiCommunity.Contracts.Community;
using Beep.KocAiCommunity.Contracts.Competitions;
using Beep.KocAiCommunity.Contracts.Connectors;
using Beep.KocAiCommunity.Contracts.Dashboard;
using Beep.KocAiCommunity.Contracts.Datasets;
using Beep.KocAiCommunity.Contracts.Engagement;
using Beep.KocAiCommunity.Contracts.Experiments;
using Beep.KocAiCommunity.Contracts.Help;
using Beep.KocAiCommunity.Contracts.Identity;
using Beep.KocAiCommunity.Contracts.Jobs;
using Beep.KocAiCommunity.Contracts.Learning;
using Beep.KocAiCommunity.Contracts.ML;
using Beep.KocAiCommunity.Contracts.Notifications;
using Beep.KocAiCommunity.Contracts.Studio;
using Beep.KocAiCommunity.Contracts.Supervision;
using Beep.KocAiCommunity.Contracts.Workflow;
using ExperimentRunDto = Beep.KocAiCommunity.Contracts.Experiments.RunDto;
// RunDto exists in both Jobs (background jobs) and Experiments; the unqualified name means the job one.
using RunDto = Beep.KocAiCommunity.Contracts.Jobs.RunDto;

namespace Beep.KocAiCommunity.Web.Services;

/// <summary>Typed client the Web uses to talk to <c>/api/v1</c>. The Web never touches the database.</summary>
public interface IKocApiClient
{
    Task<MeResponse?> GetMeAsync(CancellationToken ct = default);
    Task<IReadOnlyList<TrackDto>> GetTracksAsync(CancellationToken ct = default);
    Task<TrackDetailDto?> GetTrackAsync(Guid trackId, CancellationToken ct = default);
    Task EnrollAsync(Guid trackId, CancellationToken ct = default);
    Task CompleteLessonAsync(Guid trackId, Guid lessonId, CancellationToken ct = default);
    Task<IReadOnlyList<MyLearningDto>> GetMyLearningAsync(CancellationToken ct = default);
    Task<IReadOnlyList<CompetitionDto>> GetCompetitionsAsync(CancellationToken ct = default);
    Task<CompetitionDto?> CreateCompetitionAsync(CreateCompetitionRequest request, CancellationToken ct = default);
    Task SetAnswerKeyAsync(Guid competitionId, Stream csv, string fileName, CancellationToken ct = default);
    Task<SubmissionResultDto?> SubmitAsync(Guid competitionId, Stream csv, string fileName, CancellationToken ct = default);
    Task<SubmissionResultDto?> SubmitPipelineAsync(Guid competitionId, WorkflowDefinition definition, CancellationToken ct = default);
    Task SetCompetitionDatasetsAsync(Guid competitionId, Stream training, Stream evaluation, string labelColumn, string idColumn, string task, CancellationToken ct = default);
    Task<string?> GetCompetitionDataAsync(Guid competitionId, string which, CancellationToken ct = default);
    Task<IReadOnlyList<SubmissionResultDto>> GetMySubmissionsAsync(Guid competitionId, CancellationToken ct = default);
    Task<IReadOnlyList<LeaderboardEntryDto>> GetLeaderboardAsync(Guid competitionId, CancellationToken ct = default);
    Task<IReadOnlyList<NotificationDto>> GetNotificationsAsync(bool unreadOnly = false, int take = 30, CancellationToken ct = default);
    Task<int> GetUnreadNotificationCountAsync(CancellationToken ct = default);
    Task MarkNotificationReadAsync(Guid id, CancellationToken ct = default);
    Task MarkAllNotificationsReadAsync(CancellationToken ct = default);
    Task SetCompetitionStatusAsync(Guid competitionId, string status, CancellationToken ct = default);
    Task SetCompetitionRevealAsync(Guid competitionId, DateTime? revealUtc, CancellationToken ct = default);
    /// <summary>Final standings; null when still concealed until the reveal time.</summary>
    Task<IReadOnlyList<LeaderboardEntryDto>?> GetFinalLeaderboardAsync(Guid competitionId, CancellationToken ct = default);

    /// <summary>Returns null when the caller is not a supervisor (403).</summary>
    Task<SupervisionRollupDto?> GetSupervisionRollupAsync(CancellationToken ct = default);
    Task<PersonalDashboardDto?> GetPersonalDashboardAsync(CancellationToken ct = default);

    Task<IReadOnlyList<ProjectDto>> GetProjectsAsync(CancellationToken ct = default);
    Task<ProjectDto?> CreateProjectAsync(CreateProjectRequest request, CancellationToken ct = default);
    Task<ProjectDetailDto?> GetProjectAsync(Guid id, CancellationToken ct = default);
    Task SaveProjectAsync(Guid id, SaveProjectRequest request, CancellationToken ct = default);
    Task DeleteProjectAsync(Guid id, CancellationToken ct = default);

    Task<IReadOnlyList<DatasetDto>> GetDatasetsAsync(CancellationToken ct = default);
    Task<DatasetDto?> CreateDatasetAsync(CreateDatasetRequest request, CancellationToken ct = default);
    Task<VisibilityOptionDto?> GetAudienceAsync(string scope, CancellationToken ct = default);

    // Dataset versioned contents.
    Task<IReadOnlyList<DatasetVersionDto>> GetDatasetVersionsAsync(Guid datasetId, CancellationToken ct = default);
    Task<DatasetVersionDetailDto?> GetDatasetVersionAsync(Guid datasetId, int versionNumber, CancellationToken ct = default);
    Task<(DatasetVersionDto? Version, string? Error)> UploadDatasetFileAsync(Guid datasetId, Stream content, string fileName, CancellationToken ct = default);
    Task<(DatasetVersionDto? Version, string? Error)> ImportDatasetUrlAsync(Guid datasetId, string url, CancellationToken ct = default);
    Task<string?> PublishDatasetVersionAsync(Guid datasetId, int versionNumber, CancellationToken ct = default);
    string DatasetFileDownloadUrl(Guid fileId);

    Task<ModelRunDto?> TrainAsync(Stream csv, string fileName, string labelColumn, string datasetName, string task, CancellationToken ct = default);
    Task<(ModelRunDto? Run, string? Error)> TrainFromDatasetAsync(Guid datasetId, string labelColumn, string task, CancellationToken ct = default);
    Task<IReadOnlyList<ModelRunDto>> GetModelRunsAsync(CancellationToken ct = default);

    Task<IReadOnlyList<DiscussionDto>> GetDiscussionsAsync(CancellationToken ct = default);
    Task<DiscussionDto?> CreateDiscussionAsync(CreateDiscussionRequest request, CancellationToken ct = default);
    Task<DiscussionDetailDto?> GetDiscussionAsync(Guid id, CancellationToken ct = default);
    Task<ReplyDto?> AddReplyAsync(Guid discussionId, string body, CancellationToken ct = default);
    Task<IReadOnlyList<ReactionDto>> ReactToDiscussionAsync(Guid id, string emoji, CancellationToken ct = default);
    Task<IReadOnlyList<ReactionDto>> ReactToReplyAsync(Guid discussionId, Guid replyId, string emoji, CancellationToken ct = default);
    Task<string?> SetDiscussionLockAsync(Guid id, bool locked, CancellationToken ct = default);
    Task<string?> SetDiscussionPinAsync(Guid id, bool pinned, CancellationToken ct = default);
    Task<string?> DeleteDiscussionAsync(Guid id, CancellationToken ct = default);
    Task<string?> DeleteReplyAsync(Guid discussionId, Guid replyId, CancellationToken ct = default);
    Task<IReadOnlyList<MentionCandidateDto>> SearchMentionCandidatesAsync(string q, CancellationToken ct = default);
    Task<(AttachmentDto? Attachment, string? Error)> AddAttachmentAsync(Guid discussionId, Stream content, string fileName, CancellationToken ct = default);

    Task<WorkflowValidationResult?> ValidateWorkflowAsync(WorkflowDefinition definition, CancellationToken ct = default);
    Task<ModelRunDto?> RunWorkflowAsync(WorkflowDefinition definition, Stream csv, string fileName, string labelColumn, CancellationToken ct = default);
    Task<PipelineExecutionResult?> ExecuteWorkflowAsync(WorkflowDefinition definition, Stream csv, string fileName, string labelColumn, string task, CancellationToken ct = default);
    Task<(PipelineExecutionResult? Result, string? Error)> ExecuteWorkflowFromDatasetAsync(WorkflowDefinition definition, Guid datasetId, string labelColumn, string task, CancellationToken ct = default);

    // Enterprise connectors (admin).
    Task<IReadOnlyList<ConnectorDescriptorDto>> GetConnectorsAsync(CancellationToken ct = default);
    Task<IReadOnlyList<ConnectorInstanceDto>> GetConnectorInstancesAsync(string code, CancellationToken ct = default);
    Task<(ConnectorInstanceDto? Instance, string? Error)> CreateConnectorInstanceAsync(string code, CreateConnectorInstanceRequest request, CancellationToken ct = default);
    Task<ConnectorInstanceDetailDto?> GetConnectorInstanceAsync(Guid id, CancellationToken ct = default);
    Task<(CredentialInfoDto? Credential, string? Error)> SetConnectorCredentialAsync(Guid id, SetCredentialRequest request, CancellationToken ct = default);
    Task<(ConnectorTestDto? Result, string? Error)> TestConnectorAsync(Guid id, CancellationToken ct = default);
    Task<(ConnectorHealthDto? Health, string? Error)> ProbeConnectorHealthAsync(Guid id, CancellationToken ct = default);
    Task<ConnectorSchemaDto?> GetConnectorSchemaAsync(Guid id, CancellationToken ct = default);
    Task<string?> DeleteConnectorInstanceAsync(Guid id, CancellationToken ct = default);

    // In-app help.
    Task<IReadOnlyList<HelpArticleSummaryDto>> GetHelpArticlesAsync(string? category = null, string? q = null, CancellationToken ct = default);
    Task<HelpArticleDto?> GetHelpArticleAsync(string slug, CancellationToken ct = default);

    // ML node catalog.
    Task<IReadOnlyList<NodeDescriptorDto>> GetMlNodesAsync(CancellationToken ct = default);
    Task<IReadOnlyList<MlTaskDto>> GetMlTasksAsync(CancellationToken ct = default);

    // Platform admin.
    Task<AdminDashboardDto?> GetAdminDashboardAsync(CancellationToken ct = default);
    Task<IReadOnlyList<SettingDto>> GetSettingsAsync(CancellationToken ct = default);
    Task<(SettingDto? Setting, string? Error)> UpdateSettingAsync(string key, string value, CancellationToken ct = default);
    Task<IReadOnlyList<FeatureFlagDto>> GetFeatureFlagsAsync(CancellationToken ct = default);
    Task<(FeatureFlagDto? Flag, string? Error)> UpsertFeatureFlagAsync(string key, UpsertFeatureFlagRequest request, CancellationToken ct = default);
    Task<IReadOnlyList<AuditLogDto>> GetAuditAsync(string? action = null, CancellationToken ct = default);

    // Versioned workflow registry.
    Task<IReadOnlyList<WorkflowSummaryDto>> GetWorkflowsAsync(CancellationToken ct = default);
    Task<(WorkflowSummaryDto? Workflow, string? Error)> CreateWorkflowAsync(CreateWorkflowRequest request, CancellationToken ct = default);
    Task<WorkflowDetailDto?> GetWorkflowDetailAsync(Guid id, CancellationToken ct = default);
    Task<WorkflowVersionDetailDto?> GetWorkflowVersionAsync(Guid id, int versionNumber, CancellationToken ct = default);
    Task<(WorkflowVersionDto? Version, string? Error)> SaveWorkflowDraftAsync(Guid id, SaveDraftRequest request, CancellationToken ct = default);
    Task<string?> DeleteWorkflowAsync(Guid id, CancellationToken ct = default);
    Task<string?> PublishWorkflowVersionAsync(Guid id, int versionNumber, CancellationToken ct = default);
    Task<string?> ArchiveWorkflowVersionAsync(Guid id, int versionNumber, CancellationToken ct = default);
    Task<(Guid? RunId, string? Error)> RunWorkflowVersionAsync(Guid id, int versionNumber, RunWorkflowVersionRequest request, CancellationToken ct = default);
    Task<WorkflowExportDto?> ExportWorkflowVersionAsync(Guid id, int versionNumber, CancellationToken ct = default);
    Task<(WorkflowSummaryDto? Workflow, string? Error)> ImportWorkflowAsync(ImportWorkflowRequest request, CancellationToken ct = default);
    Task<IReadOnlyList<WorkflowTemplateDto>> GetWorkflowTemplatesAsync(CancellationToken ct = default);
    Task<(WorkflowSummaryDto? Workflow, string? Error)> InstantiateTemplateAsync(string code, InstantiateTemplateRequest request, CancellationToken ct = default);

    Task<IReadOnlyList<RegisteredModelDto>> GetModelsAsync(CancellationToken ct = default);
    Task<ModelVersionDto?> RegisterModelAsync(RegisterModelRequest request, CancellationToken ct = default);
    Task<string?> ApproveVersionAsync(Guid versionId, CancellationToken ct = default);
    Task<string?> PromoteVersionAsync(Guid versionId, CancellationToken ct = default);
    Task<string?> RollbackVersionAsync(Guid versionId, CancellationToken ct = default);
    Task<string?> DeployVersionAsync(Guid versionId, CancellationToken ct = default);
    Task<string?> RetireDeploymentAsync(Guid deploymentId, CancellationToken ct = default);
    Task<IReadOnlyList<DeploymentDto>> GetDeploymentsAsync(CancellationToken ct = default);

    // Inference serving: online/batch scoring, audit logs, drift check.
    Task<(InferResponseDto? Result, string? Error)> InferAsync(Guid versionId, IReadOnlyDictionary<string, string> input, CancellationToken ct = default);
    Task<(InferResponseDto? Result, string? Error)> InferBatchAsync(Guid versionId, IReadOnlyList<IReadOnlyDictionary<string, string>> rows, CancellationToken ct = default);
    Task<IReadOnlyList<InferenceLogDto>> GetInferenceLogsAsync(Guid versionId, CancellationToken ct = default);
    Task<(DriftReportDto? Report, string? Error)> ComputeDriftAsync(Guid versionId, IReadOnlyList<IReadOnlyDictionary<string, string>> rows, CancellationToken ct = default);

    // Engagement: Barrels, career ladder, badges, streaks, kudos, leaderboards, activity.
    Task<ProfileDto?> GetMyProfileAsync(CancellationToken ct = default);
    Task<ProfileDto?> GetProfileAsync(string userId, CancellationToken ct = default);
    Task<ProfileDto?> UpdateProfileAsync(UpdateProfileRequest request, CancellationToken ct = default);
    Task<IReadOnlyList<XpLeaderboardRowDto>> GetXpLeaderboardAsync(string period, CancellationToken ct = default);
    Task<IReadOnlyList<TeamLeaderboardRowDto>> GetTeamLeaderboardAsync(string period, CancellationToken ct = default);
    Task<IReadOnlyList<BadgeDto>> GetBadgeCatalogAsync(CancellationToken ct = default);
    Task<IReadOnlyList<string>> GetAvatarIconsAsync(CancellationToken ct = default);
    /// <summary>Returns null on success, or the error message on a 400.</summary>
    Task<string?> GiveKudosAsync(GiveKudosRequest request, CancellationToken ct = default);
    Task<IReadOnlyList<KudosDto>> GetKudosAsync(string userId, CancellationToken ct = default);
    Task<IReadOnlyList<ActivityDto>> GetActivityAsync(CancellationToken ct = default);

    // Runs (durable background jobs).
    Task<IReadOnlyList<RunDto>> GetRunsAsync(CancellationToken ct = default);
    Task<RunDto?> GetRunAsync(Guid id, CancellationToken ct = default);
    Task<RunDto?> CreateRunAsync(CreateRunRequest request, CancellationToken ct = default);
    Task CancelRunAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<RunLogDto>> GetRunLogsAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<RunAttemptDto>> GetRunAttemptsAsync(Guid id, CancellationToken ct = default);

    // Experiment tracking.
    Task<IReadOnlyList<ExperimentDto>> GetExperimentsAsync(CancellationToken ct = default);
    Task<ExperimentDto?> CreateExperimentAsync(CreateExperimentRequest request, CancellationToken ct = default);
    Task<IReadOnlyList<ExperimentRunDto>> GetExperimentRunsAsync(Guid experimentId, CancellationToken ct = default);
    Task<IReadOnlyList<ComparisonRowDto>> GetExperimentCompareAsync(Guid experimentId, CancellationToken ct = default);
    Task<IReadOnlyList<RunMetricDto>> GetRunMetricsAsync(Guid runId, CancellationToken ct = default);
    Task<IReadOnlyList<RunParameterDto>> GetRunParametersAsync(Guid runId, CancellationToken ct = default);
    Task<ExperimentRunDto?> UpdateExperimentRunAsync(Guid runId, UpdateRunRequest request, CancellationToken ct = default);
    Task<(ModelVersionDto? Version, string? Error)> RegisterExperimentRunAsync(Guid runId, string modelName, CancellationToken ct = default);
}

public sealed class KocApiClient(HttpClient http) : IKocApiClient
{
    public Task<MeResponse?> GetMeAsync(CancellationToken ct = default) =>
        http.GetFromJsonAsync<MeResponse>("/api/v1/me", ct);

    public async Task<IReadOnlyList<TrackDto>> GetTracksAsync(CancellationToken ct = default) =>
        await http.GetFromJsonAsync<List<TrackDto>>("/api/v1/tracks", ct) ?? [];

    public Task<TrackDetailDto?> GetTrackAsync(Guid trackId, CancellationToken ct = default) =>
        http.GetFromJsonAsync<TrackDetailDto>($"/api/v1/tracks/{trackId}", ct);

    public async Task EnrollAsync(Guid trackId, CancellationToken ct = default) =>
        (await http.PostAsync($"/api/v1/tracks/{trackId}/enroll", null, ct)).EnsureSuccessStatusCode();

    public async Task CompleteLessonAsync(Guid trackId, Guid lessonId, CancellationToken ct = default) =>
        (await http.PostAsync($"/api/v1/tracks/{trackId}/lessons/{lessonId}/complete", null, ct)).EnsureSuccessStatusCode();

    public async Task<IReadOnlyList<MyLearningDto>> GetMyLearningAsync(CancellationToken ct = default) =>
        await http.GetFromJsonAsync<List<MyLearningDto>>("/api/v1/me/learning", ct) ?? [];

    public async Task<IReadOnlyList<CompetitionDto>> GetCompetitionsAsync(CancellationToken ct = default) =>
        await http.GetFromJsonAsync<List<CompetitionDto>>("/api/v1/competitions", ct) ?? [];

    public async Task<CompetitionDto?> CreateCompetitionAsync(CreateCompetitionRequest request, CancellationToken ct = default)
    {
        var response = await http.PostAsJsonAsync("/api/v1/competitions", request, ct);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<CompetitionDto>(ct);
    }

    public async Task SetAnswerKeyAsync(Guid competitionId, Stream csv, string fileName, CancellationToken ct = default)
    {
        using var content = CsvForm(csv, fileName);
        (await http.PostAsync($"/api/v1/competitions/{competitionId}/answer-key", content, ct)).EnsureSuccessStatusCode();
    }

    public async Task<SubmissionResultDto?> SubmitAsync(Guid competitionId, Stream csv, string fileName, CancellationToken ct = default)
    {
        using var content = CsvForm(csv, fileName);
        var response = await http.PostAsync($"/api/v1/competitions/{competitionId}/submissions", content, ct);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<SubmissionResultDto>(ct);
    }

    public async Task<SubmissionResultDto?> SubmitPipelineAsync(Guid competitionId, WorkflowDefinition definition, CancellationToken ct = default)
    {
        var response = await http.PostAsJsonAsync($"/api/v1/competitions/{competitionId}/submit-pipeline", definition, ct);
        if (!response.IsSuccessStatusCode)
        {
            var problem = await response.Content.ReadAsStringAsync(ct);
            throw new InvalidOperationException(string.IsNullOrWhiteSpace(problem) ? response.ReasonPhrase : problem);
        }

        return await response.Content.ReadFromJsonAsync<SubmissionResultDto>(ct);
    }

    public async Task SetCompetitionDatasetsAsync(Guid competitionId, Stream training, Stream evaluation, string labelColumn, string idColumn, string task, CancellationToken ct = default)
    {
        using var content = new MultipartFormDataContent();
        var t = new StreamContent(training);
        t.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("text/csv");
        content.Add(t, "training", "training.csv");
        var e = new StreamContent(evaluation);
        e.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("text/csv");
        content.Add(e, "evaluation", "evaluation.csv");

        var url = $"/api/v1/competitions/{competitionId}/datasets?labelColumn={Uri.EscapeDataString(labelColumn)}&idColumn={Uri.EscapeDataString(idColumn)}&task={Uri.EscapeDataString(task)}";
        (await http.PostAsync(url, content, ct)).EnsureSuccessStatusCode();
    }

    public async Task<string?> GetCompetitionDataAsync(Guid competitionId, string which, CancellationToken ct = default)
    {
        var response = await http.GetAsync($"/api/v1/competitions/{competitionId}/data/{which}", ct);
        return response.IsSuccessStatusCode ? await response.Content.ReadAsStringAsync(ct) : null;
    }

    public async Task<IReadOnlyList<SubmissionResultDto>> GetMySubmissionsAsync(Guid competitionId, CancellationToken ct = default) =>
        await http.GetFromJsonAsync<List<SubmissionResultDto>>($"/api/v1/competitions/{competitionId}/submissions", ct) ?? [];

    public async Task<IReadOnlyList<LeaderboardEntryDto>> GetLeaderboardAsync(Guid competitionId, CancellationToken ct = default) =>
        await http.GetFromJsonAsync<List<LeaderboardEntryDto>>($"/api/v1/competitions/{competitionId}/leaderboard?board=live", ct) ?? [];

    public async Task<IReadOnlyList<NotificationDto>> GetNotificationsAsync(bool unreadOnly = false, int take = 30, CancellationToken ct = default) =>
        await http.GetFromJsonAsync<List<NotificationDto>>($"/api/v1/notifications?unread={unreadOnly}&take={take}", ct) ?? [];

    public async Task<int> GetUnreadNotificationCountAsync(CancellationToken ct = default)
    {
        var result = await http.GetFromJsonAsync<UnreadCountResponse>("/api/v1/notifications/unread-count", ct);
        return result?.Count ?? 0;
    }

    public async Task MarkNotificationReadAsync(Guid id, CancellationToken ct = default) =>
        (await http.PostAsync($"/api/v1/notifications/{id}/read", null, ct)).EnsureSuccessStatusCode();

    public async Task MarkAllNotificationsReadAsync(CancellationToken ct = default) =>
        (await http.PostAsync("/api/v1/notifications/read-all", null, ct)).EnsureSuccessStatusCode();

    private sealed record UnreadCountResponse(int Count);

    public async Task SetCompetitionStatusAsync(Guid competitionId, string status, CancellationToken ct = default)
    {
        var response = await http.PostAsJsonAsync($"/api/v1/competitions/{competitionId}/status", new SetStatusRequest(status), ct);
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException(await response.Content.ReadAsStringAsync(ct));
        }
    }

    public async Task SetCompetitionRevealAsync(Guid competitionId, DateTime? revealUtc, CancellationToken ct = default)
    {
        var response = await http.PostAsJsonAsync($"/api/v1/competitions/{competitionId}/reveal", new SetRevealRequest(revealUtc), ct);
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException(await response.Content.ReadAsStringAsync(ct));
        }
    }

    public async Task<IReadOnlyList<LeaderboardEntryDto>?> GetFinalLeaderboardAsync(Guid competitionId, CancellationToken ct = default)
    {
        var response = await http.GetAsync($"/api/v1/competitions/{competitionId}/leaderboard?board=final", ct);
        // 403 = concealed until reveal day.
        return response.StatusCode == HttpStatusCode.Forbidden
            ? null
            : await response.Content.ReadFromJsonAsync<List<LeaderboardEntryDto>>(ct) ?? [];
    }

    public async Task<SupervisionRollupDto?> GetSupervisionRollupAsync(CancellationToken ct = default)
    {
        var response = await http.GetAsync("/api/v1/supervision/rollup", ct);
        if (response.StatusCode == HttpStatusCode.Forbidden)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<SupervisionRollupDto>(ct);
    }

    public Task<PersonalDashboardDto?> GetPersonalDashboardAsync(CancellationToken ct = default) =>
        http.GetFromJsonAsync<PersonalDashboardDto>("/api/v1/dashboard/me", ct);

    public async Task<IReadOnlyList<ProjectDto>> GetProjectsAsync(CancellationToken ct = default) =>
        await http.GetFromJsonAsync<List<ProjectDto>>("/api/v1/projects", ct) ?? [];

    public async Task<ProjectDto?> CreateProjectAsync(CreateProjectRequest request, CancellationToken ct = default)
    {
        var response = await http.PostAsJsonAsync("/api/v1/projects", request, ct);
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException(await response.Content.ReadAsStringAsync(ct));
        }

        return await response.Content.ReadFromJsonAsync<ProjectDto>(ct);
    }

    public Task<ProjectDetailDto?> GetProjectAsync(Guid id, CancellationToken ct = default) =>
        http.GetFromJsonAsync<ProjectDetailDto>($"/api/v1/projects/{id}", ct);

    public async Task SaveProjectAsync(Guid id, SaveProjectRequest request, CancellationToken ct = default) =>
        (await http.PutAsJsonAsync($"/api/v1/projects/{id}", request, ct)).EnsureSuccessStatusCode();

    public async Task DeleteProjectAsync(Guid id, CancellationToken ct = default) =>
        (await http.DeleteAsync($"/api/v1/projects/{id}", ct)).EnsureSuccessStatusCode();

    public async Task<IReadOnlyList<DatasetDto>> GetDatasetsAsync(CancellationToken ct = default) =>
        await http.GetFromJsonAsync<List<DatasetDto>>("/api/v1/datasets", ct) ?? [];

    public async Task<IReadOnlyList<DatasetVersionDto>> GetDatasetVersionsAsync(Guid datasetId, CancellationToken ct = default) =>
        await http.GetFromJsonAsync<List<DatasetVersionDto>>($"/api/v1/datasets/{datasetId}/versions", ct) ?? [];

    public Task<DatasetVersionDetailDto?> GetDatasetVersionAsync(Guid datasetId, int versionNumber, CancellationToken ct = default) =>
        http.GetFromJsonAsync<DatasetVersionDetailDto>($"/api/v1/datasets/{datasetId}/versions/{versionNumber}", ct);

    public async Task<(DatasetVersionDto? Version, string? Error)> UploadDatasetFileAsync(Guid datasetId, Stream content, string fileName, CancellationToken ct = default)
    {
        using var form = new MultipartFormDataContent();
        var part = new StreamContent(content);
        part.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("text/csv");
        form.Add(part, "file", fileName);
        var response = await http.PostAsync($"/api/v1/datasets/{datasetId}/files", form, ct);
        return await ReadVersionAsync(response, ct);
    }

    public async Task<(DatasetVersionDto? Version, string? Error)> ImportDatasetUrlAsync(Guid datasetId, string url, CancellationToken ct = default)
    {
        var response = await http.PostAsJsonAsync($"/api/v1/datasets/{datasetId}/imports", new ImportUrlRequest(url), ct);
        return await ReadVersionAsync(response, ct);
    }

    public Task<string?> PublishDatasetVersionAsync(Guid datasetId, int versionNumber, CancellationToken ct = default) =>
        PostVoidAsync($"/api/v1/datasets/{datasetId}/versions/{versionNumber}/publish", ct);

    public string DatasetFileDownloadUrl(Guid fileId) => $"/api/v1/datasets/files/{fileId}/download";

    private static async Task<(DatasetVersionDto? Version, string? Error)> ReadVersionAsync(HttpResponseMessage response, CancellationToken ct)
    {
        if (response.IsSuccessStatusCode)
        {
            return (await response.Content.ReadFromJsonAsync<DatasetVersionDto>(ct), null);
        }

        var problem = await response.Content.ReadFromJsonAsync<Dictionary<string, string>>(ct);
        return (null, problem is not null && problem.TryGetValue("error", out var msg) ? msg : $"Request failed ({(int)response.StatusCode}).");
    }

    public async Task<DatasetDto?> CreateDatasetAsync(CreateDatasetRequest request, CancellationToken ct = default)
    {
        var response = await http.PostAsJsonAsync("/api/v1/datasets", request, ct);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<DatasetDto>(ct);
    }

    public Task<VisibilityOptionDto?> GetAudienceAsync(string scope, CancellationToken ct = default) =>
        http.GetFromJsonAsync<VisibilityOptionDto>($"/api/v1/me/audience?scope={scope}", ct);

    public async Task<ModelRunDto?> TrainAsync(Stream csv, string fileName, string labelColumn, string datasetName, string task, CancellationToken ct = default)
    {
        using var content = CsvForm(csv, fileName);
        var url = $"/api/v1/studio/train?labelColumn={Uri.EscapeDataString(labelColumn)}&datasetName={Uri.EscapeDataString(datasetName)}&task={Uri.EscapeDataString(task)}";
        var response = await http.PostAsync(url, content, ct);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<ModelRunDto>(ct);
    }

    public async Task<(ModelRunDto? Run, string? Error)> TrainFromDatasetAsync(Guid datasetId, string labelColumn, string task, CancellationToken ct = default) =>
        await PostJsonAsync<ModelRunDto>("/api/v1/studio/train/dataset", new TrainFromDatasetRequest(datasetId, labelColumn, task), ct);

    public async Task<IReadOnlyList<ModelRunDto>> GetModelRunsAsync(CancellationToken ct = default) =>
        await http.GetFromJsonAsync<List<ModelRunDto>>("/api/v1/studio/runs", ct) ?? [];

    public async Task<IReadOnlyList<DiscussionDto>> GetDiscussionsAsync(CancellationToken ct = default) =>
        await http.GetFromJsonAsync<List<DiscussionDto>>("/api/v1/discussions", ct) ?? [];

    public async Task<DiscussionDto?> CreateDiscussionAsync(CreateDiscussionRequest request, CancellationToken ct = default)
    {
        var response = await http.PostAsJsonAsync("/api/v1/discussions", request, ct);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<DiscussionDto>(ct);
    }

    public Task<DiscussionDetailDto?> GetDiscussionAsync(Guid id, CancellationToken ct = default) =>
        http.GetFromJsonAsync<DiscussionDetailDto>($"/api/v1/discussions/{id}", ct);

    public async Task<ReplyDto?> AddReplyAsync(Guid discussionId, string body, CancellationToken ct = default)
    {
        var response = await http.PostAsJsonAsync($"/api/v1/discussions/{discussionId}/replies", new CreateReplyRequest(body), ct);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<ReplyDto>(ct);
    }

    public async Task<IReadOnlyList<ReactionDto>> ReactToDiscussionAsync(Guid id, string emoji, CancellationToken ct = default) =>
        await ReactAsync($"/api/v1/discussions/{id}/react", emoji, ct);

    public async Task<IReadOnlyList<ReactionDto>> ReactToReplyAsync(Guid discussionId, Guid replyId, string emoji, CancellationToken ct = default) =>
        await ReactAsync($"/api/v1/discussions/{discussionId}/replies/{replyId}/react", emoji, ct);

    private async Task<IReadOnlyList<ReactionDto>> ReactAsync(string url, string emoji, CancellationToken ct)
    {
        var response = await http.PostAsJsonAsync(url, new ReactRequest(emoji), ct);
        if (!response.IsSuccessStatusCode)
        {
            return [];
        }

        return await response.Content.ReadFromJsonAsync<List<ReactionDto>>(ct) ?? [];
    }

    public Task<string?> SetDiscussionLockAsync(Guid id, bool locked, CancellationToken ct = default) =>
        PostVoidAsync($"/api/v1/discussions/{id}/{(locked ? "lock" : "unlock")}", ct);

    public Task<string?> SetDiscussionPinAsync(Guid id, bool pinned, CancellationToken ct = default) =>
        PostVoidAsync($"/api/v1/discussions/{id}/{(pinned ? "pin" : "unpin")}", ct);

    public Task<string?> DeleteDiscussionAsync(Guid id, CancellationToken ct = default) =>
        DeleteVoidAsync($"/api/v1/discussions/{id}", ct);

    public Task<string?> DeleteReplyAsync(Guid discussionId, Guid replyId, CancellationToken ct = default) =>
        DeleteVoidAsync($"/api/v1/discussions/{discussionId}/replies/{replyId}", ct);

    public async Task<IReadOnlyList<MentionCandidateDto>> SearchMentionCandidatesAsync(string q, CancellationToken ct = default) =>
        await http.GetFromJsonAsync<List<MentionCandidateDto>>($"/api/v1/community/mention-candidates?q={Uri.EscapeDataString(q)}", ct) ?? [];

    public async Task<(AttachmentDto? Attachment, string? Error)> AddAttachmentAsync(Guid discussionId, Stream content, string fileName, CancellationToken ct = default)
    {
        using var form = new MultipartFormDataContent();
        var part = new StreamContent(content);
        part.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/octet-stream");
        form.Add(part, "file", fileName);
        var response = await http.PostAsync($"/api/v1/discussions/{discussionId}/attachments", form, ct);
        if (response.IsSuccessStatusCode)
        {
            return (await response.Content.ReadFromJsonAsync<AttachmentDto>(ct), null);
        }

        var problem = await response.Content.ReadFromJsonAsync<Dictionary<string, string>>(ct);
        return (null, problem is not null && problem.TryGetValue("error", out var msg) ? msg : $"Upload failed ({(int)response.StatusCode}).");
    }

    public async Task<WorkflowValidationResult?> ValidateWorkflowAsync(WorkflowDefinition definition, CancellationToken ct = default)
    {
        var response = await http.PostAsJsonAsync("/api/v1/studio/workflows/validate", definition, ct);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<WorkflowValidationResult>(ct);
    }

    public async Task<ModelRunDto?> RunWorkflowAsync(WorkflowDefinition definition, Stream csv, string fileName, string labelColumn, CancellationToken ct = default)
    {
        using var content = new MultipartFormDataContent
        {
            { new StringContent(System.Text.Json.JsonSerializer.Serialize(definition)), "definition" },
        };
        var part = new StreamContent(csv);
        part.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("text/csv");
        content.Add(part, "file", fileName);

        var response = await http.PostAsync($"/api/v1/studio/workflows/run?labelColumn={Uri.EscapeDataString(labelColumn)}", content, ct);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<ModelRunDto>(ct);
    }

    public async Task<PipelineExecutionResult?> ExecuteWorkflowAsync(WorkflowDefinition definition, Stream csv, string fileName, string labelColumn, string task, CancellationToken ct = default)
    {
        using var content = new MultipartFormDataContent
        {
            { new StringContent(System.Text.Json.JsonSerializer.Serialize(definition)), "definition" },
        };
        var part = new StreamContent(csv);
        part.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("text/csv");
        content.Add(part, "file", fileName);

        var response = await http.PostAsync($"/api/v1/studio/workflows/execute?labelColumn={Uri.EscapeDataString(labelColumn)}&task={Uri.EscapeDataString(task)}", content, ct);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<PipelineExecutionResult>(ct);
    }

    public Task<(PipelineExecutionResult? Result, string? Error)> ExecuteWorkflowFromDatasetAsync(WorkflowDefinition definition, Guid datasetId, string labelColumn, string task, CancellationToken ct = default) =>
        PostJsonAsync<PipelineExecutionResult>("/api/v1/studio/workflows/execute/dataset",
            new ExecuteFromDatasetRequest(datasetId, labelColumn, task, definition), ct);

    public async Task<IReadOnlyList<ConnectorDescriptorDto>> GetConnectorsAsync(CancellationToken ct = default) =>
        await http.GetFromJsonAsync<List<ConnectorDescriptorDto>>("/api/v1/connectors", ct) ?? [];

    public async Task<IReadOnlyList<ConnectorInstanceDto>> GetConnectorInstancesAsync(string code, CancellationToken ct = default) =>
        await http.GetFromJsonAsync<List<ConnectorInstanceDto>>($"/api/v1/connectors/{code}/instances", ct) ?? [];

    public Task<(ConnectorInstanceDto? Instance, string? Error)> CreateConnectorInstanceAsync(string code, CreateConnectorInstanceRequest request, CancellationToken ct = default) =>
        PostJsonAsync<ConnectorInstanceDto>($"/api/v1/connectors/{code}/instances", request, ct);

    public Task<ConnectorInstanceDetailDto?> GetConnectorInstanceAsync(Guid id, CancellationToken ct = default) =>
        http.GetFromJsonAsync<ConnectorInstanceDetailDto>($"/api/v1/connectors/instances/{id}", ct);

    public Task<(CredentialInfoDto? Credential, string? Error)> SetConnectorCredentialAsync(Guid id, SetCredentialRequest request, CancellationToken ct = default) =>
        PostJsonAsync<CredentialInfoDto>($"/api/v1/connectors/instances/{id}/credentials", request, ct);

    public async Task<(ConnectorTestDto? Result, string? Error)> TestConnectorAsync(Guid id, CancellationToken ct = default)
    {
        var response = await http.PostAsync($"/api/v1/connectors/instances/{id}/test", null, ct);
        return response.IsSuccessStatusCode
            ? (await response.Content.ReadFromJsonAsync<ConnectorTestDto>(ct), null)
            : (null, await ErrorAsync(response, ct));
    }

    public async Task<(ConnectorHealthDto? Health, string? Error)> ProbeConnectorHealthAsync(Guid id, CancellationToken ct = default)
    {
        var response = await http.PostAsync($"/api/v1/connectors/instances/{id}/health", null, ct);
        return response.IsSuccessStatusCode
            ? (await response.Content.ReadFromJsonAsync<ConnectorHealthDto>(ct), null)
            : (null, await ErrorAsync(response, ct));
    }

    public Task<ConnectorSchemaDto?> GetConnectorSchemaAsync(Guid id, CancellationToken ct = default) =>
        http.GetFromJsonAsync<ConnectorSchemaDto>($"/api/v1/connectors/instances/{id}/schema", ct);

    public Task<string?> DeleteConnectorInstanceAsync(Guid id, CancellationToken ct = default) =>
        DeleteVoidAsync($"/api/v1/connectors/instances/{id}", ct);

    private static async Task<string?> ErrorAsync(HttpResponseMessage response, CancellationToken ct)
    {
        var problem = await response.Content.ReadFromJsonAsync<Dictionary<string, string>>(ct);
        return problem is not null && problem.TryGetValue("error", out var msg) ? msg : $"Request failed ({(int)response.StatusCode}).";
    }

    public async Task<IReadOnlyList<HelpArticleSummaryDto>> GetHelpArticlesAsync(string? category = null, string? q = null, CancellationToken ct = default)
    {
        var query = new List<string>();
        if (!string.IsNullOrWhiteSpace(category)) { query.Add($"category={Uri.EscapeDataString(category)}"); }
        if (!string.IsNullOrWhiteSpace(q)) { query.Add($"q={Uri.EscapeDataString(q)}"); }
        var url = "/api/v1/help/articles" + (query.Count > 0 ? "?" + string.Join("&", query) : "");
        return await http.GetFromJsonAsync<List<HelpArticleSummaryDto>>(url, ct) ?? [];
    }

    public Task<HelpArticleDto?> GetHelpArticleAsync(string slug, CancellationToken ct = default) =>
        http.GetFromJsonAsync<HelpArticleDto>($"/api/v1/help/articles/{slug}", ct);

    public async Task<IReadOnlyList<NodeDescriptorDto>> GetMlNodesAsync(CancellationToken ct = default) =>
        await http.GetFromJsonAsync<List<NodeDescriptorDto>>("/api/v1/ml/nodes", ct) ?? [];

    public async Task<IReadOnlyList<MlTaskDto>> GetMlTasksAsync(CancellationToken ct = default) =>
        await http.GetFromJsonAsync<List<MlTaskDto>>("/api/v1/ml/tasks", ct) ?? [];

    public Task<AdminDashboardDto?> GetAdminDashboardAsync(CancellationToken ct = default) =>
        http.GetFromJsonAsync<AdminDashboardDto>("/api/v1/admin/dashboard", ct);

    public async Task<IReadOnlyList<SettingDto>> GetSettingsAsync(CancellationToken ct = default) =>
        await http.GetFromJsonAsync<List<SettingDto>>("/api/v1/admin/settings", ct) ?? [];

    public async Task<(SettingDto? Setting, string? Error)> UpdateSettingAsync(string key, string value, CancellationToken ct = default)
    {
        var response = await http.PutAsJsonAsync($"/api/v1/admin/settings/{key}", new UpdateSettingRequest(value), ct);
        if (response.IsSuccessStatusCode)
        {
            return (await response.Content.ReadFromJsonAsync<SettingDto>(ct), null);
        }

        var problem = await response.Content.ReadFromJsonAsync<Dictionary<string, string>>(ct);
        return (null, problem is not null && problem.TryGetValue("error", out var msg) ? msg : $"Request failed ({(int)response.StatusCode}).");
    }

    public async Task<IReadOnlyList<FeatureFlagDto>> GetFeatureFlagsAsync(CancellationToken ct = default) =>
        await http.GetFromJsonAsync<List<FeatureFlagDto>>("/api/v1/admin/feature-flags", ct) ?? [];

    public async Task<(FeatureFlagDto? Flag, string? Error)> UpsertFeatureFlagAsync(string key, UpsertFeatureFlagRequest request, CancellationToken ct = default)
    {
        var response = await http.PutAsJsonAsync($"/api/v1/admin/feature-flags/{key}", request, ct);
        if (response.IsSuccessStatusCode)
        {
            return (await response.Content.ReadFromJsonAsync<FeatureFlagDto>(ct), null);
        }

        var problem = await response.Content.ReadFromJsonAsync<Dictionary<string, string>>(ct);
        return (null, problem is not null && problem.TryGetValue("error", out var msg) ? msg : $"Request failed ({(int)response.StatusCode}).");
    }

    public async Task<IReadOnlyList<AuditLogDto>> GetAuditAsync(string? action = null, CancellationToken ct = default)
    {
        var url = string.IsNullOrWhiteSpace(action) ? "/api/v1/admin/audit" : $"/api/v1/admin/audit?action={Uri.EscapeDataString(action)}";
        return await http.GetFromJsonAsync<List<AuditLogDto>>(url, ct) ?? [];
    }

    public async Task<IReadOnlyList<WorkflowSummaryDto>> GetWorkflowsAsync(CancellationToken ct = default) =>
        await http.GetFromJsonAsync<List<WorkflowSummaryDto>>("/api/v1/workflows", ct) ?? [];

    public Task<(WorkflowSummaryDto? Workflow, string? Error)> CreateWorkflowAsync(CreateWorkflowRequest request, CancellationToken ct = default) =>
        PostJsonAsync<WorkflowSummaryDto>("/api/v1/workflows", request, ct);

    public Task<WorkflowDetailDto?> GetWorkflowDetailAsync(Guid id, CancellationToken ct = default) =>
        http.GetFromJsonAsync<WorkflowDetailDto>($"/api/v1/workflows/{id}", ct);

    public Task<WorkflowVersionDetailDto?> GetWorkflowVersionAsync(Guid id, int versionNumber, CancellationToken ct = default) =>
        http.GetFromJsonAsync<WorkflowVersionDetailDto>($"/api/v1/workflows/{id}/versions/{versionNumber}", ct);

    public Task<(WorkflowVersionDto? Version, string? Error)> SaveWorkflowDraftAsync(Guid id, SaveDraftRequest request, CancellationToken ct = default) =>
        PostJsonAsync<WorkflowVersionDto>($"/api/v1/workflows/{id}/versions", request, ct);

    public Task<string?> DeleteWorkflowAsync(Guid id, CancellationToken ct = default) =>
        DeleteVoidAsync($"/api/v1/workflows/{id}", ct);

    public Task<string?> PublishWorkflowVersionAsync(Guid id, int versionNumber, CancellationToken ct = default) =>
        PostVoidAsync($"/api/v1/workflows/{id}/versions/{versionNumber}/publish", ct);

    public Task<string?> ArchiveWorkflowVersionAsync(Guid id, int versionNumber, CancellationToken ct = default) =>
        PostVoidAsync($"/api/v1/workflows/{id}/versions/{versionNumber}/archive", ct);

    public async Task<(Guid? RunId, string? Error)> RunWorkflowVersionAsync(Guid id, int versionNumber, RunWorkflowVersionRequest request, CancellationToken ct = default)
    {
        var (dto, error) = await PostJsonAsync<RunEnqueuedDto>($"/api/v1/workflows/{id}/versions/{versionNumber}/run", request, ct);
        return (dto?.RunId, error);
    }

    public Task<WorkflowExportDto?> ExportWorkflowVersionAsync(Guid id, int versionNumber, CancellationToken ct = default) =>
        http.GetFromJsonAsync<WorkflowExportDto>($"/api/v1/workflows/{id}/versions/{versionNumber}/export", ct);

    public Task<(WorkflowSummaryDto? Workflow, string? Error)> ImportWorkflowAsync(ImportWorkflowRequest request, CancellationToken ct = default) =>
        PostJsonAsync<WorkflowSummaryDto>("/api/v1/workflows/import", request, ct);

    public async Task<IReadOnlyList<WorkflowTemplateDto>> GetWorkflowTemplatesAsync(CancellationToken ct = default) =>
        await http.GetFromJsonAsync<List<WorkflowTemplateDto>>("/api/v1/workflow-templates", ct) ?? [];

    public Task<(WorkflowSummaryDto? Workflow, string? Error)> InstantiateTemplateAsync(string code, InstantiateTemplateRequest request, CancellationToken ct = default) =>
        PostJsonAsync<WorkflowSummaryDto>($"/api/v1/workflow-templates/{code}/instantiate", request, ct);

    public async Task<IReadOnlyList<RegisteredModelDto>> GetModelsAsync(CancellationToken ct = default) =>
        await http.GetFromJsonAsync<List<RegisteredModelDto>>("/api/v1/models", ct) ?? [];

    public async Task<ModelVersionDto?> RegisterModelAsync(RegisterModelRequest request, CancellationToken ct = default)
    {
        var response = await http.PostAsJsonAsync("/api/v1/models", request, ct);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<ModelVersionDto>(ct);
    }

    public Task<string?> ApproveVersionAsync(Guid versionId, CancellationToken ct = default) => PostVoidAsync($"/api/v1/models/versions/{versionId}/approve", ct);
    public Task<string?> PromoteVersionAsync(Guid versionId, CancellationToken ct = default) => PostVoidAsync($"/api/v1/models/versions/{versionId}/promote", ct);
    public Task<string?> RollbackVersionAsync(Guid versionId, CancellationToken ct = default) => PostVoidAsync($"/api/v1/models/versions/{versionId}/rollback", ct);
    public Task<string?> DeployVersionAsync(Guid versionId, CancellationToken ct = default) => PostVoidAsync($"/api/v1/models/versions/{versionId}/deploy", ct);
    public Task<string?> RetireDeploymentAsync(Guid deploymentId, CancellationToken ct = default) => PostVoidAsync($"/api/v1/models/deployments/{deploymentId}/retire", ct);
    public async Task<IReadOnlyList<DeploymentDto>> GetDeploymentsAsync(CancellationToken ct = default) =>
        await http.GetFromJsonAsync<List<DeploymentDto>>("/api/v1/models/deployments", ct) ?? [];

    public Task<(InferResponseDto? Result, string? Error)> InferAsync(Guid versionId, IReadOnlyDictionary<string, string> input, CancellationToken ct = default) =>
        PostJsonAsync<InferResponseDto>($"/api/v1/models/versions/{versionId}/infer", new InferRequest(input), ct);

    public Task<(InferResponseDto? Result, string? Error)> InferBatchAsync(Guid versionId, IReadOnlyList<IReadOnlyDictionary<string, string>> rows, CancellationToken ct = default) =>
        PostJsonAsync<InferResponseDto>($"/api/v1/models/versions/{versionId}/infer/batch", new BatchInferRequest(rows), ct);

    public async Task<IReadOnlyList<InferenceLogDto>> GetInferenceLogsAsync(Guid versionId, CancellationToken ct = default) =>
        await http.GetFromJsonAsync<List<InferenceLogDto>>($"/api/v1/models/versions/{versionId}/inference-logs", ct) ?? [];

    public Task<(DriftReportDto? Report, string? Error)> ComputeDriftAsync(Guid versionId, IReadOnlyList<IReadOnlyDictionary<string, string>> rows, CancellationToken ct = default) =>
        PostJsonAsync<DriftReportDto>($"/api/v1/models/versions/{versionId}/drift", new DriftRequest(rows), ct);

    /// <summary>POSTs JSON and returns the typed result on success, or an error message on a 400.</summary>
    private async Task<(T? Result, string? Error)> PostJsonAsync<T>(string url, object body, CancellationToken ct)
    {
        var response = await http.PostAsJsonAsync(url, body, ct);
        if (response.IsSuccessStatusCode)
        {
            return (await response.Content.ReadFromJsonAsync<T>(ct), null);
        }

        var problem = await response.Content.ReadFromJsonAsync<Dictionary<string, string>>(ct);
        return (default, problem is not null && problem.TryGetValue("error", out var msg) ? msg : $"Request failed ({(int)response.StatusCode}).");
    }

    /// <summary>Returns null on success, or the error message on a 400.</summary>
    private async Task<string?> PostVoidAsync(string url, CancellationToken ct)
    {
        var response = await http.PostAsync(url, null, ct);
        if (response.IsSuccessStatusCode)
        {
            return null;
        }

        var problem = await response.Content.ReadFromJsonAsync<Dictionary<string, string>>(ct);
        return problem is not null && problem.TryGetValue("error", out var msg) ? msg : $"Request failed ({(int)response.StatusCode}).";
    }

    /// <summary>Returns null on success, or the error message on a 400.</summary>
    private async Task<string?> DeleteVoidAsync(string url, CancellationToken ct)
    {
        var response = await http.DeleteAsync(url, ct);
        if (response.IsSuccessStatusCode)
        {
            return null;
        }

        var problem = await response.Content.ReadFromJsonAsync<Dictionary<string, string>>(ct);
        return problem is not null && problem.TryGetValue("error", out var msg) ? msg : $"Request failed ({(int)response.StatusCode}).";
    }

    private static MultipartFormDataContent CsvForm(Stream csv, string fileName)
    {
        var content = new MultipartFormDataContent();
        var part = new StreamContent(csv);
        part.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("text/csv");
        content.Add(part, "file", fileName);
        return content;
    }

    // ---- Engagement ----

    public Task<ProfileDto?> GetMyProfileAsync(CancellationToken ct = default) =>
        http.GetFromJsonAsync<ProfileDto>("/api/v1/profiles/me", ct);

    public Task<ProfileDto?> GetProfileAsync(string userId, CancellationToken ct = default) =>
        http.GetFromJsonAsync<ProfileDto>($"/api/v1/profiles/{Uri.EscapeDataString(userId)}", ct);

    public async Task<ProfileDto?> UpdateProfileAsync(UpdateProfileRequest request, CancellationToken ct = default)
    {
        var response = await http.PutAsJsonAsync("/api/v1/profiles/me", request, ct);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<ProfileDto>(ct);
    }

    public async Task<IReadOnlyList<XpLeaderboardRowDto>> GetXpLeaderboardAsync(string period, CancellationToken ct = default) =>
        await http.GetFromJsonAsync<List<XpLeaderboardRowDto>>($"/api/v1/engagement/leaderboard?period={period}", ct) ?? [];

    public async Task<IReadOnlyList<TeamLeaderboardRowDto>> GetTeamLeaderboardAsync(string period, CancellationToken ct = default) =>
        await http.GetFromJsonAsync<List<TeamLeaderboardRowDto>>($"/api/v1/engagement/teams?period={period}", ct) ?? [];

    public async Task<IReadOnlyList<BadgeDto>> GetBadgeCatalogAsync(CancellationToken ct = default) =>
        await http.GetFromJsonAsync<List<BadgeDto>>("/api/v1/engagement/badges/catalog", ct) ?? [];

    public async Task<IReadOnlyList<string>> GetAvatarIconsAsync(CancellationToken ct = default) =>
        await http.GetFromJsonAsync<List<string>>("/api/v1/engagement/avatars", ct) ?? [];

    public async Task<string?> GiveKudosAsync(GiveKudosRequest request, CancellationToken ct = default)
    {
        var response = await http.PostAsJsonAsync("/api/v1/engagement/kudos", request, ct);
        if (response.IsSuccessStatusCode)
        {
            return null;
        }

        var problem = await response.Content.ReadFromJsonAsync<Dictionary<string, string>>(ct);
        return problem is not null && problem.TryGetValue("error", out var msg) ? msg : $"Request failed ({(int)response.StatusCode}).";
    }

    public async Task<IReadOnlyList<KudosDto>> GetKudosAsync(string userId, CancellationToken ct = default) =>
        await http.GetFromJsonAsync<List<KudosDto>>($"/api/v1/engagement/kudos/{Uri.EscapeDataString(userId)}", ct) ?? [];

    public async Task<IReadOnlyList<ActivityDto>> GetActivityAsync(CancellationToken ct = default) =>
        await http.GetFromJsonAsync<List<ActivityDto>>("/api/v1/engagement/activity", ct) ?? [];

    // ---- Runs ----

    public async Task<IReadOnlyList<RunDto>> GetRunsAsync(CancellationToken ct = default) =>
        await http.GetFromJsonAsync<List<RunDto>>("/api/v1/runs", ct) ?? [];

    public Task<RunDto?> GetRunAsync(Guid id, CancellationToken ct = default) =>
        http.GetFromJsonAsync<RunDto>($"/api/v1/runs/{id}", ct);

    public async Task<RunDto?> CreateRunAsync(CreateRunRequest request, CancellationToken ct = default)
    {
        var response = await http.PostAsJsonAsync("/api/v1/runs", request, ct);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<RunDto>(ct);
    }

    public async Task CancelRunAsync(Guid id, CancellationToken ct = default) =>
        (await http.PostAsync($"/api/v1/runs/{id}/cancel", null, ct)).EnsureSuccessStatusCode();

    public async Task<IReadOnlyList<RunLogDto>> GetRunLogsAsync(Guid id, CancellationToken ct = default) =>
        await http.GetFromJsonAsync<List<RunLogDto>>($"/api/v1/runs/{id}/logs", ct) ?? [];

    public async Task<IReadOnlyList<RunAttemptDto>> GetRunAttemptsAsync(Guid id, CancellationToken ct = default) =>
        await http.GetFromJsonAsync<List<RunAttemptDto>>($"/api/v1/runs/{id}/attempts", ct) ?? [];

    // ---- Experiments ----

    public async Task<IReadOnlyList<ExperimentDto>> GetExperimentsAsync(CancellationToken ct = default) =>
        await http.GetFromJsonAsync<List<ExperimentDto>>("/api/v1/experiments", ct) ?? [];

    public async Task<ExperimentDto?> CreateExperimentAsync(CreateExperimentRequest request, CancellationToken ct = default)
    {
        var response = await http.PostAsJsonAsync("/api/v1/experiments", request, ct);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<ExperimentDto>(ct);
    }

    public async Task<IReadOnlyList<ExperimentRunDto>> GetExperimentRunsAsync(Guid experimentId, CancellationToken ct = default) =>
        await http.GetFromJsonAsync<List<ExperimentRunDto>>($"/api/v1/experiments/{experimentId}/runs", ct) ?? [];

    public async Task<IReadOnlyList<ComparisonRowDto>> GetExperimentCompareAsync(Guid experimentId, CancellationToken ct = default) =>
        await http.GetFromJsonAsync<List<ComparisonRowDto>>($"/api/v1/experiments/{experimentId}/compare", ct) ?? [];

    public async Task<IReadOnlyList<RunMetricDto>> GetRunMetricsAsync(Guid runId, CancellationToken ct = default) =>
        await http.GetFromJsonAsync<List<RunMetricDto>>($"/api/v1/experiments/runs/{runId}/metrics", ct) ?? [];

    public async Task<IReadOnlyList<RunParameterDto>> GetRunParametersAsync(Guid runId, CancellationToken ct = default) =>
        await http.GetFromJsonAsync<List<RunParameterDto>>($"/api/v1/experiments/runs/{runId}/parameters", ct) ?? [];

    public async Task<ExperimentRunDto?> UpdateExperimentRunAsync(Guid runId, UpdateRunRequest request, CancellationToken ct = default)
    {
        var response = await http.PutAsJsonAsync($"/api/v1/experiments/runs/{runId}", request, ct);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<ExperimentRunDto>(ct);
    }

    public Task<(ModelVersionDto? Version, string? Error)> RegisterExperimentRunAsync(Guid runId, string modelName, CancellationToken ct = default) =>
        PostJsonAsync<ModelVersionDto>($"/api/v1/experiments/runs/{runId}/register", new RegisterRunRequest(modelName), ct);
}
