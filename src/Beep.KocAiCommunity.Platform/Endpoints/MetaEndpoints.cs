using Beep.KocAiCommunity.Application.Admin;
using Beep.KocAiCommunity.Contracts.Platform;
using Beep.KocAiCommunity.ServiceDefaults;

namespace Beep.KocAiCommunity.Platform.Endpoints;

/// <summary>
/// Anonymous platform metadata the UI reads at startup — currently just enough to decide whether
/// to show the demonstration-environment notice. Deliberately unauthenticated and non-sensitive.
/// </summary>
public static class MetaEndpoints
{
    public static RouteGroupBuilder MapMetaEndpoints(this RouteGroupBuilder group)
    {
        group.MapGet("/meta", async (IConfiguration config, IDemoDataService demo, CancellationToken ct) =>
        {
            var demoMode = !SecurityExtensions.IsWindowsAuthEnabled(config)
                && !SecurityExtensions.IsEntraConfigured(config);
            var status = await demo.GetStatusAsync(ct);
            return Results.Ok(new PlatformMetaDto(demoMode, status.Seeded));
        })
        .AllowAnonymous()
        .WithName("PlatformMeta");

        return group;
    }
}
