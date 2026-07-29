namespace Beep.KocAiCommunity.Contracts.Engagement;

/// <summary>A user's community profile with their earned standing.</summary>
public sealed record ProfileDto(
    string UserId,
    string DisplayName,
    string? Bio,
    string AvatarIcon,
    IReadOnlyList<string> Skills,
    int XpTotal,
    int Level,
    string LevelTitle,
    int? NextLevelXp,
    int CurrentStreakDays,
    int LongestStreakDays,
    IReadOnlyList<BadgeDto> Badges,
    /// <summary>The interface language this person chose, or null if they never did.</summary>
    string? Language = null);

/// <summary>An earned or catalog badge. <paramref name="EarnedUtc"/> is null for catalog-only rows.</summary>
public sealed record BadgeDto(string Code, string Name, string Description, string IconFile, string Tier, DateTime? EarnedUtc);

public sealed record UpdateProfileRequest(string? Bio, string? AvatarIcon, string? SkillsCsv);

/// <summary>
/// The interface language, saved against the account so it follows someone to another device. Its own
/// request rather than a field on <see cref="UpdateProfileRequest"/>: switching language should not
/// have to send a bio and an avatar back, nor risk overwriting them.
/// </summary>
public sealed record SetLanguageRequest(string Language);

/// <summary>One row on the individual Barrels leaderboard.</summary>
public sealed record XpLeaderboardRowDto(int Rank, string UserId, string DisplayName, string AvatarIcon, int Level, string LevelTitle, int XpTotal, bool IsMe);

/// <summary>One org unit (Team) on the team leaderboard, ranked by average bbl per member.</summary>
public sealed record TeamLeaderboardRowDto(int Rank, Guid OrgUnitId, string OrgUnitName, int MemberCount, int TotalXp, double AverageXp, bool IsMyTeam);

public sealed record GiveKudosRequest(string ToUserId, string Message, string Emoji, string? RefType, Guid? RefId);

public sealed record KudosDto(Guid Id, string FromUserId, string FromDisplayName, string ToUserId, string Message, string Emoji, DateTime CreatedUtc);

public sealed record ActivityDto(Guid Id, string ActorUserId, string ActorDisplayName, string Type, string? Summary, string? IconFile, DateTime CreatedUtc);
