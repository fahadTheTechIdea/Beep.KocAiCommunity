using Beep.KocAiCommunity.Domain.Admin;

namespace Beep.KocAiCommunity.Application.Admin;

/// <summary>Raised when an admin action is invalid (unknown key, bad value).</summary>
public sealed class AdminException(string message) : Exception(message);

/// <summary>One setting for display: definition metadata + current value (masked when secret).</summary>
public sealed record SettingView(
    string Key, string Category, string DisplayName, string Description, bool IsSecret,
    string Value, bool IsSet, int Version, DateTime? UpdatedUtc, string? UpdatedByUserId);

/// <summary>
/// Typed platform settings. Values are read against the code-first <see cref="SettingsCatalog"/>;
/// secrets are encrypted at rest and never returned or audited in plaintext. Every change is audited
/// and bumps the value's version.
/// </summary>
public interface ISettingsService
{
    /// <summary>All catalog settings with their current (masked, for secrets) values.</summary>
    Task<IReadOnlyList<SettingView>> GetAllAsync(CancellationToken ct = default);

    /// <summary>Sets a setting's value (encrypting secrets), audits the change, and bumps its version.</summary>
    Task<SettingView> SetAsync(string actorUserId, string key, string value, CancellationToken ct = default);

    /// <summary>The effective value for a key (decrypted for secrets), or the catalog default. Internal use.</summary>
    Task<string> GetEffectiveValueAsync(string key, CancellationToken ct = default);
}

/// <summary>Feature flags with an optional stable-hash rollout.</summary>
public interface IFeatureFlagService
{
    Task<IReadOnlyList<FeatureFlag>> ListAsync(CancellationToken ct = default);
    Task<FeatureFlag> UpsertAsync(string actorUserId, string key, string name, string description, bool isEnabled, int rolloutPercentage, CancellationToken ct = default);

    /// <summary>Whether the flag is on for a user (enabled AND within the rollout bucket for that user).</summary>
    Task<bool> IsEnabledAsync(string key, string userId, CancellationToken ct = default);
}

/// <summary>One audit row for display.</summary>
public sealed record AuditView(
    Guid Id, string ActorUserId, string? ActorRole, string Action, string Resource, string? ResourceId,
    string? BeforeJson, string? AfterJson, DateTime OccurredUtc);

/// <summary>A snapshot for the admin dashboard: platform counts, recent audit, and health.</summary>
public sealed record AdminDashboard(
    int Users, int Workflows, int Competitions, int Models, int Discussions,
    IReadOnlyList<AuditView> RecentAudit, IReadOnlyList<HealthComponent> Health);

/// <summary>One health line for a subsystem.</summary>
public sealed record HealthComponent(string Component, string Status, string Detail);

/// <summary>Read models for the admin dashboard and audit trail.</summary>
public interface IAdminDashboardService
{
    Task<AdminDashboard> GetDashboardAsync(CancellationToken ct = default);
    Task<IReadOnlyList<AuditView>> ListAuditAsync(string? action, string? actorUserId, int take = 100, CancellationToken ct = default);
}
