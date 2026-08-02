namespace Beep.KocAiCommunity.Desktop.Local;

/// <summary>
/// What a training run on a workstation is allowed to consume.
/// <para>
/// These are not tuning knobs, they are a safety belt. ML.NET AutoML searches progressively larger
/// models as time passes and does not reliably reclaim memory between trials, so an unbounded run on a
/// machine that is also running Outlook, Teams and a browser is a support ticket waiting to happen.
/// </para>
/// <para>
/// Defaults are deliberately modest. A run that finishes with a mediocre model teaches more than one
/// that takes the machine down — and the numbers are shown in Settings so nobody has to guess why a
/// run stopped.
/// </para>
/// </summary>
public sealed record LocalTrainingLimits
{
    /// <summary>
    /// Working-set ceiling for the whole process, in MB. Checked by a watchdog during the run rather
    /// than handed to AutoML: this API surface has no memory cap of its own, so this is the only
    /// defence there is.
    /// </summary>
    public int MaxMemoryMb { get; init; } = 2048;

    /// <summary>Wall-clock budget for one experiment. A beginner should get a result inside a coffee break.</summary>
    public int MaxSecondsPerExperiment { get; init; } = 300;

    /// <summary>How often the watchdog samples memory.</summary>
    public TimeSpan MemoryCheckInterval { get; init; } = TimeSpan.FromSeconds(2);

    /// <summary>Sane bounds, so a hand-edited settings file cannot disable the belt entirely.</summary>
    public LocalTrainingLimits Clamped() => this with
    {
        MaxMemoryMb = Math.Clamp(MaxMemoryMb, 512, 16384),
        MaxSecondsPerExperiment = Math.Clamp(MaxSecondsPerExperiment, 10, 3600),
    };
}
