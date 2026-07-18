using Beep.KocAiCommunity.Application.Dashboard;
using Beep.KocAiCommunity.Contracts.Dashboard;
using Beep.KocAiCommunity.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Beep.KocAiCommunity.Infrastructure.Dashboard;

public sealed class DashboardService(KocDbContext db) : IDashboardService
{
    public async Task<PersonalDashboardDto> GetPersonalAsync(string userId, CancellationToken ct = default)
    {
        var enrollments = await db.TrackEnrollments.CountAsync(e => e.UserId == userId, ct);
        var completed = await db.TrackCompletions.CountAsync(c => c.UserId == userId, ct);
        var available = await db.LearningTracks.CountAsync(t => t.Status == "published", ct);

        var mySubs = await db.Submissions.AsNoTracking()
            .Where(s => s.SubmitterUserId == userId)
            .GroupBy(s => s.CompetitionId)
            .Select(g => new { CompetitionId = g.Key, Count = g.Count() })
            .ToListAsync(ct);

        var entries = await db.LeaderboardEntries.AsNoTracking()
            .Where(l => l.SubmitterUserId == userId)
            .ToDictionaryAsync(l => l.CompetitionId, l => l, ct);

        var competitionIds = mySubs.Select(s => s.CompetitionId).ToList();
        var titles = await db.Competitions.AsNoTracking()
            .Where(c => competitionIds.Contains(c.Id))
            .ToDictionaryAsync(c => c.Id, c => c.Title, ct);

        var standings = mySubs
            .Select(s => new PersonalStandingDto(
                s.CompetitionId,
                titles.GetValueOrDefault(s.CompetitionId, "(competition)"),
                entries.TryGetValue(s.CompetitionId, out var e) ? e.Score : null,
                entries.TryGetValue(s.CompetitionId, out var r) ? r.Rank : null,
                s.Count))
            .OrderBy(s => s.Rank ?? int.MaxValue)
            .ToList();

        var bestRank = standings.Where(s => s.Rank is not null).Select(s => s.Rank!.Value).DefaultIfEmpty().Min();

        return new PersonalDashboardDto(
            Enrollments: enrollments,
            TracksCompleted: completed,
            TracksAvailable: available,
            Submissions: mySubs.Sum(s => s.Count),
            CompetitionsEntered: mySubs.Count,
            BestRank: bestRank == 0 ? null : bestRank,
            Standings: standings);
    }
}
