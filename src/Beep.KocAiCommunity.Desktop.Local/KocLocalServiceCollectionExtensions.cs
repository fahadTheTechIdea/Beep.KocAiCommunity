using Beep.KocAiCommunity.Application.ML;
using Beep.KocAiCommunity.Application.Workflow;
using Beep.KocAiCommunity.Client;
using Beep.KocAiCommunity.ML;
using Beep.KocAiCommunity.ML.Nodes;
using Beep.KocAiCommunity.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Beep.KocAiCommunity.Desktop.Local;

/// <summary>DI wiring for the offline desktop Studio: local engine + stores + a remote fallback for competitions.</summary>
public static class KocLocalServiceCollectionExtensions
{
    /// <summary>
    /// Registers the in-process pipeline engine, the local dataset/workflow stores, and an
    /// <see cref="IKocApiClient"/> that runs the Studio surface locally while forwarding competition
    /// calls to the API at <paramref name="apiBaseUrl"/> (used only when online).
    /// </summary>
    public static IServiceCollection AddKocLocalStudio(
        this IServiceCollection services, string apiBaseUrl, LocalWorkspace? workspace = null,
        LocalTrainingLimits? trainingLimits = null,
        string? platformDatabase = null, string databaseProvider = "SqlServer")
    {
        var ws = workspace ?? LocalWorkspace.Default();
        ws.EnsureCreated();
        services.AddSingleton(ws);
        services.AddSingleton<LocalDatasetStore>();
        services.AddSingleton<LocalWorkflowStore>();
        services.AddSingleton<LocalRunStore>();

        // Offline-first competitions: what was last seen, what is waiting to be sent, and whether the
        // network is there. Singletons — one queue and one answer per machine.
        services.AddSingleton<LocalCompetitionCache>();
        services.AddSingleton<SubmissionOutbox>();
        services.AddSingleton<ConnectionState>();
        services.AddSingleton<LocalModelStore>();

        // The pool caches loaded models; loading one per prediction would be slow and pointless.
        services.AddSingleton<IPredictionPool, AutoMlPredictionPool>();
        services.AddSingleton<LocalPredictionService>();

        // AutoML, on this machine. The deployment decision of 2026-08-02 left the shared hosting unable
        // to run the Worker, so the desktop is now the only place a model gets trained — which is why
        // this is registered here at all, and why it comes with limits attached.
        services.AddSingleton((trainingLimits ?? new LocalTrainingLimits()).Clamped());
        services.AddSingleton<IMlTrainer, AutoMlTrainer>();

        // Singleton, so a run survives navigating away from the AutoML page. Results used to be logged
        // in the designer and lost the moment you left it.
        services.AddSingleton<LocalTrainingService>();

        // In-process node engine — every IPipelineNodeHandler in the ML assembly (mirrors the API host).
        foreach (var handler in typeof(PluginNodeExecutor).Assembly.GetTypes()
                     .Where(t => t is { IsAbstract: false, IsInterface: false } && typeof(IPipelineNodeHandler).IsAssignableFrom(t)))
        {
            services.AddSingleton(typeof(IPipelineNodeHandler), handler);
        }

        services.AddSingleton<PluginNodeRegistry>();
        services.AddSingleton<INodeRegistry>(sp => sp.GetRequiredService<PluginNodeRegistry>());
        services.AddSingleton<IPipelineExecutor, PluginNodeExecutor>();

        services.AddSingleton<DevIdentity>();
        services.AddSingleton(new DevIdentityOptions());
        services.AddSingleton(new RealtimeOptions($"{apiBaseUrl.TrimEnd('/')}/hubs/leaderboard"));

        // The platform, read straight from its database. There is no API website in between any more —
        // this machine opens the same database the site does, using the same application services.
        if (platformDatabase is { Length: > 0 })
        {
            services.AddKocInfrastructure(new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["ConnectionStrings:kocdb"] = platformDatabase,
                    ["Database:Provider"] = databaseProvider,
                })
                .Build());

            services.AddScoped<DesktopPlatformClient>();
        }

        // The Studio-local façade. Its fallback is the direct database client when this machine is
        // configured for the platform, and nothing at all when it is not — in which case anything not
        // handled locally says so by name rather than failing obscurely.
        services.AddScoped<IKocApiClient>(sp => new LocalKocApiClient(
            platformDatabase is { Length: > 0 } ? sp.GetRequiredService<DesktopPlatformClient>() : null,
            sp.GetRequiredService<INodeRegistry>(),
            sp.GetRequiredService<IPipelineExecutor>(),
            sp.GetRequiredService<LocalDatasetStore>(),
            sp.GetRequiredService<LocalWorkflowStore>(),
            sp.GetRequiredService<LocalCompetitionCache>(),
            sp.GetRequiredService<SubmissionOutbox>(),
            sp.GetRequiredService<ConnectionState>()));

        // Deliberately NOT the local façade: its SubmitAsync is the one that enqueues, so draining
        // through it would have re-queued every entry instead of sending it.
        services.AddScoped<ISubmissionSender>(sp => new ApiSubmissionSender(
            platformDatabase is { Length: > 0 }
                ? sp.GetRequiredService<DesktopPlatformClient>()
                : sp.GetRequiredService<IKocApiClient>()));
        services.AddScoped<SyncService>();

        return services;
    }
}
