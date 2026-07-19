using Beep.KocAiCommunity.Application.Admin;
using Beep.KocAiCommunity.Application.Audit;
using Beep.KocAiCommunity.Domain.Admin;
using Beep.KocAiCommunity.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Beep.KocAiCommunity.Infrastructure.Admin;

/// <summary>
/// Typed settings backed by the code-first <see cref="SettingsCatalog"/>. Secrets are encrypted at
/// rest via <see cref="ISecretProtector"/> and are masked everywhere they surface — including audit
/// JSON. Every change is audited (masked) and bumps the value's version.
/// </summary>
public sealed class SettingsService(KocDbContext db, IAuditEnvelope audit, ISecretProtector protector) : ISettingsService
{
    private const string Mask = "••••••";
    private readonly KocDbContext _db = db;
    private readonly IAuditEnvelope _audit = audit;
    private readonly ISecretProtector _protector = protector;

    public async Task<IReadOnlyList<SettingView>> GetAllAsync(CancellationToken ct = default)
    {
        var values = await _db.Set<SettingValue>().AsNoTracking().ToDictionaryAsync(v => v.Key, v => v, StringComparer.OrdinalIgnoreCase, ct);

        return SettingsCatalog.All.Select(def =>
        {
            values.TryGetValue(def.Key, out var row);
            var display = Display(def, row);
            return new SettingView(def.Key, def.Category, def.DisplayName, def.Description, def.IsSecret,
                display, row is not null, row?.Version ?? 0, row?.UpdatedUtc, row?.UpdatedByUserId);
        }).ToList();
    }

    public async Task<SettingView> SetAsync(string actorUserId, string key, string value, CancellationToken ct = default)
    {
        var def = SettingsCatalog.Find(key) ?? throw new AdminException($"Unknown setting '{key}'.");
        value ??= string.Empty;

        var row = await _db.Set<SettingValue>().FirstOrDefaultAsync(v => v.Key == def.Key, ct);
        var before = row is null ? null : Display(def, row);
        var stored = def.IsSecret && value.Length > 0 ? _protector.Protect(value) : value;

        if (row is null)
        {
            row = new SettingValue
            {
                Key = def.Key,
                Value = stored,
                IsSecret = def.IsSecret,
                Version = 1,
                UpdatedByUserId = actorUserId,
                UpdatedUtc = DateTime.UtcNow,
                CreatedByUserId = actorUserId,
                CreatedUtc = DateTime.UtcNow,
            };
            _db.Add(row);
        }
        else
        {
            row.Value = stored;
            row.IsSecret = def.IsSecret;
            row.Version++;
            row.UpdatedByUserId = actorUserId;
            row.UpdatedUtc = DateTime.UtcNow;
        }

        await _db.SaveChangesAsync(ct);

        // Audit with masked values — a secret's plaintext must never reach the audit log.
        var after = Display(def, row);
        await _audit.WriteAsync(new AuditEntry("setting.update", "setting", def.Key,
            before is null ? null : $"{{\"value\":\"{before}\"}}", $"{{\"value\":\"{after}\",\"version\":{row.Version}}}"), ct);

        return new SettingView(def.Key, def.Category, def.DisplayName, def.Description, def.IsSecret,
            after, true, row.Version, row.UpdatedUtc, row.UpdatedByUserId);
    }

    public async Task<string> GetEffectiveValueAsync(string key, CancellationToken ct = default)
    {
        var def = SettingsCatalog.Find(key) ?? throw new AdminException($"Unknown setting '{key}'.");
        var row = await _db.Set<SettingValue>().AsNoTracking().FirstOrDefaultAsync(v => v.Key == def.Key, ct);
        if (row is null)
        {
            return def.DefaultValue;
        }

        if (!def.IsSecret || string.IsNullOrEmpty(row.Value))
        {
            return row.Value;
        }

        try
        {
            return _protector.Unprotect(row.Value);
        }
        catch (System.Security.Cryptography.CryptographicException)
        {
            return string.Empty; // key rotated / undecryptable — fail closed
        }
    }

    // What a caller is allowed to see: secrets are masked (or empty when unset), others show the value.
    private static string Display(SettingDefinition def, SettingValue? row)
    {
        if (def.IsSecret)
        {
            return row is not null && !string.IsNullOrEmpty(row.Value) ? Mask : string.Empty;
        }

        return row?.Value ?? def.DefaultValue;
    }
}
