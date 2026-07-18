using Beep.KocAiCommunity.Application.Audit;
using Beep.KocAiCommunity.Application.Security;
using Beep.KocAiCommunity.Domain.Audit;
using Beep.KocAiCommunity.Infrastructure.Persistence;

namespace Beep.KocAiCommunity.Infrastructure.Audit;

/// <summary>Writes audit rows, stamping the actor from the current user (or "system").</summary>
public sealed class AuditEnvelopeService(KocDbContext db, IKocCurrentUser? currentUser = null) : IAuditEnvelope
{
    public async Task WriteAsync(AuditEntry entry, CancellationToken ct = default)
    {
        db.AdminAuditLogs.Add(new AdminAuditLog
        {
            ActorUserId = currentUser?.UserId ?? "system",
            ActorRole = currentUser?.Roles.Count > 0 ? string.Join(',', currentUser.Roles) : null,
            Action = entry.Action,
            Resource = entry.Resource,
            ResourceId = entry.ResourceId,
            BeforeJson = entry.BeforeJson,
            AfterJson = entry.AfterJson,
            OccurredUtc = DateTime.UtcNow,
        });

        await db.SaveChangesAsync(ct);
    }
}
