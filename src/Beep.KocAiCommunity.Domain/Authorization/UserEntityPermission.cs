namespace Beep.KocAiCommunity.Domain.Authorization;

/// <summary>
/// A per-user grant of a permission key on a specific resource instance. Defense-in-depth
/// on top of coarse position/function roles and org-scoped visibility. Expired rows are
/// filtered at query time.
/// </summary>
public class UserEntityPermission
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string UserId { get; set; } = default!;
    public string ResourceType { get; set; } = default!;   // "project", "dataset", "workflow", "competition", ...
    public Guid ResourceId { get; set; }
    public string PermissionKey { get; set; } = default!;   // e.g. "project.write"
    public DateTime GrantedUtc { get; set; }
    public string GrantedByUserId { get; set; } = default!;
    public DateTime? ExpiresUtc { get; set; }

    public bool IsActive(DateTime utcNow) => ExpiresUtc is null || ExpiresUtc > utcNow;
}
