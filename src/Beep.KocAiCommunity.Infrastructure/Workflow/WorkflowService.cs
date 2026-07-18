using Beep.KocAiCommunity.Application.ML;
using Beep.KocAiCommunity.Application.Workflow;
using Beep.KocAiCommunity.Contracts.Workflow;
using Beep.KocAiCommunity.Domain.Studio;
using Beep.KocAiCommunity.Infrastructure.Persistence;
using Beep.KocAiCommunity.Workflow;

namespace Beep.KocAiCommunity.Infrastructure.Workflow;

public sealed class WorkflowService(KocDbContext db, IMlTrainer trainer) : IWorkflowService
{
    public WorkflowValidationResult Validate(WorkflowDefinition definition) => WorkflowCompiler.Compile(definition);

    public async Task<ModelRun> RunAsync(string userId, WorkflowDefinition definition, string labelColumn, Stream csv, int maxSeconds, CancellationToken ct = default)
    {
        var compiled = WorkflowCompiler.Compile(definition);
        if (!compiled.IsValid)
        {
            throw new WorkflowException(string.Join(" ", compiled.Errors));
        }

        // Execute in topological order. The train node performs the AutoML training (binary by
        // default); other node kinds (dataset/split/evaluate) pass the data through in this runtime.
        var result = await trainer.TrainAsync(MlTaskType.BinaryClassification, csv, labelColumn, maxSeconds, ct);

        var run = new ModelRun
        {
            DatasetName = string.IsNullOrWhiteSpace(definition.Name) ? "Workflow run" : definition.Name,
            LabelColumn = labelColumn,
            Task = result.Task,
            Algorithm = result.Algorithm,
            PrimaryMetric = result.PrimaryMetric,
            PrimaryValue = result.PrimaryValue,
            SecondaryMetric = result.SecondaryMetric,
            SecondaryValue = result.SecondaryValue,
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
