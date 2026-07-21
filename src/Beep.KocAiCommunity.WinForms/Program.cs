using Beep.KocAiCommunity.Client;
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

        // Local-first: the Studio designer + runs execute in-process (offline). Competition
        // calls fall through to this API base URL when the KOC network is reachable.
        var settings = AppSettings.Load();
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
