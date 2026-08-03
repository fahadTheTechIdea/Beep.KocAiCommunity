using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using Beep.KocAiCommunity.Contracts.Studio;
using FluentAssertions;
using Xunit;

namespace Beep.KocAiCommunity.IntegrationTests;

public class InferenceEndpointsTests(KocApiFactory factory) : IClassFixture<KocApiFactory>
{
    private readonly KocApiFactory _factory = factory;

    [Fact]
    public async Task Promoted_version_serves_predictions_logs_and_drift()
    {
        var owner = _factory.CreateClientAs("inf-owner", "Employee");
        var version = await TrainRegisterPromoteAsync(owner, "ESP inference model");

        // Online inference: a high-feature row scores true, a low-feature row scores false.
        var high = await Infer(owner, version.Id, new() { ["x1"] = "9", ["x2"] = "9" });
        high.Predictions.Should().ContainSingle();
        high.Predictions[0].PredictedLabel.Should().Be("true");

        var low = await Infer(owner, version.Id, new() { ["x1"] = "0", ["x2"] = "0" });
        low.Predictions[0].PredictedLabel.Should().Be("false");

        // Batch inference: two rows in, two predictions out.
        var batch = await (await owner.PostAsJsonAsync($"/api/v1/models/versions/{version.Id}/infer/batch",
            new BatchInferRequest([
                new Dictionary<string, string> { ["x1"] = "9", ["x2"] = "8" },
                new Dictionary<string, string> { ["x1"] = "1", ["x2"] = "0" },
            ]))).Content.ReadFromJsonAsync<InferResponseDto>();
        batch!.Predictions.Should().HaveCount(2);

        // Any employee may score a production version.
        var other = _factory.CreateClientAs("inf-other", "Employee");
        (await other.PostAsJsonAsync($"/api/v1/models/versions/{version.Id}/infer",
            new InferRequest(new Dictionary<string, string> { ["x1"] = "8", ["x2"] = "9" })))
            .StatusCode.Should().Be(HttpStatusCode.OK);

        // Drift check against the training baseline returns per-feature signals.
        var drift = await (await owner.PostAsJsonAsync($"/api/v1/models/versions/{version.Id}/drift",
            new DriftRequest([new Dictionary<string, string> { ["x1"] = "9", ["x2"] = "9" }])))
            .Content.ReadFromJsonAsync<DriftReportDto>();
        drift!.Features.Should().NotBeEmpty();
        drift.BaselineRows.Should().BeGreaterThan(0);

        // Every call was logged; the owner can read the audit trail.
        var logs = await owner.GetFromJsonAsync<List<InferenceLogDto>>($"/api/v1/models/versions/{version.Id}/inference-logs");
        logs!.Count.Should().BeGreaterThanOrEqualTo(4);
        logs.Should().OnlyContain(l => l.Success);
        logs.Should().Contain(l => l.Endpoint == "batch");
    }

    [Fact]
    public async Task Non_production_version_is_owner_only()
    {
        var owner = _factory.CreateClientAs("stg-owner", "Employee");
        var trained = await TrainAsync("Staging only");
        var version = (await (await owner.PostAsJsonAsync("/api/v1/models", new RegisterModelRequest("Staging only model", trained)))
            .Content.ReadFromJsonAsync<ModelVersionDto>())!;
        version.Status.Should().Be("staging");

        // The owner may score their own staging version.
        (await owner.PostAsJsonAsync($"/api/v1/models/versions/{version.Id}/infer",
            new InferRequest(new Dictionary<string, string> { ["x1"] = "9", ["x2"] = "9" })))
            .StatusCode.Should().Be(HttpStatusCode.OK);

        // A different employee may not.
        (await _factory.CreateClientAs("stg-intruder", "Employee").PostAsJsonAsync($"/api/v1/models/versions/{version.Id}/infer",
            new InferRequest(new Dictionary<string, string> { ["x1"] = "9", ["x2"] = "9" })))
            .StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    private async Task<ModelVersionDto> TrainRegisterPromoteAsync(HttpClient owner, string modelName)
    {
        var trained = await TrainAsync("ESP");
        var version = (await (await owner.PostAsJsonAsync("/api/v1/models", new RegisterModelRequest(modelName, trained)))
            .Content.ReadFromJsonAsync<ModelVersionDto>())!;
        await _factory.CreateClientAs("inf-approver-1", "Employee").PostAsync($"/api/v1/models/versions/{version.Id}/approve", null);
        await _factory.CreateClientAs("inf-approver-2", "Employee").PostAsync($"/api/v1/models/versions/{version.Id}/approve", null);
        (await owner.PostAsync($"/api/v1/models/versions/{version.Id}/promote", null)).StatusCode.Should().Be(HttpStatusCode.OK);
        return version;
    }

    // Trained in-process. The platform has no route that trains any more, and these tests are about
    // serving predictions from a registered model rather than about how it came to exist.
    private Task<Guid> TrainAsync(string datasetName) =>
        ModelFixture.TrainAsync(_factory, "inf-owner", datasetName);

    private static async Task<InferResponseDto> Infer(HttpClient client, Guid versionId, Dictionary<string, string> input)
    {
        var response = await client.PostAsJsonAsync($"/api/v1/models/versions/{versionId}/infer", new InferRequest(input));
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        return (await response.Content.ReadFromJsonAsync<InferResponseDto>())!;
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
