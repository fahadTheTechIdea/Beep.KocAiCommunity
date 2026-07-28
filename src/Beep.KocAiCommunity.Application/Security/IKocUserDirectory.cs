namespace Beep.KocAiCommunity.Application.Security;

/// <summary>
/// The platform's own record of who a person is and what they may do — independent of how they proved
/// their identity. Sign-in varies by deployment (a password on this site, the corporate intranet
/// account, or Entra); roles, org placement, and every other authorization fact live here, in the app's
/// database, in all of them.
/// </summary>
public interface IKocUserDirectory
{
    /// <summary>
    /// Records a person the first time they appear, whichever way they signed in, and returns the roles
    /// the platform grants them. On an otherwise empty installation the first arrival becomes the
    /// Platform Admin — without that, a freshly deployed site would have nobody able to administer it.
    /// </summary>
    Task<IReadOnlyList<string>> EnsureUserAsync(string userId, string? displayName, string? email, CancellationToken ct = default);

    /// <summary>The roles this user holds, or an empty list if the platform doesn't know them.</summary>
    Task<IReadOnlyList<string>> GetRolesAsync(string userId, CancellationToken ct = default);

    /// <summary>
    /// Replaces a user's roles with exactly <paramref name="roles"/> (from <see cref="KocRoles"/>).
    /// Unknown names are rejected rather than silently dropped.
    /// </summary>
    Task SetRolesAsync(string userId, IReadOnlyList<string> roles, CancellationToken ct = default);

    /// <summary>True when no account exists yet, so the next arrival claims the platform.</summary>
    Task<bool> IsEmptyAsync(CancellationToken ct = default);
}
