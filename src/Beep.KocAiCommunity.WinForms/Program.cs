using System.Globalization;
using Beep.KocAiCommunity.Client;
using Beep.KocAiCommunity.Contracts.Localization;
using Beep.KocAiCommunity.Desktop.Local;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor.Services;

namespace Beep.KocAiCommunity.WinForms;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        ApplicationConfiguration.Initialize();

        var services = new ServiceCollection();
        services.AddWindowsFormsBlazorWebView();
#if DEBUG
        services.AddBlazorWebViewDeveloperTools();
#endif
        services.AddMudServices();

        // The shared components take IStringLocalizer, so the desktop host needs the same resource
        // machinery the web host has — without it every shared label throws rather than falling back.
        services.AddLogging();
        services.AddLocalization();

        // Local-first: the Studio designer + runs execute in-process (offline). Competition
        // calls fall through to this API base URL when the KOC network is reachable.
        var settings = AppSettings.Load();

        // No request pipeline here, so the culture is set once for the process. Numbers and dates stay
        // pinned exactly as they are on the web, so a score reads the same in both.
        CultureInfo.DefaultThreadCurrentUICulture = new CultureInfo(KocLanguages.Normalize(settings.Language));
        CultureInfo.DefaultThreadCurrentCulture = new CultureInfo(KocLanguages.FormattingCulture);
        var apiBaseUrl = Environment.GetEnvironmentVariable("KOC_API_BASEURL") ?? settings.ApiBaseUrl;
        services.AddSingleton(settings);
        // The signed-in user comes from the intranet session (no extra login). Swap this provider
        // for a directory-API implementation later to fill in department/profile info.
        services.AddSingleton<IEnvironmentUserProvider, WindowsEnvironmentUserProvider>();
        services.AddSingleton<SignedInUser>();
        services.AddKocLocalStudio(apiBaseUrl);

        using var provider = services.BuildServiceProvider();

        // Resolve the signed-in user once and cache it for the session.
        var signedIn = provider.GetRequiredService<SignedInUser>();
        try
        {
            signedIn.Current = provider.GetRequiredService<IEnvironmentUserProvider>().GetCurrentAsync().GetAwaiter().GetResult();
        }
        catch
        {
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
    }
}
