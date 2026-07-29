using Beep.KocAiCommunity.Application.Engagement;
using Beep.KocAiCommunity.Application.Security;
using Beep.KocAiCommunity.Contracts.Engagement;
using Beep.KocAiCommunity.Infrastructure.Engagement;

namespace Beep.KocAiCommunity.Api.Endpoints;

public static class EngagementEndpoints
{
    public static RouteGroupBuilder MapEngagementEndpoints(this RouteGroupBuilder group)
    {
        // ---- Profiles ----
        group.MapGet("/profiles/me", async (IKocCurrentUser me, IEngagementService svc, CancellationToken ct) =>
            Results.Ok(await svc.GetProfileAsync(me.UserId!, me.DisplayName, ct)))
        .WithName("GetMyProfile")
        .RequireAuthorization(KocPolicies.RequireEmployee);

        group.MapGet("/profiles/{userId}", async (string userId, IEngagementService svc, CancellationToken ct) =>
            Results.Ok(await svc.GetProfileAsync(userId, null, ct)))
        .WithName("GetProfile")
        .RequireAuthorization(KocPolicies.RequireEmployee);

        group.MapPut("/profiles/me", async (UpdateProfileRequest req, IKocCurrentUser me, IEngagementService svc, CancellationToken ct) =>
            Results.Ok(await svc.UpdateProfileAsync(me.UserId!, req, ct)))
        .WithName("UpdateMyProfile")
        .RequireAuthorization(KocPolicies.RequireEmployee);

        group.MapPut("/profiles/me/language", async (SetLanguageRequest req, IKocCurrentUser me, IEngagementService svc, CancellationToken ct) =>
        {
            await svc.SetLanguageAsync(me.UserId!, req.Language, ct);
            return Results.NoContent();
        })
        .WithName("SetMyLanguage")
        .RequireAuthorization(KocPolicies.RequireEmployee);

        // ---- Leaderboards ----
        group.MapGet("/engagement/leaderboard", async (string? period, IKocCurrentUser me, IEngagementService svc, CancellationToken ct) =>
            Results.Ok(await svc.GetXpLeaderboardAsync(me.UserId!, ParsePeriod(period), ct)))
        .WithName("GetXpLeaderboard")
        .RequireAuthorization(KocPolicies.RequireEmployee);

        group.MapGet("/engagement/teams", async (string? period, IKocCurrentUser me, IEngagementService svc, CancellationToken ct) =>
            Results.Ok(await svc.GetTeamLeaderboardAsync(me.UserId!, ParsePeriod(period), ct)))
        .WithName("GetTeamLeaderboard")
        .RequireAuthorization(KocPolicies.RequireEmployee);

        // ---- Badges + avatars ----
        group.MapGet("/engagement/badges/catalog", async (IEngagementService svc, CancellationToken ct) =>
            Results.Ok(await svc.GetBadgeCatalogAsync(ct)))
        .WithName("GetBadgeCatalog")
        .RequireAuthorization(KocPolicies.RequireEmployee);

        group.MapGet("/engagement/avatars", () => Results.Ok(IconLibrary.Avatars))
        .WithName("GetAvatarIcons")
        .RequireAuthorization(KocPolicies.RequireEmployee);

        // ---- Kudos ----
        group.MapPost("/engagement/kudos", async (GiveKudosRequest req, IKocCurrentUser me, IEngagementService svc, CancellationToken ct) =>
        {
            try
            {
                await svc.GiveKudosAsync(me.UserId!, req, ct);
                return Results.NoContent();
            }
            catch (EngagementException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        })
        .WithName("GiveKudos")
        .RequireAuthorization(KocPolicies.RequireEmployee);

        group.MapGet("/engagement/kudos/{userId}", async (string userId, int? take, IEngagementService svc, CancellationToken ct) =>
            Results.Ok(await svc.GetKudosForAsync(userId, take ?? 30, ct)))
        .WithName("GetKudos")
        .RequireAuthorization(KocPolicies.RequireEmployee);

        // ---- Activity feed ----
        group.MapGet("/engagement/activity", async (int? take, IKocCurrentUser me, IEngagementService svc, CancellationToken ct) =>
            Results.Ok(await svc.GetActivityFeedAsync(me.UserId!, take ?? 40, ct)))
        .WithName("GetActivityFeed")
        .RequireAuthorization(KocPolicies.RequireEmployee);

        return group;
    }

    private static LeaderboardPeriod ParsePeriod(string? period) => period?.ToLowerInvariant() switch
    {
        "month" => LeaderboardPeriod.Month,
        "all" or "alltime" => LeaderboardPeriod.AllTime,
        _ => LeaderboardPeriod.Week,
    };
}
