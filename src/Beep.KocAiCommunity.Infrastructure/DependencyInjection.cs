using Beep.KocAiCommunity.Application.Abstractions;
using Beep.KocAiCommunity.Application.Audit;
using Beep.KocAiCommunity.Application.Authorization;
using Beep.KocAiCommunity.Application.Competitions;
using Beep.KocAiCommunity.Application.Learning;
using Beep.KocAiCommunity.Application.Organization;
using Beep.KocAiCommunity.Application.RealTime;
using Beep.KocAiCommunity.Application.Storage;
using Beep.KocAiCommunity.Infrastructure.Audit;
using Beep.KocAiCommunity.Infrastructure.Authorization;
using Beep.KocAiCommunity.Infrastructure.Messaging;
using Beep.KocAiCommunity.Infrastructure.Organization;
using Beep.KocAiCommunity.Infrastructure.Persistence;
using Beep.KocAiCommunity.Infrastructure.Storage;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Beep.KocAiCommunity.Infrastructure;

public static class DependencyInjection
{
    /// <summary>
    /// Registers the KOC DbContext (SQL Server in production, SQLite in dev/test) and the
    /// org/visibility/audit services. Provider is chosen by <c>Database:Provider</c>
    /// (<c>Sqlite</c> default, or <c>SqlServer</c>); connection string from <c>ConnectionStrings:kocdb</c>.
    /// </summary>
    public static IServiceCollection AddKocInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var provider = configuration["Database:Provider"] ?? "Sqlite";
        var connectionString = configuration.GetConnectionString("kocdb");

        services.AddDbContext<KocDbContext>(options =>
        {
            if (string.Equals(provider, "SqlServer", StringComparison.OrdinalIgnoreCase))
            {
                options.UseSqlServer(
                    connectionString ?? throw new InvalidOperationException("ConnectionStrings:kocdb is required for the SqlServer provider."),
                    sql => sql.MigrationsAssembly("Beep.KocAiCommunity.Infrastructure.SqlServerMigrations"));
            }
            else
            {
                options.UseSqlite(connectionString ?? "Data Source=koc-dev.db");
            }
        });

        services.AddScoped<IOrgDirectory, OrgDirectory>();
        services.AddScoped<IOrgScopeResolver, OrgScopeResolver>();
        services.AddScoped<IVisibilityEvaluator, VisibilityEvaluator>();
        services.AddScoped<IAuditEnvelope, AuditEnvelopeService>();

        // Artifact storage: local filesystem provider + governed upload service.
        // (Azure Blob provider is added for production; local covers dev/test.)
        var artifactOptions = BuildArtifactOptions(configuration);
        services.AddSingleton(Options.Create(artifactOptions));
        services.AddScoped<IArtifactStore>(_ => new LocalFileArtifactStore(artifactOptions.RootPath));
        services.AddScoped<IArtifactService, ArtifactService>();

        // Transactional outbox writer (dispatcher lives in the API host).
        services.AddScoped<IOutboxWriter, OutboxWriter>();

        // Learning tracks.
        services.AddScoped<ILearningService, Learning.LearningService>();

        // Competitions + trusted scorers.
        services.AddSingleton<IScoringPlugin, Competitions.AccuracyScorer>();
        services.AddSingleton<IScoringPlugin, Competitions.RmseScorer>();
        services.AddSingleton<IScorerRegistry, Competitions.ScorerRegistry>();
        services.AddScoped<ICompetitionService, Competitions.CompetitionService>();

        // Supervisor rollups.
        services.AddScoped<Application.Supervision.ISupervisionService, Supervision.SupervisionService>();

        // Datasets (org-scoped visibility) + versioned contents (files, schema, profiling, imports).
        services.AddScoped<Application.Datasets.IDatasetService, Datasets.DatasetService>();
        services.AddScoped<Application.Datasets.IDatasetContentService, Datasets.DatasetContentService>();

        // Studio (ML.NET training). The IMlTrainer implementation is registered by the API host
        // (which references the ML project); StudioService orchestrates train + record.
        services.AddScoped<Application.Studio.IStudioService, Studio.StudioService>();

        // Community discussions (org-scoped visibility).
        services.AddScoped<Application.Community.ICommunityService, Community.CommunityService>();

        // Workflow compiler + the plug-and-play node executor. Every IPipelineNodeHandler in the ML
        // assembly is auto-registered, so adding a node kind is one handler class — the catalog, the
        // compiler's known kinds, and the executor's dispatch all pick it up.
        services.AddScoped<Application.Workflow.IWorkflowService, Workflow.WorkflowService>();
        foreach (var handler in typeof(ML.Nodes.PluginNodeExecutor).Assembly.GetTypes()
                     .Where(t => t is { IsAbstract: false, IsInterface: false } && typeof(ML.Nodes.IPipelineNodeHandler).IsAssignableFrom(t)))
        {
            services.AddSingleton(typeof(ML.Nodes.IPipelineNodeHandler), handler);
        }

        services.AddSingleton<ML.Nodes.PluginNodeRegistry>();
        services.AddSingleton<Application.ML.INodeRegistry>(sp => sp.GetRequiredService<ML.Nodes.PluginNodeRegistry>());
        services.AddScoped<Application.Workflow.IPipelineExecutor, ML.Nodes.PluginNodeExecutor>();

        // In-app help (code-first article catalog).
        services.AddSingleton<Application.Help.IHelpService, Application.Help.HelpService>();

        // KOC enterprise connectors: factory (mock adapters until live endpoints ship) + instance service.
        services.AddSingleton<Application.Connectors.IKocConnectorFactory, Connectors.MockConnectorFactory>();
        services.AddScoped<Application.Connectors.IConnectorService, Connectors.ConnectorService>();

        // Workflow versioning (immutable versions, draft→publish, import/export) + templates.
        services.AddScoped<Application.Workflow.IWorkflowVersionService, Workflow.WorkflowVersionService>();
        services.AddScoped<Application.Workflow.IWorkflowTemplateService, Workflow.WorkflowTemplateService>();

        // Model registry (register → approve → promote).
        services.AddScoped<Application.Studio.IModelRegistry, Studio.ModelRegistryService>();

        // Inference serving (dynamic-schema scoring via the prediction pool + per-call audit logs).
        // The IPredictionPool implementation lives in the ML project and is registered by the hosts.
        services.AddScoped<Application.Studio.IInferenceService, Studio.InferenceService>();

        // In-app notifications.
        services.AddScoped<Application.Notifications.INotificationService, Notifications.NotificationService>();

        // Personal + management dashboards.
        services.AddScoped<Application.Dashboard.IDashboardService, Dashboard.DashboardService>();

        // Engagement: Barrels XP, career ladder, badges, streaks, kudos, team leaderboards.
        services.AddScoped<Application.Engagement.IEngagementService, Engagement.EngagementService>();

        // Durable job queue (enqueued by the API, executed by the Worker).
        services.AddScoped<Application.Jobs.IJobQueue, Jobs.EfJobQueue>();

        // Experiment tracking + the default metric sink (MLflow REST adapter can be added alongside).
        services.AddScoped<Application.Experiments.IExperimentSink, Experiments.EfExperimentSink>();
        services.AddScoped<Application.Experiments.IExperimentService, Experiments.ExperimentService>();

        // Platform admin: typed settings (secrets encrypted via ISecretProtector), feature flags, dashboard.
        // The ISecretProtector implementation is registered by the host (AddKocSecretProtection).
        services.AddScoped<Application.Admin.ISettingsService, Admin.SettingsService>();
        services.AddScoped<Application.Admin.IFeatureFlagService, Admin.FeatureFlagService>();
        services.AddScoped<Application.Admin.IAdminDashboardService, Admin.AdminDashboardService>();
        services.AddScoped<Application.Admin.IDemoDataService, Admin.DemoDataService>();
        services.AddScoped<Application.Admin.IAccessAdminService, Admin.AccessAdminService>();

        return services;
    }

    private static ArtifactUploadOptions BuildArtifactOptions(IConfiguration configuration)
    {
        var section = configuration.GetSection(ArtifactUploadOptions.SectionName);
        var options = new ArtifactUploadOptions();

        if (!string.IsNullOrWhiteSpace(section["RootPath"]))
        {
            options.RootPath = section["RootPath"]!;
        }

        if (long.TryParse(section["MaxSizeBytes"], out var maxSize))
        {
            options.MaxSizeBytes = maxSize;
        }

        foreach (var ext in section.GetSection("AllowedExtensions").GetChildren())
        {
            if (!string.IsNullOrWhiteSpace(ext.Value))
            {
                options.AllowedExtensions.Add(ext.Value);
            }
        }

        return options;
    }
}
