using Beep.KocAiCommunity.Domain.Common;
using Beep.KocAiCommunity.Domain.Organization;

namespace Beep.KocAiCommunity.Domain.Datasets;

/// <summary>
/// A KOC dataset. Carries org-scoped visibility (who can see it) and an information-security
/// classification. Versions, files, schemas, and profiling are the full Phase 07 build; this is
/// the metadata + visibility core.
/// </summary>
public class Dataset : AuditableEntity
{
    public string Name { get; set; } = default!;
    public string Description { get; set; } = default!;
    public string OwnerUserId { get; set; } = default!;

    public VisibilityScope VisibilityScope { get; set; } = VisibilityScope.Team;
    public Guid VisibilityOrgUnitId { get; set; }

    public KocDataClassification Classification { get; set; } = KocDataClassification.Internal;
    public string Domain { get; set; } = "upstream";
    public string? Tags { get; set; }

    /// <summary>SPDX license expression (e.g. "CC-BY-4.0"), if declared.</summary>
    public string? LicenseSpdxId { get; set; }

    /// <summary>The highest version number issued (draft or published).</summary>
    public int LatestVersionNumber { get; set; }

    public Guid? FileArtifactId { get; set; }
}
