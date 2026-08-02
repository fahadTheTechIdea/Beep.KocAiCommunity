using System.Diagnostics;
using System.Text;
using System.Text.Json;
using Beep.KocAiCommunity.Application.ML;

namespace Beep.KocAiCommunity.Desktop.Local;

/// <summary>Where a training run has got to. The middle state is the one that usually gets skipped.</summary>
public enum TrainingPhase
{
    Idle,
    Running,

    /// <summary>
    /// Stop was pressed and the run is being wound up. Brief, but a UI that jumps straight to Idle
    /// here reads as a button that did nothing.
    /// </summary>
    Stopping,

    Done,
}

/// <summary>A snapshot of a run for the UI to render.</summary>
public sealed record TrainingProgress
{
    public TrainingPhase Phase { get; init; } = TrainingPhase.Idle;
    public IReadOnlyList<TrialReport> Trials { get; init; } = [];
    public TrialReport? Best { get; init; }
    public double ElapsedSeconds { get; init; }
    public long WorkingSetMb { get; init; }
    public string? Message { get; init; }
}

/// <summary>
/// Runs AutoML in a child process, within limits, and records the result.
/// <para>
/// The desktop is the training tier for the pilot — the shared hosting cannot run the Worker — so this
/// is the path that replaces it, and it has to behave on a machine that is also running Outlook and
/// Teams. Training is out of process because that is what makes Stop and the memory ceiling real: see
/// <see cref="TrainingHost"/> for what was measured and why in-process was abandoned.
/// </para>
/// </summary>
public sealed class LocalTrainingService(
    LocalWorkspace workspace,
    LocalDatasetStore datasets,
    LocalRunStore runs,
    LocalTrainingLimits limits)
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { WriteIndented = true };

    /// <summary>Raised as the run progresses. Fires off the UI thread — the caller marshals.</summary>
    public event Action<TrainingProgress>? Progress;

    /// <summary>
    /// How the watchdog reads the child's memory, in MB. Replaced in tests: a ceiling cannot be made to
    /// trip on demand otherwise, and a watchdog nobody can test is a watchdog nobody knows works.
    /// </summary>
    public Func<Process, long> ReadWorkingSetMb { get; set; } = p =>
    {
        p.Refresh();
        return p.WorkingSet64 / (1024 * 1024);
    };

    /// <summary>
    /// How a training child is started. Replaced in tests so the plumbing — limits, cancellation,
    /// recording — can be exercised without a real four-minute AutoML run.
    /// </summary>
    public Func<string, Process?> StartChild { get; set; } = DefaultStartChild;

    private CancellationTokenSource? _cts;

    /// <summary>True while a run is in flight, so the UI can refuse to start a second one.</summary>
    public bool IsRunning => _cts is not null;

    /// <summary>Asks the run to stop. The child is killed; the attempts so far are kept.</summary>
    public void Stop() => _cts?.Cancel();

    public async Task<LocalRun> TrainAsync(
        Guid datasetId, string targetColumn, MlTaskType task, CancellationToken ct = default)
    {
        var effective = limits.Clamped();
        var started = DateTime.UtcNow;
        var stopwatch = Stopwatch.StartNew();

        var dataset = datasets.List().FirstOrDefault(d => d.Id == datasetId)
                      ?? throw new InvalidOperationException("That dataset is no longer in the workspace.");
        var csvPath = datasets.PathFor(datasetId)
                      ?? throw new InvalidOperationException("The dataset's file is missing.");

        var run = new LocalRun
        {
            Id = LocalRunStore.NewId(started),
            StartedUtc = started,
            DatasetId = datasetId.ToString(),
            DatasetName = dataset.Name,
            DatasetHash = await LocalRunStore.HashFileAsync(csvPath, ct),
            Task = task.ToString(),
            TargetColumn = targetColumn,
            LimitSeconds = effective.MaxSecondsPerExperiment,
            LimitMemoryMb = effective.MaxMemoryMb,
        };

        // The child writes model.zip straight into the run's own folder, so the model never crosses the
        // pipe. run.json lands beside it when this returns.
        var runDirectory = Path.Combine(workspace.RootPath, "runs", run.Id);
        Directory.CreateDirectory(runDirectory);

        var jobPath = Path.Combine(workspace.TempPath, $"train-{run.Id}.json");
        Directory.CreateDirectory(workspace.TempPath);
        await File.WriteAllTextAsync(jobPath, JsonSerializer.Serialize(new TrainingJob
        {
            CsvPath = csvPath,
            TargetColumn = targetColumn,
            Task = task.ToString(),
            MaxSeconds = effective.MaxSecondsPerExperiment,
            OutputDirectory = runDirectory,
        }, Json), ct);

        // Written by the reader thread, read by the watchdog timer's. Everything that touches either
        // takes this lock — an unguarded List would throw mid-enumeration, and only sometimes.
        var trials = new List<TrialReport>();
        var trialsLock = new object();
        var log = new StringBuilder();

        TrialReport[] Snapshot()
        {
            lock (trialsLock)
            {
                return [.. trials];
            }
        }

        _cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        var token = _cts.Token;

        var outcome = LocalRunOutcome.Completed;
        string? error = null;
        TrainingMessage? result = null;
        var overBudget = false;

        try
        {
            Emit(TrainingPhase.Running, Snapshot(), stopwatch, 0, "Starting…");

            using var child = StartChild(jobPath)
                              ?? throw new InvalidOperationException(
                                  "Training could not be started as a separate process.");

            child.OutputDataReceived += (_, e) =>
            {
                if (TrainingHost.Parse(e.Data) is not { } message)
                {
                    return;
                }

                if (message.Type == "trial")
                {
                    var trial = new TrialReport(
                        message.TrialNumber, message.TrainerName ?? "", message.MetricName ?? "",
                        message.MetricValue, message.RuntimeSeconds);

                    lock (trialsLock)
                    {
                        trials.Add(trial);
                        log.AppendLine(
                            $"trial {trial.TrialNumber,3}  {trial.TrainerName,-32} {trial.MetricName}={trial.MetricValue:0.####}  {trial.RuntimeSeconds:0.0}s");
                    }

                    Emit(TrainingPhase.Running, Snapshot(), stopwatch, Memory(child), null);
                }
                else if (message.Type == "result")
                {
                    result = message;
                }
                else if (message.Type == "error")
                {
                    error = message.Message;
                }
            };

            child.BeginOutputReadLine();

            // Stop kills immediately rather than at the watchdog's next tick — waiting up to a whole
            // sampling interval is exactly what makes a Stop button feel broken.
            using var stopRequest = token.Register(() => Kill(child));

            // The watchdog is the second half of the memory defence: the child's own budget is advisory
            // in this ML.NET version, so the ceiling is enforced from out here, where it can be.
            using var watchdog = new Timer(_ => Sample(child), null,
                effective.MemoryCheckInterval, effective.MemoryCheckInterval);

            await child.WaitForExitAsync(CancellationToken.None);

            // The async wait returns before redirected output is drained; without this the last trials,
            // and sometimes the result itself, arrive after we have already written the record.
            child.WaitForExit();

            if (overBudget)
            {
                outcome = LocalRunOutcome.OutOfBudget;
                error = $"Stopped at the {effective.MaxMemoryMb} MB memory limit.";
            }
            else if (token.IsCancellationRequested)
            {
                outcome = LocalRunOutcome.Stopped;
                error = "Stopped before it finished.";
            }
            else if (error is not null || result is null)
            {
                outcome = LocalRunOutcome.Failed;
                error ??= $"Training ended unexpectedly (exit code {child.ExitCode}).";
            }

            void Sample(Process p)
            {
                try
                {
                    if (p.HasExited)
                    {
                        return;
                    }

                    var mb = Memory(p);
                    if (mb > effective.MaxMemoryMb)
                    {
                        overBudget = true;
                        Emit(TrainingPhase.Stopping, Snapshot(), stopwatch, mb,
                            $"Memory reached {mb} MB — stopping to keep the machine responsive.");
                        Kill(p);
                        return;
                    }

                    Emit(TrainingPhase.Running, Snapshot(), stopwatch, mb, null);
                }
                catch (Exception)
                {
                    // A watchdog that throws — on a process that exited between the check and the read,
                    // most likely — must never be the thing that ends a run.
                }
            }
        }
        catch (Exception ex)
        {
            outcome = LocalRunOutcome.Failed;
            error = ex.Message;
            log.AppendLine().AppendLine(ex.ToString());
        }
        finally
        {
            stopwatch.Stop();
            _cts?.Dispose();
            _cts = null;
            TryDelete(jobPath);
        }

        // A killed child leaves a half-written model behind. Keeping it would put a file in the history
        // that claims to be a model and is not.
        if (outcome is not LocalRunOutcome.Completed)
        {
            TryDelete(Path.Combine(runDirectory, "model.zip"));
        }

        var completed = Snapshot();
        run = run with
        {
            DurationSeconds = Math.Round(stopwatch.Elapsed.TotalSeconds, 1),
            Outcome = outcome,
            Error = error,
            TrialsCompleted = completed.Length,
            Algorithm = result?.Algorithm,
            PrimaryMetric = result?.PrimaryMetric,
            PrimaryValue = result is null ? null : result.PrimaryValue,
            SecondaryMetric = result?.SecondaryMetric,
            SecondaryValue = result is null ? null : result.SecondaryValue,
            RowCount = result?.RowCount ?? 0,
        };

        string trialLog;
        lock (trialsLock)
        {
            trialLog = log.ToString();
        }

        // Recorded whatever happened. A failed run is exactly the one somebody comes back to look at.
        await runs.SaveAsync(run, model: null, trialLog, CancellationToken.None);

        Emit(TrainingPhase.Done, completed, stopwatch, 0, error);
        return run;
    }

    private long Memory(Process child)
    {
        try
        {
            return ReadWorkingSetMb(child);
        }
        catch (Exception)
        {
            return 0;
        }
    }

    private void Emit(
        TrainingPhase phase, IReadOnlyList<TrialReport> trials, Stopwatch stopwatch, long workingSetMb, string? message)
    {
        // Stop was pressed but the child has not gone yet.
        var effectivePhase = phase == TrainingPhase.Running && _cts is { IsCancellationRequested: true }
            ? TrainingPhase.Stopping
            : phase;

        Progress?.Invoke(new TrainingProgress
        {
            Phase = effectivePhase,
            Trials = trials,
            Best = BestOf(trials),
            ElapsedSeconds = Math.Round(stopwatch.Elapsed.TotalSeconds, 1),
            WorkingSetMb = workingSetMb,
            Message = message,
        });
    }

    /// <summary>
    /// Highest metric wins. Every metric AutoML optimises here — Accuracy, MicroAccuracy, R² — is
    /// higher-is-better, so this holds for the trial stream even though RMSE is reported alongside.
    /// </summary>
    private static TrialReport? BestOf(IReadOnlyList<TrialReport> trials) =>
        trials.Count == 0 ? null : trials.MaxBy(t => t.MetricValue);

    private static void TryDelete(string path)
    {
        try { File.Delete(path); } catch (IOException) { /* best effort */ }
    }

    /// <summary>The whole tree — ML.NET spawns workers, and orphaning them defeats the point.</summary>
    private static void Kill(Process child)
    {
        try
        {
            if (!child.HasExited)
            {
                child.Kill(entireProcessTree: true);
            }
        }
        catch (Exception)
        {
            // It exited on its own between the check and the kill. Nothing to do.
        }
    }

    /// <summary>
    /// Re-launches this same executable in training mode.
    /// <para>
    /// Self-hosting means there is one binary to sign and ship rather than two — which matters to
    /// Phase 07 — and no chance of a version skew between the app and its trainer. Under <c>dotnet
    /// run</c> the process is <c>dotnet</c> itself, so the assembly is passed back to it.
    /// </para>
    /// </summary>
    private static Process? DefaultStartChild(string jobPath)
    {
        var host = Environment.ProcessPath;
        if (string.IsNullOrEmpty(host))
        {
            return null;
        }

        var info = new ProcessStartInfo
        {
            FileName = host,
            RedirectStandardOutput = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        if (Path.GetFileNameWithoutExtension(host).Equals("dotnet", StringComparison.OrdinalIgnoreCase))
        {
            info.ArgumentList.Add(Environment.GetCommandLineArgs()[0]);
        }

        info.ArgumentList.Add(TrainingHost.CommandLineSwitch);
        info.ArgumentList.Add(jobPath);

        return Process.Start(info);
    }
}
