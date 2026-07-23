using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace Beep.KocAiCommunity.ServiceDefaults;

/// <summary>
/// A fail-fast startup guard: when the app is running in the <b>Production</b> environment, it refuses
/// to start on a configuration that only makes sense for dev/demo. Better to stop loudly at boot than to
/// silently run production on a local SQLite file, with demo data, or with authentication disabled.
/// <para>No-op outside Production, so Development and the test host are never affected.</para>
/// </summary>
public static class KocProductionPreflight
{
    /// <summary>
    /// Throws <see cref="InvalidOperationException"/> if a Production host is misconfigured. Callers pass
    /// which facets apply to them: the API checks all; the Web (which never touches the DB or seeds)
    /// checks only authentication.
    /// </summary>
    public static void Validate(
        IConfiguration configuration,
        IHostEnvironment environment,
        bool checkDatabase = true,
        bool checkSeed = true,
        bool checkAuth = true)
    {
        if (!environment.IsProduction())
        {
            return;
        }

        var errors = new List<string>();

        if (checkAuth)
        {
            if (configuration.GetValue("DevAuth:Enabled", false))
            {
                errors.Add("DevAuth:Enabled must be false in Production — it authenticates every request as a dev user.");
            }

            if (!SecurityExtensions.IsWindowsAuthEnabled(configuration) && !SecurityExtensions.IsEntraConfigured(configuration))
            {
                errors.Add("No production authentication is configured — set AzureAd:* (Microsoft Entra) or WindowsAuth:Enabled=true.");
            }
        }

        if (checkSeed && configuration.GetValue("Seed:Enabled", false))
        {
            errors.Add("Seed:Enabled must be false in Production — it seeds demonstration/dev data.");
        }

        if (checkDatabase)
        {
            var provider = configuration["Database:Provider"] ?? "Sqlite";
            if (!string.Equals(provider, "SqlServer", StringComparison.OrdinalIgnoreCase))
            {
                errors.Add($"Database:Provider is '{provider}' — Production expects 'SqlServer' with a real connection string.");
            }
        }

        if (errors.Count > 0)
        {
            throw new InvalidOperationException(
                "KOC production preflight failed — unsafe configuration for the Production environment:"
                + Environment.NewLine
                + string.Join(Environment.NewLine, errors.Select(e => "  • " + e))
                + Environment.NewLine
                + "Correct the configuration, or run in the Development environment (ship appsettings.Development.json / set ASPNETCORE_ENVIRONMENT=Development).");
        }
    }
}
