using System.Net;
using System.Net.Http.Json;
using Beep.KocAiCommunity.Contracts.Experiments;
using Beep.KocAiCommunity.Contracts.Studio;
using Beep.KocAiCommunity.Domain.Experiments;
using Beep.KocAiCommunity.Domain.Studio;
using Beep.KocAiCommunity.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Beep.KocAiCommunity.IntegrationTests;

/// <summary>Track → register: an experiment run that produced a model can be registered in the registry.</summary>
public class ExperimentRegisterTests(KocApiFactory factory) : IClassFixture<KocApiFactory>
{
    private readonly KocApiFactory _factory = factory;

    [Fact]
    public async Task A_run_with_a_model_registers_into_the_registry()
    {
        var runId = await SeedRunAsync("emp1", withModel: true);
        var me = _factory.CreateClientAs("emp1", "Employee");

        var version = await (await me.PostAsJsonAsync($"/api/v1/experiments/runs/{runId}/register", new RegisterRunRequest("Pump failure model")))
            .Content.ReadFromJsonAsync<ModelVersionDto>();
        version!.SemVer.Should().Be("1.0.0");
        version.Status.Should().Be("staging");

        // It now shows up in the model registry.
        var models = await me.GetFromJsonAsync<List<RegisteredModelDto>>("/api/v1/models");
        models.Should().Contain(m => m.Name == "Pump failure model");
    }

    [Fact]
    public async Task A_run_without_a_model_cannot_be_registered()
    {
        var runId = await SeedRunAsync("emp1", withModel: false);
        var me = _factory.CreateClientAs("emp1", "Employee");

        (await me.PostAsJsonAsync($"/api/v1/experiments/runs/{runId}/register", new RegisterRunRequest("Nope")))
            .StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Only_the_run_owner_can_register_it()
    {
        var runId = await SeedRunAsync("emp1", withModel: true);
        var intruder = _factory.CreateClientAs("emp2", "Employee");

        (await intruder.PostAsJsonAsync($"/api/v1/experiments/runs/{runId}/register", new RegisterRunRequest("Steal")))
            .StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // Seeds an experiment + completed run directly (the Worker executes real jobs out of process).
    private async Task<Guid> SeedRunAsync(string owner, bool withModel)
    {
        _factory.CreateClientAs(null); // ensure the schema + org tree exist before seeding
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<KocDbContext>();

        Guid? modelRunId = null;
        if (withModel)
        {
            var modelRun = new ModelRun
            {
                DatasetName = "ESP",
                LabelColumn = "label",
                Task = "BinaryClassification",
                Algorithm = "FastTree",
                PrimaryMetric = "Accuracy",
                PrimaryValue = 0.91,
                SecondaryMetric = "AUC",
                SecondaryValue = 0.95,
                RowCount = 120,
                RunByUserId = owner,
                CompletedUtc = DateTime.UtcNow,
                ModelArtifactId = Guid.NewGuid(),
                CreatedUtc = DateTime.UtcNow,
            };
            db.Add(modelRun);
            modelRunId = modelRun.Id;
        }

        var experiment = new Experiment { Name = "Exp", Description = "", OwnerUserId = owner, CreatedUtc = DateTime.UtcNow };
        db.Add(experiment);
        var run = new Run
        {
            ExperimentId = experiment.Id,
            RunByUserId = owner,
            Status = "completed",
            Task = "BinaryClassification",
            Algorithm = "FastTree",
            PrimaryMetric = "Accuracy",
            PrimaryValue = 0.91,
            ModelRunId = modelRunId,
            CompletedUtc = DateTime.UtcNow,
            CreatedUtc = DateTime.UtcNow,
        };
        db.Add(run);
        await db.SaveChangesAsync();
        return run.Id;
    }
}
