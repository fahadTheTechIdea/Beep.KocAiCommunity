using Beep.KocAiCommunity.Application.Dashboard;
using Beep.KocAiCommunity.Application.Security;

namespace Beep.KocAiCommunity.Api.Endpoints;

public static class DashboardEndpoints
{
    public static RouteGroupBuilder MapDashboardEndpoints(this RouteGroupBuilder group)
    {
        group.MapGet("/dashboard/me", async (IKocCurrentUser me, IDashboardService svc, CancellationToken ct) =>
            Results.Ok(await svc.GetPersonalAsync(me.UserId!, ct)))
        .WithName("PersonalDashboard")
        .RequireAuthorization(KocPolicies.RequireEmployee);

        return group;
    }
}
