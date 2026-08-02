using Beep.KocAiCommunity.Ui.Studio.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Beep.KocAiCommunity.Ui.Studio;

/// <summary>Marker used by the Web host to register this RCL's routable components.</summary>
public static class UiStudioAssembly;

/// <summary>Services the Studio's components need, registered the same way by both hosts.</summary>
public static class UiStudioServiceCollectionExtensions
{
    /// <summary>
    /// Registers the designer's session state.
    /// <para>
    /// Scoped: one per circuit on the Web, one per window on the desktop. A singleton would hand one
    /// engineer's run results to everyone else on the server.
    /// </para>
    /// </summary>
    public static IServiceCollection AddKocStudioUi(this IServiceCollection services)
    {
        services.AddScoped<RunSession>();
        return services;
    }
}
