using System.Text;
using Beep.KocAiCommunity.Client;
using Beep.KocAiCommunity.Contracts.Workflow;
using Beep.KocAiCommunity.Desktop.Local;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Beep.KocAiCommunity.UnitTests;

/// <summary>
/// The offline desktop Studio: node catalog, workflow registry, and pipeline execution all run
/// in-process with no API server — the guarantee that the designer works when the web server is down.
/// </summary>
public sealed class LocalStudioTests : IDisposable
{
    private readonly LocalWorkspace _workspace = new()
    {
        RootPath = Path.Combine(Path.GetTempPath(), "koc-local-test-" + Guid.NewGuid().ToString("N")),
    };
    private readonly ServiceProvider _provider;

    public LocalStudioTests()
    {
        var services = new ServiceCollection();
        // The API base URL is never contacted in these tests (only local methods are exercised).
        services.AddKocLocalStudio("http://localhost:1", _workspace);
        _provider = services.BuildServiceProvider();
    }

    private IKocApiClient Api()
    {
        var api = _provider.CreateScope().ServiceProvider.GetRequiredService<IKocApiClient>();
        return api;
    }

    [Fact]
    public async Task Node_catalog_is_served_locally_with_ml_and_duckdb_nodes()
    {
        var nodes = await Api().GetMlNodesAsync();
        var kinds = nodes.Select(n => n.Kind).ToList();
        kinds.Should().Contain(["dataset", "split", "train", "evaluate"]);   // ML
        kinds.Should().Contain(["sql", "join-dataset", "group-by"]);          // DuckDB
    }

    [Fact]
    public async Task Workflow_registry_round_trips_on_local_files()
    {
        var api = Api();
        var (created, createErr) = await api.CreateWorkflowAsync(new CreateWorkflowRequest("My pipeline", "", "Internal"));
        createErr.Should().BeNull();
        var id = created!.Id;

        var (version, saveErr) = await api.SaveWorkflowDraftAsync(id, new SaveDraftRequest("""{"Name":"p","Nodes":[],"Edges":[]}""", "wip"));
        saveErr.Should().BeNull();

        var detail = await api.GetWorkflowDetailAsync(id);
        detail!.Name.Should().Be("My pipeline");
        var body = await api.GetWorkflowVersionAsync(id, version!.VersionNumber);
        body!.DefinitionJson.Should().Contain("\"Nodes\"");

        (await api.PublishWorkflowVersionAsync(id, version.VersionNumber)).Should().BeNull();
        (await api.GetWorkflowDetailAsync(id))!.Versions.Single(v => v.VersionNumber == version.VersionNumber)
            .Status.Should().Be("published");

        (await api.GetWorkflowsAsync()).Should().ContainSingle(w => w.Id == id);
    }

    [Fact]
    public async Task Pipeline_runs_end_to_end_offline_against_a_local_csv()
    {
        // A local CSV dataset.
        var sb = new StringBuilder("x1,x2,label\n");
        for (var i = 0; i < 60; i++)
        {
            sb.Append($"{7 + (i % 3)},{7 + ((i / 3) % 3)},true\n");
            sb.Append($"{i % 3},{(i / 3) % 3},false\n");
        }

        await File.WriteAllTextAsync(Path.Combine(_workspace.DatasetsPath, "wells.csv"), sb.ToString());

        var api = Api();
        var dataset = (await api.GetDatasetsAsync()).Single();
        dataset.Name.Should().Be("wells");
        dataset.HasFile.Should().BeTrue();

        var def = new WorkflowDefinition
        {
            Name = "p",
            Nodes =
            [
                new() { Id = "d", Kind = "dataset" },
                new() { Id = "sp", Kind = "split" },
                new() { Id = "tr", Kind = "train" },
                new() { Id = "ev", Kind = "evaluate" },
            ],
            Edges = [new("d", "sp"), new("sp", "tr"), new("tr", "ev")],
        };

        var (result, error) = await api.ExecuteWorkflowFromDatasetAsync(def, dataset.Id, "label", "BinaryClassification");

        error.Should().BeNull();
        result!.Success.Should().BeTrue();
        result.Nodes.Should().OnlyContain(n => n.Status == "done");
        result.PrimaryValue.Should().BeGreaterThan(0.8);
    }

    public void Dispose()
    {
        _provider.Dispose();
        try
        {
            Directory.Delete(_workspace.RootPath, recursive: true);
        }
        catch
        {
            // best-effort temp cleanup
        }
    }
}
