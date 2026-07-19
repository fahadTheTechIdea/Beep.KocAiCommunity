using System.Threading.RateLimiting;
using Beep.KocAiCommunity.Api.Endpoints;
using Beep.KocAiCommunity.Api.RealTime;
using Beep.KocAiCommunity.Infrastructure;
using Beep.KocAiCommunity.Infrastructure.Learning;
using Beep.KocAiCommunity.Infrastructure.Persistence;
using Beep.KocAiCommunity.ServiceDefaults;
using Beep.KocAiCommunity.ServiceDefaults.Security;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();
builder.Services.AddOpenApi();
builder.Services.AddProblemDetails();
builder.Services.AddSignalR();

// Persistence + KOC security.
builder.Services.AddKocInfrastructure(builder.Configuration);
builder.Services.AddScoped<Beep.KocAiCommunity.Application.ML.IMlTrainer, Beep.KocAiCommunity.ML.AutoMlTrainer>();

// Prediction pool: a singleton, hot-reloadable cache of loaded models for inference serving.
builder.Services.AddSingleton<Beep.KocAiCommunity.Application.ML.IPredictionPool, Beep.KocAiCommunity.ML.AutoMlPredictionPool>();

// Data Protection + the ISecretProtector used to encrypt secret platform settings.
builder.Services.AddKocSecretProtection();
builder.Services.AddKocCurrentUser();
builder.Services.AddKocAuthorization();
builder.Services.AddKocApiAuthentication(builder.Configuration);

// Global fixed-window rate limit (generous default; per-endpoint policies layer on later).
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
        RateLimitPartition.GetFixedWindowLimiter(
            context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions { PermitLimit = 1000, Window = TimeSpan.FromMinutes(1) }));
});

// The outbox dispatcher is disabled in tests to avoid contending on the shared in-memory database.
if (builder.Configuration.GetValue("Outbox:DispatcherEnabled", true))
{
    builder.Services.AddHostedService<OutboxDispatcher>();
}

var app = builder.Build();

app.UseExceptionHandler();
app.UseStatusCodePages();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

// In dev the API is served over plain HTTP; HTTPS redirect only applies outside Development.
if (!app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}

app.UseRateLimiter();

app.UseAuthentication();
app.UseAuthorization();

// /health and /alive.
app.MapDefaultEndpoints();

// Versioned application surface.
var v1 = app.MapGroup("/api/v1");
v1.MapGet("/ping", () => Results.Ok(new { message = "pong" }));
v1.MapMeEndpoints();
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
v1.MapProjectEndpoints();
v1.MapEngagementEndpoints();
v1.MapRunEndpoints();
v1.MapExperimentEndpoints();
v1.MapAdminEndpoints();
v1.MapMlNodeEndpoints();
v1.MapHelpEndpoints();

app.MapHub<LeaderboardHub>("/hubs/leaderboard");

// Dev convenience: migrate the database and seed the starter learning tracks on startup.
if (app.Configuration.GetValue("Seed:Enabled", false))
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<KocDbContext>();
    await db.Database.MigrateAsync();
    await LearningSeeder.SeedTracksAsync(db);
    await Beep.KocAiCommunity.Infrastructure.Engagement.EngagementSeeder.SeedBadgesAsync(db);
    await Beep.KocAiCommunity.Infrastructure.Organization.DevOrgSeeder.SeedDevOrgAsync(db);
    await Beep.KocAiCommunity.Infrastructure.Workflow.WorkflowTemplateSeeder.SeedAsync(db);
    var artifacts = scope.ServiceProvider.GetRequiredService<Beep.KocAiCommunity.Application.Storage.IArtifactService>();
    await Beep.KocAiCommunity.Infrastructure.Competitions.CompetitionSeeder.SeedDemoAsync(db, artifacts);
}
// Production: apply migrations on startup without seeding (provider-specific migrations are selected automatically).
else if (app.Configuration.GetValue("Database:MigrateOnStartup", false))
{
    using var scope = app.Services.CreateScope();
    await scope.ServiceProvider.GetRequiredService<KocDbContext>().Database.MigrateAsync();
}

app.Run();

/// <summary>Exposed so integration/end-to-end tests can use WebApplicationFactory.</summary>
public partial class Program;
