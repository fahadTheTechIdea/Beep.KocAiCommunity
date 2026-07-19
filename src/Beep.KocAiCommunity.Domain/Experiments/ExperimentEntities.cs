using Beep.KocAiCommunity.Domain.Common;

namespace Beep.KocAiCommunity.Domain.Experiments;

/// <summary>
/// A named container for related runs (e.g. "Pump failure — Q3 data"). Tracks its current best run so
/// the UI can surface the leading model at a glance.
/// </summary>
public class Experiment : AuditableEntity
{
    public string Name { get; set; } = default!;
    public string Description { get; set; } = default!;
    public string OwnerUserId { get; set; } = default!;
    public Guid? ProjectId { get; set; }
    public string Status { get; set; } = "active";        // active, archived
    public Guid? BestRunId { get; set; }
    public string? Tags { get; set; }
}

/// <summary>
/// One training run within an experiment. Carries the summary metrics plus lineage (parent run,
/// dataset, hyperparameters, environment, and content snapshot hashes) so a run is reproducible.
/// </summary>
public class Run : AuditableEntity
{
    public Guid ExperimentId { get; set; }
    public Guid? ParentRunId { get; set; }
    public Guid? DatasetId { get; set; }
    public string RunByUserId { get; set; } = default!;

    public string Status { get; set; } = "pending";       // pending, running, completed, failed
    public string? FailureStage { get; set; }

    public string Task { get; set; } = "BinaryClassification";
    public string? Algorithm { get; set; }
    public string? PrimaryMetric { get; set; }
    public double? PrimaryValue { get; set; }
    public string? SecondaryMetric { get; set; }
    public double? SecondaryValue { get; set; }
    public long RowCount { get; set; }
    public int TrialCount { get; set; }

    // Lineage / reproducibility.
    public string? HyperparametersJson { get; set; }
    public string? EnvironmentJson { get; set; }
    public string? DatasetSnapshotHash { get; set; }

    /// <summary>The registerable <see cref="Domain.Studio.ModelRun"/> this run produced, if any.</summary>
    public Guid? ModelRunId { get; set; }

    public DateTime? StartedUtc { get; set; }
    public DateTime? CompletedUtc { get; set; }

    public bool IsFavorite { get; set; }
    public bool IsBest { get; set; }
    public string? Tags { get; set; }
}

/// <summary>A single metric observation — the time series behind a run (one row per AutoML trial).</summary>
public class RunMetric : AuditableEntity
{
    public Guid RunId { get; set; }
    public string Name { get; set; } = default!;
    public double Value { get; set; }
    public string? Dataset { get; set; }                  // "train", "validation", "test"
    public string? Phase { get; set; }                    // e.g. "trial"
    public int Step { get; set; }
    public DateTime LoggedUtc { get; set; }
}

/// <summary>A recorded hyperparameter / input for a run.</summary>
public class RunParameter : AuditableEntity
{
    public Guid RunId { get; set; }
    public string Name { get; set; } = default!;
    public string ValueJson { get; set; } = default!;
}
