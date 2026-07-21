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
        var apiBaseUrl = Environment.GetEnvironmentVariable("KOC_API_BASEURL") ?? "http://localhost:5250";
        services.AddKocLocalStudio(apiBaseUrl);

        using var provider = services.BuildServiceProvider();
        // Fully qualified: the Beep.KocAiCommunity.Application namespace shadows WinForms' Application here.
        System.Windows.Forms.Application.Run(new MainForm(provider));
    }
}
