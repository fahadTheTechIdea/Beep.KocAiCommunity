using Beep.KocAiCommunity.Application.Admin;
using Beep.KocAiCommunity.Application.Audit;
using Beep.KocAiCommunity.Domain.Common;
using Beep.KocAiCommunity.Domain.Community;
using Beep.KocAiCommunity.Domain.Competitions;
using Beep.KocAiCommunity.Domain.Datasets;
using Beep.KocAiCommunity.Domain.Engagement;
using Beep.KocAiCommunity.Domain.Organization;
using Beep.KocAiCommunity.Infrastructure.Engagement;
using Beep.KocAiCommunity.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Beep.KocAiCommunity.Infrastructure.Admin;

/// <summary>
/// Creates and removes a self-contained demo of the platform. Everything it writes is namespaced:
/// people are <c>demo-*</c> user ids and the org unit lives at <c>/demo</c>, so unseed can remove the
/// demo precisely without touching real KOC records.
/// </summary>
public sealed class DemoDataService(KocDbContext db, IAuditEnvelope audit) : IDemoDataService
{
    private const string Prefix = "demo-";          // every demo user id starts with this
    private const string OrgPath = "/demo";         // the demo org subtree

    private sealed record DemoPerson(string UserId, string Name, string Avatar, int Xp, int Streak, string Skills);

    private static readonly DemoPerson[] People =
    [
        new($"{Prefix}alice", "Alice Al-Sabah", "185-worker.png", 420, 12, "ML.NET,Reservoir"),
        new($"{Prefix}bob", "Bob Al-Rashid", "179-exploration.png", 260, 5, "Python,Production"),
        new($"{Prefix}carol", "Carol Mansour", "042-oil-pump.png", 155, 3, "HSE,Analytics"),
        new($"{Prefix}dana", "Dana Khalifa", "016-drill.png", 60, 1, "Geoscience"),
    ];

    public async Task<DemoDataStatus> GetStatusAsync(CancellationToken ct = default)
    {
        var users = await db.Set<UserProfile>().CountAsync(p => p.UserId.StartsWith(Prefix), ct);
        var competitions = await db.Set<Competition>().CountAsync(c => c.CreatedByUserId!.StartsWith(Prefix), ct);
        var discussions = await db.Set<Discussion>().CountAsync(d => d.AuthorUserId.StartsWith(Prefix), ct);
        var datasets = await db.Set<Dataset>().CountAsync(d => d.OwnerUserId.StartsWith(Prefix), ct);
        return new DemoDataStatus(users > 0, users, competitions, discussions, datasets);
    }

    public async Task<DemoDataStatus> SeedAsync(string actorUserId, CancellationToken ct = default)
    {
        var status = await GetStatusAsync(ct);
        if (status.Seeded)
        {
            return status;   // idempotent — unseed first to refresh
        }

        var now = DateTime.UtcNow;

        // --- Org: a standalone demo team so demo people never mix into the real KOC tree ---
        var team = new OrgUnit
        {
            Name = "[Demo] Reservoir AI",
            Type = OrgUnitType.Team,
            ParentId = null,
            Path = OrgPath,
            LeaderUserId = People[0].UserId,
            CreatedByUserId = actorUserId,
            CreatedUtc = now,
        };
        db.Add(team);

        // --- People: membership, profile, XP ledger, a badge ---
        foreach (var (person, index) in People.Select((p, i) => (p, i)))
        {
            db.Add(new OrgMembership
            {
                UserId = person.UserId,
                OrgUnitId = team.Id,
                PositionLevel = index == 0 ? PositionLevel.TeamLeader : PositionLevel.Employee,
                IsPrimary = true,
                FromUtc = now,
                CreatedByUserId = actorUserId,
                CreatedUtc = now,
            });

            var level = KocLevels.ForXp(person.Xp);
            db.Add(new UserProfile
            {
                UserId = person.UserId,
                DisplayName = person.Name,
                Bio = "Demo colleague — remove with Unseed demo data.",
                AvatarIcon = person.Avatar,
                SkillsCsv = person.Skills,
                XpTotal = person.Xp,
                Level = level.Level,
                CurrentStreakDays = person.Streak,
                LongestStreakDays = person.Streak,
                LastActiveDate = DateOnly.FromDateTime(now),
                CreatedByUserId = actorUserId,
                CreatedUtc = now,
            });

            // A small ledger that adds up to the profile total, so the leaderboards look real.
            db.Add(new XpEvent { UserId = person.UserId, Source = XpSources.LessonCompleted, Points = person.Xp / 2, RefType = "demo", CreatedByUserId = actorUserId, CreatedUtc = now.AddDays(-3) });
            db.Add(new XpEvent { UserId = person.UserId, Source = XpSources.DiscussionCreated, Points = person.Xp - (person.Xp / 2), RefType = "demo", CreatedByUserId = actorUserId, CreatedUtc = now.AddDays(-1) });
            db.Add(new UserBadge { UserId = person.UserId, BadgeCode = BadgeCatalog.FirstBarrel, CreatedByUserId = actorUserId, CreatedUtc = now });
        }

        db.Add(new Kudos
        {
            FromUserId = People[0].UserId,
            ToUserId = People[1].UserId,
            Message = "Great catch on the ESP sensor drift — saved us a rerun.",
            Emoji = "👏",
            CreatedByUserId = actorUserId,
            CreatedUtc = now,
        });

        // --- A company-wide competition with a leaderboard ---
        var competition = new Competition
        {
            Title = "[Demo] ESP Failure Challenge",
            Description = "Predict electric submersible pump failures from sensor readings. Demo content.",
            Status = "active",
            VisibilityScope = VisibilityScope.Company,
            VisibilityOrgUnitId = Guid.Empty,
            ScorerCode = "accuracy",
            LabelColumn = "label",
            IdColumn = "id",
            TaskType = "BinaryClassification",
            SubmissionQuotaPerDay = 5,
            CreatedByUserId = People[0].UserId,
            CreatedUtc = now.AddDays(-7),
        };
        db.Add(competition);

        var scores = new[] { 0.94, 0.91, 0.88, 0.80 };
        foreach (var (person, index) in People.Select((p, i) => (p, i)))
        {
            db.Add(new LeaderboardEntry
            {
                CompetitionId = competition.Id,
                SubmitterUserId = person.UserId,
                Score = scores[index],
                Rank = index + 1,
                CreatedByUserId = actorUserId,
                CreatedUtc = now,
            });
        }

        // --- A discussion with a reply and a reaction ---
        var discussion = new Discussion
        {
            Title = "[Demo] Welcome to the ESP challenge",
            Body = "Share what features moved your score. Remember: split before you fit!",
            AuthorUserId = People[0].UserId,
            VisibilityScope = VisibilityScope.Company,
            VisibilityOrgUnitId = Guid.Empty,
            CreatedByUserId = People[0].UserId,
            CreatedUtc = now.AddDays(-2),
        };
        db.Add(discussion);
        db.Add(new DiscussionReply
        {
            DiscussionId = discussion.Id,
            AuthorUserId = People[1].UserId,
            Body = "Rolling averages on vibration helped me most.",
            CreatedByUserId = People[1].UserId,
            CreatedUtc = now.AddDays(-1),
        });
        db.Add(new Reaction
        {
            TargetType = ReactionTargetDiscussion,
            TargetId = discussion.Id,
            UserId = People[2].UserId,
            Emoji = "👍",
            CreatedByUserId = People[2].UserId,
            CreatedUtc = now,
        });

        // --- A dataset (metadata only; upload a file from the Datasets page to train on it) ---
        db.Add(new Dataset
        {
            Name = "[Demo] ESP sensor readings",
            Description = "Demo dataset placeholder — upload a CSV to make it trainable.",
            OwnerUserId = People[0].UserId,
            VisibilityScope = VisibilityScope.Company,
            VisibilityOrgUnitId = Guid.Empty,
            Classification = KocDataClassification.Internal,
            Domain = "upstream",
            CreatedByUserId = People[0].UserId,
            CreatedUtc = now,
        });

        await db.SaveChangesAsync(ct);
        await audit.WriteAsync(new AuditEntry("demo.seed", "demo-data", null, null, $"{{\"users\":{People.Length}}}"), ct);
        return await GetStatusAsync(ct);
    }

    public async Task<DemoDataStatus> UnseedAsync(string actorUserId, CancellationToken ct = default)
    {
        // Leaf/user-keyed rows first, then parents (children of those cascade).
        await db.Set<Kudos>().Where(k => k.FromUserId.StartsWith(Prefix) || k.ToUserId.StartsWith(Prefix)).ExecuteDeleteAsync(ct);
        await db.Set<UserBadge>().Where(b => b.UserId.StartsWith(Prefix)).ExecuteDeleteAsync(ct);
        await db.Set<XpEvent>().Where(x => x.UserId.StartsWith(Prefix)).ExecuteDeleteAsync(ct);
        await db.Set<ActivityEvent>().Where(a => a.ActorUserId.StartsWith(Prefix)).ExecuteDeleteAsync(ct);
        await db.Set<Reaction>().Where(r => r.UserId.StartsWith(Prefix)).ExecuteDeleteAsync(ct);
        await db.Set<Mention>().Where(m => m.ByUserId.StartsWith(Prefix) || m.MentionedUserId.StartsWith(Prefix)).ExecuteDeleteAsync(ct);
        await db.Set<DiscussionReply>().Where(r => r.AuthorUserId.StartsWith(Prefix)).ExecuteDeleteAsync(ct);
        await db.Set<Discussion>().Where(d => d.AuthorUserId.StartsWith(Prefix)).ExecuteDeleteAsync(ct);
        await db.Set<LeaderboardEntry>().Where(e => e.SubmitterUserId.StartsWith(Prefix)).ExecuteDeleteAsync(ct);
        await db.Set<Submission>().Where(s => s.SubmitterUserId.StartsWith(Prefix)).ExecuteDeleteAsync(ct);
        await db.Set<Competition>().Where(c => c.CreatedByUserId!.StartsWith(Prefix)).ExecuteDeleteAsync(ct);
        await db.Set<Dataset>().Where(d => d.OwnerUserId.StartsWith(Prefix)).ExecuteDeleteAsync(ct);
        await db.Set<UserProfile>().Where(p => p.UserId.StartsWith(Prefix)).ExecuteDeleteAsync(ct);
        await db.Set<OrgMembership>().Where(m => m.UserId.StartsWith(Prefix)).ExecuteDeleteAsync(ct);
        await db.Set<OrgUnit>().Where(u => u.Path.StartsWith(OrgPath)).ExecuteDeleteAsync(ct);

        await audit.WriteAsync(new AuditEntry("demo.unseed", "demo-data", null, null, null), ct);
        return await GetStatusAsync(ct);
    }

    private const string ReactionTargetDiscussion = "discussion";
}
