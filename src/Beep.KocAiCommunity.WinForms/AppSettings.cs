using System.Text.Json;
using Beep.KocAiCommunity.Contracts.Localization;
using Beep.KocAiCommunity.Desktop.Local;

namespace Beep.KocAiCommunity.WinForms;

/// <summary>Desktop app configuration, persisted to %LOCALAPPDATA%/KocStudio/settings.json.</summary>
public sealed class AppSettings
{
    /// <summary>
    /// The website's address. Since the API merged into it, this is only used for the live leaderboard
    /// hub — everything else KOC Studio needs it reads from the database itself.
    /// </summary>
    public string ApiBaseUrl { get; set; } = "http://localhost:5150";

    /// <summary>Which identity to act as: "__me" (the real signed-in Windows user, default) or a dev persona key.</summary>
    public string PersonaKey { get; set; } = "__me";

    /// <summary>
    /// Interface language, "en" or "ar". The desktop has no request and no cookie, so the choice lives
    /// here — this file is the equivalent of the browser's culture cookie.
    /// </summary>
    public string Language { get; set; } = KocLanguages.English;

    /// <summary>
    /// How this machine reaches the platform database.
    /// <para>
    /// KOC Studio reads the platform directly — there is no API website in between. Empty means this
    /// installation is local-only: datasets, the designer, training, runs and models all still work,
    /// and anything needing the platform says so rather than failing obscurely.
    /// </para>
    /// </summary>
    public string PlatformDatabase { get; set; } = "";

    /// <summary>"SqlServer" for a KOC install; "Sqlite" when pointed at a local file for testing.</summary>
    public string DatabaseProvider { get; set; } = "SqlServer";

    /// <summary>
    /// Writes Debug-level detail to the log. Off by default — it is noisy — and turned on in Settings
    /// for a support session, so a reproduction can be captured without a new build.
    /// </summary>
    public bool VerboseLogging { get; set; }

    /// <summary>
    /// Working-set ceiling for a training run, in MB. This is a shared workstation with Outlook and Teams
    /// on it, not a training box — see <see cref="LocalTrainingLimits"/> for why AutoML
    /// needs a ceiling imposed from outside.
    /// </summary>
    public int MaxTrainingMemoryMb { get; set; } = 2048;

    /// <summary>Wall-clock budget for one AutoML run, in seconds.</summary>
    public int MaxTrainingSeconds { get; set; } = 300;

    /// <summary>The limits as the training service takes them, clamped so a hand-edited file stays sane.</summary>
    public LocalTrainingLimits TrainingLimits() => new LocalTrainingLimits
    {
        MaxMemoryMb = MaxTrainingMemoryMb,
        MaxSecondsPerExperiment = MaxTrainingSeconds,
    }.Clamped();

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
