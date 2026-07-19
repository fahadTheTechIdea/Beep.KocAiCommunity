using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using Beep.KocAiCommunity.Contracts.Datasets;
using Beep.KocAiCommunity.Contracts.Studio;
using FluentAssertions;
using Xunit;

namespace Beep.KocAiCommunity.IntegrationTests;

/// <summary>Datasets → AutoML: train a model directly from a catalog dataset (no re-upload).</summary>
public class StudioDatasetTrainingTests(KocApiFactory factory) : IClassFixture<KocApiFactory>
{
    private readonly KocApiFactory _factory = factory;

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
    public async Task Trains_a_model_from_a_catalog_dataset()
    {
        var owner = _factory.CreateClientAs("emp1", "Employee");
        var id = await CreateDatasetWithFileAsync(owner, "ESP sensors", "Team", "Internal", SeparableCsv());

        var run = await (await owner.PostAsJsonAsync("/api/v1/studio/train/dataset",
            new TrainFromDatasetRequest(id, "label", "BinaryClassification"))).Content.ReadFromJsonAsync<ModelRunDto>();

        run!.DatasetName.Should().Be("ESP sensors");   // named after the dataset, not an upload
        run.RowCount.Should().BeGreaterThan(0);
        run.Algorithm.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task Confidential_dataset_training_is_owner_gated()
    {
        var owner = _factory.CreateClientAs("emp1", "Employee");
        var id = await CreateDatasetWithFileAsync(owner, "Well tops", "Company", "Confidential", SeparableCsv());

        // A peer who can see it (Company scope) still cannot train on Confidential data.
        var other = _factory.CreateClientAs("emp2", "Employee");
        (await other.PostAsJsonAsync("/api/v1/studio/train/dataset", new TrainFromDatasetRequest(id, "label", "BinaryClassification")))
            .StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task A_dataset_without_a_file_cannot_be_trained()
    {
        var owner = _factory.CreateClientAs("emp1", "Employee");
        var created = await (await owner.PostAsJsonAsync("/api/v1/datasets",
            new CreateDatasetRequest("Empty", "", "Team", "Internal", "upstream", null))).Content.ReadFromJsonAsync<DatasetDto>();

        (await owner.PostAsJsonAsync("/api/v1/studio/train/dataset", new TrainFromDatasetRequest(created!.Id, "label", "BinaryClassification")))
            .StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    private async Task<Guid> CreateDatasetWithFileAsync(HttpClient client, string name, string scope, string classification, string csv)
    {
        var created = await (await client.PostAsJsonAsync("/api/v1/datasets",
            new CreateDatasetRequest(name, "", scope, classification, "upstream", null))).Content.ReadFromJsonAsync<DatasetDto>();

        var form = new MultipartFormDataContent();
        var part = new StringContent(csv);
        part.Headers.ContentType = new MediaTypeHeaderValue("text/csv");
        form.Add(part, "file", "data.csv");
        (await client.PostAsync($"/api/v1/datasets/{created!.Id}/files", form)).EnsureSuccessStatusCode();
        return created.Id;
    }
}
