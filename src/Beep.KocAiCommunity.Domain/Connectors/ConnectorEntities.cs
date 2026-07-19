using Beep.KocAiCommunity.Domain.Common;

namespace Beep.KocAiCommunity.Domain.Connectors;

/// <summary>
/// A configured instance of a catalog connector: its endpoint, auth mode, default classification, and
/// health-probe cadence. Credentials live in <see cref="CredentialVaultEntry"/>, encrypted at rest.
/// </summary>
public class ConnectorInstance : AuditableEntity
{
    public string ConnectorCode { get; set; } = default!;   // ppdm, openwells, ecosys, sap, pi, adls
    public string Name { get; set; } = default!;
    public string Endpoint { get; set; } = default!;
    public string AuthMode { get; set; } = "None";
    public KocDataClassification DefaultClassification { get; set; } = KocDataClassification.Internal;
    public bool IsEnabled { get; set; } = true;
    public int HealthProbeIntervalSeconds { get; set; } = 60;
}

/// <summary>An encrypted credential for a connector instance. The plaintext never leaves the process.</summary>
public class CredentialVaultEntry : AuditableEntity
{
    public Guid ConnectorInstanceId { get; set; }
    public string Key { get; set; } = default!;             // e.g. "password", "clientSecret", "sharedKey"
    public string EncryptedValue { get; set; } = default!;  // ISecretProtector ciphertext
    public string ProtectionDescriptor { get; set; } = "DataProtection";
    public DateTime LastRotatedUtc { get; set; }
    public DateTime? ExpiresUtc { get; set; }
}

/// <summary>A recorded health measurement for a connector instance.</summary>
public class ConnectorHealthSnapshot : AuditableEntity
{
    public Guid ConnectorInstanceId { get; set; }
    public string Status { get; set; } = default!;   // Healthy, Degraded, Unhealthy
    public int LatencyMs { get; set; }
    public string? Detail { get; set; }
    public DateTime MeasuredUtc { get; set; }
}
