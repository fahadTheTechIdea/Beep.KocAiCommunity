using System.Text.Json;
using Beep.KocAiCommunity.Contracts.Localization;

namespace Beep.KocAiCommunity.WinForms;

/// <summary>Desktop app configuration, persisted to %LOCALAPPDATA%/KocStudio/settings.json.</summary>
public sealed class AppSettings
{
    public string ApiBaseUrl { get; set; } = "http://localhost:5250";

    /// <summary>Which identity to act as: "__me" (the real signed-in Windows user, default) or a dev persona key.</summary>
    public string PersonaKey { get; set; } = "__me";

    /// <summary>
    /// Interface language, "en" or "ar". The desktop has no request and no cookie, so the choice lives
    /// here — this file is the equivalent of the browser's culture cookie.
    /// </summary>
    public string Language { get; set; } = KocLanguages.English;

    /// <summary>
    /// Writes Debug-level detail to the log. Off by default — it is noisy — and turned on in Settings
    /// for a support session, so a reproduction can be captured without a new build.
    /// </summary>
    public bool VerboseLogging { get; set; }

    private static string FilePath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "KocStudio", "settings.json");

    public static AppSettings Load()
    {
        try
        {
            if (File.Exists(FilePath))
            {
                return JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(FilePath)) ?? new AppSettings();
            }
        }
        catch
        {
            // fall through to defaults
        }

        return new AppSettings();
    }

    public void Save()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
        File.WriteAllText(FilePath, JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }));
    }
}
