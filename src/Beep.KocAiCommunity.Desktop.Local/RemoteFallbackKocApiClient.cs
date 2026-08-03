using Beep.KocAiCommunity.Client;
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
using Beep.KocAiCommunity.Contracts.Platform;
using Beep.KocAiCommunity.Contracts.Studio;
using Beep.KocAiCommunity.Contracts.Supervision;
using Beep.KocAiCommunity.Contracts.Workflow;
using ExperimentRunDto = Beep.KocAiCommunity.Contracts.Experiments.RunDto;
using RunDto = Beep.KocAiCommunity.Contracts.Jobs.RunDto;

namespace Beep.KocAiCommunity.Desktop.Local;

/// <summary>
/// Implements the full <see cref="IKocApiClient"/> by delegating every call to an inner HTTP
/// client. <see cref="LocalKocApiClient"/> derives from this and overrides only the Studio
/// methods to run in-process (offline); everything else (competitions, /me, …) flows to the API.
/// </summary>
public abstract class RemoteFallbackKocApiClient(IKocApiClient? remote) : IKocApiClient
{
    /// <summary>
    /// The client anything not overridden falls through to.
    /// <para>
    /// Optional since 2026-08-02: KOC Studio reads the database itself now and has no HTTP client at
    /// all. Where a derived class has not implemented a call, this throws by name instead of failing
    /// somewhere further down — a desktop build that quietly returned nothing would look like an empty
    /// platform rather than a missing implementation.
    /// </para>
    /// </summary>
    protected IKocApiClient Remote => remote
        ?? throw new NotSupportedException("This is not available in KOC Studio — it needs the website.");

    public virtual Task<PlatformMetaDto?> GetPlatformMetaAsync(CancellationToken ct = default) => Remote.GetPlatformMetaAsync(ct);
    public virtual Task<MeResponse?> GetMeAsync(CancellationToken ct = default) => Remote.GetMeAsync(ct);
    public virtual Task<AssignableRolesDto?> GetAssignableRolesAsync(CancellationToken ct = default) => Remote.GetAssignableRolesAsync(ct);
    public virtual Task<IReadOnlyList<CompetitionCategoryDto>> GetCompetitionCategoriesAsync(CancellationToken ct = default) => Remote.GetCompetitionCategoriesAsync(ct);
    public virtual Task<IReadOnlyList<CompetitionCategoryDto>> GetAdminCompetitionCategoriesAsync(CancellationToken ct = default) => Remote.GetAdminCompetitionCategoriesAsync(ct);
    public virtual Task<string?> UpsertCompetitionCategoryAsync(UpsertCompetitionCategoryRequest request, CancellationToken ct = default) => Remote.UpsertCompetitionCategoryAsync(request, ct);
    public virtual Task<string?> DeleteCompetitionCategoryAsync(string code, CancellationToken ct = default) => Remote.DeleteCompetitionCategoryAsync(code, ct);
    public virtual Task<string?> SetCompetitionCategoryAsync(Guid competitionId, string? code, CancellationToken ct = default) => Remote.SetCompetitionCategoryAsync(competitionId, code, ct);
    public virtual Task<IReadOnlyList<LearningLinkDto>> GetLearningLinksAsync(CancellationToken ct = default) => Remote.GetLearningLinksAsync(ct);
    public virtual Task<string?> SetTrackRecommendedCompetitionAsync(Guid trackId, Guid? competitionId, CancellationToken ct = default) => Remote.SetTrackRecommendedCompetitionAsync(trackId, competitionId, ct);
    public virtual Task<string?> SetCompetitionRecommendedTrackAsync(Guid competitionId, Guid? trackId, CancellationToken ct = default) => Remote.SetCompetitionRecommendedTrackAsync(competitionId, trackId, ct);
    public virtual Task<string?> SetUserRolesAsync(string userId, IReadOnlyList<string> roles, CancellationToken ct = default) => Remote.SetUserRolesAsync(userId, roles, ct);
    public virtual Task<string?> SetUserPositionAsync(string userId, string position, CancellationToken ct = default) => Remote.SetUserPositionAsync(userId, position, ct);
    public virtual Task<(AuthTokenResponse? Auth, string? Error)> RegisterAsync(RegisterRequest request, CancellationToken ct = default) => Remote.RegisterAsync(request, ct);
    public virtual Task<(AuthTokenResponse? Auth, string? Error)> LoginAsync(LoginRequest request, CancellationToken ct = default) => Remote.LoginAsync(request, ct);
    public virtual Task<RegistrationStateResponse?> GetRegistrationStateAsync(CancellationToken ct = default) => Remote.GetRegistrationStateAsync(ct);
    public virtual Task<IReadOnlyList<TrackDto>> GetTracksAsync(string? language = null, CancellationToken ct = default) => Remote.GetTracksAsync(language, ct);
    public virtual Task<TrackDetailDto?> GetTrackAsync(Guid trackId, CancellationToken ct = default) => Remote.GetTrackAsync(trackId, ct);
    public virtual Task EnrollAsync(Guid trackId, CancellationToken ct = default) => Remote.EnrollAsync(trackId, ct);
    public virtual Task CompleteLessonAsync(Guid trackId, Guid lessonId, CancellationToken ct = default) => Remote.CompleteLessonAsync(trackId, lessonId, ct);
    public virtual Task<IReadOnlyList<MyLearningDto>> GetMyLearningAsync(CancellationToken ct = default) => Remote.GetMyLearningAsync(ct);
    public virtual Task<QuizDto?> GetTrackQuizAsync(Guid trackId, CancellationToken ct = default) => Remote.GetTrackQuizAsync(trackId, ct);
    public virtual Task<(QuizAttemptResultDto? Result, string? Error)> SubmitQuizAsync(Guid trackId, SubmitQuizRequest request, CancellationToken ct = default) => Remote.SubmitQuizAsync(trackId, request, ct);
    public virtual Task<IReadOnlyList<QuizAttemptSummaryDto>> GetMyQuizAttemptsAsync(Guid trackId, CancellationToken ct = default) => Remote.GetMyQuizAttemptsAsync(trackId, ct);
    public virtual Task<CertificateDto?> GetCertificateAsync(Guid trackId, CancellationToken ct = default) => Remote.GetCertificateAsync(trackId, ct);
    public virtual Task<AdminQuizDto?> GetQuizForAdminAsync(Guid trackId, CancellationToken ct = default) => Remote.GetQuizForAdminAsync(trackId, ct);
    public virtual Task<(AdminQuizDto? Quiz, string? Error)> UpsertQuizAsync(Guid trackId, UpsertQuizRequest request, CancellationToken ct = default) => Remote.UpsertQuizAsync(trackId, request, ct);
    public virtual Task<(AdminQuizDto? Quiz, string? Error)> SaveQuizQuestionAsync(Guid trackId, UpsertQuizQuestionRequest request, CancellationToken ct = default) => Remote.SaveQuizQuestionAsync(trackId, request, ct);
    public virtual Task<(AdminQuizDto? Quiz, string? Error)> DeleteQuizQuestionAsync(Guid trackId, Guid questionId, CancellationToken ct = default) => Remote.DeleteQuizQuestionAsync(trackId, questionId, ct);
    public virtual Task<IReadOnlyList<CompetitionDto>> GetCompetitionsAsync(CancellationToken ct = default) => Remote.GetCompetitionsAsync(ct);
    public virtual Task<PublicShowcaseDto> GetPublicShowcaseAsync(CancellationToken ct = default) => Remote.GetPublicShowcaseAsync(ct);
    public virtual Task<CompetitionDto?> GetCompetitionAsync(Guid competitionId, CancellationToken ct = default) => Remote.GetCompetitionAsync(competitionId, ct);
    public virtual Task<CompetitionDto?> CreateCompetitionAsync(CreateCompetitionRequest request, CancellationToken ct = default) => Remote.CreateCompetitionAsync(request, ct);
    public virtual Task SetAnswerKeyAsync(Guid competitionId, Stream csv, string fileName, CancellationToken ct = default) => Remote.SetAnswerKeyAsync(competitionId, csv, fileName, ct);
    public virtual Task<SubmissionResultDto?> SubmitAsync(Guid competitionId, Stream csv, string fileName, string? idempotencyKey = null, CancellationToken ct = default) => Remote.SubmitAsync(competitionId, csv, fileName, idempotencyKey, ct);
    public virtual Task<SubmissionResultDto?> SubmitPipelineAsync(Guid competitionId, WorkflowDefinition definition, string? idempotencyKey = null, CancellationToken ct = default) => Remote.SubmitPipelineAsync(competitionId, definition, idempotencyKey, ct);
    public virtual Task SetCompetitionDatasetsAsync(Guid competitionId, Stream training, Stream evaluation, string labelColumn, string idColumn, string task, CancellationToken ct = default) => Remote.SetCompetitionDatasetsAsync(competitionId, training, evaluation, labelColumn, idColumn, task, ct);
    public virtual Task<string?> GetCompetitionDataAsync(Guid competitionId, string which, CancellationToken ct = default) => Remote.GetCompetitionDataAsync(competitionId, which, ct);
    public virtual Task<IReadOnlyList<SubmissionResultDto>> GetMySubmissionsAsync(Guid competitionId, CancellationToken ct = default) => Remote.GetMySubmissionsAsync(competitionId, ct);
    public virtual Task<IReadOnlyList<LeaderboardEntryDto>> GetLeaderboardAsync(Guid competitionId, CancellationToken ct = default) => Remote.GetLeaderboardAsync(competitionId, ct);
    public virtual Task<IReadOnlyList<NotificationDto>> GetNotificationsAsync(bool unreadOnly = false, int take = 30, CancellationToken ct = default) => Remote.GetNotificationsAsync(unreadOnly, take, ct);
    public virtual Task<int> GetUnreadNotificationCountAsync(CancellationToken ct = default) => Remote.GetUnreadNotificationCountAsync(ct);
    public virtual Task MarkNotificationReadAsync(Guid id, CancellationToken ct = default) => Remote.MarkNotificationReadAsync(id, ct);
    public virtual Task MarkAllNotificationsReadAsync(CancellationToken ct = default) => Remote.MarkAllNotificationsReadAsync(ct);
    public virtual Task SetCompetitionStatusAsync(Guid competitionId, string status, CancellationToken ct = default) => Remote.SetCompetitionStatusAsync(competitionId, status, ct);
    public virtual Task SetCompetitionFeaturedAsync(Guid competitionId, CancellationToken ct = default) => Remote.SetCompetitionFeaturedAsync(competitionId, ct);
    public virtual Task SetCompetitionPrizesAsync(Guid competitionId, SetPrizesRequest request, CancellationToken ct = default) => Remote.SetCompetitionPrizesAsync(competitionId, request, ct);
    public virtual Task SetCompetitionHeroImagePathAsync(Guid competitionId, string? path, CancellationToken ct = default) => Remote.SetCompetitionHeroImagePathAsync(competitionId, path, ct);
    public virtual Task SetCompetitionRevealAsync(Guid competitionId, DateTime? revealUtc, CancellationToken ct = default) => Remote.SetCompetitionRevealAsync(competitionId, revealUtc, ct);
    public virtual Task<IReadOnlyList<LeaderboardEntryDto>?> GetFinalLeaderboardAsync(Guid competitionId, CancellationToken ct = default) => Remote.GetFinalLeaderboardAsync(competitionId, ct);
    public virtual Task<SupervisionRollupDto?> GetSupervisionRollupAsync(CancellationToken ct = default) => Remote.GetSupervisionRollupAsync(ct);
    public virtual Task<PersonalDashboardDto?> GetPersonalDashboardAsync(CancellationToken ct = default) => Remote.GetPersonalDashboardAsync(ct);
    public virtual Task<IReadOnlyList<DatasetDto>> GetDatasetsAsync(CancellationToken ct = default) => Remote.GetDatasetsAsync(ct);
    public virtual Task<DatasetDto?> CreateDatasetAsync(CreateDatasetRequest request, CancellationToken ct = default) => Remote.CreateDatasetAsync(request, ct);
    public virtual Task<VisibilityOptionDto?> GetAudienceAsync(string scope, CancellationToken ct = default) => Remote.GetAudienceAsync(scope, ct);
    public virtual Task<IReadOnlyList<DatasetVersionDto>> GetDatasetVersionsAsync(Guid datasetId, CancellationToken ct = default) => Remote.GetDatasetVersionsAsync(datasetId, ct);
    public virtual Task<DatasetVersionDetailDto?> GetDatasetVersionAsync(Guid datasetId, int versionNumber, CancellationToken ct = default) => Remote.GetDatasetVersionAsync(datasetId, versionNumber, ct);
    public virtual Task<(DatasetVersionDto? Version, string? Error)> UploadDatasetFileAsync(Guid datasetId, Stream content, string fileName, CancellationToken ct = default) => Remote.UploadDatasetFileAsync(datasetId, content, fileName, ct);
    public virtual Task<(DatasetVersionDto? Version, string? Error)> ImportDatasetUrlAsync(Guid datasetId, string url, CancellationToken ct = default) => Remote.ImportDatasetUrlAsync(datasetId, url, ct);
    public virtual Task<string?> PublishDatasetVersionAsync(Guid datasetId, int versionNumber, CancellationToken ct = default) => Remote.PublishDatasetVersionAsync(datasetId, versionNumber, ct);
    public virtual string DatasetFileDownloadUrl(Guid fileId) => Remote.DatasetFileDownloadUrl(fileId);
    public virtual Task<ModelRunDto?> TrainAsync(Stream csv, string fileName, string labelColumn, string datasetName, string task, CancellationToken ct = default) => Remote.TrainAsync(csv, fileName, labelColumn, datasetName, task, ct);
    public virtual Task<(ModelRunDto? Run, string? Error)> TrainFromDatasetAsync(Guid datasetId, string labelColumn, string task, CancellationToken ct = default) => Remote.TrainFromDatasetAsync(datasetId, labelColumn, task, ct);
    public virtual Task<IReadOnlyList<ModelRunDto>> GetModelRunsAsync(CancellationToken ct = default) => Remote.GetModelRunsAsync(ct);
    public virtual Task<IReadOnlyList<DiscussionDto>> GetDiscussionsAsync(CancellationToken ct = default) => Remote.GetDiscussionsAsync(ct);
    public virtual Task<DiscussionDto?> CreateDiscussionAsync(CreateDiscussionRequest request, CancellationToken ct = default) => Remote.CreateDiscussionAsync(request, ct);
    public virtual Task<DiscussionDetailDto?> GetDiscussionAsync(Guid id, CancellationToken ct = default) => Remote.GetDiscussionAsync(id, ct);
    public virtual Task<ReplyDto?> AddReplyAsync(Guid discussionId, string body, CancellationToken ct = default) => Remote.AddReplyAsync(discussionId, body, ct);
    public virtual Task<IReadOnlyList<ReactionDto>> ReactToDiscussionAsync(Guid id, string emoji, CancellationToken ct = default) => Remote.ReactToDiscussionAsync(id, emoji, ct);
    public virtual Task<IReadOnlyList<ReactionDto>> ReactToReplyAsync(Guid discussionId, Guid replyId, string emoji, CancellationToken ct = default) => Remote.ReactToReplyAsync(discussionId, replyId, emoji, ct);
    public virtual Task<string?> SetDiscussionLockAsync(Guid id, bool locked, CancellationToken ct = default) => Remote.SetDiscussionLockAsync(id, locked, ct);
    public virtual Task<string?> SetDiscussionPinAsync(Guid id, bool pinned, CancellationToken ct = default) => Remote.SetDiscussionPinAsync(id, pinned, ct);
    public virtual Task<string?> DeleteDiscussionAsync(Guid id, CancellationToken ct = default) => Remote.DeleteDiscussionAsync(id, ct);
    public virtual Task<string?> DeleteReplyAsync(Guid discussionId, Guid replyId, CancellationToken ct = default) => Remote.DeleteReplyAsync(discussionId, replyId, ct);
    public virtual Task<IReadOnlyList<MentionCandidateDto>> SearchMentionCandidatesAsync(string q, CancellationToken ct = default) => Remote.SearchMentionCandidatesAsync(q, ct);
    public virtual Task<(AttachmentDto? Attachment, string? Error)> AddAttachmentAsync(Guid discussionId, Stream content, string fileName, CancellationToken ct = default) => Remote.AddAttachmentAsync(discussionId, content, fileName, ct);
    public virtual Task<WorkflowValidationResult?> ValidateWorkflowAsync(WorkflowDefinition definition, CancellationToken ct = default) => Remote.ValidateWorkflowAsync(definition, ct);
    public virtual Task<ModelRunDto?> RunWorkflowAsync(WorkflowDefinition definition, Stream csv, string fileName, string labelColumn, CancellationToken ct = default) => Remote.RunWorkflowAsync(definition, csv, fileName, labelColumn, ct);
    public virtual Task<PipelineExecutionResult?> ExecuteWorkflowAsync(WorkflowDefinition definition, Stream csv, string fileName, string labelColumn, string task, CancellationToken ct = default) => Remote.ExecuteWorkflowAsync(definition, csv, fileName, labelColumn, task, ct);
    public virtual Task<(PipelineExecutionResult? Result, string? Error)> ExecuteWorkflowFromDatasetAsync(WorkflowDefinition definition, Guid datasetId, string labelColumn, string task, CancellationToken ct = default) => Remote.ExecuteWorkflowFromDatasetAsync(definition, datasetId, labelColumn, task, ct);
    public virtual Task<IReadOnlyList<ConnectorDescriptorDto>> GetConnectorsAsync(CancellationToken ct = default) => Remote.GetConnectorsAsync(ct);
    public virtual Task<IReadOnlyList<ConnectorInstanceDto>> GetConnectorInstancesAsync(string code, CancellationToken ct = default) => Remote.GetConnectorInstancesAsync(code, ct);
    public virtual Task<(ConnectorInstanceDto? Instance, string? Error)> CreateConnectorInstanceAsync(string code, CreateConnectorInstanceRequest request, CancellationToken ct = default) => Remote.CreateConnectorInstanceAsync(code, request, ct);
    public virtual Task<ConnectorInstanceDetailDto?> GetConnectorInstanceAsync(Guid id, CancellationToken ct = default) => Remote.GetConnectorInstanceAsync(id, ct);
    public virtual Task<(CredentialInfoDto? Credential, string? Error)> SetConnectorCredentialAsync(Guid id, SetCredentialRequest request, CancellationToken ct = default) => Remote.SetConnectorCredentialAsync(id, request, ct);
    public virtual Task<(ConnectorTestDto? Result, string? Error)> TestConnectorAsync(Guid id, CancellationToken ct = default) => Remote.TestConnectorAsync(id, ct);
    public virtual Task<(ConnectorHealthDto? Health, string? Error)> ProbeConnectorHealthAsync(Guid id, CancellationToken ct = default) => Remote.ProbeConnectorHealthAsync(id, ct);
    public virtual Task<ConnectorSchemaDto?> GetConnectorSchemaAsync(Guid id, CancellationToken ct = default) => Remote.GetConnectorSchemaAsync(id, ct);
    public virtual Task<string?> DeleteConnectorInstanceAsync(Guid id, CancellationToken ct = default) => Remote.DeleteConnectorInstanceAsync(id, ct);
    public virtual Task<IReadOnlyList<HelpArticleSummaryDto>> GetHelpArticlesAsync(string? category = null, string? q = null, CancellationToken ct = default) => Remote.GetHelpArticlesAsync(category, q, ct);
    public virtual Task<HelpArticleDto?> GetHelpArticleAsync(string slug, CancellationToken ct = default) => Remote.GetHelpArticleAsync(slug, ct);
    public virtual Task<IReadOnlyList<NodeDescriptorDto>> GetMlNodesAsync(CancellationToken ct = default) => Remote.GetMlNodesAsync(ct);
    public virtual Task<IReadOnlyList<MlTaskDto>> GetMlTasksAsync(CancellationToken ct = default) => Remote.GetMlTasksAsync(ct);
    public virtual Task<AdminDashboardDto?> GetAdminDashboardAsync(CancellationToken ct = default) => Remote.GetAdminDashboardAsync(ct);
    public virtual Task<IReadOnlyList<SettingDto>> GetSettingsAsync(CancellationToken ct = default) => Remote.GetSettingsAsync(ct);
    public virtual Task<(SettingDto? Setting, string? Error)> UpdateSettingAsync(string key, string value, CancellationToken ct = default) => Remote.UpdateSettingAsync(key, value, ct);
    public virtual Task<IReadOnlyList<FeatureFlagDto>> GetFeatureFlagsAsync(CancellationToken ct = default) => Remote.GetFeatureFlagsAsync(ct);
    public virtual Task<(FeatureFlagDto? Flag, string? Error)> UpsertFeatureFlagAsync(string key, UpsertFeatureFlagRequest request, CancellationToken ct = default) => Remote.UpsertFeatureFlagAsync(key, request, ct);
    public virtual Task<IReadOnlyList<AuditLogDto>> GetAuditAsync(string? action = null, CancellationToken ct = default) => Remote.GetAuditAsync(action, ct);
    public virtual Task<DemoDataStatusDto?> GetDemoStatusAsync(CancellationToken ct = default) => Remote.GetDemoStatusAsync(ct);
    public virtual Task<(DemoDataStatusDto? Status, string? Error)> SeedDemoAsync(CancellationToken ct = default) => Remote.SeedDemoAsync(ct);
    public virtual Task<(DemoDataStatusDto? Status, string? Error)> UnseedDemoAsync(CancellationToken ct = default) => Remote.UnseedDemoAsync(ct);
    public virtual Task<IReadOnlyList<AdminUserDto>> GetAdminUsersAsync(CancellationToken ct = default) => Remote.GetAdminUsersAsync(ct);
    public virtual Task<IReadOnlyList<OrgUnitCodeDto>> GetAdminOrgUnitsAsync(CancellationToken ct = default) => Remote.GetAdminOrgUnitsAsync(ct);
    public virtual Task<(AdminUserDto? User, string? Error)> UpsertUserProfileAsync(string userId, UpsertUserProfileRequest request, CancellationToken ct = default) => Remote.UpsertUserProfileAsync(userId, request, ct);
    public virtual Task<string?> SetCompetitionGrantAsync(string userId, string maxScope, CancellationToken ct = default) => Remote.SetCompetitionGrantAsync(userId, maxScope, ct);
    public virtual Task<string?> RevokeCompetitionGrantAsync(string userId, CancellationToken ct = default) => Remote.RevokeCompetitionGrantAsync(userId, ct);
    public virtual Task<string?> SetOrgUnitCodeAsync(Guid orgUnitId, string? code, CancellationToken ct = default) => Remote.SetOrgUnitCodeAsync(orgUnitId, code, ct);
    public virtual Task<IReadOnlyList<WorkflowSummaryDto>> GetWorkflowsAsync(CancellationToken ct = default) => Remote.GetWorkflowsAsync(ct);
    public virtual Task<(WorkflowSummaryDto? Workflow, string? Error)> CreateWorkflowAsync(CreateWorkflowRequest request, CancellationToken ct = default) => Remote.CreateWorkflowAsync(request, ct);
    public virtual Task<WorkflowDetailDto?> GetWorkflowDetailAsync(Guid id, CancellationToken ct = default) => Remote.GetWorkflowDetailAsync(id, ct);
    public virtual Task<WorkflowVersionDetailDto?> GetWorkflowVersionAsync(Guid id, int versionNumber, CancellationToken ct = default) => Remote.GetWorkflowVersionAsync(id, versionNumber, ct);
    public virtual Task<(WorkflowVersionDto? Version, string? Error)> SaveWorkflowDraftAsync(Guid id, SaveDraftRequest request, CancellationToken ct = default) => Remote.SaveWorkflowDraftAsync(id, request, ct);
    public virtual Task<string?> DeleteWorkflowAsync(Guid id, CancellationToken ct = default) => Remote.DeleteWorkflowAsync(id, ct);
    public virtual Task<string?> PublishWorkflowVersionAsync(Guid id, int versionNumber, CancellationToken ct = default) => Remote.PublishWorkflowVersionAsync(id, versionNumber, ct);
    public virtual Task<string?> ArchiveWorkflowVersionAsync(Guid id, int versionNumber, CancellationToken ct = default) => Remote.ArchiveWorkflowVersionAsync(id, versionNumber, ct);
    public virtual Task<(Guid? RunId, string? Error)> RunWorkflowVersionAsync(Guid id, int versionNumber, RunWorkflowVersionRequest request, CancellationToken ct = default) => Remote.RunWorkflowVersionAsync(id, versionNumber, request, ct);
    public virtual Task<WorkflowExportDto?> ExportWorkflowVersionAsync(Guid id, int versionNumber, CancellationToken ct = default) => Remote.ExportWorkflowVersionAsync(id, versionNumber, ct);
    public virtual Task<(WorkflowSummaryDto? Workflow, string? Error)> ImportWorkflowAsync(ImportWorkflowRequest request, CancellationToken ct = default) => Remote.ImportWorkflowAsync(request, ct);
    public virtual Task<IReadOnlyList<WorkflowTemplateDto>> GetWorkflowTemplatesAsync(CancellationToken ct = default) => Remote.GetWorkflowTemplatesAsync(ct);
    public virtual Task<(WorkflowSummaryDto? Workflow, string? Error)> InstantiateTemplateAsync(string code, InstantiateTemplateRequest request, CancellationToken ct = default) => Remote.InstantiateTemplateAsync(code, request, ct);
    public virtual Task<IReadOnlyList<RegisteredModelDto>> GetModelsAsync(CancellationToken ct = default) => Remote.GetModelsAsync(ct);
    public virtual Task<ModelVersionDto?> RegisterModelAsync(RegisterModelRequest request, CancellationToken ct = default) => Remote.RegisterModelAsync(request, ct);
    public virtual Task<string?> ApproveVersionAsync(Guid versionId, CancellationToken ct = default) => Remote.ApproveVersionAsync(versionId, ct);
    public virtual Task<string?> PromoteVersionAsync(Guid versionId, CancellationToken ct = default) => Remote.PromoteVersionAsync(versionId, ct);
    public virtual Task<string?> RollbackVersionAsync(Guid versionId, CancellationToken ct = default) => Remote.RollbackVersionAsync(versionId, ct);
    public virtual Task<string?> DeployVersionAsync(Guid versionId, CancellationToken ct = default) => Remote.DeployVersionAsync(versionId, ct);
    public virtual Task<string?> RetireDeploymentAsync(Guid deploymentId, CancellationToken ct = default) => Remote.RetireDeploymentAsync(deploymentId, ct);
    public virtual Task<IReadOnlyList<DeploymentDto>> GetDeploymentsAsync(CancellationToken ct = default) => Remote.GetDeploymentsAsync(ct);
    public virtual Task<(InferResponseDto? Result, string? Error)> InferAsync(Guid versionId, IReadOnlyDictionary<string, string> input, CancellationToken ct = default) => Remote.InferAsync(versionId, input, ct);
    public virtual Task<(InferResponseDto? Result, string? Error)> InferBatchAsync(Guid versionId, IReadOnlyList<IReadOnlyDictionary<string, string>> rows, CancellationToken ct = default) => Remote.InferBatchAsync(versionId, rows, ct);
    public virtual Task<IReadOnlyList<InferenceLogDto>> GetInferenceLogsAsync(Guid versionId, CancellationToken ct = default) => Remote.GetInferenceLogsAsync(versionId, ct);
    public virtual Task<(DriftReportDto? Report, string? Error)> ComputeDriftAsync(Guid versionId, IReadOnlyList<IReadOnlyDictionary<string, string>> rows, CancellationToken ct = default) => Remote.ComputeDriftAsync(versionId, rows, ct);
    public virtual Task<ProfileDto?> GetMyProfileAsync(CancellationToken ct = default) => Remote.GetMyProfileAsync(ct);
    public virtual Task SetMyLanguageAsync(string language, CancellationToken ct = default) => Remote.SetMyLanguageAsync(language, ct);
    public virtual Task<ProfileDto?> GetProfileAsync(string userId, CancellationToken ct = default) => Remote.GetProfileAsync(userId, ct);
    public virtual Task<ProfileDto?> UpdateProfileAsync(UpdateProfileRequest request, CancellationToken ct = default) => Remote.UpdateProfileAsync(request, ct);
    public virtual Task<IReadOnlyList<XpLeaderboardRowDto>> GetXpLeaderboardAsync(string period, CancellationToken ct = default) => Remote.GetXpLeaderboardAsync(period, ct);
    public virtual Task<IReadOnlyList<TeamLeaderboardRowDto>> GetTeamLeaderboardAsync(string period, CancellationToken ct = default) => Remote.GetTeamLeaderboardAsync(period, ct);
    public virtual Task<IReadOnlyList<BadgeDto>> GetBadgeCatalogAsync(CancellationToken ct = default) => Remote.GetBadgeCatalogAsync(ct);
    public virtual Task<IReadOnlyList<string>> GetAvatarIconsAsync(CancellationToken ct = default) => Remote.GetAvatarIconsAsync(ct);
    public virtual Task<string?> GiveKudosAsync(GiveKudosRequest request, CancellationToken ct = default) => Remote.GiveKudosAsync(request, ct);
    public virtual Task<IReadOnlyList<KudosDto>> GetKudosAsync(string userId, CancellationToken ct = default) => Remote.GetKudosAsync(userId, ct);
    public virtual Task<IReadOnlyList<ActivityDto>> GetActivityAsync(CancellationToken ct = default) => Remote.GetActivityAsync(ct);
    public virtual Task<IReadOnlyList<RunDto>> GetRunsAsync(CancellationToken ct = default) => Remote.GetRunsAsync(ct);
    public virtual Task<RunDto?> GetRunAsync(Guid id, CancellationToken ct = default) => Remote.GetRunAsync(id, ct);
    public virtual Task<RunDto?> CreateRunAsync(CreateRunRequest request, CancellationToken ct = default) => Remote.CreateRunAsync(request, ct);
    public virtual Task CancelRunAsync(Guid id, CancellationToken ct = default) => Remote.CancelRunAsync(id, ct);
    public virtual Task<IReadOnlyList<RunLogDto>> GetRunLogsAsync(Guid id, CancellationToken ct = default) => Remote.GetRunLogsAsync(id, ct);
    public virtual Task<IReadOnlyList<RunAttemptDto>> GetRunAttemptsAsync(Guid id, CancellationToken ct = default) => Remote.GetRunAttemptsAsync(id, ct);
    public virtual Task<IReadOnlyList<ExperimentDto>> GetExperimentsAsync(CancellationToken ct = default) => Remote.GetExperimentsAsync(ct);
    public virtual Task<ExperimentDto?> CreateExperimentAsync(CreateExperimentRequest request, CancellationToken ct = default) => Remote.CreateExperimentAsync(request, ct);
    public virtual Task<IReadOnlyList<ExperimentRunDto>> GetExperimentRunsAsync(Guid experimentId, CancellationToken ct = default) => Remote.GetExperimentRunsAsync(experimentId, ct);
    public virtual Task<IReadOnlyList<ComparisonRowDto>> GetExperimentCompareAsync(Guid experimentId, CancellationToken ct = default) => Remote.GetExperimentCompareAsync(experimentId, ct);
    public virtual Task<IReadOnlyList<RunMetricDto>> GetRunMetricsAsync(Guid runId, CancellationToken ct = default) => Remote.GetRunMetricsAsync(runId, ct);
    public virtual Task<IReadOnlyList<RunParameterDto>> GetRunParametersAsync(Guid runId, CancellationToken ct = default) => Remote.GetRunParametersAsync(runId, ct);
    public virtual Task<ExperimentRunDto?> UpdateExperimentRunAsync(Guid runId, UpdateRunRequest request, CancellationToken ct = default) => Remote.UpdateExperimentRunAsync(runId, request, ct);
    public virtual Task<(ModelVersionDto? Version, string? Error)> RegisterExperimentRunAsync(Guid runId, string modelName, CancellationToken ct = default) => Remote.RegisterExperimentRunAsync(runId, modelName, ct);
}
