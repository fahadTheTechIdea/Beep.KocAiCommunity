namespace Beep.KocAiCommunity.Contracts.Notifications;

public sealed record NotificationDto(
    Guid Id,
    string Type,
    string Title,
    string Message,
    string? LinkUrl,
    DateTime CreatedUtc,
    bool IsRead);
