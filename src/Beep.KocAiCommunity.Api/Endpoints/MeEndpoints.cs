using Beep.KocAiCommunity.Application.Competitions;
using Beep.KocAiCommunity.Application.Organization;
using Beep.KocAiCommunity.Application.Security;
using Beep.KocAiCommunity.Contracts.Identity;
using Beep.KocAiCommunity.Contracts.Organization;

namespace Beep.KocAiCommunity.Api.Endpoints;

public static class MeEndpoints
{
    public static RouteGroupBuilder MapMeEndpoints(this RouteGroupBuilder group)
    {
        group.MapGet("/me", async (IKocCurrentUser me, IOrgDirectory directory, ICompetitionService competitions, CancellationToken ct) =>
        {
            if (!me.IsAuthenticated || me.UserId is null)
            {
                return Results.Unauthorized();
            }

            var profile = await directory.GetProfileAsync(me.UserId, ct);
            var maxScope = await competitions.GetMaxCreateScopeAsync(me.UserId, me.IsInRole(KocRoles.PlatformAdmin), ct);
            return Results.Ok(new MeResponse(
                me.UserId,
                me.DisplayName,
                [.. me.Roles],
                me.PositionLevel.ToString(),
                profile?.HomeOrgUnitId,
                profile?.LedOrgUnitId,
                maxScope?.ToString()));
        })
        .WithName("GetMe")
        .RequireAuthorization(KocPolicies.RequireEmployee);

        group.MapGet("/me/scope", async (IKocCurrentUser me, IOrgScopeResolver resolver, CancellationToken ct) =>
        {
            var scope = await resolver.ResolveForUserAsync(me.UserId!, ct);
            return Results.Ok(new OrgScopeDto([.. scope.OrgUnitIds], scope.MemberUserIds.Count));
        })
        .WithName("GetMyScope")
        .RequireAuthorization(KocPolicies.RequireEmployee);

        return group;
    }
}
