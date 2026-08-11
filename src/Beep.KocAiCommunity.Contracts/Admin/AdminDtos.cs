namespace Beep.KocAiCommunity.Contracts.Admin;

public sealed record SettingDto(
    string Key, string Category, string DisplayName, string Description, bool IsSecret,
    string Value, bool IsSet, int Version, DateTime? UpdatedUtc, string? UpdatedByUserId);

public sealed record UpdateSettingRequest(string Value);

public sealed record FeatureFlagDto(string Key, string Name, string Description, bool IsEnabled, int RolloutPercentage, DateTime UpdatedUtc);

public sealed record UpsertFeatureFlagRequest(string Name, string Description, bool IsEnabled, int RolloutPercentage);

public sealed record AuditLogDto(
    Guid Id, string ActorUserId, string? ActorRole, string Action, string Resource, string? ResourceId,
    string? BeforeJson, string? AfterJson, DateTime OccurredUtc);

public sealed record HealthComponentDto(string Component, string Status, string Detail);

/// <summary>What demo content is currently seeded.</summary>
public sealed record DemoDataStatusDto(bool Seeded, int Users, int Submissions, int Discussions, int Datasets);

public sealed record AdminDashboardDto(
    int Users, int Workflows, int Competitions, int Models, int Discussions,
    IReadOnlyList<AuditLogDto> RecentAudit, IReadOnlyList<HealthComponentDto> Health);
