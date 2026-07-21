using System.Text.Json;

namespace Beep.KocAiCommunity.WinForms;

/// <summary>Desktop app configuration, persisted to %LOCALAPPDATA%/KocStudio/settings.json.</summary>
public sealed class AppSettings
{
    public string ApiBaseUrl { get; set; } = "http://localhost:5250";
    public string PersonaKey { get; set; } = "platformadmin";

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
