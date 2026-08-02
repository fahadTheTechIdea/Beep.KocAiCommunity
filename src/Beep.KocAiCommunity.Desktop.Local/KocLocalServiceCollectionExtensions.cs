using Beep.KocAiCommunity.Application.ML;
using Beep.KocAiCommunity.Application.Workflow;
using Beep.KocAiCommunity.Client;
using Beep.KocAiCommunity.ML;
using Beep.KocAiCommunity.ML.Nodes;
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
        LocalTrainingLimits? trainingLimits = null)
    {
        var ws = workspace ?? LocalWorkspace.Default();
        ws.EnsureCreated();
        services.AddSingleton(ws);
        services.AddSingleton<LocalDatasetStore>();
        services.AddSingleton<LocalWorkflowStore>();
        services.AddSingleton<LocalRunStore>();
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

        // Remote HTTP client for competitions (+ dev identity forwarding) and the realtime hub URL.
        services.AddSingleton<DevIdentity>();
        services.AddSingleton(new DevIdentityOptions());
        services.AddTransient<DevIdentityHandler>();
        services.AddHttpClient<KocApiClient>(client => client.BaseAddress = new Uri(apiBaseUrl))
            .AddHttpMessageHandler<DevIdentityHandler>();
        services.AddSingleton(new RealtimeOptions($"{apiBaseUrl.TrimEnd('/')}/hubs/leaderboard"));

        // The Studio-local façade with the HTTP client as its online fallback.
        services.AddScoped<IKocApiClient>(sp => new LocalKocApiClient(
            sp.GetRequiredService<KocApiClient>(),
            sp.GetRequiredService<INodeRegistry>(),
            sp.GetRequiredService<IPipelineExecutor>(),
            sp.GetRequiredService<LocalDatasetStore>(),
            sp.GetRequiredService<LocalWorkflowStore>()));

        return services;
    }
}
