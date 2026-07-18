namespace Beep.KocAiCommunity.Contracts.Organization;

/// <summary>An org-tree node for browsing and visibility pickers.</summary>
public sealed record OrgUnitDto(Guid Id, string Name, string Type, Guid? ParentId, string Path);

/// <summary>The caller's supervisory subtree summary.</summary>
public sealed record OrgScopeDto(IReadOnlyList<Guid> OrgUnitIds, int MemberCount);

/// <summary>Audience-count preview for a "who can see this" choice.</summary>
public sealed record AudienceDto(string Scope, Guid OrgUnitId, int UserCount);
