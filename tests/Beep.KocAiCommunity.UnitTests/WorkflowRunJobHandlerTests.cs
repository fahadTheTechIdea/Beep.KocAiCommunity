using System.Text;
using System.Text.Json;
using Beep.KocAiCommunity.Application.Jobs;
using Beep.KocAiCommunity.Application.Storage;
using Beep.KocAiCommunity.Contracts.Jobs;
using Beep.KocAiCommunity.Domain.Common;
using Beep.KocAiCommunity.Domain.Datasets;
using Beep.KocAiCommunity.Domain.Studio;
using Beep.KocAiCommunity.Infrastructure.Experiments;
using Beep.KocAiCommunity.Infrastructure.Jobs;
using Beep.KocAiCommunity.Infrastructure.Storage;
using Beep.KocAiCommunity.ML;
using FluentAssertions;
using Microsoft.Extensions.Options;
using Xunit;
using ExperimentRun = Beep.KocAiCommunity.Domain.Experiments.Run;
using WorkflowEntity = Beep.KocAiCommunity.Domain.Studio.Workflow;

namespace Beep.KocAiCommunity.UnitTests;

/// <summary>
/// Executes the workflow.run job handler end to end: a published workflow + a dataset with a file →
/// a completed experiment run (with trials) AND a registerable ModelRun with a saved model artifact.
/// </summary>
[Collection(MlTrainingCollection.Name)]
public class WorkflowRunJobHandlerTests
{
    private const string Graph = """{"schemaVersion":1,"name":"ESP","nodes":[{"id":"ds","kind":"dataset"},{"id":"sp","kind":"split"},{"id":"tr","kind":"train"},{"id":"ev","kind":"evaluate"}],"edges":[{"fromNodeId":"ds","toNodeId":"sp"},{"fromNodeId":"sp","toNodeId":"tr"},{"fromNodeId":"tr","toNodeId":"ev"}]}""";

    private static string SeparableCsv()
    {
        var sb = new StringBuilder("x1,x2,label\n");
        for (var i = 0; i < 60; i++)
        {
            sb.Append($"{7 + (i % 3)},{7 + ((i / 3) % 3)},true\n");
            sb.Append($"{i % 3},{(i / 3) % 3},false\n");
        }

        return sb.ToString();
    }

    [Fact]
    public async Task Runs_a_published_workflow_into_an_experiment_and_a_model_run()
    {
        using var ctx = new OrgTestContext();
        var root = Path.Combine(Path.GetTempPath(), "koc-wfrun-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var artifacts = new ArtifactService(ctx.Db, new LocalFileArtifactStore(root), Options.Create(new ArtifactUploadOptions { RootPath = root }));
        var experiments = new ExperimentService(ctx.Db, [new EfExperimentSink(ctx.Db)]);
        var handler = new WorkflowRunJobHandler(ctx.Db, artifacts, new AutoMlTrainer(), experiments);

        // Seed: a dataset whose file is stored, and a published workflow version.
        var fileRef = await artifacts.SaveAsync(new MemoryStream(Encoding.UTF8.GetBytes(SeparableCsv())), "datasets/x/data.csv", "text/csv", KocDataClassification.Internal);
        var dataset = new Dataset { Name = "ESP sensors", Description = "", OwnerUserId = "emp1", Domain = "upstream", Classification = KocDataClassification.Internal, FileArtifactId = fileRef.Id, CreatedUtc = DateTime.UtcNow };
        ctx.Db.Add(dataset);
        var workflow = new WorkflowEntity { Name = "ESP pipeline", Description = "", OwnerUserId = "emp1", LatestVersionNumber = 1, CreatedUtc = DateTime.UtcNow };
        ctx.Db.Add(workflow);
        ctx.Db.Add(new WorkflowVersion { WorkflowId = workflow.Id, VersionNumber = 1, Status = "published", DefinitionJson = Graph, SnapshotHash = "hash", CreatedUtc = DateTime.UtcNow });
        await ctx.Db.SaveChangesAsync();

        var payload = new WorkflowRunPayload(workflow.Id, 1, workflow.Name, dataset.Id, "label", "BinaryClassification", 5, "emp1");
        var context = new JobExecutionContext(Guid.NewGuid(), JsonSerializer.Serialize(payload), (_, _) => Task.CompletedTask);

        await handler.ExecuteAsync(context, CancellationToken.None);

        // A registerable ModelRun with a saved model artifact.
        var run = ctx.Db.Set<ModelRun>().Should().ContainSingle().Subject;
        run.DatasetName.Should().Be("ESP pipeline");
        run.ModelArtifactId.Should().NotBeNull();
        run.Algorithm.Should().NotBeNullOrEmpty();

        // A completed experiment run with at least one tracked trial, linked to the ModelRun.
        var expRun = ctx.Db.Set<ExperimentRun>().Should().ContainSingle().Subject;
        expRun.Status.Should().Be("completed");
        expRun.TrialCount.Should().BeGreaterThan(0);
        expRun.ModelRunId.Should().Be(run.Id);
    }
}
