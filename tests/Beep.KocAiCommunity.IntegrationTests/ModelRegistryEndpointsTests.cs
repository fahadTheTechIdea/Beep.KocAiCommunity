using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using Beep.KocAiCommunity.Contracts.Studio;
using FluentAssertions;
using Xunit;

namespace Beep.KocAiCommunity.IntegrationTests;

public class ModelRegistryEndpointsTests(KocApiFactory factory) : IClassFixture<KocApiFactory>
{
    private readonly KocApiFactory _factory = factory;

    [Fact]
    public async Task Promotion_requires_two_distinct_approvals()
    {
        // Train a model to register.
        var owner = _factory.CreateClientAs("reg-owner", "Employee");
        var sb = new StringBuilder("x1,x2,label\n");
        for (var i = 0; i < 60; i++)
        {
            sb.Append($"{7 + (i % 3)},{7 + ((i / 3) % 3)},true\n");
            sb.Append($"{i % 3},{(i / 3) % 3},false\n");
        }
        var trained = (await (await owner.PostAsync("/api/v1/studio/train?labelColumn=label&datasetName=ESP", CsvFile(sb.ToString())))
            .Content.ReadFromJsonAsync<ModelRunDto>())!;

        // Register it as a model version (staging).
        var version = (await (await owner.PostAsJsonAsync("/api/v1/models", new RegisterModelRequest("ESP failure model", trained.Id)))
            .Content.ReadFromJsonAsync<ModelVersionDto>())!;
        version.Status.Should().Be("staging");
        version.SemVer.Should().Be("1.0.0");

        // Promotion with no approvals is rejected.
        (await owner.PostAsync($"/api/v1/models/versions/{version.Id}/promote", null))
            .StatusCode.Should().Be(HttpStatusCode.BadRequest);

        // The registrar cannot approve their own version.
        (await owner.PostAsync($"/api/v1/models/versions/{version.Id}/approve", null))
            .StatusCode.Should().Be(HttpStatusCode.BadRequest);

        // Two distinct approvers approve.
        (await _factory.CreateClientAs("approver-1", "Employee").PostAsync($"/api/v1/models/versions/{version.Id}/approve", null))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);

        var approver2 = _factory.CreateClientAs("approver-2", "Employee");
        (await approver2.PostAsync($"/api/v1/models/versions/{version.Id}/approve", null))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Now it can be promoted to production.
        var promote = await owner.PostAsync($"/api/v1/models/versions/{version.Id}/promote", null);
        promote.StatusCode.Should().Be(HttpStatusCode.OK);

        var models = (await owner.GetFromJsonAsync<List<RegisteredModelDto>>("/api/v1/models"))!;
        var v = models.SelectMany(m => m.Versions).Single(x => x.Id == version.Id);
        v.Status.Should().Be("production");
        v.Approvals.Should().Be(2);
    }

    [Fact]
    public async Task Only_promoted_versions_deploy_and_redeploy_retires_the_previous()
    {
        var owner = _factory.CreateClientAs("dep-owner", "Employee");
        var sb = new StringBuilder("x1,x2,label\n");
        for (var i = 0; i < 60; i++)
        {
            sb.Append($"{7 + (i % 3)},{7 + ((i / 3) % 3)},true\n");
            sb.Append($"{i % 3},{(i / 3) % 3},false\n");
        }
        var trained = (await (await owner.PostAsync("/api/v1/studio/train?labelColumn=label&datasetName=Dep", CsvFile(sb.ToString())))
            .Content.ReadFromJsonAsync<ModelRunDto>())!;
        var v1 = (await (await owner.PostAsJsonAsync("/api/v1/models", new RegisterModelRequest("Deploy model", trained.Id)))
            .Content.ReadFromJsonAsync<ModelVersionDto>())!;

        // Staging cannot be deployed.
        (await owner.PostAsync($"/api/v1/models/versions/{v1.Id}/deploy", null)).StatusCode.Should().Be(HttpStatusCode.BadRequest);

        // Approve + promote, then deploy.
        await _factory.CreateClientAs("dep-approver-1", "Employee").PostAsync($"/api/v1/models/versions/{v1.Id}/approve", null);
        await _factory.CreateClientAs("dep-approver-2", "Employee").PostAsync($"/api/v1/models/versions/{v1.Id}/approve", null);
        (await owner.PostAsync($"/api/v1/models/versions/{v1.Id}/promote", null)).StatusCode.Should().Be(HttpStatusCode.OK);
        (await owner.PostAsync($"/api/v1/models/versions/{v1.Id}/deploy", null)).StatusCode.Should().Be(HttpStatusCode.OK);

        var deployments = (await owner.GetFromJsonAsync<List<DeploymentDto>>("/api/v1/models/deployments"))!;
        deployments.Should().ContainSingle(d => d.ModelVersionId == v1.Id && d.Status == "active");

        // Register + promote a second version, deploy it → the first deployment retires.
        var v2 = (await (await owner.PostAsJsonAsync("/api/v1/models", new RegisterModelRequest("Deploy model", trained.Id)))
            .Content.ReadFromJsonAsync<ModelVersionDto>())!;
        await _factory.CreateClientAs("dep-approver-1", "Employee").PostAsync($"/api/v1/models/versions/{v2.Id}/approve", null);
        await _factory.CreateClientAs("dep-approver-2", "Employee").PostAsync($"/api/v1/models/versions/{v2.Id}/approve", null);
        await owner.PostAsync($"/api/v1/models/versions/{v2.Id}/promote", null);
        (await owner.PostAsync($"/api/v1/models/versions/{v2.Id}/deploy", null)).StatusCode.Should().Be(HttpStatusCode.OK);

        var after = (await owner.GetFromJsonAsync<List<DeploymentDto>>("/api/v1/models/deployments"))!;
        after.Count(d => d.Status == "active").Should().Be(1);
        after.Single(d => d.Status == "active").ModelVersionId.Should().Be(v2.Id);
        after.Should().Contain(d => d.ModelVersionId == v1.Id && d.Status == "retired");
    }

    private static MultipartFormDataContent CsvFile(string content)
    {
        var form = new MultipartFormDataContent();
        var part = new StringContent(content);
        part.Headers.ContentType = new MediaTypeHeaderValue("text/csv");
        form.Add(part, "file", "data.csv");
        return form;
    }
}
