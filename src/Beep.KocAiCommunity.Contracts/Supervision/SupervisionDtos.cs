namespace Beep.KocAiCommunity.Contracts.Supervision;

/// <summary>One person's participation, for a supervisor's read-only rollup.</summary>
public sealed record MemberParticipationDto(
    string UserId,
    int Enrollments,
    int TracksCompleted,
    int Submissions,
    double? BestScore);

/// <summary>Aggregated participation across a supervisor's org subtree.</summary>
public sealed record SupervisionRollupDto(
    string ScopeLabel,
    int MemberCount,
    int ActiveLearners,
    int TracksCompleted,
    int CompetitionsEntered,
    IReadOnlyList<MemberParticipationDto> Members);
