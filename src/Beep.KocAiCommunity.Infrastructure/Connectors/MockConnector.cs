using Beep.KocAiCommunity.Application.Connectors;

namespace Beep.KocAiCommunity.Infrastructure.Connectors;

/// <summary>
/// A deterministic, offline connector used as the staging default until live KOC endpoints (PPDM, PI,
/// SAP, …) are reachable. Returns a code-appropriate fake schema and reports healthy. Real adapters
/// implement <see cref="IKocConnector"/> the same way and are swapped in at deployment.
/// </summary>
public sealed class MockConnector(string code) : IKocConnector
{
    public string Code { get; } = code;

    public Task<ConnectorTestResult> TestAsync(ConnectorContext context, CancellationToken ct = default) =>
        Task.FromResult(new ConnectorTestResult(true, $"Mock {Code} reachable at {context.Endpoint}.", 5));

    public Task<ConnectorHealthResult> HealthAsync(ConnectorContext context, CancellationToken ct = default) =>
        Task.FromResult(new ConnectorHealthResult("Healthy", 5, $"Mock {Code} probe ok."));

    public Task<ConnectorSchema> GetSchemaAsync(ConnectorContext context, CancellationToken ct = default) =>
        Task.FromResult(new ConnectorSchema(SchemaFor(Code)));

    private static IReadOnlyList<ConnectorResource> SchemaFor(string code) => code.ToLowerInvariant() switch
    {
        "ppdm" =>
        [
            Table("WELL", ("UWI", "string"), ("WELL_NAME", "string"), ("SPUD_DATE", "date"), ("SURFACE_LATITUDE", "number")),
            Table("WELLBORE", ("UWI", "string"), ("WELLBORE_NAME", "string"), ("TOTAL_DEPTH", "number")),
            Table("PDEN_VOL_SUMMARY", ("UWI", "string"), ("PROD_DATE", "date"), ("OIL_VOLUME", "number"), ("GAS_VOLUME", "number")),
        ],
        "pi" =>
        [
            new("\\\\PISRV\\OG-Assets", "Tags", "af-database",
                [new("Tag", "string"), new("Value", "number"), new("Timestamp", "date"), new("Units", "string")]),
        ],
        "openwells" => [Table("Activity", ("ActivityId", "string"), ("WellId", "string"), ("Phase", "string"), ("StartUtc", "date"))],
        "ecosys" => [Table("Project", ("ProjectId", "string"), ("Name", "string"), ("Portfolio", "string"), ("Budget", "number"))],
        "sap" => [Table("Equipment", ("EquipmentId", "string"), ("FunctionalLocation", "string"), ("Status", "string"))],
        "adls" => [new("/raw", "raw", "container", [new("path", "string"), new("size", "number")])],
        _ => [Table("Table1", ("id", "string"), ("value", "number"))],
    };

    private static ConnectorResource Table(string name, params (string Name, string Type)[] columns) =>
        new(name, name, "table", columns.Select(c => new ConnectorColumn(c.Name, c.Type)).ToList());
}

/// <summary>Resolves a connector by code. Returns a <see cref="MockConnector"/> until live adapters ship.</summary>
public sealed class MockConnectorFactory : IKocConnectorFactory
{
    public IKocConnector Resolve(string code)
    {
        if (ConnectorCatalog.Find(code) is null)
        {
            throw new ConnectorException($"Unknown connector '{code}'.");
        }

        return new MockConnector(code);
    }
}
