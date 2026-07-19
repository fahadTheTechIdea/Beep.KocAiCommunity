using Beep.KocAiCommunity.Domain.Common;

namespace Beep.KocAiCommunity.Domain.Admin;

/// <summary>
/// The persisted state of one platform setting. The <em>behaviour</em> (key, type, category, whether
/// it's a secret) is code-first in the settings catalog; this row only holds the current value and its
/// revision. Secret values are stored encrypted in <see cref="Value"/>.
/// </summary>
public class SettingValue : AuditableEntity
{
    public string Key { get; set; } = default!;
    public string Value { get; set; } = default!;   // plaintext for normal settings; ciphertext for secrets
    public bool IsSecret { get; set; }
    public int Version { get; set; } = 1;
    public string UpdatedByUserId { get; set; } = default!;
    public DateTime UpdatedUtc { get; set; }
}

/// <summary>
/// A feature flag: a boolean plus an optional rollout percentage. When enabled with a rollout below
/// 100, membership is decided by a stable hash of the user id so a given user is consistently in or out.
/// </summary>
public class FeatureFlag : AuditableEntity
{
    public string Key { get; set; } = default!;
    public string Name { get; set; } = default!;
    public string Description { get; set; } = default!;
    public bool IsEnabled { get; set; }
    public int RolloutPercentage { get; set; } = 100;   // 0..100; applies only when IsEnabled
    public string UpdatedByUserId { get; set; } = default!;
    public DateTime UpdatedUtc { get; set; }
}
