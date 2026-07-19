using System.Net;
using System.Net.Http.Json;
using Beep.KocAiCommunity.Contracts.Experiments;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Beep.KocAiCommunity.IntegrationTests;

public class ExperimentEndpointsTests(KocApiFactory factory) : IClassFixture<KocApiFactory>
{
    private readonly KocApiFactory _factory = factory;

    [Fact]
    public async Task Experiment_can_be_created_and_listed_by_its_owner()
    {
        var emp = _factory.CreateClientAs("emp1", "Employee");

        var created = await (await emp.PostAsJsonAsync("/api/v1/experiments",
            new CreateExperimentRequest("Pump failure — Q3", "trial", null, null)))
            .Content.ReadFromJsonAsync<ExperimentDto>();

        created.Should().NotBeNull();
        created!.Name.Should().Be("Pump failure — Q3");

        var mine = await emp.GetFromJsonAsync<List<ExperimentDto>>("/api/v1/experiments");
        mine!.Should().Contain(e => e.Id == created.Id);
    }

    [Fact]
    public async Task Metrics_ingested_for_a_running_run_are_returned()
    {
        var emp = _factory.CreateClientAs("emp1", "Employee");
        var exp = await (await emp.PostAsJsonAsync("/api/v1/experiments",
            new CreateExperimentRequest("Metrics", "", null, null))).Content.ReadFromJsonAsync<ExperimentDto>();

        // Seed a running run directly so the endpoint test doesn't need the Worker.
        Guid runId;
        using (var scope = _factory.Services.CreateScope())
        {
            var svc = scope.ServiceProvider.GetRequiredService<Beep.KocAiCommunity.Application.Experiments.IExperimentService>();
            runId = await svc.StartRunAsync(new Beep.KocAiCommunity.Application.Experiments.StartRunRequest(exp!.Id, "emp1", "BinaryClassification", null, null));
        }

        var log = await emp.PostAsJsonAsync($"/api/v1/experiments/runs/{runId}/metrics",
            new LogMetricsRequest([new RunMetricInput("Accuracy", 0.81, "validation", "trial", 1)]));
        log.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var metrics = await emp.GetFromJsonAsync<List<RunMetricDto>>($"/api/v1/experiments/runs/{runId}/metrics");
        metrics!.Should().ContainSingle(m => m.Name == "Accuracy" && m.Step == 1);
    }

    [Fact]
    public async Task Best_run_and_comparison_reflect_finished_runs()
    {
        var emp = _factory.CreateClientAs("emp1", "Employee");
        var exp = await (await emp.PostAsJsonAsync("/api/v1/experiments",
            new CreateExperimentRequest("Compare", "", null, null))).Content.ReadFromJsonAsync<ExperimentDto>();

        using (var scope = _factory.Services.CreateScope())
        {
            var svc = scope.ServiceProvider.GetRequiredService<Beep.KocAiCommunity.Application.Experiments.IExperimentService>();
            var r1 = await svc.StartRunAsync(new Beep.KocAiCommunity.Application.Experiments.StartRunRequest(exp!.Id, "emp1", "BinaryClassification", null, null));
            await svc.FinishRunAsync(r1, new Beep.KocAiCommunity.Application.Experiments.FinishRunRequest("completed", "SdcaLogisticRegression", "Accuracy", 0.78, "AUC", 0.85, 100, 4, "{}", "{}", null, null));
            var r2 = await svc.StartRunAsync(new Beep.KocAiCommunity.Application.Experiments.StartRunRequest(exp.Id, "emp1", "BinaryClassification", null, null));
            await svc.FinishRunAsync(r2, new Beep.KocAiCommunity.Application.Experiments.FinishRunRequest("completed", "FastTree", "Accuracy", 0.93, "AUC", 0.96, 100, 4, "{}", "{}", null, null));
        }

        var best = await emp.GetFromJsonAsync<RunDto>($"/api/v1/experiments/{exp!.Id}/best-run");
        best!.PrimaryValue.Should().BeApproximately(0.93, 1e-9);

        var comparison = await emp.GetFromJsonAsync<List<ComparisonRowDto>>($"/api/v1/experiments/{exp.Id}/compare");
        comparison!.Should().HaveCount(2);
        comparison![0].PrimaryValue.Should().BeApproximately(0.93, 1e-9);
    }

    [Fact]
    public async Task Experiments_require_authentication()
    {
        var anon = _factory.CreateClientAs(null);
        (await anon.GetAsync("/api/v1/experiments")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
