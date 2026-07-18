using Beep.KocAiCommunity.Domain.Common;

namespace Beep.KocAiCommunity.Domain.Studio;

/// <summary>
/// A user's ML project — the container for a workflow. Every workflow lives in a project: either a
/// <b>personal</b> project (CompetitionId null) or one targeting a <b>competition</b>. The workflow
/// graph is stored as JSON so it can be reopened, edited, run, and (for competition projects) submitted.
/// </summary>
public class Project : AuditableEntity
{
    public string Name { get; set; } = default!;
    public string OwnerUserId { get; set; } = default!;
    public Guid? CompetitionId { get; set; }          // null = personal project
    public string DefinitionJson { get; set; } = "{}"; // serialized WorkflowDefinition
    public string LabelColumn { get; set; } = "label";
    public string TaskType { get; set; } = "BinaryClassification";
}
