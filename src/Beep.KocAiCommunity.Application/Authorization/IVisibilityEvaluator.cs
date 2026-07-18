using Beep.KocAiCommunity.Domain.Organization;

namespace Beep.KocAiCommunity.Application.Authorization;

/// <summary>
/// Decides whether a user may see a competition, dataset, or project given its org-scoped
/// visibility. Company scope is visible to all KOC users; otherwise the user's home unit must
/// be within the visibility unit's subtree.
/// </summary>
public interface IVisibilityEvaluator
{
    Task<bool> CanSeeAsync(string userId, VisibilityScope scope, Guid visibilityOrgUnitId, CancellationToken ct = default);
}
