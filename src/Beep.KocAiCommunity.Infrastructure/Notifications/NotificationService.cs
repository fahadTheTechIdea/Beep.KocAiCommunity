using Beep.KocAiCommunity.Application.Notifications;
using Beep.KocAiCommunity.Application.RealTime;
using Beep.KocAiCommunity.Domain.Notifications;
using Beep.KocAiCommunity.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Beep.KocAiCommunity.Infrastructure.Notifications;

public sealed class NotificationService(KocDbContext db, IOutboxWriter outbox) : INotificationService
{
    public async Task NotifyAsync(string userId, string type, string title, string message, string? linkUrl, CancellationToken ct = default)
    {
        db.Set<Notification>().Add(Build(userId, type, title, message, linkUrl));
        await outbox.EnqueueAsync(new NotificationCreatedEvent(userId), ct);
        await db.SaveChangesAsync(ct);
    }

    public async Task NotifyManyAsync(IEnumerable<string> userIds, string type, string title, string message, string? linkUrl, CancellationToken ct = default)
    {
        var distinct = userIds.Where(u => !string.IsNullOrWhiteSpace(u)).Distinct(StringComparer.Ordinal).ToList();
        if (distinct.Count == 0)
        {
            return;
        }

        db.Set<Notification>().AddRange(distinct.Select(u => Build(u, type, title, message, linkUrl)));
        foreach (var u in distinct)
        {
            await outbox.EnqueueAsync(new NotificationCreatedEvent(u), ct);
        }
        await db.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<Notification>> GetAsync(string userId, bool unreadOnly, int take, CancellationToken ct = default)
    {
        var query = db.Set<Notification>().AsNoTracking().Where(n => n.UserId == userId);
        if (unreadOnly)
        {
            query = query.Where(n => n.ReadUtc == null);
        }

        return await query.OrderByDescending(n => n.CreatedUtc).Take(Math.Clamp(take, 1, 200)).ToListAsync(ct);
    }

    public Task<int> UnreadCountAsync(string userId, CancellationToken ct = default) =>
        db.Set<Notification>().CountAsync(n => n.UserId == userId && n.ReadUtc == null, ct);

    public async Task MarkReadAsync(string userId, Guid id, CancellationToken ct = default)
    {
        var n = await db.Set<Notification>().FirstOrDefaultAsync(x => x.Id == id && x.UserId == userId, ct);
        if (n is { ReadUtc: null })
        {
            n.ReadUtc = DateTime.UtcNow;
            await db.SaveChangesAsync(ct);
        }
    }

    public async Task MarkAllReadAsync(string userId, CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        var unread = await db.Set<Notification>().Where(n => n.UserId == userId && n.ReadUtc == null).ToListAsync(ct);
        foreach (var n in unread)
        {
            n.ReadUtc = now;
        }

        if (unread.Count > 0)
        {
            await db.SaveChangesAsync(ct);
        }
    }

    private static Notification Build(string userId, string type, string title, string message, string? linkUrl) => new()
    {
        UserId = userId,
        Type = type,
        Title = title,
        Message = message,
        LinkUrl = linkUrl,
        CreatedUtc = DateTime.UtcNow,
    };
}
