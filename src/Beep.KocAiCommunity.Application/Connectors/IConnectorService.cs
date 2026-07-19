using Beep.KocAiCommunity.Domain.Connectors;

namespace Beep.KocAiCommunity.Application.Connectors;

/// <summary>Raised when a connector action is invalid (unknown code, blocked endpoint, not found).</summary>
public sealed class ConnectorException(string message) : Exception(message);

/// <summary>A credential key with metadata — never the plaintext value.</summary>
public sealed record CredentialInfo(string Key, DateTime LastRotatedUtc, DateTime? ExpiresUtc);

/// <summary>A connector instance with its (value-masked) credentials and latest health.</summary>
public sealed record ConnectorInstanceDetail(
    ConnectorInstance Instance, IReadOnlyList<CredentialInfo> Credentials, ConnectorHealthSnapshot? LatestHealth);

/// <summary>
/// Manages connector instances and their encrypted credential vault; runs test/schema/health via the
/// connector factory. All mutations require a platform admin and are audited; secrets are never returned.
/// </summary>
public interface IConnectorService
{
    Task<ConnectorInstance> CreateInstanceAsync(string actorUserId, string code, string name, string endpoint, string authMode, string classification, CancellationToken ct = default);
    Task<IReadOnlyList<ConnectorInstance>> ListInstancesAsync(string? code, CancellationToken ct = default);
    Task<ConnectorInstanceDetail?> GetInstanceAsync(Guid instanceId, CancellationToken ct = default);
    Task DeleteInstanceAsync(string actorUserId, Guid instanceId, CancellationToken ct = default);

    /// <summary>Stores (or rotates) a credential, encrypting it at rest. Returns the credential metadata.</summary>
    Task<CredentialInfo> SetCredentialAsync(string actorUserId, Guid instanceId, string key, string value, DateTime? expiresUtc, CancellationToken ct = default);
    Task DeleteCredentialAsync(string actorUserId, Guid instanceId, string key, CancellationToken ct = default);

    Task<ConnectorTestResult> TestAsync(Guid instanceId, CancellationToken ct = default);
    Task<ConnectorSchema> GetSchemaAsync(Guid instanceId, CancellationToken ct = default);

    /// <summary>Measures health and records a <see cref="ConnectorHealthSnapshot"/>.</summary>
    Task<ConnectorHealthSnapshot> ProbeHealthAsync(Guid instanceId, CancellationToken ct = default);
}
