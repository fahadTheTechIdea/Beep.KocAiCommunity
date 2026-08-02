using System.Diagnostics;
using Microsoft.Web.WebView2.Core;

namespace Beep.KocAiCommunity.WinForms.Diagnostics;

/// <summary>
/// Confirms the WebView2 runtime is present before the app tries to render into it.
/// <para>
/// The whole UI is Blazor inside a <c>BlazorWebView</c>. With no runtime, the control fails and the user
/// gets a blank window — indistinguishable from a hang, and impossible to act on. Windows 11 ships the
/// runtime; Windows 10 machines may not have it.
/// </para>
/// <para>
/// We ship Evergreen rather than a fixed version (decision D3): Microsoft's own guidance, because
/// security fixes then arrive with Edge instead of waiting on us to rebuild.
/// </para>
/// </summary>
public static class WebViewRuntimeCheck
{
    private const string DownloadUrl = "https://developer.microsoft.com/microsoft-edge/webview2/";

    /// <summary>The installed runtime version, or null when it is absent.</summary>
    public static string? InstalledVersion()
    {
        try
        {
            var version = CoreWebView2Environment.GetAvailableBrowserVersionString();
            return string.IsNullOrWhiteSpace(version) ? null : version;
        }
        catch (WebView2RuntimeNotFoundException)
        {
            return null;
        }
        catch (Exception)
        {
            // A malformed install reports as absent — which is the actionable answer either way.
            return null;
        }
    }

    /// <summary>
    /// True when the app can render. On false it has already told the user what to do, so the caller
    /// should exit quietly rather than adding a second message.
    /// </summary>
    public static bool EnsureAvailable()
    {
        if (InstalledVersion() is not null)
        {
            return true;
        }

        var choice = MessageBox.Show(
            "KOC Studio needs the Microsoft Edge WebView2 runtime, which isn't installed on this machine."
            + Environment.NewLine + Environment.NewLine
            + "It's a free Microsoft component and is already present on most Windows 11 machines."
            + Environment.NewLine + Environment.NewLine
            + "Open the download page now?",
            "KOC Studio — WebView2 runtime required",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Information);

        if (choice == DialogResult.Yes)
        {
            OpenDownloadPage();
        }

        return false;
    }

    private static void OpenDownloadPage()
    {
        try
        {
            Process.Start(new ProcessStartInfo { FileName = DownloadUrl, UseShellExecute = true });
        }
        catch (Exception)
        {
            MessageBox.Show(
                $"Couldn't open the browser. The download page is:{Environment.NewLine}{DownloadUrl}",
                "KOC Studio", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
    }
}
