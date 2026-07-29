using Beep.KocAiCommunity.Contracts.Engagement;

namespace Beep.KocAiCommunity.Application.Engagement;

/// <summary>How far back a leaderboard aggregates.</summary>
public enum LeaderboardPeriod
{
    Week = 0,
    Month = 1,
    AllTime = 2,
}

/// <summary>
/// The engagement layer: Barrels (bbl) XP, the O&amp;G career ladder, badges, streaks, kudos, team
/// leaderboards, and the activity feed. XP is only ever granted server-side by <see cref="AwardXpAsync"/>.
/// </summary>
public interface IEngagementService
{
    Task<ProfileDto> GetProfileAsync(string userId, string? displayNameIfNew = null, CancellationToken ct = default);
    Task<ProfileDto> UpdateProfileAsync(string userId, UpdateProfileRequest request, CancellationToken ct = default);

    /// <summary>Remember this person's interface language. Unrecognised values fall back to English.</summary>
    Task SetLanguageAsync(string userId, string language, CancellationToken ct = default);

    /// <summary>
    /// Awards Barrels. Idempotent per (userId, source, refId); applies per-source daily caps; rolls up
    /// the profile total, recomputes the level, touches the streak, evaluates badge rules, and emits
    /// activity + real-time celebrations. Never throws for a duplicate award.
    /// </summary>
    Task AwardXpAsync(string userId, string source, string? refType = null, Guid? refId = null, CancellationToken ct = default);

    Task<IReadOnlyList<XpLeaderboardRowDto>> GetXpLeaderboardAsync(string callerUserId, LeaderboardPeriod period, CancellationToken ct = default);
    Task<IReadOnlyList<TeamLeaderboardRowDto>> GetTeamLeaderboardAsync(string callerUserId, LeaderboardPeriod period, CancellationToken ct = default);

    Task<IReadOnlyList<BadgeDto>> GetBadgeCatalogAsync(CancellationToken ct = default);
    Task GiveKudosAsync(string fromUserId, GiveKudosRequest request, CancellationToken ct = default);
    Task<IReadOnlyList<KudosDto>> GetKudosForAsync(string userId, int take = 30, CancellationToken ct = default);
    Task<IReadOnlyList<ActivityDto>> GetActivityFeedAsync(string callerUserId, int take = 40, CancellationToken ct = default);
}
