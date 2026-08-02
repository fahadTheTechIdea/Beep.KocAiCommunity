namespace Beep.KocAiCommunity.Contracts.Workflow;

/// <summary>
/// Application-owned, versioned workflow document (decoupled from any editor's internal state).
/// A minimal typed graph: dataset source → optional transforms → train → evaluate.
/// </summary>
public sealed record WorkflowDefinition
{
    public int SchemaVersion { get; init; } = 1;
    public string Name { get; init; } = string.Empty;
    public IReadOnlyList<WorkflowNode> Nodes { get; init; } = [];
    public IReadOnlyList<WorkflowEdge> Edges { get; init; } = [];
}

/// <summary>A node. <see cref="Kind"/> is one of: dataset, split, train, evaluate.</summary>
public sealed record WorkflowNode
{
    public string Id { get; init; } = string.Empty;
    public string Kind { get; init; } = string.Empty;
    public IReadOnlyDictionary<string, string>? Config { get; init; }
}

public sealed record WorkflowEdge(string FromNodeId, string ToNodeId);

/// <summary>Result of compiling a workflow: validity, ordered node ids, and any errors.</summary>
public sealed record WorkflowValidationResult(bool IsValid, IReadOnlyList<string> Order, IReadOnlyList<string> Errors);

/// <summary>
/// A bounded look at the table a node produced.
/// <para>
/// Bounded at construction and never afterwards. Retaining whole tables for a forty-node pipeline is
/// how a designer runs a machine out of memory; <see cref="TotalColumns"/> and
/// <see cref="TotalRows"/> say what was left out so the view can be honest about being a sample.
/// </para>
/// </summary>
public sealed record NodeSample(
    IReadOnlyList<string> Columns,
    IReadOnlyList<string[]> Rows,
    int TotalColumns,
    long TotalRows)
{
    /// <summary>Rows kept per node. Enough to see what the data looks like, not enough to be a copy.</summary>
    public const int MaxRows = 100;

    /// <summary>Columns kept per node.</summary>
    public const int MaxColumns = 50;

    public bool ColumnsTruncated => TotalColumns > Columns.Count;

    public bool RowsTruncated => TotalRows > Rows.Count;
}

/// <summary>
/// The result of executing a single pipeline node.
/// <para>
/// <see cref="RowsIn"/>, <see cref="RowsOut"/> and <see cref="Sample"/> are what turn debugging a
/// pipeline from deduction into observation: without them, an odd metric can only be explained by
/// reasoning about what each node <em>probably</em> did to the data.
/// </para>
/// </summary>
public sealed record NodeExecutionResult(
    string NodeId,
    string Kind,
    string Status,
    string Detail,
    long RowsIn = 0,
    long RowsOut = 0,
    long ElapsedMs = 0,
    NodeSample? Sample = null);

/// <summary>The result of executing a whole pipeline node by node.</summary>
public sealed record PipelineExecutionResult(
    bool Success,
    string? Algorithm,
    string? PrimaryMetric,
    double PrimaryValue,
    IReadOnlyList<NodeExecutionResult> Nodes,
    long RowCount = 0);
