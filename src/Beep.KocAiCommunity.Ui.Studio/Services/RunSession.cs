using Beep.KocAiCommunity.Contracts.Workflow;

namespace Beep.KocAiCommunity.Ui.Studio.Services;

/// <summary>One line of the designer's log, kept beyond the life of the page.</summary>
public sealed record RunLogEntry(DateTime At, string Level, string Message);

/// <summary>
/// The designer's run results and log, held outside the component that renders them.
/// <para>
/// They used to live in component state, so navigating to Datasets and back lost the run you were in
/// the middle of reading — which made comparing two attempts a matter of memory. Scoped, so it lasts as
/// long as the circuit on the Web and as long as the window on the desktop.
/// </para>
/// </summary>
public sealed class RunSession
{
    /// <summary>
    /// Log lines kept. Enough to read back through a session's worth of runs; bounded because this
    /// outlives the page and an unbounded list that never resets is a slow leak.
    /// </summary>
    public const int MaxLogEntries = 500;

    private readonly List<RunLogEntry> _log = [];
    private readonly Dictionary<string, NodeExecutionResult> _byNode = new(StringComparer.Ordinal);

    /// <summary>The most recent run, or null if nothing has run in this session.</summary>
    public PipelineExecutionResult? Last { get; private set; }

    public IReadOnlyList<RunLogEntry> Log => _log;

    /// <summary>Node results from the last run, keyed by node id.</summary>
    public NodeExecutionResult? ResultFor(string nodeId) => _byNode.GetValueOrDefault(nodeId);

    /// <summary>
    /// Records a run, replacing the previous one's per-node results.
    /// <para>
    /// Replacing rather than merging: a node that did not execute this time has no current result, and
    /// showing the previous run's data beside this run's metric is how somebody reaches a confident
    /// wrong conclusion.
    /// </para>
    /// </summary>
    public void Record(PipelineExecutionResult result)
    {
        Last = result;
        _byNode.Clear();

        foreach (var node in result.Nodes)
        {
            if (!string.IsNullOrEmpty(node.NodeId))
            {
                _byNode[node.NodeId] = node;
            }
        }
    }

    public void Add(string level, string message)
    {
        _log.Add(new RunLogEntry(DateTime.Now, level, message));

        if (_log.Count > MaxLogEntries)
        {
            _log.RemoveRange(0, _log.Count - MaxLogEntries);
        }
    }

    /// <summary>Clears the log but keeps the last run — the two are cleared for different reasons.</summary>
    public void ClearLog() => _log.Clear();

    /// <summary>Forgets everything. Called when a different workflow is loaded.</summary>
    public void Reset()
    {
        _log.Clear();
        _byNode.Clear();
        Last = null;
    }
}
