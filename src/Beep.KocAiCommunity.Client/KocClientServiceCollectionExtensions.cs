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
    public static IServiceCollection AddKocHttpClient(this IServiceCollection services, string apiBaseUrl)
    {
        services.AddSingleton<DevIdentity>();
        services.AddTransient<DevIdentityHandler>();
        services.AddHttpClient<IKocApiClient, KocApiClient>(client => client.BaseAddress = new Uri(apiBaseUrl))
            .AddHttpMessageHandler<DevIdentityHandler>();
        services.AddSingleton(new RealtimeOptions($"{apiBaseUrl.TrimEnd('/')}/hubs/leaderboard"));
        return services;
    }
}
