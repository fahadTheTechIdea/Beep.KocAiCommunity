using Blazor.Diagrams.Core.Geometry;
using Blazor.Diagrams.Core.Models;

namespace Beep.KocAiCommunity.Ui.Studio.Diagrams;

/// <summary>A workflow node on the canvas: an ML pipeline step with a kind and live status.</summary>
public sealed class MlNode : NodeModel
{
    public string Kind { get; }
    public string NodeId { get; }
    public string Status { get; private set; } = "idle";

    /// <summary>Per-node configuration (algorithm, split fraction, columns, …) edited on the canvas.</summary>
    public Dictionary<string, string> Config { get; } = [];

    public MlNode(string id, string kind, Point position) : base(position)
    {
        NodeId = id;
        Kind = kind;
        Title = kind;
    }

    // Track status for a subtle canvas cue, but never write execution messages onto the node — those go to
    // the designer's log pane. The node's title stays its kind.
    public void SetStatus(string status, string? detail = null)
    {
        Status = status;
        Refresh();
    }
}
