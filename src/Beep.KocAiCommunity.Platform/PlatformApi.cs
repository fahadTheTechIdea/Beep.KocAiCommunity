using System.Threading.RateLimiting;
using Beep.KocAiCommunity.Platform.Endpoints;
using Beep.KocAiCommunity.Platform.RealTime;
using Beep.KocAiCommunity.Platform.Security;
using Beep.KocAiCommunity.Infrastructure;
using Beep.KocAiCommunity.Infrastructure.Identity;
using Beep.KocAiCommunity.Infrastructure.Learning;
using Beep.KocAiCommunity.Infrastructure.Persistence;
using Beep.KocAiCommunity.ServiceDefaults;
using Beep.KocAiCommunity.ServiceDefaults.Security;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Beep.KocAiCommunity.Platform;

/// <summary>
/// The platform's data surface: every service, endpoint and hub that used to be a separate API website.
/// <para>
/// It stopped being its own deployment on 2026-08-02. Both hosts now carry it in-process — the website
/// serves it to browsers and to itself, and KOC Studio on the desktop runs its own copy against the
/// database so an engineer's machine reaches the platform without a second site in between.
/// </para>
/// <para>
/// Keeping it as endpoints rather than a hand-written service façade is deliberate. The authorization
/// lives on the endpoints — around 120 policy checks — and a façade would have had to reproduce every
/// one of them faithfully. Endpoints in a library are the same code both hosts already trusted.
/// </para>
/// </summary>
public static class PlatformApi
{
    /// <summary>
    /// Everything the surface needs: persistence, ML, secret protection, localization, and the platform's
    /// own token validation.
    /// </summary>
    /// <param name="registerAuthentication">
    /// False where the host already owns the authentication stack and is adding the platform's token
    /// scheme itself — the website signs people in with a cookie and must not have its default scheme
    /// replaced.
    /// </param>
    public static IServiceCollection AddKocPlatform(
        this IServiceCollection services, IConfiguration configuration, bool registerAuthentication = true)
    {
        services.AddProblemDetails();
        services.AddSignalR();

        services.AddKocInfrastructure(configuration);
        services.AddScoped<Application.ML.IMlTrainer, ML.AutoMlTrainer>();

        // A singleton, hot-reloadable cache of loaded models for inference serving.
        services.AddSingleton<Application.ML.IPredictionPool, ML.AutoMlPredictionPool>();

        services.AddKocSecretProtection();
        services.AddKocSetup();

        // The caller says which language it wants its content in, via Accept-Language.
        services.AddKocApiLocalization();
        services.AddKocCurrentUser();
        services.AddKocAuthorization();

        if (registerAuthentication)
        {
            services.AddKocApiAuthentication(configuration);
        }

        // Nothing here varies by deployment. The surface validates this platform's own access token,
        // reads roles from this platform's database, and can always hold an account for someone the
        // corporate directory doesn't cover.
        services.AddKocUserDirectory();
        services.AddKocLocalAccounts();
        services.AddScoped<AccessTokenIssuer>();
        services.AddScoped<Microsoft.AspNetCore.Authentication.IClaimsTransformation, AppDatabaseRoleClaims>();

        return services;
    }

    /// <summary>
    /// A generous global limit, plus a tight one on sign-in — the only anonymous write path, so it gets
    /// a budget that fits someone mistyping a password and not someone guessing one.
    /// </summary>
    public static IServiceCollection AddKocPlatformRateLimiting(this IServiceCollection services) =>
        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
                RateLimitPartition.GetFixedWindowLimiter(
                    context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                    _ => new FixedWindowRateLimiterOptions { PermitLimit = 1000, Window = TimeSpan.FromMinutes(1) }));

            options.AddPolicy(AuthEndpoints.RateLimitPolicy, context =>
                RateLimitPartition.GetFixedWindowLimiter(
                    context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                    _ => new FixedWindowRateLimiterOptions { PermitLimit = 20, Window = TimeSpan.FromMinutes(1) }));
        });

    /// <summary>The outbox dispatcher. Disabled in tests, which share one in-memory database.</summary>
    public static IServiceCollection AddKocOutboxDispatcher(this IServiceCollection services, IConfiguration configuration)
    {
        if (configuration.GetValue("Outbox:DispatcherEnabled", true))
        {
            services.AddHostedService<OutboxDispatcher>();

            // The reveal-time scheduler rides the same switch: it is background machinery of the same
            // kind, and the tests that turn the dispatcher off want a quiet host for the same reason.
            services.AddHostedService<Competitions.RevealScheduler>();
        }

        return services;
    }

    /// <summary>
    /// Maps <c>/api/v1</c> and the leaderboard hub onto a host.
    /// <para>
    /// <paramref name="internalHosts"/> confines the API to a listener the outside world cannot reach —
    /// pass the host:port pairs of a loopback-only endpoint and the route simply does not exist on the
    /// public one. Not merely refused there: unmatched, so there is nothing to probe. Null leaves it
    /// unrestricted, which is what an in-process test host wants.
    /// </para>
    /// <para>
    /// The hub is never restricted. KOC Studio subscribes to live standings from a workstation, so it
    /// is the one part of this surface with a genuine remote caller.
    /// </para>
    /// </summary>
    public static IEndpointRouteBuilder MapKocPlatform(this IEndpointRouteBuilder app, string[]? internalHosts = null)
    {
        var v1 = app.MapGroup(RoutePrefix);

        if (internalHosts is { Length: > 0 })
        {
            v1.RequireHost(internalHosts);
        }

        v1.MapGet("/ping", () => Results.Ok(new { message = "pong" }));
        v1.MapMetaEndpoints();
        v1.MapMeEndpoints();
        v1.MapAuthEndpoints();
        v1.MapOrgEndpoints();
        v1.MapLearningEndpoints();
        v1.MapCompetitionEndpoints();
        v1.MapDatasetEndpoints();
        v1.MapStudioEndpoints();
        v1.MapModelEndpoints();
        v1.MapWorkflowRegistryEndpoints();
        v1.MapDiscussionEndpoints();
        v1.MapNotificationEndpoints();
        v1.MapDashboardEndpoints();
        v1.MapEngagementEndpoints();
        v1.MapRunEndpoints();
        v1.MapExperimentEndpoints();
        v1.MapAdminEndpoints();
        v1.MapMlNodeEndpoints();
        v1.MapHelpEndpoints();
        v1.MapConnectorEndpoints();

        app.MapHub<LeaderboardHub>(HubPath);
        return app;
    }

    /// <summary>Where the surface lives. Hosts exclude these paths from browser-facing middleware.</summary>
    public const string RoutePrefix = "/api/v1";

    public const string HubPath = "/hubs/leaderboard";

    /// <summary>
    /// Brings the database up to date and puts the platform's own content in it — learning, badges,
    /// categories, workflow templates, and the competitions with their data. <c>Seed:Enabled</c> adds
    /// the dev personas' org on top, and people and their participation come from an administrator
    /// (<see cref="Application.Admin.IDemoDataService"/>) or from whoever registers.
    /// <para>
    /// Both hosts may call this; migrating twice is a no-op, and on the desktop it is what lets a
    /// workstation open a database nobody has migrated for it.
    /// </para>
    /// </summary>
    public static async Task UseKocPlatformDatabaseAsync(this IServiceProvider services, IConfiguration configuration)
    {
        var seed = configuration.GetValue("Seed:Enabled", false);
        if (!seed && !configuration.GetValue("Database:MigrateOnStartup", false))
        {
            return;
        }

        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<KocDbContext>();
        await db.Database.MigrateAsync();

        // Dev SQLite is shared by every process. WAL journaling stops writers from blocking readers —
        // without it, bulk work like the admin demo seed stalls on file locks.
        if (db.Database.IsSqlite())
        {
            await db.Database.ExecuteSqlRawAsync("PRAGMA journal_mode=WAL;");
        }

        // The platform's own content, in every environment. Tracks to learn, badges to earn, the
        // operational domains a challenge belongs to, the starter workflow templates, the competitions
        // with their training, evaluation and answer-key data, and the Arabic for all of it. This ships
        // with the product the way the pages do — a site without it is not an empty platform waiting to
        // be filled in, it is a broken one.
        //
        // Each seeder matches its items by key: missing ones are added, existing rows are never
        // overwritten. So this is also how a later release's additions reach a database that already
        // exists, and anything an administrator renamed, disabled or retranslated stays as they left it.
        await LearningSeeder.SeedTracksAsync(db);
        await Infrastructure.Engagement.EngagementSeeder.SeedBadgesAsync(db);
        await Infrastructure.Workflow.WorkflowTemplateSeeder.SeedAsync(db);
        await Infrastructure.Competitions.CompetitionCategorySeeder.SeedAsync(db);

        var artifacts = scope.ServiceProvider.GetRequiredService<Application.Storage.IArtifactService>();
        await Infrastructure.Competitions.CompetitionSeeder.SeedCompetitionsAsync(db, artifacts);

        // After the rows it translates exist, so a fresh install has both from the first request.
        await Infrastructure.Localization.ContentTranslationSeeder.SeedAsync(db);

        if (!seed)
        {
            return;
        }

        // Development only: the personas dev auth signs people in as, and the org they sit in. People
        // are not platform content — a real deployment gets them from its administrator (demo data) or
        // from whoever registers.
        await Infrastructure.Organization.DevOrgSeeder.SeedDevOrgAsync(db);
    }
}
