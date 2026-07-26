using Beep.KocAiCommunity.Application.ML;
using Beep.KocAiCommunity.Application.Workflow;
using Beep.KocAiCommunity.Contracts.Workflow;
using Beep.KocAiCommunity.Domain.Studio;
using Beep.KocAiCommunity.Infrastructure.Persistence;
using Beep.KocAiCommunity.Workflow;

namespace Beep.KocAiCommunity.Infrastructure.Workflow;

public sealed class WorkflowService(KocDbContext db, IPipelineExecutor executor) : IWorkflowService
{
    public WorkflowValidationResult Validate(WorkflowDefinition definition) => WorkflowCompiler.Compile(definition);

    public async Task<ModelRun> RunAsync(
        string userId, WorkflowDefinition definition, string labelColumn, MlTaskType task, Stream csv, int maxSeconds,
        IReadOnlyDictionary<Guid, Stream>? secondaryDatasets = null, CancellationToken ct = default)
    {
        var compiled = WorkflowCompiler.Compile(definition);
        if (!compiled.IsValid)
        {
            throw new WorkflowException(string.Join(" ", compiled.Errors));
        }

        // Execute the actual node graph — the same engine a competition submission scores against — so a
        // "run" reflects the pipeline the user built (its transforms, chosen trainer, and task), not a
        // substituted AutoML model on the raw CSV.
        var result = await executor.ExecuteAsync(definition, labelColumn, task, csv, maxSeconds, secondaryDatasets, ct);
        if (!result.Success)
        {
            var failed = result.Nodes.FirstOrDefault(n => n.Status == "failed");
            throw new WorkflowException(failed is null
                ? "The pipeline did not complete successfully."
                : $"Node '{failed.Kind}' failed: {failed.Detail}");
        }

        var run = new ModelRun
        {
            DatasetName = string.IsNullOrWhiteSpace(definition.Name) ? "Workflow run" : definition.Name,
            LabelColumn = labelColumn,
            Task = task.ToString(),
            Algorithm = result.Algorithm ?? "(pipeline)",
            PrimaryMetric = result.PrimaryMetric ?? string.Empty,
            PrimaryValue = result.PrimaryValue,
            SecondaryMetric = string.Empty, // the graph reports a single headline metric
            SecondaryValue = 0,
            RowCount = result.RowCount,
            RunByUserId = userId,
            CompletedUtc = DateTime.UtcNow,
            CreatedByUserId = userId,
            CreatedUtc = DateTime.UtcNow,
        };

        db.Set<ModelRun>().Add(run);
        await db.SaveChangesAsync(ct);
        return run;
    }
}
