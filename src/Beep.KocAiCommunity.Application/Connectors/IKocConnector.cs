namespace Beep.KocAiCommunity.Application.Connectors;

/// <summary>The resolved configuration passed to a connector for a single operation.</summary>
public sealed record ConnectorContext(string Code, string Endpoint, string AuthMode, IReadOnlyDictionary<string, string> Credentials);

/// <summary>One column of a connector resource's schema.</summary>
public sealed record ConnectorColumn(string Name, string DataType);

/// <summary>One browsable resource (table/entity/tag/container) exposed by a connector.</summary>
public sealed record ConnectorResource(string Path, string Name, string Kind, IReadOnlyList<ConnectorColumn> Columns);

/// <summary>The schema a connector introspects from its source.</summary>
public sealed record ConnectorSchema(IReadOnlyList<ConnectorResource> Resources);

/// <summary>The result of a connection test.</summary>
public sealed record ConnectorTestResult(bool Ok, string Message, int LatencyMs);

/// <summary>A health measurement for a connector instance.</summary>
public sealed record ConnectorHealthResult(string Status, int LatencyMs, string? Detail);

/// <summary>
/// A KOC enterprise data connector. Concrete adapters (PPDM, PI, SAP, …) live in Infrastructure; a
/// deterministic mock adapter is the staging default until live endpoints are reachable.
/// </summary>
public interface IKocConnector
{
    string Code { get; }
    Task<ConnectorTestResult> TestAsync(ConnectorContext context, CancellationToken ct = default);
    Task<ConnectorSchema> GetSchemaAsync(ConnectorContext context, CancellationToken ct = default);
    Task<ConnectorHealthResult> HealthAsync(ConnectorContext context, CancellationToken ct = default);
}

/// <summary>Resolves a connector implementation by its catalog code.</summary>
public interface IKocConnectorFactory
{
    IKocConnector Resolve(string code);
}
