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

            // The setup mode is the single answer to "how does this host authenticate people".
            var setup = new Security.KocSetupStore(configuration);
            switch (setup.Mode)
            {
                case Security.KocAuthMode.DemoPersonas:
                    errors.Add("Authentication is set to the demo persona switcher, which signs everyone in without a password — finish setup and choose accounts, intranet sign-on, or Entra.");
                    break;

                case Security.KocAuthMode.Unconfigured:
                    errors.Add("No authentication has been configured — run the first-run setup, or set Auth:Mode (LocalAccounts, WindowsIntranet, EntraId).");
                    break;

                case Security.KocAuthMode.LocalAccounts when string.IsNullOrWhiteSpace(setup.Current.TokenSigningKey):
                    errors.Add("Local accounts are configured without a token signing key — re-run setup, or set Auth:TokenSigningKey.");
                    break;
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
