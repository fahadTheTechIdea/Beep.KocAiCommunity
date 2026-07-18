namespace Beep.KocAiCommunity.Contracts.Datasets;

/// <summary>Create a dataset. Scope is Team/Group/Directorate/Company; classification is the KOC label.</summary>
public sealed record CreateDatasetRequest(
    string Name,
    string Description,
    string Scope,
    string Classification,
    string Domain,
    string? Tags);

public sealed record DatasetDto(
    Guid Id,
    string Name,
    string Description,
    string Scope,
    string Classification,
    string Domain,
    string OwnerUserId,
    bool HasFile);

/// <summary>A visibility option for the "who can see this" picker, with a live audience count.</summary>
public sealed record VisibilityOptionDto(string Scope, Guid? OrgUnitId, string OrgUnitName, int UserCount);
