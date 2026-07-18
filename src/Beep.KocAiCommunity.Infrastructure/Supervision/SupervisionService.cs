using Beep.KocAiCommunity.Application.Organization;
using Beep.KocAiCommunity.Application.Supervision;
using Beep.KocAiCommunity.Contracts.Supervision;
using Beep.KocAiCommunity.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Beep.KocAiCommunity.Infrastructure.Supervision;

public sealed class SupervisionService(
    KocDbContext db,
    IOrgScopeResolver scope,
    IOrgDirectory directory) : ISupervisionService
{
    public async Task<SupervisionRollupDto> GetRollupAsync(string userId, CancellationToken ct = default)
    {
        var resolved = await scope.ResolveForUserAsync(userId, ct);
        var members = resolved.MemberUserIds.ToList();

        var enrollments = await db.TrackEnrollments.Where(e => members.Contains(e.UserId))
            .GroupBy(e => e.UserId).Select(g => new { User = g.Key, Count = g.Count() }).ToDictionaryAsync(x => x.User, x => x.Count, ct);

        var completions = await db.TrackCompletions.Where(c => members.Contains(c.UserId))
            .GroupBy(c => c.UserId).Select(g => new { User = g.Key, Count = g.Count() }).ToDictionaryAsync(x => x.User, x => x.Count, ct);

        var submissions = await db.Submissions.Where(s => members.Contains(s.SubmitterUserId))
            .GroupBy(s => s.SubmitterUserId).Select(g => new { User = g.Key, Count = g.Count() }).ToDictionaryAsync(x => x.User, x => x.Count, ct);

        var bestScores = await db.LeaderboardEntries.Where(l => members.Contains(l.SubmitterUserId))
            .GroupBy(l => l.SubmitterUserId).Select(g => new { User = g.Key, Best = g.Max(x => x.Score) }).ToDictionaryAsync(x => x.User, x => x.Best, ct);

        var memberDtos = members
            .OrderBy(m => m, StringComparer.Ordinal)
            .Select(m => new MemberParticipationDto(
                m,
                enrollments.GetValueOrDefault(m),
                completions.GetValueOrDefault(m),
                submissions.GetValueOrDefault(m),
                bestScores.TryGetValue(m, out var best) ? best : null))
            .ToList();

        var competitionsEntered = await db.Submissions
            .Where(s => members.Contains(s.SubmitterUserId))
            .Select(s => s.CompetitionId).Distinct().CountAsync(ct);

        return new SupervisionRollupDto(
            ScopeLabel: await ResolveLabelAsync(userId, ct),
            MemberCount: members.Count,
            ActiveLearners: memberDtos.Count(m => m.Enrollments > 0),
            TracksCompleted: memberDtos.Sum(m => m.TracksCompleted),
            CompetitionsEntered: competitionsEntered,
            Members: memberDtos);
    }

    private async Task<string> ResolveLabelAsync(string userId, CancellationToken ct)
    {
        var profile = await directory.GetProfileAsync(userId, ct);
        if (profile?.LedOrgUnitId is not { } ledId)
        {
            return "Your participation";
        }

        var unit = await db.OrgUnits.Where(u => u.Id == ledId).Select(u => new { u.Type, u.Name }).FirstOrDefaultAsync(ct);
        return unit is null ? "Your organization" : $"{unit.Type}: {unit.Name}";
    }
}
