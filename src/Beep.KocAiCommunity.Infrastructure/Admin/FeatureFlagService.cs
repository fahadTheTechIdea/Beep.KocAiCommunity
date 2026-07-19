using System.Security.Cryptography;
using System.Text;
using Beep.KocAiCommunity.Application.Admin;
using Beep.KocAiCommunity.Application.Audit;
using Beep.KocAiCommunity.Domain.Admin;
using Beep.KocAiCommunity.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Beep.KocAiCommunity.Infrastructure.Admin;

/// <summary>Feature flags with a stable-hash rollout so a user is consistently in or out of a bucket.</summary>
public sealed class FeatureFlagService(KocDbContext db, IAuditEnvelope audit) : IFeatureFlagService
{
    public async Task<IReadOnlyList<FeatureFlag>> ListAsync(CancellationToken ct = default) =>
        await db.Set<FeatureFlag>().AsNoTracking().OrderBy(f => f.Key).ToListAsync(ct);

    public async Task<FeatureFlag> UpsertAsync(string actorUserId, string key, string name, string description, bool isEnabled, int rolloutPercentage, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            throw new AdminException("A flag key is required.");
        }

        var rollout = Math.Clamp(rolloutPercentage, 0, 100);
        var flag = await db.Set<FeatureFlag>().FirstOrDefaultAsync(f => f.Key == key, ct);
        var before = flag is null ? null : $"{{\"enabled\":{flag.IsEnabled.ToString().ToLowerInvariant()},\"rollout\":{flag.RolloutPercentage}}}";

        if (flag is null)
        {
            flag = new FeatureFlag
            {
                Key = key.Trim(),
                Name = string.IsNullOrWhiteSpace(name) ? key.Trim() : name.Trim(),
                Description = description ?? string.Empty,
                IsEnabled = isEnabled,
                RolloutPercentage = rollout,
                UpdatedByUserId = actorUserId,
                UpdatedUtc = DateTime.UtcNow,
                CreatedByUserId = actorUserId,
                CreatedUtc = DateTime.UtcNow,
            };
            db.Add(flag);
        }
        else
        {
            flag.Name = string.IsNullOrWhiteSpace(name) ? flag.Name : name.Trim();
            flag.Description = description ?? flag.Description;
            flag.IsEnabled = isEnabled;
            flag.RolloutPercentage = rollout;
            flag.UpdatedByUserId = actorUserId;
            flag.UpdatedUtc = DateTime.UtcNow;
        }

        await db.SaveChangesAsync(ct);
        await audit.WriteAsync(new AuditEntry("feature-flag.update", "feature-flag", flag.Key,
            before, $"{{\"enabled\":{flag.IsEnabled.ToString().ToLowerInvariant()},\"rollout\":{flag.RolloutPercentage}}}"), ct);
        return flag;
    }

    public async Task<bool> IsEnabledAsync(string key, string userId, CancellationToken ct = default)
    {
        var flag = await db.Set<FeatureFlag>().AsNoTracking().FirstOrDefaultAsync(f => f.Key == key, ct);
        if (flag is null || !flag.IsEnabled)
        {
            return false;
        }

        if (flag.RolloutPercentage >= 100)
        {
            return true;
        }

        if (flag.RolloutPercentage <= 0)
        {
            return false;
        }

        return Bucket(key, userId) < flag.RolloutPercentage;
    }

    // A deterministic 0..99 bucket from the flag key + user id.
    private static int Bucket(string key, string userId)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes($"{key}:{userId}"));
        var value = (uint)(hash[0] << 24 | hash[1] << 16 | hash[2] << 8 | hash[3]);
        return (int)(value % 100);
    }
}
