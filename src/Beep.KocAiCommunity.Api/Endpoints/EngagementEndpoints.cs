using Beep.KocAiCommunity.Domain.Localization;
using Beep.KocAiCommunity.Application.Localization;
using Beep.KocAiCommunity.Api.Security;
using Beep.KocAiCommunity.Application.Engagement;
using Beep.KocAiCommunity.Application.Security;
using Beep.KocAiCommunity.Contracts.Engagement;
using Beep.KocAiCommunity.Infrastructure.Engagement;

namespace Beep.KocAiCommunity.Api.Endpoints;

public static class EngagementEndpoints
{
    public static RouteGroupBuilder MapEngagementEndpoints(this RouteGroupBuilder group)
    {
        // One localizer for the whole group: it reads the request culture on every call,
        // so a singleton captured here still answers each request in its own language.
        var messages = group.ServiceMessages();

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
        // Readable without an account, like the discussions. Earning a place on one still needs a
        // person; "IsMe" is simply false for a reader who isn't anybody yet.
        group.MapGet("/engagement/leaderboard", async (string? period, IKocCurrentUser me, IEngagementService svc, CancellationToken ct) =>
            Results.Ok(await svc.GetXpLeaderboardAsync(me.UserId ?? string.Empty, ParsePeriod(period), ct)))
        .WithName("GetXpLeaderboard")
        .AllowAnonymous();

        group.MapGet("/engagement/teams", async (string? period, IKocCurrentUser me, IEngagementService svc, CancellationToken ct) =>
            Results.Ok(await svc.GetTeamLeaderboardAsync(me.UserId ?? string.Empty, ParsePeriod(period), ct)))
        .WithName("GetTeamLeaderboard")
        .AllowAnonymous();

        // ---- Badges + avatars ----
        group.MapGet("/engagement/badges/catalog", async (
            HttpContext http, IEngagementService svc, IContentTranslator translator, CancellationToken ct) =>
        {
            var badges = await svc.GetBadgeCatalogAsync(ct);
            var language = http.RequestLanguage();
            var names = await translator.LookupAsync(TranslatedContent.Badge, TranslatedContent.Name, language, ct);
            var descriptions = await translator.LookupAsync(TranslatedContent.Badge, TranslatedContent.Description, language, ct);

            return Results.Ok(badges
                .Select(b => b with
                {
                    Name = names.GetValueOrDefault(b.Code, b.Name),
                    Description = descriptions.GetValueOrDefault(b.Code, b.Description),
                })
                .ToList());
        })
        .WithName("GetBadgeCatalog")
        // The catalogue is what the platform offers, not what anyone has earned — nothing personal in it.
        .AllowAnonymous();

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
                return Results.BadRequest(new { error = messages.For(ex) });
            }
        })
        .WithName("GiveKudos")
        .RequireAuthorization(KocPolicies.RequireEmployee);

        group.MapGet("/engagement/kudos/{userId}", async (string userId, int? take, IEngagementService svc, CancellationToken ct) =>
            Results.Ok(await svc.GetKudosForAsync(userId, take ?? 30, ct)))
        .WithName("GetKudos")
        .RequireAuthorization(KocPolicies.RequireEmployee);

        // ---- Activity feed ----
        // Open too. The feed is already filtered by each event's visibility scope, and a reader with no
        // account resolves to no org membership — so they see company-wide activity and nothing narrower.
        group.MapGet("/engagement/activity", async (int? take, IKocCurrentUser me, IEngagementService svc, CancellationToken ct) =>
            Results.Ok(await svc.GetActivityFeedAsync(me.UserId ?? string.Empty, take ?? 40, ct)))
        .WithName("GetActivityFeed")
        .AllowAnonymous();

        return group;
    }

    private static LeaderboardPeriod ParsePeriod(string? period) => period?.ToLowerInvariant() switch
    {
        "month" => LeaderboardPeriod.Month,
        "all" or "alltime" => LeaderboardPeriod.AllTime,
        _ => LeaderboardPeriod.Week,
    };
}
