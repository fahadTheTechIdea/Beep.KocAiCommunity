using Beep.KocAiCommunity.Domain.Notifications;

namespace Beep.KocAiCommunity.Application.Notifications;

public interface INotificationService
{
    /// <summary>Creates a notification for one user.</summary>
    Task NotifyAsync(string userId, string type, string title, string message, string? linkUrl, CancellationToken ct = default);

    /// <summary>Creates the same notification for many users (e.g. all participants of a competition).</summary>
    Task NotifyManyAsync(IEnumerable<string> userIds, string type, string title, string message, string? linkUrl, CancellationToken ct = default);

    Task<IReadOnlyList<Notification>> GetAsync(string userId, bool unreadOnly, int take, CancellationToken ct = default);
    Task<int> UnreadCountAsync(string userId, CancellationToken ct = default);
    Task MarkReadAsync(string userId, Guid id, CancellationToken ct = default);
    Task MarkAllReadAsync(string userId, CancellationToken ct = default);
}
