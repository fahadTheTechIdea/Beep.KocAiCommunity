using Beep.KocAiCommunity.Domain.Common;

namespace Beep.KocAiCommunity.Domain.Datasets;

/// <summary>
/// An immutable version of a dataset's contents. Drafts accept file uploads; once published (or
/// archived) the files, schema, and profile are frozen. A new upload after publish opens a new draft.
/// </summary>
public class DatasetVersion : AuditableEntity
{
    public Guid DatasetId { get; set; }
    public int VersionNumber { get; set; }
    public string Status { get; set; } = "draft";   // draft, published, archived
    public string? Notes { get; set; }
    public long TotalSizeBytes { get; set; }
    public string? Sha256 { get; set; }
    public DateTime? PublishedUtc { get; set; }
    public string? PublishedByUserId { get; set; }
}

/// <summary>A file stored under a dataset version, backed by an artifact reference.</summary>
public class DatasetFile : AuditableEntity
{
    public Guid DatasetVersionId { get; set; }
    public Guid ArtifactReferenceId { get; set; }
    public string LogicalPath { get; set; } = default!;
    public string ContentType { get; set; } = default!;
    public long SizeBytes { get; set; }
    public long RowCount { get; set; }
}

/// <summary>One inferred column of a dataset version's tabular schema.</summary>
public class DatasetSchemaColumn : AuditableEntity
{
    public Guid DatasetVersionId { get; set; }
    public int Ordinal { get; set; }
    public string ColumnName { get; set; } = default!;
    public string DataType { get; set; } = default!;   // integer, number, boolean, date, string
    public bool Nullable { get; set; }
}

/// <summary>A sampled profile of a dataset version (reproducible: first N rows).</summary>
public class DatasetProfile : AuditableEntity
{
    public Guid DatasetVersionId { get; set; }
    public long SampledRows { get; set; }
    public long TotalRows { get; set; }
    public DateTime GeneratedUtc { get; set; }
}

/// <summary>Per-column summary statistics within a <see cref="DatasetProfile"/>.</summary>
public class DatasetProfileColumn : AuditableEntity
{
    public Guid DatasetProfileId { get; set; }
    public string ColumnName { get; set; } = default!;
    public long NullCount { get; set; }
    public long DistinctCount { get; set; }
    public double? Min { get; set; }
    public double? Max { get; set; }
    public double? Mean { get; set; }
}
