using System.Text.Json;
using Beep.KocAiCommunity.Application.RealTime;
using Beep.KocAiCommunity.Infrastructure.Persistence;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace Beep.KocAiCommunity.Api.RealTime;

/// <summary>
/// Drains the transactional outbox oldest-first and relays each message to SignalR, then marks it
/// processed. At-least-once delivery; ordering by CreatedUtc then Id.
/// </summary>
public sealed class OutboxDispatcher(
    IServiceScopeFactory scopeFactory,
    IHubContext<LeaderboardHub> leaderboard,
    ILogger<OutboxDispatcher> logger) : BackgroundService
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(2);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await DispatchBatchAsync(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Outbox dispatch failed");
            }

            try
            {
                await Task.Delay(PollInterval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    private async Task DispatchBatchAsync(CancellationToken ct)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<KocDbContext>();

        var batch = await db.OutboxMessages
            .Where(m => m.ProcessedUtc == null)
            .OrderBy(m => m.CreatedUtc).ThenBy(m => m.Id)
            .Take(50)
            .ToListAsync(ct);

        if (batch.Count == 0)
        {
            return;
        }

        foreach (var message in batch)
        {
            // Route each event to the group that cares about it; fall back to a broadcast.
            var target = ResolveTarget(leaderboard, message.Type, message.PayloadJson);
            await target.SendAsync(message.Type, message.PayloadJson, ct);
            message.ProcessedUtc = DateTime.UtcNow;
        }

        await db.SaveChangesAsync(ct);
    }

    private static IClientProxy ResolveTarget(IHubContext<LeaderboardHub> hub, string type, string payloadJson)
    {
        try
        {
            switch (type)
            {
                case "leaderboard.updated":
                    var lb = JsonSerializer.Deserialize<LeaderboardUpdatedEvent>(payloadJson);
                    if (lb is not null)
                    {
                        return hub.Clients.Group(LeaderboardHub.CompetitionGroup(lb.CompetitionId.ToString()));
                    }
                    break;

                case "notification.created":
                    var nt = JsonSerializer.Deserialize<NotificationCreatedEvent>(payloadJson);
                    if (nt is not null && !string.IsNullOrWhiteSpace(nt.UserId))
                    {
                        return hub.Clients.Group(LeaderboardHub.UserGroup(nt.UserId));
                    }
                    break;
            }
        }
        catch (JsonException)
        {
            // Malformed payload — fall back to broadcast below.
        }

        return hub.Clients.All;
    }
}
