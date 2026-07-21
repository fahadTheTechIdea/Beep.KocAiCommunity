using Beep.KocAiCommunity.Domain.Common;
using Beep.KocAiCommunity.Domain.Organization;

namespace Beep.KocAiCommunity.Domain.Authorization;

/// <summary>
/// An admin-granted capability to create competitions, capped at a maximum audience level.
/// A grant of <see cref="MaxScope"/> = Directorate lets the holder create Directorate, Group,
/// or Team competitions (any audience at or narrower than the cap). One grant per user;
/// PlatformAdmins need none (they may always create at any level).
/// </summary>
public class CompetitionCreatorGrant : AuditableEntity
{
    public string UserId { get; set; } = default!;

    /// <summary>The widest audience the holder may target. VisibilityScope is ordered Team(0) … Company(3).</summary>
    public VisibilityScope MaxScope { get; set; }

    public string GrantedByUserId { get; set; } = default!;
    public DateTime? ExpiresUtc { get; set; }

    public bool IsActive(DateTime utcNow) => ExpiresUtc is null || ExpiresUtc > utcNow;
}
