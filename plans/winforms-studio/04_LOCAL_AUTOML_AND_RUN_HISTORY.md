# 04 — Local AutoML and run history

> **Depends on:** 01 (logging), 02 (profiling). **Blocks:** 05.
> **This is the phase the pilot depends on.** The hosting cannot run the Worker, so if the desktop
> cannot train, nothing trains.

## Context

The desktop can already train — but only by hand-building a pipeline with a `train` node. The Web's
one-click path (*upload a CSV, name the target column, get the best model*) does not exist here, because
`IMlTrainer` is registered in the Worker's DI and nowhere else.

For a learning platform this is the wrong way round. The AutoML page is where a beginner starts; the
designer is where they graduate to. The desktop currently offers only the graduate path.

Separately: **run results are lost.** The designer logs them and forgets on navigation. There is no way
to compare this run against the last one, which is the entire activity of tuning a model.

## The constraint that shapes this phase

From Phase 00 §5, and it is not a footnote:

- **AutoML searches progressively larger models as time passes**, and users hit out-of-memory errors
  because of it. `MaximumMemoryUsageInMegaByte` exists precisely for this.
- **Memory is not reliably reclaimed between trials.** Usage grows across a run.
- **`RunAsync(CancellationToken)` does not return promptly.** It calls `MLContext.CancelExecution` and
  waits for in-flight trials.

This runs on a KOC workstation that is also running Outlook, Teams and a browser. An uncapped AutoML
experiment on that machine is a support ticket with the app's name on it.

## Scope

**In**

- `IMlTrainer` in desktop DI; an AutoML page
- Explicit memory ceiling and time budget, defaulted conservatively and user-adjustable
- Honest cancellation
- Live trial progress
- Persisted run history with metrics, parameters and lineage
- Run comparison

**Out**

- Distributed or GPU training.
- Hyperparameter search beyond what ML.NET AutoML provides.
- Automatic model deployment. That is Phase 05, and it should be deliberate.

## Design

### Registering the trainer

```csharp
// KocLocalServiceCollectionExtensions.AddKocLocalStudio
services.AddScoped<IMlTrainer, AutoMlTrainer>();
services.AddSingleton<IPredictionPool, AutoMlPredictionPool>();
```

Both types are in `Beep.KocAiCommunity.ML`, which `Desktop.Local` already references. This is two lines;
the work is everything downstream of them.

### Resource limits — non-negotiable

```csharp
public sealed record LocalTrainingLimits
{
    /// Below the process ceiling, leaving headroom for the WebView and the OS.
    public int MaxMemoryMb { get; init; } = 2048;

    /// A beginner should get a result inside a coffee break.
    public int MaxSecondsPerExperiment { get; init; } = 300;

    /// Scratch lives in the workspace so cleanup is one folder.
    public string CacheDirectory { get; init; } = "<workspace>/temp/automl";
}
```

Surfaced in Settings with plain-language guidance, not raw numbers alone: *"Training stops after 5
minutes or 2 GB, whichever comes first. Raise these if your models are being cut off."*

Defaults are deliberately modest. A run that finishes with a mediocre model teaches more than one that
takes the machine down.

### Cancellation that tells the truth

Per **D5**. Three states, and the UI must distinguish them:

| State | UI |
|---|---|
| Running | Progress, trial count, best-so-far. **Stop** enabled |
| Stopping | "Finishing the current trial…". **Stop** disabled, spinner continues |
| Stopped | Best result *so far* is offered — `RunAsync` returns it if any trial completed |

The middle state is the one that gets skipped and the one that makes a cancel button look broken.

### Progress

`AutoMLExperiment` reports per-trial. Surface: trial number, algorithm tried, its metric, elapsed, and
the best so far. This is also the teaching surface — watching FastTree beat SDCA on this data is the
lesson.

Marshal to the UI thread; the trial callback does not arrive on it.

### Run history

Per **D7**, files in the workspace, not a database.

```
workspace/
  runs/
    2026-08-02T14-33-07Z-{shortId}/
      run.json          ← task, target, metrics, params, dataset id + hash, duration, limits
      model.zip         ← the trained model, when one was produced
      log.txt           ← trial-by-trial
```

`run.json` records the **dataset content hash**, not just its id: a run whose data has since changed is
not reproducible, and the history should be able to say so rather than implying otherwise.

Retention: keep everything, show the last 50, and offer "delete runs older than…". Models are the bulk;
a run whose model was deleted keeps its metrics.

### Comparison

Select two or more runs → a table of metric, algorithm, parameters, dataset, duration, with differences
marked. This is the tuning loop, and without it every comparison is done from memory or a notebook.

## Files

| File | Change |
|---|---|
| `Desktop.Local/KocLocalServiceCollectionExtensions.cs` | Register `IMlTrainer`, `IPredictionPool`, limits |
| `Desktop.Local/LocalRunStore.cs` | New — write, list, read, delete runs |
| `Desktop.Local/LocalTrainingLimits.cs` | New |
| `Desktop.Local/LocalKocApiClient.cs` | Override the training and run-listing calls to hit local |
| `WinForms/Components/AutoMl.razor` | New — the CSV → model page |
| `WinForms/Components/Runs.razor` | New — history and comparison |
| `WinForms/Components/Settings.razor` | Limits, with guidance |
| `ML/AutoMlTrainer.cs` | Accept limits and a progress callback; honour cancellation |

## Acceptance criteria

- [ ] AutoML page: pick a local dataset, name the target, pick a task, train — no graph needed
- [ ] Progress shows trials as they complete, with the best so far
- [ ] **Stop** moves to "finishing the current trial…", then returns the best completed result
- [ ] A run that would exceed the memory ceiling stops cleanly and says so — it does not take the app down
- [ ] The time budget is honoured within a few seconds
- [ ] Every run appears in history with its metrics and its dataset hash
- [ ] History survives a restart
- [ ] Two runs can be compared side by side
- [ ] A run whose dataset has since changed is marked as such
- [ ] Scratch files are cleaned up after a run, and orphans are swept at launch

## Tests

| Test | Level |
|---|---|
| Limits reach the `AutoMLExperiment` configuration | Unit |
| Cancellation surfaces the best completed trial rather than throwing | Unit |
| A run with no completed trial reports honestly rather than pretending success | Unit |
| Run records round-trip through the store | Unit |
| Dataset hash change is detected on read | Unit |
| Retention deletes only beyond the cutoff | Unit |
| Progress callbacks marshal to the UI thread | Unit |
| Scratch directory is emptied after a run | Unit |

A real AutoML run is slow and non-deterministic. Test the *plumbing* — limits, cancellation, recording —
against a fake `IMlTrainer`, and verify actual training by hand.

## Risks

| Risk | Mitigation |
|---|---|
| **AutoML exhausts memory despite the cap** — the cap is advisory in some ML.NET versions | Also watch the process working set and abort; treat the cap as one of two defences |
| Memory not reclaimed between trials | Run the experiment in a scope disposed afterwards; if leaks persist, consider a child process — heavier, but it can be killed |
| A long run blocks the UI | Off the UI thread; the app stays usable, with a visible indicator |
| Model files fill the disk | Report workspace size in Settings; offer retention |
| A user expects server-grade results on a laptop | Say what the limits are, in the UI, in plain words |

## Note on the pilot

Until Phase 07 ships an installer, the people who can train are the people who can build the solution.
That is fine for a pilot of one or two, and untenable beyond it. **04 and 07 are the pair that make the
desktop a training tier rather than a developer's tool.**
