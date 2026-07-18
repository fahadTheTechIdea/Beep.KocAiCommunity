using Beep.KocAiCommunity.Domain.Audit;
using Beep.KocAiCommunity.Domain.Authorization;
using Beep.KocAiCommunity.Domain.Community;
using Beep.KocAiCommunity.Domain.Competitions;
using Beep.KocAiCommunity.Domain.Datasets;
using Beep.KocAiCommunity.Domain.Learning;
using Beep.KocAiCommunity.Domain.Messaging;
using Beep.KocAiCommunity.Domain.Notifications;
using Beep.KocAiCommunity.Domain.Organization;
using Beep.KocAiCommunity.Domain.Storage;
using Beep.KocAiCommunity.Domain.Studio;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

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
    public DbSet<ModelRun> ModelRuns => Set<ModelRun>();
    public DbSet<Project> Projects => Set<Project>();
    public DbSet<RegisteredModel> RegisteredModels => Set<RegisteredModel>();
    public DbSet<ModelVersion> ModelVersions => Set<ModelVersion>();
    public DbSet<ModelApproval> ModelApprovals => Set<ModelApproval>();
    public DbSet<ModelDeployment> ModelDeployments => Set<ModelDeployment>();
    public DbSet<Discussion> Discussions => Set<Discussion>();
    public DbSet<DiscussionReply> DiscussionReplies => Set<DiscussionReply>();
    public DbSet<Notification> Notifications => Set<Notification>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        builder.ApplyConfigurationsFromAssembly(typeof(KocDbContext).Assembly);
    }
}
