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
        services.AddKocLocalStudio(apiBaseUrl);

        using var provider = services.BuildServiceProvider();

        // Identity: default to the real signed-in Windows/Entra user; a saved dev persona overrides it.
        var identity = provider.GetRequiredService<DevIdentity>();
        if (settings.PersonaKey == DevIdentity.RealUserKey)
        {
            var (userId, displayName) = WindowsUser.Current();
            identity.SetRealUser(userId, displayName, []);
        }
        else
        {
            identity.SetPersona(settings.PersonaKey);
        }
        // Fully qualified: the Beep.KocAiCommunity.Application namespace shadows WinForms' Application here.
        System.Windows.Forms.Application.Run(new MainForm(provider));
    }
}
