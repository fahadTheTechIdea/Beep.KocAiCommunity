namespace Beep.KocAiCommunity.Contracts.Connectors;

public sealed record ConnectorCapabilitiesDto(bool Schema, bool Browse, bool Import, bool ReadOnly);

public sealed record ConnectorDescriptorDto(
    string Code, string DisplayName, string Version, string Description,
    string DefaultClassification, IReadOnlyList<string> AuthModes, ConnectorCapabilitiesDto Capabilities);

public sealed record ConnectorInstanceDto(
    Guid Id, string Code, string Name, string Endpoint, string AuthMode, string DefaultClassification, bool IsEnabled, DateTime CreatedUtc);

public sealed record CreateConnectorInstanceRequest(string Code, string Name, string Endpoint, string AuthMode, string Classification);

public sealed record CredentialInfoDto(string Key, DateTime LastRotatedUtc, DateTime? ExpiresUtc);

public sealed record SetCredentialRequest(string Key, string Value, DateTime? ExpiresUtc);

public sealed record ConnectorHealthDto(string Status, int LatencyMs, string? Detail, DateTime MeasuredUtc);

public sealed record ConnectorInstanceDetailDto(
    ConnectorInstanceDto Instance, IReadOnlyList<CredentialInfoDto> Credentials, ConnectorHealthDto? LatestHealth);

public sealed record ConnectorTestDto(bool Ok, string Message, int LatencyMs);

public sealed record ConnectorColumnDto(string Name, string DataType);
public sealed record ConnectorResourceDto(string Path, string Name, string Kind, IReadOnlyList<ConnectorColumnDto> Columns);
public sealed record ConnectorSchemaDto(IReadOnlyList<ConnectorResourceDto> Resources);
