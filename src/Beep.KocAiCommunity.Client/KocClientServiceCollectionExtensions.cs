using Microsoft.Extensions.DependencyInjection;

namespace Beep.KocAiCommunity.Client;

/// <summary>DI wiring for the KOC HTTP API client: dev identity, the forwarding handler,
/// the typed <see cref="IKocApiClient"/>, and the realtime hub URL.</summary>
public static class KocClientServiceCollectionExtensions
{
    /// <summary>
    /// Registers everything needed to call the KOC API at <paramref name="apiBaseUrl"/>:
    /// the <see cref="DevIdentity"/> persona, the <see cref="DevIdentityHandler"/> that forwards it,
    /// the typed <see cref="IKocApiClient"/>/<see cref="KocApiClient"/>, and a
    /// <see cref="RealtimeOptions"/> pointing at the leaderboard hub.
    /// </summary>
    /// <param name="perUserIdentity">
    /// <c>true</c> on a multi-user web server, where the current user is per-circuit and must never be
    /// shared; <c>false</c> (the default) for single-user hosts like the desktop shell, which resolve
    /// <see cref="DevIdentity"/> from the root provider.
    /// </param>
    /// <param name="forwardDevHeaders">
    /// <c>false</c> once a real sign-in mode is configured, so the client stops sending the
    /// <c>X-Dev-User</c>/<c>X-Dev-Roles</c> persona headers that only demo mode honours.
    /// </param>
    public static IServiceCollection AddKocHttpClient(
        this IServiceCollection services, string apiBaseUrl, bool perUserIdentity = false, bool forwardDevHeaders = true)
    {
        if (perUserIdentity)
        {
            services.AddScoped<DevIdentity>();
        }
        else
        {
            services.AddSingleton<DevIdentity>();
        }

        services.AddSingleton(new DevIdentityOptions(forwardDevHeaders));
        services.AddTransient<DevIdentityHandler>();
        services.AddHttpClient<IKocApiClient, KocApiClient>(client => client.BaseAddress = new Uri(apiBaseUrl))
            .AddHttpMessageHandler<DevIdentityHandler>();
        services.AddSingleton(new RealtimeOptions($"{apiBaseUrl.TrimEnd('/')}/hubs/leaderboard"));
        return services;
    }
}
