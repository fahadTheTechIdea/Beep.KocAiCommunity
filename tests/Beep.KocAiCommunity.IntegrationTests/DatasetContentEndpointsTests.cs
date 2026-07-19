using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using Beep.KocAiCommunity.Contracts.Datasets;
using FluentAssertions;
using Xunit;

namespace Beep.KocAiCommunity.IntegrationTests;

public class DatasetContentEndpointsTests(KocApiFactory factory) : IClassFixture<KocApiFactory>
{
    private readonly KocApiFactory _factory = factory;

    private const string SampleCsv = "age,ratio,name\n30,1.5,alice\n40,2.5,bob\n50,3.5,carol\n";

    [Fact]
    public async Task Upload_infers_schema_and_profile_then_publish_freezes_the_version()
    {
        var owner = _factory.CreateClientAs("emp1", "Employee");   // seeded member of Team t1
        var id = await CreateDatasetAsync(owner, "Team", "Internal");

        // Upload → draft v1 with schema + profile.
        var v1 = await (await owner.PostAsync($"/api/v1/datasets/{id}/files", CsvFile(SampleCsv)))
            .Content.ReadFromJsonAsync<DatasetVersionDto>();
        v1!.VersionNumber.Should().Be(1);
        v1.Status.Should().Be("draft");

        var detail = await owner.GetFromJsonAsync<DatasetVersionDetailDto>($"/api/v1/datasets/{id}/versions/1");
        detail!.Schema.Should().Contain(c => c.ColumnName == "age" && c.DataType == "integer");
        detail.Profile!.TotalRows.Should().Be(3);
        detail.Profile.Columns.Single(c => c.ColumnName == "age").Mean.Should().Be(40);
        detail.Files.Should().ContainSingle();

        // Publish → frozen; a new upload opens v2.
        (await owner.PostAsync($"/api/v1/datasets/{id}/versions/1/publish", null)).StatusCode.Should().Be(HttpStatusCode.OK);
        var v2 = await (await owner.PostAsync($"/api/v1/datasets/{id}/files", CsvFile(SampleCsv)))
            .Content.ReadFromJsonAsync<DatasetVersionDto>();
        v2!.VersionNumber.Should().Be(2);

        var frozen = await owner.GetFromJsonAsync<DatasetVersionDetailDto>($"/api/v1/datasets/{id}/versions/1");
        frozen!.Version.Status.Should().Be("published");
    }

    [Fact]
    public async Task Download_is_allowed_for_internal_but_gated_for_confidential()
    {
        var owner = _factory.CreateClientAs("emp1", "Employee");
        var id = await CreateDatasetAsync(owner, "Company", "Confidential");
        await owner.PostAsync($"/api/v1/datasets/{id}/files", CsvFile(SampleCsv));

        var detail = await owner.GetFromJsonAsync<DatasetVersionDetailDto>($"/api/v1/datasets/{id}/versions/1");
        var fileId = detail!.Files.Single().Id;

        // The owner can download their confidential dataset.
        (await owner.GetAsync($"/api/v1/datasets/files/{fileId}/download")).StatusCode.Should().Be(HttpStatusCode.OK);

        // A Company-scoped dataset is visible to any employee, but Confidential download needs permission.
        var other = _factory.CreateClientAs("emp2", "Employee");
        (await other.GetAsync($"/api/v1/datasets/files/{fileId}/download")).StatusCode.Should().Be(HttpStatusCode.BadRequest);

        // A platform admin can download it.
        var admin = _factory.CreateClientAs("cls-admin", "Employee", "PlatformAdmin");
        (await admin.GetAsync($"/api/v1/datasets/files/{fileId}/download")).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Url_import_blocks_private_addresses()
    {
        var owner = _factory.CreateClientAs("emp1", "Employee");
        var id = await CreateDatasetAsync(owner, "Team", "Internal");

        var response = await owner.PostAsJsonAsync($"/api/v1/datasets/{id}/imports", new ImportUrlRequest("http://169.254.169.254/latest/meta-data"));
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    private async Task<Guid> CreateDatasetAsync(HttpClient client, string scope, string classification)
    {
        var created = await (await client.PostAsJsonAsync("/api/v1/datasets",
            new CreateDatasetRequest("Well logs", "desc", scope, classification, "upstream", null)))
            .Content.ReadFromJsonAsync<DatasetDto>();
        return created!.Id;
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
