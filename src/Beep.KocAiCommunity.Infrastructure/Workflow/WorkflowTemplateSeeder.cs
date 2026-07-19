using Beep.KocAiCommunity.Contracts.Workflow;
using Beep.KocAiCommunity.Domain.Studio;
using Beep.KocAiCommunity.Infrastructure.Persistence;
using Beep.KocAiCommunity.Workflow;
using Microsoft.EntityFrameworkCore;

namespace Beep.KocAiCommunity.Infrastructure.Workflow;

/// <summary>Seeds a small set of O&amp;G starter workflow templates. Idempotent per template code.</summary>
public static class WorkflowTemplateSeeder
{
    public static async Task SeedAsync(KocDbContext db, CancellationToken ct = default)
    {
        foreach (var (code, name, description, domain, definition) in Templates())
        {
            if (await db.Set<WorkflowTemplate>().AnyAsync(t => t.Code == code, ct))
            {
                continue;
            }

            var canonical = WorkflowSerializer.Canonicalize(definition);
            db.Set<WorkflowTemplate>().Add(new WorkflowTemplate
            {
                Code = code,
                DisplayName = name,
                Description = description,
                Domain = domain,
                DefinitionJson = canonical,
                SchemaVersion = 1,
                SnapshotHash = WorkflowSerializer.Hash(canonical),
                CreatedUtc = DateTime.UtcNow,
            });
        }

        await db.SaveChangesAsync(ct);
    }

    private static IEnumerable<(string Code, string Name, string Description, string Domain, WorkflowDefinition Definition)> Templates()
    {
        yield return ("esp-failure-classifier", "ESP failure classifier",
            "Predict electric submersible pump failures from sensor readings — dataset → split → train → evaluate.",
            "upstream", Chain("ESP failure classifier", "train"));

        yield return ("production-rate-regressor", "Production rate regressor",
            "Estimate well production rate from reservoir and operating features — dataset → split → train → evaluate.",
            "upstream", Chain("Production rate regressor", "train"));

        yield return ("well-log-clustering", "Well-log clustering",
            "Group well-log intervals into facies with unsupervised clustering — dataset → cluster → evaluate.",
            "upstream", Cluster("Well-log clustering"));

        yield return ("pipeline-leak-detector", "Pipeline leak detector",
            "Classify pipeline segments as leaking / normal from flow + pressure telemetry.",
            "midstream", Chain("Pipeline leak detector", "train"));

        yield return ("refinery-yield-regressor", "Refinery yield regressor",
            "Estimate refinery product yield from crude assay + operating conditions.",
            "downstream", Chain("Refinery yield regressor", "train"));

        yield return ("hse-incident-classifier", "HSE incident classifier",
            "Classify HSE reports by severity to prioritise review.",
            "hse", Chain("HSE incident classifier", "train"));
    }

    // dataset → split → train → evaluate
    private static WorkflowDefinition Chain(string name, string modelKind) => new()
    {
        SchemaVersion = 1,
        Name = name,
        Nodes =
        [
            new WorkflowNode { Id = "ds", Kind = "dataset" },
            new WorkflowNode { Id = "sp", Kind = "split", Config = new Dictionary<string, string> { ["testFraction"] = "0.25" } },
            new WorkflowNode { Id = "tr", Kind = modelKind },
            new WorkflowNode { Id = "ev", Kind = "evaluate" },
        ],
        Edges =
        [
            new WorkflowEdge("ds", "sp"),
            new WorkflowEdge("sp", "tr"),
            new WorkflowEdge("tr", "ev"),
        ],
    };

    // dataset → cluster → evaluate
    private static WorkflowDefinition Cluster(string name) => new()
    {
        SchemaVersion = 1,
        Name = name,
        Nodes =
        [
            new WorkflowNode { Id = "ds", Kind = "dataset" },
            new WorkflowNode { Id = "cl", Kind = "cluster", Config = new Dictionary<string, string> { ["clusters"] = "4" } },
            new WorkflowNode { Id = "ev", Kind = "evaluate" },
        ],
        Edges =
        [
            new WorkflowEdge("ds", "cl"),
            new WorkflowEdge("cl", "ev"),
        ],
    };
}
