using Beep.KocAiCommunity.Domain.Common;
using Beep.KocAiCommunity.Domain.Organization;

namespace Beep.KocAiCommunity.Domain.Engagement;

/// <summary>
/// A KOC employee's community profile: their public face on the platform (avatar, bio, skills) and
/// their earned standing (Barrels, level, streak). Created lazily on first engagement.
/// </summary>
public class UserProfile : AuditableEntity
{
    public string UserId { get; set; } = default!;              // Entra oid — same key style as OrgMembership
    public string DisplayName { get; set; } = default!;

    // ---- Identity & org placement (managed in the admin RBAC console) ----

    /// <summary>Work email. Unique when set (enforced by the admin service).</summary>
    public string? Email { get; set; }

    /// <summary>The Company-root <see cref="OrgUnit.Code"/> this user belongs to, e.g. "KOC".</summary>
    public string? CompanyId { get; set; }

    /// <summary>The <see cref="OrgUnit.Code"/> of the exact unit (department/team) this user belongs to, e.g. "AX01".</summary>
    public string? DepartmentId { get; set; }

    /// <summary>FK to the exact <see cref="OrgUnit"/> this user belongs to — the authoritative org pointer.</summary>
    public Guid? OrgUnitId { get; set; }

    public string? Bio { get; set; }                            // max 280
    public string AvatarIcon { get; set; } = "185-worker.png";  // file name resolved via KocBrand.Icon()
    public string? SkillsCsv { get; set; }                      // "ML.NET,Python,Reservoir"

    /// <summary>Rollup of the <see cref="XpEvent"/> ledger; the ledger is authoritative.</summary>
    public int XpTotal { get; set; }
    public int Level { get; set; } = 1;

    public int CurrentStreakDays { get; set; }
    public int LongestStreakDays { get; set; }
    public DateOnly? LastActiveDate { get; set; }
}

/// <summary>An append-only Barrels (bbl) ledger row. Idempotent per (UserId, Source, RefId).</summary>
public class XpEvent : AuditableEntity
{
    public string UserId { get; set; } = default!;
    public string Source { get; set; } = default!;              // "lesson.completed", "submission.scored", …
    public int Points { get; set; }
    public string? RefType { get; set; }                        // "lesson", "submission", "discussion", "kudos"
    public Guid? RefId { get; set; }
}

/// <summary>A seeded badge-catalog row. Icon art is a file in the shared O&amp;G icon library.</summary>
public class Badge : AuditableEntity
{
    public string Code { get; set; } = default!;               // "first-submission"
    public string Name { get; set; } = default!;
    public string Description { get; set; } = default!;
    public string IconFile { get; set; } = default!;           // "179-exploration.png"
    public string Tier { get; set; } = "bronze";               // bronze, silver, gold
}

/// <summary>A badge a user has earned. Idempotent per (UserId, BadgeCode).</summary>
public class UserBadge : AuditableEntity
{
    public string UserId { get; set; } = default!;
    public string BadgeCode { get; set; } = default!;
    public Guid? RefId { get; set; }
}

/// <summary>Peer-to-peer recognition. Both sides must be KOC employees.</summary>
public class Kudos : AuditableEntity
{
    public string FromUserId { get; set; } = default!;
    public string ToUserId { get; set; } = default!;
    public string Message { get; set; } = default!;            // max 200
    public string Emoji { get; set; } = "👏";                  // curated set: 👏 🚀 🛢️ 🌟 🤝
    public string? RefType { get; set; }                       // optional link: "submission", "discussion"
    public Guid? RefId { get; set; }
}

/// <summary>An org-scoped activity-feed row, written alongside domain actions.</summary>
public class ActivityEvent : AuditableEntity
{
    public string ActorUserId { get; set; } = default!;
    public string Type { get; set; } = default!;               // "badge.earned", "level.up", "kudos.received", …
    public string? RefType { get; set; }
    public Guid? RefId { get; set; }
    public string? PayloadJson { get; set; }

    public VisibilityScope VisibilityScope { get; set; } = VisibilityScope.Team;
    public Guid VisibilityOrgUnitId { get; set; }
}
