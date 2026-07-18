using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;

namespace Beep.KocAiCommunity.ServiceDefaults;

/// <summary>
/// Shared host defaults for every KOC service: health checks and the standard health
/// endpoints. OpenTelemetry, service discovery, and standard HTTP resilience handlers are
/// added here once the Aspire package set is finalized (Phase 01 follow-up).
/// </summary>
public static class Extensions
{
    public static TBuilder AddServiceDefaults<TBuilder>(this TBuilder builder)
        where TBuilder : IHostApplicationBuilder
    {
        builder.Services.AddHealthChecks()
            .AddCheck("self", () => HealthCheckResult.Healthy(), tags: ["live"]);

        return builder;
    }

    /// <summary>Maps <c>/health</c> (readiness) and <c>/alive</c> (liveness).</summary>
    public static WebApplication MapDefaultEndpoints(this WebApplication app)
    {
        app.MapHealthChecks("/health");
        app.MapHealthChecks("/alive", new HealthCheckOptions
        {
            Predicate = static registration => registration.Tags.Contains("live"),
        });

        return app;
    }
}
