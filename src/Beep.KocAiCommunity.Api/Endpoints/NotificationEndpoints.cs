using Beep.KocAiCommunity.Application.Notifications;
using Beep.KocAiCommunity.Application.Security;
using Beep.KocAiCommunity.Contracts.Notifications;
using Beep.KocAiCommunity.Domain.Notifications;

namespace Beep.KocAiCommunity.Api.Endpoints;

public static class NotificationEndpoints
{
    public static RouteGroupBuilder MapNotificationEndpoints(this RouteGroupBuilder group)
    {
        group.MapGet("/notifications", async (bool? unread, int? take, IKocCurrentUser me, INotificationService svc, CancellationToken ct) =>
        {
            var items = await svc.GetAsync(me.UserId!, unread ?? false, take ?? 30, ct);
            return Results.Ok(items.Select(ToDto).ToList());
        })
        .WithName("GetNotifications")
        .RequireAuthorization(KocPolicies.RequireEmployee);

        group.MapGet("/notifications/unread-count", async (IKocCurrentUser me, INotificationService svc, CancellationToken ct) =>
            Results.Ok(new { count = await svc.UnreadCountAsync(me.UserId!, ct) }))
        .WithName("UnreadNotificationCount")
        .RequireAuthorization(KocPolicies.RequireEmployee);

        group.MapPost("/notifications/{id:guid}/read", async (Guid id, IKocCurrentUser me, INotificationService svc, CancellationToken ct) =>
        {
            await svc.MarkReadAsync(me.UserId!, id, ct);
            return Results.NoContent();
        })
        .WithName("MarkNotificationRead")
        .RequireAuthorization(KocPolicies.RequireEmployee);

        group.MapPost("/notifications/read-all", async (IKocCurrentUser me, INotificationService svc, CancellationToken ct) =>
        {
            await svc.MarkAllReadAsync(me.UserId!, ct);
            return Results.NoContent();
        })
        .WithName("MarkAllNotificationsRead")
        .RequireAuthorization(KocPolicies.RequireEmployee);

        return group;
    }

    private static NotificationDto ToDto(Notification n) =>
        new(n.Id, n.Type, n.Title, n.Message, n.LinkUrl, n.CreatedUtc, n.ReadUtc is not null);
}
