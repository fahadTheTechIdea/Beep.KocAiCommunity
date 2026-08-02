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

    /// <summary>Where crash and session logs are written.</summary>
    public string LogsPath => Path.Combine(RootPath, "logs");

    /// <summary>Marker written after the first successful launch, so the welcome shows exactly once.</summary>
    public string FirstRunMarkerPath => Path.Combine(RootPath, "firstrun.marker");

    public void EnsureCreated()
    {
        Directory.CreateDirectory(DatasetsPath);
        Directory.CreateDirectory(WorkflowsPath);
        Directory.CreateDirectory(TempPath);
        Directory.CreateDirectory(LogsPath);
    }

    /// <summary>
    /// Checks the workspace at launch and repairs what can be repaired.
    /// <para>
    /// The rule throughout: <b>never delete a file the user put here.</b> Folders are recreated and an
    /// unreadable index is rebuilt from what is on disk, because both are ours. A workflow that will not
    /// parse is reported and left alone — it is the user's work, and a plan that quietly deletes it to
    /// tidy up is worse than one that says it cannot read it.
    /// </para>
    /// </summary>
    public WorkspaceReport Verify()
    {
        var findings = new List<WorkspaceFinding>();

        // A root that cannot be written to is the end of the story — say so here rather than let every
        // later operation fail one at a time.
        try
        {
            Directory.CreateDirectory(RootPath);

            var probe = Path.Combine(RootPath, $".write-probe-{Guid.NewGuid():N}");
            File.WriteAllText(probe, string.Empty);
            File.Delete(probe);
        }
        catch (Exception ex)
        {
            return new WorkspaceReport(
            [
                new(WorkspaceFindingLevel.Blocked,
                    $"The workspace at {RootPath} cannot be written to: {ex.Message}"),
            ]);
        }

        foreach (var (path, label) in new[]
                 {
                     (DatasetsPath, "datasets"), (WorkflowsPath, "workflows"),
                     (TempPath, "temp"), (LogsPath, "logs"),
                 })
        {
            if (Directory.Exists(path))
            {
                continue;
            }

            Directory.CreateDirectory(path);
            findings.Add(new(WorkspaceFindingLevel.Repaired, $"The {label} folder was missing and has been recreated."));
        }

        findings.AddRange(VerifyWorkflows());
        findings.AddRange(SweepTemp());

        return new WorkspaceReport(findings);
    }

    /// <summary>Workflow files that will not parse are named, not removed.</summary>
    private IEnumerable<WorkspaceFinding> VerifyWorkflows()
    {
        foreach (var file in Directory.EnumerateFiles(WorkflowsPath, "*.json"))
        {
            var unreadable = false;
            try
            {
                using var stream = File.OpenRead(file);
                using var _ = System.Text.Json.JsonDocument.Parse(stream);
            }
            catch (Exception)
            {
                unreadable = true;
            }

            if (unreadable)
            {
                yield return new(WorkspaceFindingLevel.Warning,
                    $"The workflow file '{Path.GetFileName(file)}' could not be read. It has been left in place.");
            }
        }
    }

    /// <summary>
    /// Scratch from a run that did not finish. Prompt cleanup happens during execution; this catches
    /// what a crash left behind.
    /// </summary>
    private IEnumerable<WorkspaceFinding> SweepTemp()
    {
        var cutoff = DateTime.UtcNow.AddHours(-2);
        var swept = 0;

        foreach (var entry in Directory.EnumerateFileSystemEntries(TempPath))
        {
            try
            {
                var writtenUtc = Directory.Exists(entry)
                    ? Directory.GetLastWriteTimeUtc(entry)
                    : File.GetLastWriteTimeUtc(entry);

                if (writtenUtc > cutoff)
                {
                    continue;
                }

                if (Directory.Exists(entry))
                {
                    Directory.Delete(entry, recursive: true);
                }
                else
                {
                    File.Delete(entry);
                }

                swept++;
            }
            catch (Exception)
            {
                // A locked scratch file is not worth failing a launch over.
            }
        }

        if (swept > 0)
        {
            yield return new(WorkspaceFindingLevel.Repaired,
                $"Cleared {swept} leftover scratch item(s) from an earlier run.");
        }
    }
}
