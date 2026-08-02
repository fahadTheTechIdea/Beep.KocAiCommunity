namespace Beep.KocAiCommunity.Desktop.Local;

/// <summary>How serious a workspace finding is.</summary>
public enum WorkspaceFindingLevel
{
    /// <summary>Something was wrong and has been put right. Worth saying, not worth worrying about.</summary>
    Repaired,

    /// <summary>Something is wrong that the app can work around, but the user should know.</summary>
    Warning,

    /// <summary>The workspace cannot be used. The app should say so rather than fail later.</summary>
    Blocked,
}

/// <summary>One thing the integrity check found.</summary>
/// <param name="Level">How serious.</param>
/// <param name="Message">Plain language, naming the path where a path is the point.</param>
public sealed record WorkspaceFinding(WorkspaceFindingLevel Level, string Message);

/// <summary>
/// The result of checking a workspace at launch.
/// <para>
/// Repairs are reported rather than announced: a workspace that fixed itself is not something to
/// interrupt someone with, but it is something they should be able to find in Settings when a file they
/// expected has gone missing.
/// </para>
/// </summary>
public sealed record WorkspaceReport(IReadOnlyList<WorkspaceFinding> Findings)
{
    public static readonly WorkspaceReport Clean = new([]);

    /// <summary>True when the workspace cannot be used at all.</summary>
    public bool IsBlocked => Findings.Any(f => f.Level == WorkspaceFindingLevel.Blocked);

    /// <summary>True when nothing needed saying.</summary>
    public bool IsClean => Findings.Count == 0;

    /// <summary>The blocking reason, for the message shown before the app gives up.</summary>
    public string? BlockedReason =>
        Findings.FirstOrDefault(f => f.Level == WorkspaceFindingLevel.Blocked)?.Message;
}
