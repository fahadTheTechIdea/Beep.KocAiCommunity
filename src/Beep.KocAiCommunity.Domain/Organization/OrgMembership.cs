using Beep.KocAiCommunity.Domain.Common;

namespace Beep.KocAiCommunity.Domain.Organization;

/// <summary>
/// Places a person in an org unit at a position level. A person has exactly one primary
/// membership (their home Team). Leaders additionally lead a higher unit via
/// <see cref="OrgUnit.LeaderUserId"/>.
/// </summary>
public class OrgMembership : AuditableEntity
{
    public string UserId { get; set; } = default!;
    public Guid OrgUnitId { get; set; }
    public PositionLevel PositionLevel { get; set; }
    public bool IsPrimary { get; set; } = true;
    public DateTime FromUtc { get; set; }
    public DateTime? ToUtc { get; set; }
}
