using Beep.KocAiCommunity.Domain.Audit;
using Beep.KocAiCommunity.Domain.Authorization;
using Beep.KocAiCommunity.Domain.Community;
using Beep.KocAiCommunity.Domain.Competitions;
using Beep.KocAiCommunity.Domain.Datasets;
using Beep.KocAiCommunity.Domain.Engagement;
using Beep.KocAiCommunity.Domain.Experiments;
using Beep.KocAiCommunity.Domain.Jobs;
using Beep.KocAiCommunity.Domain.Learning;
using Beep.KocAiCommunity.Domain.Messaging;
using Beep.KocAiCommunity.Domain.Notifications;
using Beep.KocAiCommunity.Domain.Organization;
using Beep.KocAiCommunity.Domain.Storage;
using Beep.KocAiCommunity.Domain.Studio;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using WorkflowEntity = Beep.KocAiCommunity.Domain.Studio.Workflow;

namespace Beep.KocAiCommunity.Infrastructure.Persistence;

/// <summary>
/// Canonical application DbContext. Identity tables live in schema <c>dbo</c>; org/RBAC tables
/// in <c>koc</c>; audit in <c>platform</c>. Entity configurations are applied from
/// <see cref="KocDbContext"/>'s assembly.
/// </summary>
public class KocDbContext(DbContextOptions<KocDbContext> options)
    : IdentityDbContext<IdentityUser, IdentityRole, string>(options)
{
    public DbSet<OrgUnit> OrgUnits => Set<OrgUnit>();
    public DbSet<OrgMembership> OrgMemberships => Set<OrgMembership>();
    public DbSet<UserEntityPermission> UserEntityPermissions => Set<UserEntityPermission>();
    public DbSet<CompetitionCreatorGrant> CompetitionCreatorGrants => Set<CompetitionCreatorGrant>();
    public DbSet<AdminAuditLog> AdminAuditLogs => Set<AdminAuditLog>();
    public DbSet<ArtifactReference> ArtifactReferences => Set<ArtifactReference>();
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();
    public DbSet<LearningTrack> LearningTracks => Set<LearningTrack>();
    public DbSet<Lesson> Lessons => Set<Lesson>();
    public DbSet<TrackEnrollment> TrackEnrollments => Set<TrackEnrollment>();
    public DbSet<LessonProgress> LessonProgress => Set<LessonProgress>();
    public DbSet<TrackCompletion> TrackCompletions => Set<TrackCompletion>();
    public DbSet<Competition> Competitions => Set<Competition>();
    public DbSet<Submission> Submissions => Set<Submission>();
    public DbSet<LeaderboardEntry> LeaderboardEntries => Set<LeaderboardEntry>();
    public DbSet<Dataset> Datasets => Set<Dataset>();
    public DbSet<DatasetVersion> DatasetVersions => Set<DatasetVersion>();
    public DbSet<DatasetFile> DatasetFiles => Set<DatasetFile>();
    public DbSet<DatasetSchemaColumn> DatasetSchemaColumns => Set<DatasetSchemaColumn>();
    public DbSet<DatasetProfile> DatasetProfiles => Set<DatasetProfile>();
    public DbSet<DatasetProfileColumn> DatasetProfileColumns => Set<DatasetProfileColumn>();
    public DbSet<ModelRun> ModelRuns => Set<ModelRun>();
    public DbSet<RegisteredModel> RegisteredModels => Set<RegisteredModel>();
    public DbSet<ModelVersion> ModelVersions => Set<ModelVersion>();
    public DbSet<ModelApproval> ModelApprovals => Set<ModelApproval>();
    public DbSet<ModelDeployment> ModelDeployments => Set<ModelDeployment>();
    public DbSet<ModelInferenceLog> ModelInferenceLogs => Set<ModelInferenceLog>();
    public DbSet<Discussion> Discussions => Set<Discussion>();
    public DbSet<DiscussionReply> DiscussionReplies => Set<DiscussionReply>();
    public DbSet<Reaction> Reactions => Set<Reaction>();
    public DbSet<Mention> Mentions => Set<Mention>();
    public DbSet<DiscussionAttachment> DiscussionAttachments => Set<DiscussionAttachment>();
    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<UserProfile> UserProfiles => Set<UserProfile>();
    public DbSet<XpEvent> XpEvents => Set<XpEvent>();
    public DbSet<Badge> Badges => Set<Badge>();
    public DbSet<UserBadge> UserBadges => Set<UserBadge>();
    public DbSet<Kudos> Kudos => Set<Kudos>();
    public DbSet<ActivityEvent> ActivityEvents => Set<ActivityEvent>();
    public DbSet<Job> Jobs => Set<Job>();
    public DbSet<JobAttempt> JobAttempts => Set<JobAttempt>();
    public DbSet<JobLog> JobLogs => Set<JobLog>();
    public DbSet<Experiment> Experiments => Set<Experiment>();
    public DbSet<Run> ExperimentRuns => Set<Run>();
    public DbSet<RunMetric> RunMetrics => Set<RunMetric>();
    public DbSet<RunParameter> RunParameters => Set<RunParameter>();
    public DbSet<WorkflowEntity> Workflows => Set<WorkflowEntity>();
    public DbSet<WorkflowVersion> WorkflowVersions => Set<WorkflowVersion>();
    public DbSet<WorkflowTemplate> WorkflowTemplates => Set<WorkflowTemplate>();
    public DbSet<Domain.Admin.SettingValue> SettingValues => Set<Domain.Admin.SettingValue>();
    public DbSet<Domain.Admin.FeatureFlag> FeatureFlags => Set<Domain.Admin.FeatureFlag>();
    public DbSet<Domain.Connectors.ConnectorInstance> ConnectorInstances => Set<Domain.Connectors.ConnectorInstance>();
    public DbSet<Domain.Connectors.CredentialVaultEntry> CredentialVaultEntries => Set<Domain.Connectors.CredentialVaultEntry>();
    public DbSet<Domain.Connectors.ConnectorHealthSnapshot> ConnectorHealthSnapshots => Set<Domain.Connectors.ConnectorHealthSnapshot>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        builder.ApplyConfigurationsFromAssembly(typeof(KocDbContext).Assembly);
    }
}
