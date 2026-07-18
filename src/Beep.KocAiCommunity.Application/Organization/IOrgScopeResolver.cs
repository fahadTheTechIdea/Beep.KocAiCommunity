namespace Beep.KocAiCommunity.Application.Organization;

/// <summary>The set of org units and people a supervisor may see (their subtree).</summary>
public sealed record OrgScope(IReadOnlyCollection<Guid> OrgUnitIds, IReadOnlyCollection<string> MemberUserIds)
{
    public static readonly OrgScope Empty = new([], []);
}

/// <summary>
/// Resolves the supervisory subtree for read-only rollups. An Employee (leads nothing) resolves
/// to themselves only; a leader resolves to every unit at or below the unit they lead.
/// </summary>
public interface IOrgScopeResolver
{
    Task<OrgScope> ResolveForUserAsync(string userId, CancellationToken ct = default);

    /// <summary>True when <paramref name="orgUnitId"/> is inside the caller's supervisory subtree.</summary>
    Task<bool> IsWithinScopeAsync(string userId, Guid orgUnitId, CancellationToken ct = default);
}
