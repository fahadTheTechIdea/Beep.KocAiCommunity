using Beep.KocAiCommunity.Contracts.Engagement;

namespace Beep.KocAiCommunity.Contracts.Competitions;

/// <summary>
/// Curated, read-only data for the signed-out landing page: active company-wide competitions, the
/// featured competition's live top-3, and this month's top learners. Contains nothing private, so it
/// can be served anonymously to entice guests to sign in.
/// </summary>
public sealed record PublicShowcaseDto(
    Guid? FeaturedId,
    IReadOnlyList<CompetitionDto> Competitions,
    IReadOnlyList<LeaderboardEntryDto> FeaturedBoard,
    IReadOnlyList<XpLeaderboardRowDto> TopLearners);
