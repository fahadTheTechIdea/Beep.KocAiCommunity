using Beep.KocAiCommunity.Application.Admin;
using Beep.KocAiCommunity.Domain.Audit;
using Beep.KocAiCommunity.Domain.Community;
using Beep.KocAiCommunity.Domain.Competitions;
using Beep.KocAiCommunity.Domain.Jobs;
using Beep.KocAiCommunity.Domain.Studio;
using Beep.KocAiCommunity.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using WorkflowEntity = Beep.KocAiCommunity.Domain.Studio.Workflow;

namespace Beep.KocAiCommunity.Infrastructure.Admin;

/// <summary>Read models for the admin dashboard and audit trail — live counts, recent audit, health.</summary>
public sealed class AdminDashboardService(KocDbContext db) : IAdminDashboardService
{
    public async Task<AdminDashboard> GetDashboardAsync(CancellationToken ct = default)
    {
        // People are UserProfile rows keyed by Entra id — the AspNet Identity table stays empty here.
        var users = await db.Set<Domain.Engagement.UserProfile>().CountAsync(ct);
        var workflows = await db.Set<WorkflowEntity>().CountAsync(w => !w.IsDeleted, ct);
        var competitions = await db.Set<Competition>().CountAsync(ct);
        var models = await db.Set<RegisteredModel>().CountAsync(ct);
        var discussions = await db.Set<Discussion>().CountAsync(d => !d.IsDeleted, ct);

        var recent = await db.AdminAuditLogs.AsNoTracking()
            .OrderByDescending(a => a.OccurredUtc).Take(10).ToListAsync(ct);

        var jobs = await db.Set<Job>().CountAsync(ct);
        var auditCount = await db.AdminAuditLogs.CountAsync(ct);
        var health = new List<HealthComponent>
        {
            new("Database", "Healthy", "Connected; queries succeeding."),
            new("Background jobs", "Healthy", $"{jobs} job(s) tracked."),
            new("Audit trail", "Healthy", $"{auditCount} event(s) recorded."),
        };

        return new AdminDashboard(users, workflows, competitions, models, discussions, recent.Select(ToView).ToList(), health);
    }

    public async Task<IReadOnlyList<AuditView>> ListAuditAsync(string? action, string? actorUserId, int take = 100, CancellationToken ct = default)
    {
        var q = db.AdminAuditLogs.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(action))
        {
            q = q.Where(a => a.Action.Contains(action));
        }

        if (!string.IsNullOrWhiteSpace(actorUserId))
        {
            q = q.Where(a => a.ActorUserId == actorUserId);
        }

        var rows = await q.OrderByDescending(a => a.OccurredUtc).Take(Math.Clamp(take, 1, 500)).ToListAsync(ct);
        return rows.Select(ToView).ToList();
    }

    private static AuditView ToView(AdminAuditLog a) =>
        new(a.Id, a.ActorUserId, a.ActorRole, a.Action, a.Resource, a.ResourceId, a.BeforeJson, a.AfterJson, a.OccurredUtc);
}
