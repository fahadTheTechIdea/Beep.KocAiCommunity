using Beep.KocAiCommunity.WinForms.Components;
using Microsoft.AspNetCore.Components.WebView.WindowsForms;

namespace Beep.KocAiCommunity.WinForms;

/// <summary>The desktop window: a full-bleed BlazorWebView that renders the shared Studio UI.</summary>
public sealed class MainForm : Form
{
    public MainForm(IServiceProvider services)
    {
        Text = "KOC Studio";
        Width = 1440;
        Height = 900;
        StartPosition = FormStartPosition.CenterScreen;

        var blazor = new BlazorWebView
        {
            Dock = DockStyle.Fill,
            HostPage = Path.Combine("wwwroot", "index.html"),
            Services = services,
        };
        blazor.RootComponents.Add<Shell>("#app");
        Controls.Add(blazor);
    }
}
