namespace Beep.KocAiCommunity.Ui.Studio.Services;

/// <summary>
/// Undo and redo over whole-graph snapshots.
/// <para>
/// A snapshot is the workflow's serialized definition. That is the simplest thing that is definitely
/// correct: an edit-by-edit command log has to model every operation the canvas can perform — move,
/// connect, disconnect, retype a property — and gets one of them subtly wrong. Definitions are small
/// JSON, so the memory a naive approach costs is bounded and cheap.
/// </para>
/// </summary>
public sealed class GraphHistory
{
    /// <summary>
    /// Snapshots kept. Deep enough to undo a wrong turn, shallow enough that a long session on a big
    /// graph cannot grow without limit.
    /// </summary>
    public const int Capacity = 50;

    private readonly List<string> _entries = [];
    private int _position = -1;

    /// <summary>The snapshot the graph is currently at, or null before anything has been recorded.</summary>
    public string? Current => _position >= 0 && _position < _entries.Count ? _entries[_position] : null;

    public bool CanUndo => _position > 0;

    public bool CanRedo => _position >= 0 && _position < _entries.Count - 1;

    public int Count => _entries.Count;

    /// <summary>
    /// Records a state, if it differs from the current one.
    /// <para>
    /// Recording after an undo drops whatever was ahead — the future you were going to redo into is
    /// not reachable from where the graph now is, and offering it would restore an edit made to a graph
    /// that no longer exists.
    /// </para>
    /// </summary>
    public void Record(string snapshot)
    {
        if (string.Equals(Current, snapshot, StringComparison.Ordinal))
        {
            return;
        }

        if (_position < _entries.Count - 1)
        {
            _entries.RemoveRange(_position + 1, _entries.Count - _position - 1);
        }

        _entries.Add(snapshot);

        if (_entries.Count > Capacity)
        {
            _entries.RemoveRange(0, _entries.Count - Capacity);
        }

        _position = _entries.Count - 1;
    }

    /// <summary>The previous state, or null if there is nothing to go back to.</summary>
    public string? Undo() => CanUndo ? _entries[--_position] : null;

    /// <summary>The state undone from, or null if nothing has been undone.</summary>
    public string? Redo() => CanRedo ? _entries[++_position] : null;

    /// <summary>Starts again from one state. Called when a different workflow is loaded.</summary>
    public void Reset(string? snapshot = null)
    {
        _entries.Clear();
        _position = -1;

        if (snapshot is not null)
        {
            Record(snapshot);
        }
    }
}
