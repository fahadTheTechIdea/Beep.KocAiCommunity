using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Beep.KocAiCommunity.Application.Authorization;
using Beep.KocAiCommunity.Application.Engagement;
using Beep.KocAiCommunity.Application.Organization;
using Beep.KocAiCommunity.Application.RealTime;
using Beep.KocAiCommunity.Contracts.Engagement;
using Beep.KocAiCommunity.Contracts.Localization;
using Beep.KocAiCommunity.Domain.Engagement;
using Beep.KocAiCommunity.Domain.Notifications;
using Beep.KocAiCommunity.Domain.Organization;
using Beep.KocAiCommunity.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Beep.KocAiCommunity.Infrastructure.Engagement;

/// <summary>Thrown for expected engagement errors (bad kudos, quota reached) surfaced to the caller.</summary>
public sealed class EngagementException(string message) : Exception(message);

public sealed class EngagementService(
    KocDbContext db,
    IOutboxWriter outbox,
    IVisibilityEvaluator visibility,
    IOrgDirectory directory) : IEngagementService
{
    private static readonly string[] AllowedEmoji = ["👏", "🚀", "🛢️", "🌟", "🤝"];

    // ---- Profiles ---------------------------------------------------------------------------

    public async Task<ProfileDto> GetProfileAsync(string userId, string? displayNameIfNew = null, CancellationToken ct = default)
    {
        var profile = await GetOrCreateProfileAsync(userId, displayNameIfNew, ct);

        // Backfill a real display name once we learn it (profiles created by XP hooks start as the id).
        if (!string.IsNullOrWhiteSpace(displayNameIfNew) && profile.DisplayName == profile.UserId && displayNameIfNew != profile.UserId)
        {
            profile.DisplayName = displayNameIfNew!;
        }

        if (db.ChangeTracker.HasChanges())
        {
            await db.SaveChangesAsync(ct);
        }

        return await BuildProfileDtoAsync(profile, ct);
    }

    public async Task SetLanguageAsync(string userId, string language, CancellationToken ct = default)
    {
        var profile = await GetOrCreateProfileAsync(userId, null, ct);
        profile.Language = KocLanguages.Normalize(language);
        profile.LastModifiedUtc = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
    }

    public async Task<ProfileDto> UpdateProfileAsync(string userId, UpdateProfileRequest request, CancellationToken ct = default)
    {
        var profile = await GetOrCreateProfileAsync(userId, null, ct);

        if (request.Bio is not null)
        {
            profile.Bio = request.Bio.Length > 280 ? request.Bio[..280] : request.Bio;
        }

        if (!string.IsNullOrWhiteSpace(request.AvatarIcon) && IconLibrary.IsAllowed(request.AvatarIcon))
        {
            profile.AvatarIcon = request.AvatarIcon;
        }

        if (request.SkillsCsv is not null)
        {
            var skills = request.SkillsCsv.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Take(12);
            profile.SkillsCsv = string.Join(',', skills);
        }

        profile.LastModifiedUtc = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return await BuildProfileDtoAsync(profile, ct);
    }

    // ---- Awarding XP ------------------------------------------------------------------------

    public async Task AwardXpAsync(string userId, string source, string? refType = null, Guid? refId = null, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(userId))
        {
            return;
        }

        // Idempotency: never award the same (source, ref) twice.
        if (refId is { } rid && await db.Set<XpEvent>().AnyAsync(e => e.UserId == userId && e.Source == source && e.RefId == rid, ct))
        {
            return;
        }

        var profile = await GetOrCreateProfileAsync(userId, null, ct);
        var oldLevel = profile.Level;

        // Advance the daily streak (once per active day) and maybe award the weekly streak bonus.
        UpdateStreakAndMaybeAward(profile, userId);

        var points = XpSources.Points(source);
        if (XpSources.IsDailyCapped(source) && points > 0)
        {
            var since = DateTime.UtcNow.Date;
            var spentToday = await db.Set<XpEvent>()
                .Where(e => e.UserId == userId && e.CreatedUtc >= since &&
                            (e.Source == XpSources.DiscussionCreated || e.Source == XpSources.DiscussionReplied))
                .SumAsync(e => (int?)e.Points, ct) ?? 0;
            points = Math.Clamp(XpSources.DailyCappedCeiling - spentToday, 0, points);
        }

        AddXpEvent(userId, source, points, refType, refId);
        profile.XpTotal += points;

        // One-time bonus on a user's first-ever scored submission.
        if (source == XpSources.SubmissionScored &&
            !await db.Set<XpEvent>().AnyAsync(e => e.UserId == userId && e.Source == XpSources.SubmissionScored, ct))
        {
            AddXpEvent(userId, XpSources.SubmissionFirst, XpSources.Points(XpSources.SubmissionFirst), refType, refId);
            profile.XpTotal += XpSources.Points(XpSources.SubmissionFirst);
        }

        // Recompute level from the new total.
        var newLevel = KocLevels.LevelForXp(profile.XpTotal);
        profile.Level = newLevel;
        var leveledUp = newLevel > oldLevel;

        // Persist the ledger + profile first so badge rules count committed events.
        await db.SaveChangesAsync(ct);

        var newBadges = await EvaluateBadgesAsync(userId, profile, ct);

        // Celebrations: a level-up and each new badge → activity + notification + real-time confetti.
        if (leveledUp)
        {
            var title = KocLevels.TitleForLevel(newLevel);
            await CelebrateAsync(userId, "levelup", "Level up!", $"You reached level {newLevel} — {title}.", "", "level.up", null, ct);
        }

        foreach (var code in newBadges)
        {
            var def = BadgeCatalog.Find(code);
            if (def is null)
            {
                continue;
            }

            await CelebrateAsync(userId, "badge", $"Badge earned: {def.Name}", def.Description, def.IconFile, "badge.earned", null, ct);
        }

        await db.SaveChangesAsync(ct);
    }

    private void UpdateStreakAndMaybeAward(UserProfile profile, string userId)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        if (profile.LastActiveDate == today)
        {
            return; // already counted today
        }

        profile.CurrentStreakDays = profile.LastActiveDate == today.AddDays(-1) ? profile.CurrentStreakDays + 1 : 1;
        profile.LastActiveDate = today;
        profile.LongestStreakDays = Math.Max(profile.LongestStreakDays, profile.CurrentStreakDays);

        if (profile.CurrentStreakDays > 0 && profile.CurrentStreakDays % 7 == 0)
        {
            var weekRef = DeterministicGuid($"streak:{userId}:{profile.CurrentStreakDays}");
            AddXpEvent(userId, XpSources.StreakWeek, XpSources.Points(XpSources.StreakWeek), "streak", weekRef);
            profile.XpTotal += XpSources.Points(XpSources.StreakWeek);
        }
    }

    private void AddXpEvent(string userId, string source, int points, string? refType, Guid? refId) =>
        db.Set<XpEvent>().Add(new XpEvent
        {
            UserId = userId,
            Source = source,
            Points = points,
            RefType = refType,
            RefId = refId,
            CreatedByUserId = userId,
            CreatedUtc = DateTime.UtcNow,
        });

    private async Task<IReadOnlyList<string>> EvaluateBadgesAsync(string userId, UserProfile profile, CancellationToken ct)
    {
        var earned = await db.Set<UserBadge>().Where(b => b.UserId == userId).Select(b => b.BadgeCode).ToListAsync(ct);
        var earnedSet = earned.ToHashSet(StringComparer.Ordinal);

        var context = new BadgeContext(
            XpEventCount: await db.Set<XpEvent>().CountAsync(e => e.UserId == userId, ct),
            ScoredSubmissionCount: await db.Set<XpEvent>().CountAsync(e => e.UserId == userId && e.Source == XpSources.SubmissionScored, ct),
            TrackCompletionCount: await db.Set<XpEvent>().CountAsync(e => e.UserId == userId && e.Source == XpSources.TrackCompleted, ct),
            PublishedTrackCount: await db.LearningTracks.CountAsync(t => t.Status == "published", ct),
            DiscussionCreatedCount: await db.Set<XpEvent>().CountAsync(e => e.UserId == userId && e.Source == XpSources.DiscussionCreated, ct),
            HasCompetitionWin: await db.Set<XpEvent>().AnyAsync(e => e.UserId == userId && e.Source == XpSources.CompetitionWin, ct),
            HasCompetitionPodium: await db.Set<XpEvent>().AnyAsync(e => e.UserId == userId && e.Source == XpSources.CompetitionTop3, ct),
            CurrentStreakDays: profile.CurrentStreakDays,
            KudosReceivedCount: await db.Set<Kudos>().CountAsync(k => k.ToUserId == userId, ct));

        var newCodes = BadgeRules.NewlyEarned(context, earnedSet);
        foreach (var code in newCodes)
        {
            db.Set<UserBadge>().Add(new UserBadge
            {
                UserId = userId,
                BadgeCode = code,
                CreatedByUserId = userId,
                CreatedUtc = DateTime.UtcNow,
            });
        }

        return newCodes;
    }

    // ---- Kudos ------------------------------------------------------------------------------

    public async Task GiveKudosAsync(string fromUserId, GiveKudosRequest request, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.ToUserId) || request.ToUserId == fromUserId)
        {
            throw new EngagementException("Choose a colleague other than yourself to thank.");
        }

        if (string.IsNullOrWhiteSpace(request.Message))
        {
            throw new EngagementException("Add a short message so your kudos means something.");
        }

        var since = DateTime.UtcNow.Date;
        var givenToday = await db.Set<Kudos>().CountAsync(k => k.FromUserId == fromUserId && k.CreatedUtc >= since, ct);
        if (givenToday >= 10)
        {
            throw new EngagementException("You've reached today's limit of 10 kudos. Save some for tomorrow!");
        }

        var message = request.Message.Length > 200 ? request.Message[..200] : request.Message.Trim();
        var emoji = AllowedEmoji.Contains(request.Emoji) ? request.Emoji : "👏";

        var kudos = new Kudos
        {
            FromUserId = fromUserId,
            ToUserId = request.ToUserId,
            Message = message,
            Emoji = emoji,
            RefType = request.RefType,
            RefId = request.RefId,
            CreatedByUserId = fromUserId,
            CreatedUtc = DateTime.UtcNow,
        };
        db.Set<Kudos>().Add(kudos);
        await db.SaveChangesAsync(ct);

        // Recipient earns bbl (and maybe the Good Neighbor badge); then a personal notification.
        await AwardXpAsync(request.ToUserId, XpSources.KudosReceived, "kudos", kudos.Id, ct);

        var fromName = await DisplayNameAsync(fromUserId, ct);
        db.Set<Notification>().Add(new Notification
        {
            UserId = request.ToUserId,
            Type = "kudos-received",
            Title = $"{emoji} Kudos from {fromName}",
            Message = message,
            LinkUrl = "/profile",
            CreatedUtc = DateTime.UtcNow,
        });
        await outbox.EnqueueAsync(new NotificationCreatedEvent(request.ToUserId), ct);
        await WriteActivityAsync(request.ToUserId, "kudos.received", "kudos", kudos.Id, $"received kudos from {fromName}", "118-approval.png", ct);
        await db.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<KudosDto>> GetKudosForAsync(string userId, int take = 30, CancellationToken ct = default)
    {
        var rows = await db.Set<Kudos>().AsNoTracking()
            .Where(k => k.ToUserId == userId)
            .OrderByDescending(k => k.CreatedUtc)
            .Take(Math.Clamp(take, 1, 100))
            .ToListAsync(ct);

        var names = await DisplayNamesAsync(rows.Select(k => k.FromUserId), ct);
        return [.. rows.Select(k => new KudosDto(k.Id, k.FromUserId, names.GetValueOrDefault(k.FromUserId, k.FromUserId), k.ToUserId, k.Message, k.Emoji, k.CreatedUtc))];
    }

    // ---- Leaderboards -----------------------------------------------------------------------

    public async Task<IReadOnlyList<XpLeaderboardRowDto>> GetXpLeaderboardAsync(string callerUserId, LeaderboardPeriod period, CancellationToken ct = default)
    {
        var scores = await ScoresByUserAsync(period, ct);

        var profiles = await db.Set<UserProfile>().AsNoTracking()
            .Where(p => scores.Keys.Contains(p.UserId))
            .ToDictionaryAsync(p => p.UserId, ct);

        var ordered = scores
            .Select(kv => (UserId: kv.Key, Score: kv.Value))
            .OrderByDescending(x => x.Score)
            .ThenBy(x => x.UserId, StringComparer.Ordinal)
            .ToList();

        var rows = new List<XpLeaderboardRowDto>();
        for (var i = 0; i < ordered.Count && i < 10; i++)
        {
            rows.Add(BuildXpRow(ordered[i].UserId, i + 1, ordered[i].Score, profiles, callerUserId));
        }

        // Always include the caller, even if they're outside the top 10 ("never shame" — top 10 + you).
        if (!rows.Any(r => r.IsMe))
        {
            var idx = ordered.FindIndex(x => x.UserId == callerUserId);
            if (idx >= 0)
            {
                rows.Add(BuildXpRow(callerUserId, idx + 1, ordered[idx].Score, profiles, callerUserId));
            }
        }

        return rows;
    }

    public async Task<IReadOnlyList<TeamLeaderboardRowDto>> GetTeamLeaderboardAsync(string callerUserId, LeaderboardPeriod period, CancellationToken ct = default)
    {
        var scores = await ScoresByUserAsync(period, ct);
        if (scores.Count == 0)
        {
            return [];
        }

        // Map each scoring user to their home (primary, active) org unit.
        var memberships = await db.OrgMemberships.AsNoTracking()
            .Where(m => m.IsPrimary && m.ToUtc == null && scores.Keys.Contains(m.UserId))
            .Select(m => new { m.UserId, m.OrgUnitId })
            .ToListAsync(ct);

        var teams = memberships
            .GroupBy(m => m.OrgUnitId)
            .Select(g =>
            {
                var total = g.Sum(m => scores.GetValueOrDefault(m.UserId, 0));
                var count = g.Count();
                return (OrgUnitId: g.Key, MemberCount: count, TotalXp: total, AverageXp: count == 0 ? 0 : (double)total / count);
            })
            .OrderByDescending(t => t.AverageXp)
            .ToList();

        var unitIds = teams.Select(t => t.OrgUnitId).ToList();
        var names = await db.OrgUnits.AsNoTracking()
            .Where(u => unitIds.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, u => u.Name, ct);

        var myTeam = await db.OrgMemberships.AsNoTracking()
            .Where(m => m.UserId == callerUserId && m.IsPrimary && m.ToUtc == null)
            .Select(m => (Guid?)m.OrgUnitId)
            .FirstOrDefaultAsync(ct);

        return [.. teams.Take(10).Select((t, i) => new TeamLeaderboardRowDto(
            i + 1, t.OrgUnitId, names.GetValueOrDefault(t.OrgUnitId, "Team"), t.MemberCount, t.TotalXp,
            Math.Round(t.AverageXp, 1), t.OrgUnitId == myTeam))];
    }

    // ---- Catalog + activity -----------------------------------------------------------------

    public Task<IReadOnlyList<BadgeDto>> GetBadgeCatalogAsync(CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<BadgeDto>>(
            [.. BadgeCatalog.All.Select(b => new BadgeDto(b.Code, b.Name, b.Description, b.IconFile, b.Tier, null))]);

    public async Task<IReadOnlyList<ActivityDto>> GetActivityFeedAsync(string callerUserId, int take = 40, CancellationToken ct = default)
    {
        var recent = await db.Set<ActivityEvent>().AsNoTracking()
            .OrderByDescending(a => a.CreatedUtc)
            .Take(Math.Clamp(take, 1, 100) * 3) // over-fetch, then visibility-filter
            .ToListAsync(ct);

        var visible = new List<ActivityEvent>();
        foreach (var a in recent)
        {
            if (a.ActorUserId == callerUserId || await visibility.CanSeeAsync(callerUserId, a.VisibilityScope, a.VisibilityOrgUnitId, ct))
            {
                visible.Add(a);
                if (visible.Count >= take)
                {
                    break;
                }
            }
        }

        var names = await DisplayNamesAsync(visible.Select(a => a.ActorUserId), ct);
        return [.. visible.Select(a =>
        {
            var (summary, icon) = ParsePayload(a.PayloadJson);
            return new ActivityDto(a.Id, a.ActorUserId, names.GetValueOrDefault(a.ActorUserId, a.ActorUserId), a.Type, summary, icon, a.CreatedUtc);
        })];
    }

    // ---- Helpers ----------------------------------------------------------------------------

    private async Task<UserProfile> GetOrCreateProfileAsync(string userId, string? displayNameIfNew, CancellationToken ct)
    {
        var profile = await db.Set<UserProfile>().FirstOrDefaultAsync(p => p.UserId == userId, ct);
        if (profile is not null)
        {
            return profile;
        }

        profile = new UserProfile
        {
            UserId = userId,
            DisplayName = string.IsNullOrWhiteSpace(displayNameIfNew) ? userId : displayNameIfNew!,
            CreatedByUserId = userId,
            CreatedUtc = DateTime.UtcNow,
        };
        db.Set<UserProfile>().Add(profile);
        return profile;
    }

    private async Task<ProfileDto> BuildProfileDtoAsync(UserProfile profile, CancellationToken ct)
    {
        var earned = await db.Set<UserBadge>().AsNoTracking()
            .Where(b => b.UserId == profile.UserId)
            .ToDictionaryAsync(b => b.BadgeCode, b => b.CreatedUtc, ct);

        var badges = BadgeCatalog.All
            .Where(b => earned.ContainsKey(b.Code))
            .Select(b => new BadgeDto(b.Code, b.Name, b.Description, b.IconFile, b.Tier, earned[b.Code]))
            .ToList();

        var (level, title, _, nextXp) = KocLevels.ForXp(profile.XpTotal);
        var skills = string.IsNullOrWhiteSpace(profile.SkillsCsv)
            ? Array.Empty<string>()
            : profile.SkillsCsv.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        return new ProfileDto(profile.UserId, profile.DisplayName, profile.Bio, profile.AvatarIcon, skills,
            profile.XpTotal, level, title, nextXp, profile.CurrentStreakDays, profile.LongestStreakDays, badges,
            profile.Language);
    }

    /// <summary>Per-user bbl for the period: the profile total for all-time, otherwise a windowed ledger sum.</summary>
    private async Task<Dictionary<string, int>> ScoresByUserAsync(LeaderboardPeriod period, CancellationToken ct)
    {
        if (period == LeaderboardPeriod.AllTime)
        {
            return await db.Set<UserProfile>().AsNoTracking()
                .Where(p => p.XpTotal > 0)
                .ToDictionaryAsync(p => p.UserId, p => p.XpTotal, ct);
        }

        var since = DateTime.UtcNow.AddDays(period == LeaderboardPeriod.Week ? -7 : -30);
        var grouped = await db.Set<XpEvent>().AsNoTracking()
            .Where(e => e.CreatedUtc >= since)
            .GroupBy(e => e.UserId)
            .Select(g => new { UserId = g.Key, Score = g.Sum(e => e.Points) })
            .ToListAsync(ct);

        return grouped.Where(x => x.Score > 0).ToDictionary(x => x.UserId, x => x.Score);
    }

    private static XpLeaderboardRowDto BuildXpRow(string userId, int rank, int score, IReadOnlyDictionary<string, UserProfile> profiles, string callerUserId)
    {
        var p = profiles.GetValueOrDefault(userId);
        var name = p?.DisplayName ?? userId;
        var avatar = p?.AvatarIcon ?? "185-worker.png";
        var (level, title, _, _) = KocLevels.ForXp(p?.XpTotal ?? score);
        return new XpLeaderboardRowDto(rank, userId, name, avatar, level, title, score, userId == callerUserId);
    }

    private async Task CelebrateAsync(string userId, string kind, string title, string message, string iconFile, string activityType, Guid? refId, CancellationToken ct)
    {
        db.Set<Notification>().Add(new Notification
        {
            UserId = userId,
            Type = kind == "badge" ? "badge-earned" : "level-up",
            Title = title,
            Message = message,
            LinkUrl = "/profile",
            CreatedUtc = DateTime.UtcNow,
        });

        await outbox.EnqueueAsync(new NotificationCreatedEvent(userId), ct);
        await outbox.EnqueueAsync(new EngagementCelebrationEvent(userId, kind, title, message, iconFile), ct);
        await WriteActivityAsync(userId, activityType, "badge", refId, message, iconFile, ct);
    }

    private async Task WriteActivityAsync(string actorUserId, string type, string? refType, Guid? refId, string summary, string iconFile, CancellationToken ct)
    {
        // Scope the activity to the actor's home Team; falls back to Company when they have no unit yet.
        var homeUnit = await directory.ResolveScopeUnitAsync(actorUserId, VisibilityScope.Team, ct);
        db.Set<ActivityEvent>().Add(new ActivityEvent
        {
            ActorUserId = actorUserId,
            Type = type,
            RefType = refType,
            RefId = refId,
            PayloadJson = JsonSerializer.Serialize(new { summary, icon = iconFile }),
            VisibilityScope = homeUnit is null ? VisibilityScope.Company : VisibilityScope.Team,
            VisibilityOrgUnitId = homeUnit ?? Guid.Empty,
            CreatedByUserId = actorUserId,
            CreatedUtc = DateTime.UtcNow,
        });
    }

    private static (string? summary, string? icon) ParsePayload(string? payloadJson)
    {
        if (string.IsNullOrWhiteSpace(payloadJson))
        {
            return (null, null);
        }

        try
        {
            using var doc = JsonDocument.Parse(payloadJson);
            var root = doc.RootElement;
            var summary = root.TryGetProperty("summary", out var s) ? s.GetString() : null;
            var icon = root.TryGetProperty("icon", out var i) ? i.GetString() : null;
            return (summary, string.IsNullOrWhiteSpace(icon) ? null : icon);
        }
        catch (JsonException)
        {
            return (null, null);
        }
    }

    private async Task<string> DisplayNameAsync(string userId, CancellationToken ct) =>
        await db.Set<UserProfile>().AsNoTracking().Where(p => p.UserId == userId).Select(p => p.DisplayName).FirstOrDefaultAsync(ct) ?? userId;

    private async Task<Dictionary<string, string>> DisplayNamesAsync(IEnumerable<string> userIds, CancellationToken ct)
    {
        var ids = userIds.Distinct(StringComparer.Ordinal).ToList();
        return await db.Set<UserProfile>().AsNoTracking()
            .Where(p => ids.Contains(p.UserId))
            .ToDictionaryAsync(p => p.UserId, p => p.DisplayName, ct);
    }

    private static Guid DeterministicGuid(string input)
    {
        var bytes = MD5.HashData(Encoding.UTF8.GetBytes(input));
        return new Guid(bytes);
    }
}
