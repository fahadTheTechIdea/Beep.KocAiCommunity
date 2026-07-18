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

        // Datasets (org-scoped visibility).
        services.AddScoped<Application.Datasets.IDatasetService, Datasets.DatasetService>();

        // Studio (ML.NET training). The IMlTrainer implementation is registered by the API host
        // (which references the ML project); StudioService orchestrates train + record.
        services.AddScoped<Application.Studio.IStudioService, Studio.StudioService>();

        // Community discussions (org-scoped visibility).
        services.AddScoped<Application.Community.ICommunityService, Community.CommunityService>();

        // Workflow compiler + node-by-node ML pipeline executor.
        services.AddScoped<Application.Workflow.IWorkflowService, Workflow.WorkflowService>();
        services.AddScoped<Application.Workflow.IPipelineExecutor, ML.MlPipelineExecutor>();

        // Model registry (register → approve → promote).
        services.AddScoped<Application.Studio.IModelRegistry, Studio.ModelRegistryService>();

        // In-app notifications.
        services.AddScoped<Application.Notifications.INotificationService, Notifications.NotificationService>();

        // Personal + management dashboards.
        services.AddScoped<Application.Dashboard.IDashboardService, Dashboard.DashboardService>();

        // ML projects (a workflow lives in a project — personal or competition-targeted).
        services.AddScoped<Application.Studio.IProjectService, Studio.ProjectService>();

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
