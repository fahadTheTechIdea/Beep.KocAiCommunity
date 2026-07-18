namespace Beep.KocAiCommunity.Contracts.Dashboard;

/// <summary>One competition a person has entered, with their best result.</summary>
public sealed record PersonalStandingDto(Guid CompetitionId, string Title, double? BestScore, int? Rank, int Submissions);

/// <summary>A person's own learning + competition snapshot for the dashboard.</summary>
public sealed record PersonalDashboardDto(
    int Enrollments,
    int TracksCompleted,
    int TracksAvailable,
    int Submissions,
    int CompetitionsEntered,
    int? BestRank,
    IReadOnlyList<PersonalStandingDto> Standings);
