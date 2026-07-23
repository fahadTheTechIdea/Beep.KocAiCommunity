using Microsoft.Extensions.Hosting;

namespace Beep.KocAiCommunity.ServiceDefaults;

/// <summary>
/// Resolves the hosting environment with an immutable-binary philosophy: the same compiled app is
/// deployed everywhere, and <b>what ships next to it</b> decides the environment.
/// <para>
/// Precedence: an explicit <c>ASPNETCORE_ENVIRONMENT</c> / <c>DOTNET_ENVIRONMENT</c> always wins (so
/// containers, IIS publish profiles, and CI stay in full control — the recommended production lever).
/// When neither is set, the environment is inferred from whether <c>appsettings.Development.json</c>
/// shipped beside the binaries: present ⇒ Development, absent ⇒ Production. Production publishes exclude
/// that file (see the host <c>.csproj</c>), so a deployed build resolves to Production automatically
/// instead of relying on someone remembering to set a variable.
/// </para>
/// </summary>
public static class KocHostEnvironment
{
    /// <summary>The resolved environment name to pass to <c>WebApplicationOptions.EnvironmentName</c>.</summary>
    public static string Resolve()
    {
        var explicitEnv = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT")
            ?? Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT");
        if (!string.IsNullOrWhiteSpace(explicitEnv))
        {
            return explicitEnv;
        }

        var devSettings = Path.Combine(AppContext.BaseDirectory, "appsettings.Development.json");
        return File.Exists(devSettings) ? Environments.Development : Environments.Production;
    }
}
