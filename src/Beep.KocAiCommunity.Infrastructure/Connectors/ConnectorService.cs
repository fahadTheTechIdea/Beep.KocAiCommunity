using System.Diagnostics;
using Beep.KocAiCommunity.Application.Admin;
using Beep.KocAiCommunity.Application.Audit;
using Beep.KocAiCommunity.Application.Connectors;
using Beep.KocAiCommunity.Domain.Common;
using Beep.KocAiCommunity.Domain.Connectors;
using Beep.KocAiCommunity.Infrastructure.Datasets;
using Beep.KocAiCommunity.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Beep.KocAiCommunity.Infrastructure.Connectors;

/// <summary>
/// Manages connector instances and their encrypted credential vault, and runs test/schema/health via
/// the connector factory. Endpoints are SSRF-checked; credentials are encrypted via
/// <see cref="ISecretProtector"/> and never returned; every mutation is audited.
/// </summary>
public sealed class ConnectorService(
    KocDbContext db,
    IKocConnectorFactory factory,
    ISecretProtector protector,
    IAuditEnvelope audit) : IConnectorService
{
    public async Task<ConnectorInstance> CreateInstanceAsync(string actorUserId, string code, string name, string endpoint, string authMode, string classification, CancellationToken ct = default)
    {
        var descriptor = ConnectorCatalog.Find(code) ?? throw new ConnectorException($"Unknown connector '{code}'.");
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ConnectorException("A connector instance name is required.");
        }

        ValidateEndpoint(endpoint);

        if (!Enum.TryParse<KocDataClassification>(classification, ignoreCase: true, out var cls))
        {
            cls = descriptor.DefaultClassification;
        }

        var instance = new ConnectorInstance
        {
            ConnectorCode = descriptor.Code,
            Name = name.Trim(),
            Endpoint = endpoint.Trim(),
            AuthMode = string.IsNullOrWhiteSpace(authMode) ? "None" : authMode.Trim(),
            DefaultClassification = cls,
            IsEnabled = true,
            CreatedByUserId = actorUserId,
            CreatedUtc = DateTime.UtcNow,
        };
        db.Set<ConnectorInstance>().Add(instance);
        await db.SaveChangesAsync(ct);
        await audit.WriteAsync(new AuditEntry("connector.create", "connector-instance", instance.Id.ToString(),
            null, $"{{\"code\":\"{descriptor.Code}\",\"name\":\"{instance.Name}\"}}"), ct);
        return instance;
    }

    public async Task<IReadOnlyList<ConnectorInstance>> ListInstancesAsync(string? code, CancellationToken ct = default)
    {
        var q = db.Set<ConnectorInstance>().AsNoTracking();
        if (!string.IsNullOrWhiteSpace(code))
        {
            q = q.Where(i => i.ConnectorCode == code);
        }

        return await q.OrderBy(i => i.Name).ToListAsync(ct);
    }

    public async Task<ConnectorInstanceDetail?> GetInstanceAsync(Guid instanceId, CancellationToken ct = default)
    {
        var instance = await db.Set<ConnectorInstance>().AsNoTracking().FirstOrDefaultAsync(i => i.Id == instanceId, ct);
        if (instance is null)
        {
            return null;
        }

        var creds = await db.Set<CredentialVaultEntry>().AsNoTracking()
            .Where(c => c.ConnectorInstanceId == instanceId)
            .Select(c => new CredentialInfo(c.Key, c.LastRotatedUtc, c.ExpiresUtc))
            .ToListAsync(ct);
        var health = await db.Set<ConnectorHealthSnapshot>().AsNoTracking()
            .Where(h => h.ConnectorInstanceId == instanceId)
            .OrderByDescending(h => h.MeasuredUtc).FirstOrDefaultAsync(ct);

        return new ConnectorInstanceDetail(instance, creds, health);
    }

    public async Task DeleteInstanceAsync(string actorUserId, Guid instanceId, CancellationToken ct = default)
    {
        var instance = await db.Set<ConnectorInstance>().FirstOrDefaultAsync(i => i.Id == instanceId, ct)
            ?? throw new ConnectorException("Connector instance not found.");
        db.Set<ConnectorInstance>().Remove(instance);
        await db.SaveChangesAsync(ct);
        await audit.WriteAsync(new AuditEntry("connector.delete", "connector-instance", instanceId.ToString()), ct);
    }

    public async Task<CredentialInfo> SetCredentialAsync(string actorUserId, Guid instanceId, string key, string value, DateTime? expiresUtc, CancellationToken ct = default)
    {
        await RequireInstanceAsync(instanceId, ct);
        if (string.IsNullOrWhiteSpace(key))
        {
            throw new ConnectorException("A credential key is required.");
        }

        var entry = await db.Set<CredentialVaultEntry>().FirstOrDefaultAsync(c => c.ConnectorInstanceId == instanceId && c.Key == key, ct);
        if (entry is null)
        {
            entry = new CredentialVaultEntry
            {
                ConnectorInstanceId = instanceId,
                Key = key.Trim(),
                CreatedByUserId = actorUserId,
                CreatedUtc = DateTime.UtcNow,
            };
            db.Set<CredentialVaultEntry>().Add(entry);
        }

        entry.EncryptedValue = protector.Protect(value ?? string.Empty);
        entry.ProtectionDescriptor = "DataProtection";
        entry.LastRotatedUtc = DateTime.UtcNow;
        entry.ExpiresUtc = expiresUtc;
        await db.SaveChangesAsync(ct);

        // Audit records the key only — never the secret value.
        await audit.WriteAsync(new AuditEntry("connector.credential.set", "connector-credential", $"{instanceId}:{entry.Key}"), ct);
        return new CredentialInfo(entry.Key, entry.LastRotatedUtc, entry.ExpiresUtc);
    }

    public async Task DeleteCredentialAsync(string actorUserId, Guid instanceId, string key, CancellationToken ct = default)
    {
        var entry = await db.Set<CredentialVaultEntry>().FirstOrDefaultAsync(c => c.ConnectorInstanceId == instanceId && c.Key == key, ct)
            ?? throw new ConnectorException("Credential not found.");
        db.Set<CredentialVaultEntry>().Remove(entry);
        await db.SaveChangesAsync(ct);
        await audit.WriteAsync(new AuditEntry("connector.credential.delete", "connector-credential", $"{instanceId}:{key}"), ct);
    }

    public async Task<ConnectorTestResult> TestAsync(Guid instanceId, CancellationToken ct = default)
    {
        var (instance, context) = await BuildContextAsync(instanceId, ct);
        return await factory.Resolve(instance.ConnectorCode).TestAsync(context, ct);
    }

    public async Task<ConnectorSchema> GetSchemaAsync(Guid instanceId, CancellationToken ct = default)
    {
        var (instance, context) = await BuildContextAsync(instanceId, ct);
        return await factory.Resolve(instance.ConnectorCode).GetSchemaAsync(context, ct);
    }

    public async Task<ConnectorHealthSnapshot> ProbeHealthAsync(Guid instanceId, CancellationToken ct = default)
    {
        var (instance, context) = await BuildContextAsync(instanceId, ct);
        var stopwatch = Stopwatch.StartNew();
        ConnectorHealthResult result;
        try
        {
            result = await factory.Resolve(instance.ConnectorCode).HealthAsync(context, ct);
        }
        catch (Exception ex)
        {
            result = new ConnectorHealthResult("Unhealthy", (int)stopwatch.ElapsedMilliseconds, ex.Message);
        }

        var snapshot = new ConnectorHealthSnapshot
        {
            ConnectorInstanceId = instanceId,
            Status = result.Status,
            LatencyMs = result.LatencyMs,
            Detail = result.Detail,
            MeasuredUtc = DateTime.UtcNow,
            CreatedUtc = DateTime.UtcNow,
        };
        db.Set<ConnectorHealthSnapshot>().Add(snapshot);
        await db.SaveChangesAsync(ct);
        return snapshot;
    }

    private async Task<(ConnectorInstance Instance, ConnectorContext Context)> BuildContextAsync(Guid instanceId, CancellationToken ct)
    {
        var instance = await RequireInstanceAsync(instanceId, ct);
        var creds = await db.Set<CredentialVaultEntry>().AsNoTracking().Where(c => c.ConnectorInstanceId == instanceId).ToListAsync(ct);
        var decrypted = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var c in creds)
        {
            try { decrypted[c.Key] = protector.Unprotect(c.EncryptedValue); }
            catch (System.Security.Cryptography.CryptographicException) { /* undecryptable — omit */ }
        }

        return (instance, new ConnectorContext(instance.ConnectorCode, instance.Endpoint, instance.AuthMode, decrypted));
    }

    private async Task<ConnectorInstance> RequireInstanceAsync(Guid instanceId, CancellationToken ct) =>
        await db.Set<ConnectorInstance>().FirstOrDefaultAsync(i => i.Id == instanceId, ct)
            ?? throw new ConnectorException("Connector instance not found.");

    // SSRF: when the endpoint is an http(s) URL it must not resolve to a private/loopback address.
    private static void ValidateEndpoint(string endpoint)
    {
        if (string.IsNullOrWhiteSpace(endpoint))
        {
            throw new ConnectorException("An endpoint is required.");
        }

        if (Uri.TryCreate(endpoint, UriKind.Absolute, out var uri) &&
            (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps) &&
            UrlImportGuard.Check(endpoint) == UrlBlockReason.PrivateAddress)
        {
            throw new ConnectorException("The endpoint resolves to a private or internal address and is blocked.");
        }
    }
}
