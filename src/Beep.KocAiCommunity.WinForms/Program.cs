using System.Globalization;
using Beep.KocAiCommunity.Client;
using Beep.KocAiCommunity.Contracts.Localization;
using Beep.KocAiCommunity.Desktop.Local;
using Beep.KocAiCommunity.WinForms.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MudBlazor.Services;

namespace Beep.KocAiCommunity.WinForms;

internal static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        // Training runs as a child of this same executable. One binary to sign and ship, and no way for
        // the app and its trainer to drift apart. Checked before anything else — a training child must
        // not create a window, load MudBlazor, or touch the settings file.
        if (args is [TrainingHost.CommandLineSwitch, var jobPath, ..])
        {
            return TrainingHost.RunAsync(jobPath, Console.Out).GetAwaiter().GetResult();
        }

        // The workspace path is needed before anything else, because that is where crashes are written.
        // Resolved without DI so a failure during composition still has somewhere to go.
        var workspace = LocalWorkspace.Default();

        // 1. Handlers first. An exception thrown while composing the app is exactly the one that used
        //    to close the window with no message and nothing to report.
        CrashReporter.Install(workspace.LogsPath);

        ApplicationConfiguration.Initialize();

        // 2. Settings, tolerating a corrupt file (AppSettings.Load already falls back to defaults).
        var settings = AppSettings.Load();

        // 3. Culture, once for the process. There is no request pipeline here. Numbers and dates stay
        //    pinned exactly as they are on the web, so a score reads the same in both.
        CultureInfo.DefaultThreadCurrentUICulture = new CultureInfo(KocLanguages.Normalize(settings.Language));
        CultureInfo.DefaultThreadCurrentCulture = new CultureInfo(KocLanguages.FormattingCulture);

        // 4. Without the WebView2 runtime the whole UI is a blank window. Say so and stop.
        if (!WebViewRuntimeCheck.EnsureAvailable())
        {
            return 1;
        }

        // 5. Repair what is repairable; refuse to start on a workspace that cannot be written to,
        //    rather than failing one operation at a time later.
        workspace.EnsureCreated();
        var report = workspace.Verify();
        if (report.IsBlocked)
        {
            MessageBox.Show(
                report.BlockedReason,
                "KOC Studio — workspace unavailable", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return 1;
        }

        var services = new ServiceCollection();
        services.AddWindowsFormsBlazorWebView();
#if DEBUG
        services.AddBlazorWebViewDeveloperTools();
#endif
        services.AddMudServices();

        // The shared components take IStringLocalizer, so the desktop host needs the same resource
        // machinery the web host has — without it every shared label throws rather than falling back.
        var fileLogging = new FileLoggerProvider(workspace.LogsPath)
        {
            MinimumLevel = settings.VerboseLogging ? LogLevel.Debug : LogLevel.Information,
        };
        services.AddLogging(builder => builder.AddProvider(fileLogging).SetMinimumLevel(LogLevel.Debug));
        services.AddSingleton(fileLogging);
        services.AddLocalization();

        // Local-first: the Studio designer + runs execute in-process (offline). Competition
        // calls fall through to this API base URL when the KOC network is reachable.
        var apiBaseUrl = Environment.GetEnvironmentVariable("KOC_API_BASEURL") ?? settings.ApiBaseUrl;
        services.AddSingleton(settings);
        services.AddSingleton(report);

        // The signed-in user comes from the intranet session (no extra login). Swap this provider
        // for a directory-API implementation later to fill in department/profile info.
        services.AddSingleton<IEnvironmentUserProvider, WindowsEnvironmentUserProvider>();
        services.AddSingleton<SignedInUser>();
        services.AddKocLocalStudio(apiBaseUrl, workspace, settings.TrainingLimits());

        using var provider = services.BuildServiceProvider();

        var log = provider.GetRequiredService<ILoggerFactory>().CreateLogger("Startup");
        log.LogInformation(
            "KOC Studio {Version} starting · {Os} · 64-bit {Bitness} · WebView2 {WebView} · workspace {Workspace}",
            CrashReporter.Version, Environment.OSVersion, Environment.Is64BitProcess,
            WebViewRuntimeCheck.InstalledVersion(), workspace.RootPath);

        foreach (var finding in report.Findings)
        {
            log.LogInformation("Workspace: [{Level}] {Message}", finding.Level, finding.Message);
        }

        // 6. First launch seeds a sample so the designer opens with something to run on.
        FirstRun.EnsureSeeded(workspace, log);

        // Resolve the signed-in user once and cache it for the session.
        var signedIn = provider.GetRequiredService<SignedInUser>();
        try
        {
            signedIn.Current = provider.GetRequiredService<IEnvironmentUserProvider>().GetCurrentAsync().GetAwaiter().GetResult();
        }
        catch (Exception ex)
        {
            log.LogWarning(ex, "Could not resolve the Windows user; falling back to the process account.");
            signedIn.Current = new EnvironmentUser(Environment.UserName, Environment.UserName);
        }

        // Identity: default to the real signed-in user; a saved dev persona overrides it.
        var identity = provider.GetRequiredService<DevIdentity>();
        var me = signedIn.Current!;
        if (settings.PersonaKey == DevIdentity.RealUserKey)
        {
            identity.SetRealUser(me.UserId, me.DisplayName, me.Roles ?? []);
        }
        else
        {
            identity.SetPersona(settings.PersonaKey);
        }

        // Fully qualified: the Beep.KocAiCommunity.Application namespace shadows WinForms' Application here.
        System.Windows.Forms.Application.Run(new MainForm(provider));
        log.LogInformation("KOC Studio closed.");
        return 0;
    }
}
