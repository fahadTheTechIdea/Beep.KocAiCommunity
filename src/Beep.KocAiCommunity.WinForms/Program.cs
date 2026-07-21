using Beep.KocAiCommunity.Client;
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

        // Stage 3 (thin client): render the shared Studio UI against a running API.
        // Stage 4 replaces AddKocHttpClient with the local in-process engine (offline).
        var apiBaseUrl = Environment.GetEnvironmentVariable("KOC_API_BASEURL") ?? "http://localhost:5250";
        services.AddKocHttpClient(apiBaseUrl);

        using var provider = services.BuildServiceProvider();
        Application.Run(new MainForm(provider));
    }
}
