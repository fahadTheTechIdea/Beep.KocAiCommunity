using Microsoft.AspNetCore.SignalR;

namespace Beep.KocAiCommunity.Api.RealTime;

/// <summary>
/// Real-time updates for leaderboards and notifications. Events are relayed here by the outbox
/// dispatcher rather than pushed directly from request handlers, so delivery is transactionally
/// consistent. Clients join per-resource groups to receive only what concerns them.
/// </summary>
public sealed class LeaderboardHub : Hub
{
    public static string UserGroup(string userId) => $"user:{userId}";
    public static string CompetitionGroup(string competitionId) => $"competition:{competitionId}";

    public Task JoinUser(string userId) => Groups.AddToGroupAsync(Context.ConnectionId, UserGroup(userId));

    public Task JoinCompetition(string competitionId) => Groups.AddToGroupAsync(Context.ConnectionId, CompetitionGroup(competitionId));

    public Task LeaveCompetition(string competitionId) => Groups.RemoveFromGroupAsync(Context.ConnectionId, CompetitionGroup(competitionId));
}
