using System.Net;
using System.Net.Http.Json;
using Beep.KocAiCommunity.Contracts.Admin;
using Beep.KocAiCommunity.Contracts.Connectors;
using FluentAssertions;
using Xunit;

namespace Beep.KocAiCommunity.IntegrationTests;

public class ConnectorEndpointsTests(KocApiFactory factory) : IClassFixture<KocApiFactory>
{
    private readonly KocApiFactory _factory = factory;

    [Fact]
    public async Task Connector_surface_requires_platform_admin()
    {
        var employee = _factory.CreateClientAs("conn-emp", "Employee");
        (await employee.GetAsync("/api/v1/connectors")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Catalog_lists_connectors()
    {
        var admin = _factory.CreateClientAs("conn-admin1", "Employee", "PlatformAdmin");
        var catalog = await admin.GetFromJsonAsync<List<ConnectorDescriptorDto>>("/api/v1/connectors");
        catalog.Should().Contain(c => c.Code == "ppdm").And.Contain(c => c.Code == "pi");
    }

    [Fact]
    public async Task Endpoints_resolving_to_private_addresses_are_blocked()
    {
        var admin = _factory.CreateClientAs("conn-admin2", "Employee", "PlatformAdmin");
        var response = await admin.PostAsJsonAsync("/api/v1/connectors/ppdm/instances",
            new CreateConnectorInstanceRequest("ppdm", "Bad", "http://169.254.169.254/", "Basic", "Confidential"));
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Full_lifecycle_create_credential_test_schema_health_with_secret_never_exposed()
    {
        var admin = _factory.CreateClientAs("conn-admin3", "Employee", "PlatformAdmin");

        // Create a PPDM instance (non-URL endpoint → no SSRF check).
        var instance = await (await admin.PostAsJsonAsync("/api/v1/connectors/ppdm/instances",
            new CreateConnectorInstanceRequest("ppdm", "KOC PPDM", "Server=ppdm;Database=PPDM39", "Basic", "Confidential")))
            .Content.ReadFromJsonAsync<ConnectorInstanceDto>();
        instance!.Code.Should().Be("ppdm");

        // Store a secret credential.
        const string secret = "ppdm-p@ssw0rd";
        var cred = await (await admin.PostAsJsonAsync($"/api/v1/connectors/instances/{instance.Id}/credentials",
            new SetCredentialRequest("password", secret, null))).Content.ReadFromJsonAsync<CredentialInfoDto>();
        cred!.Key.Should().Be("password");

        // The detail view exposes the credential key but never its value.
        var detail = await admin.GetFromJsonAsync<ConnectorInstanceDetailDto>($"/api/v1/connectors/instances/{instance.Id}");
        detail!.Credentials.Should().ContainSingle(c => c.Key == "password");
        (await admin.GetStringAsync($"/api/v1/connectors/instances/{instance.Id}")).Should().NotContain(secret);

        // The secret must never appear in the audit log.
        var audit = await admin.GetFromJsonAsync<List<AuditLogDto>>("/api/v1/admin/audit");
        audit!.Should().NotContain(a => (a.AfterJson ?? "").Contains(secret) || (a.BeforeJson ?? "").Contains(secret));

        // Test + schema + health via the (mock) connector.
        var test = await (await admin.PostAsync($"/api/v1/connectors/instances/{instance.Id}/test", null))
            .Content.ReadFromJsonAsync<ConnectorTestDto>();
        test!.Ok.Should().BeTrue();

        var schema = await admin.GetFromJsonAsync<ConnectorSchemaDto>($"/api/v1/connectors/instances/{instance.Id}/schema");
        schema!.Resources.Should().Contain(r => r.Name == "WELL");

        var health = await (await admin.PostAsync($"/api/v1/connectors/instances/{instance.Id}/health", null))
            .Content.ReadFromJsonAsync<ConnectorHealthDto>();
        health!.Status.Should().Be("Healthy");

        // The health snapshot is recorded and surfaces on the instance detail.
        var after = await admin.GetFromJsonAsync<ConnectorInstanceDetailDto>($"/api/v1/connectors/instances/{instance.Id}");
        after!.LatestHealth.Should().NotBeNull();
        after.LatestHealth!.Status.Should().Be("Healthy");
    }
}
