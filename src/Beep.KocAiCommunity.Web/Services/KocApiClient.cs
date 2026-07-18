using System.Net;
using System.Net.Http.Json;
using Beep.KocAiCommunity.Contracts.Community;
using Beep.KocAiCommunity.Contracts.Competitions;
using Beep.KocAiCommunity.Contracts.Dashboard;
using Beep.KocAiCommunity.Contracts.Datasets;
using Beep.KocAiCommunity.Contracts.Identity;
using Beep.KocAiCommunity.Contracts.Learning;
using Beep.KocAiCommunity.Contracts.Notifications;
using Beep.KocAiCommunity.Contracts.Studio;
using Beep.KocAiCommunity.Contracts.Supervision;
using Beep.KocAiCommunity.Contracts.Workflow;

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

    Task<ModelRunDto?> TrainAsync(Stream csv, string fileName, string labelColumn, string datasetName, string task, CancellationToken ct = default);
    Task<IReadOnlyList<ModelRunDto>> GetModelRunsAsync(CancellationToken ct = default);

    Task<IReadOnlyList<DiscussionDto>> GetDiscussionsAsync(CancellationToken ct = default);
    Task<DiscussionDto?> CreateDiscussionAsync(CreateDiscussionRequest request, CancellationToken ct = default);
    Task<DiscussionDetailDto?> GetDiscussionAsync(Guid id, CancellationToken ct = default);
    Task<ReplyDto?> AddReplyAsync(Guid discussionId, string body, CancellationToken ct = default);

    Task<WorkflowValidationResult?> ValidateWorkflowAsync(WorkflowDefinition definition, CancellationToken ct = default);
    Task<ModelRunDto?> RunWorkflowAsync(WorkflowDefinition definition, Stream csv, string fileName, string labelColumn, CancellationToken ct = default);
    Task<PipelineExecutionResult?> ExecuteWorkflowAsync(WorkflowDefinition definition, Stream csv, string fileName, string labelColumn, string task, CancellationToken ct = default);

    Task<IReadOnlyList<RegisteredModelDto>> GetModelsAsync(CancellationToken ct = default);
    Task<ModelVersionDto?> RegisterModelAsync(RegisterModelRequest request, CancellationToken ct = default);
    Task<string?> ApproveVersionAsync(Guid versionId, CancellationToken ct = default);
    Task<string?> PromoteVersionAsync(Guid versionId, CancellationToken ct = default);
    Task<string?> RollbackVersionAsync(Guid versionId, CancellationToken ct = default);
    Task<string?> DeployVersionAsync(Guid versionId, CancellationToken ct = default);
    Task<string?> RetireDeploymentAsync(Guid deploymentId, CancellationToken ct = default);
    Task<IReadOnlyList<DeploymentDto>> GetDeploymentsAsync(CancellationToken ct = default);
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

    private static MultipartFormDataContent CsvForm(Stream csv, string fileName)
    {
        var content = new MultipartFormDataContent();
        var part = new StreamContent(csv);
        part.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("text/csv");
        content.Add(part, "file", fileName);
        return content;
    }
}
