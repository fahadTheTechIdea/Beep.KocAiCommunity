using Beep.KocAiCommunity.Application.Connectors;
using Beep.KocAiCommunity.Infrastructure.Connectors;
using FluentAssertions;
using Xunit;

namespace Beep.KocAiCommunity.UnitTests;

public class ConnectorCatalogTests
{
    [Fact]
    public void Catalog_lists_the_six_koc_connectors()
    {
        ConnectorCatalog.All.Select(c => c.Code).Should()
            .BeEquivalentTo("ppdm", "openwells", "ecosys", "sap", "pi", "adls");
        ConnectorCatalog.Find("pi")!.DefaultClassification.ToString().Should().Be("Restricted");
    }

    [Fact]
    public void Factory_resolves_known_codes_and_rejects_unknown()
    {
        var factory = new MockConnectorFactory();
        factory.Resolve("ppdm").Code.Should().Be("ppdm");

        var act = () => factory.Resolve("nope");
        act.Should().Throw<ConnectorException>();
    }

    [Fact]
    public async Task Mock_connector_returns_a_code_appropriate_schema()
    {
        var ctx = new ConnectorContext("ppdm", "Server=ppdm", "Basic", new Dictionary<string, string>());
        var schema = await new MockConnector("ppdm").GetSchemaAsync(ctx);
        schema.Resources.Should().Contain(r => r.Name == "WELL");
        schema.Resources.Single(r => r.Name == "WELL").Columns.Should().Contain(c => c.Name == "UWI");

        var health = await new MockConnector("pi").HealthAsync(ctx);
        health.Status.Should().Be("Healthy");
    }
}
