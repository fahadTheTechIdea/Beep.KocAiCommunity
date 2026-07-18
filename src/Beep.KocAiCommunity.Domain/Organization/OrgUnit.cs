using Beep.KocAiCommunity.Domain.Common;

namespace Beep.KocAiCommunity.Domain.Organization;

/// <summary>
/// A node in the KOC org tree: Company (root) → Directorate → Group → Team.
/// <see cref="Path"/> is a materialized path (e.g. "/koc/exploration/subsurface/reservoir-analytics")
/// so supervisory and visibility subtrees resolve with a single prefix scan.
/// </summary>
public class OrgUnit : AuditableEntity
{
    public string Name { get; set; } = default!;
    public OrgUnitType Type { get; set; }

    /// <summary>Null only for the single Company root.</summary>
    public Guid? ParentId { get; set; }

    /// <summary>Materialized path, always starting and delimited by '/'. Lower-cased slugs.</summary>
    public string Path { get; set; } = "/";

    /// <summary>The Entra user id of the person who leads this unit (TeamLeader/Manager/DCEO/CEO).</summary>
    public string? LeaderUserId { get; set; }

    /// <summary>True when <paramref name="other"/> is at or below this unit in the tree.</summary>
    public bool Contains(OrgUnit other) => Contains(other.Path);

    /// <summary>True when <paramref name="otherPath"/> is at or below this unit's path.</summary>
    public bool Contains(string otherPath) =>
        otherPath == Path || otherPath.StartsWith(EnsureTrailingSlash(Path), StringComparison.Ordinal);

    private static string EnsureTrailingSlash(string path) =>
        path.EndsWith('/') ? path : path + "/";
}
