namespace Beep.KocAiCommunity.Desktop.Local;

/// <summary>Filesystem locations for the desktop app's local Studio data.</summary>
public sealed class LocalWorkspace
{
    public required string RootPath { get; init; }

    public string DatasetsPath => Path.Combine(RootPath, "datasets");
    public string WorkflowsPath => Path.Combine(RootPath, "workflows");
    public string TempPath => Path.Combine(RootPath, "temp");

    /// <summary>The default workspace under %LOCALAPPDATA%/KocStudio.</summary>
    public static LocalWorkspace Default() => new()
    {
        RootPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "KocStudio"),
    };

    public void EnsureCreated()
    {
        Directory.CreateDirectory(DatasetsPath);
        Directory.CreateDirectory(WorkflowsPath);
        Directory.CreateDirectory(TempPath);
    }
}
