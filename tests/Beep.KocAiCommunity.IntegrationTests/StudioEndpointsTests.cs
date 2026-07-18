using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using Beep.KocAiCommunity.Contracts.Studio;
using FluentAssertions;
using Xunit;

namespace Beep.KocAiCommunity.IntegrationTests;

public class StudioEndpointsTests(KocApiFactory factory) : IClassFixture<KocApiFactory>
{
    private readonly KocApiFactory _factory = factory;

    [Fact]
    public async Task Uploading_a_csv_trains_a_model_and_records_the_run()
    {
        var client = _factory.CreateClientAs("ml-user", "Employee");

        // Balanced, separable dataset.
        var sb = new StringBuilder("x1,x2,label\n");
        for (var i = 0; i < 60; i++)
        {
            sb.Append($"{7 + (i % 3)},{7 + ((i / 3) % 3)},true\n");
            sb.Append($"{i % 3},{(i / 3) % 3},false\n");
        }

        var response = await client.PostAsync("/api/v1/studio/train?labelColumn=label&datasetName=ESP%20sensors", CsvFile(sb.ToString()));
        response.EnsureSuccessStatusCode();

        var run = (await response.Content.ReadFromJsonAsync<ModelRunDto>())!;
        run.Task.Should().Be("BinaryClassification");
        run.PrimaryMetric.Should().Be("Accuracy");
        run.Algorithm.Should().NotBeNullOrWhiteSpace();
        run.PrimaryValue.Should().BeInRange(0.0, 1.0);
        run.RowCount.Should().Be(120);

        var runs = (await client.GetFromJsonAsync<List<ModelRunDto>>("/api/v1/studio/runs"))!;
        runs.Should().ContainSingle(r => r.DatasetName == "ESP sensors");
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
