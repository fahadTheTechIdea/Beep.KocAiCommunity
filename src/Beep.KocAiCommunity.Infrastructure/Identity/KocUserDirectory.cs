using Beep.KocAiCommunity.Application.Security;
using Beep.KocAiCommunity.Domain.Engagement;
using Beep.KocAiCommunity.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Beep.KocAiCommunity.Infrastructure.Identity;

/// <summary>
/// The app's user and role store, shared by every sign-in mode.
/// <para>
/// It is built on the ASP.NET Identity tables because they are already part of the schema and give us a
/// tested role store — but the account row here means "a person this platform knows", not "a person with
/// a password". A locally registered user also has a password hash; someone arriving from the corporate
/// intranet or Entra gets a row keyed by <em>their own</em> id (the Windows account or the Entra object
/// id) and no credentials, because the identity provider holds those. Either way the roles, the profile,
/// the org placement and the grants all live in this database, so authorization behaves identically
/// however the person got here.
/// </para>
/// </summary>
public sealed class KocUserDirectory(
    UserManager<IdentityUser> users,
    RoleManager<IdentityRole> roles,
    KocDbContext db,
    ILogger<KocUserDirectory> logger) : IKocUserDirectory
{
    /// <summary>Roles granted to the first person to appear on an empty installation.</summary>
    public static readonly string[] FirstAccountRoles = [KocRoles.Manager, KocRoles.PlatformAdmin];

    /// <summary>Roles granted to everyone after the first.</summary>
    public static readonly string[] DefaultRoles = [KocRoles.Employee];

    /// <summary>Every role name an administrator may assign.</summary>
    public static readonly string[] AssignableRoles =
        [.. KocRoles.AllPositions, KocRoles.PlatformAdmin, KocRoles.CompetitionAdmin, KocRoles.LearningAdmin, KocRoles.Auditor];

    public Task<bool> IsEmptyAsync(CancellationToken ct = default) => IsEmptyCoreAsync(ct);

    public async Task<IReadOnlyList<string>> EnsureUserAsync(string userId, string? displayName, string? email, CancellationToken ct = default)
    {
        var user = await users.FindByIdAsync(userId);
        if (user is not null)
        {
            return [.. await users.GetRolesAsync(user)];
        }

        // Decided before the insert: whoever arrives first on an empty install administers it.
        var isFirst = await IsEmptyCoreAsync(ct);

        // The id is the caller's own identifier (Entra object id, DOMAIN\user, …) so every other table
        // keyed by UserId — profiles, memberships, submissions, XP — lines up without a mapping step.
        user = new IdentityUser { Id = userId, UserName = userId, Email = email };
        var created = await users.CreateAsync(user);
        if (!created.Succeeded)
        {
            // Provisioning must never block sign-in; the person is simply unknown until it succeeds.
            logger.LogWarning("Could not record user {UserId}: {Errors}", userId,
                string.Join("; ", created.Errors.Select(e => e.Description)));
            return [];
        }

        var granted = isFirst ? FirstAccountRoles : DefaultRoles;
        await AssignAsync(user, granted);
        await EnsureProfileAsync(userId, DisplayNameFor(displayName, email ?? userId), email, ct);

        logger.LogInformation("Recorded {UserId}{First}.", userId, isFirst ? " as the first account (Platform Admin)" : string.Empty);
        return granted;
    }

    public async Task<IReadOnlyList<string>> GetRolesAsync(string userId, CancellationToken ct = default)
    {
        var user = await users.FindByIdAsync(userId);
        return user is null ? [] : [.. await users.GetRolesAsync(user)];
    }

    public async Task SetRolesAsync(string userId, IReadOnlyList<string> requested, CancellationToken ct = default)
    {
        var unknown = requested.Where(r => !AssignableRoles.Contains(r, StringComparer.OrdinalIgnoreCase)).ToList();
        if (unknown.Count > 0)
        {
            throw new InvalidOperationException($"Unknown role(s): {string.Join(", ", unknown)}.");
        }

        // An admin can assign roles to someone who has never signed in (pre-provisioning a colleague),
        // so create the row rather than failing.
        var user = await users.FindByIdAsync(userId);
        if (user is null)
        {
            await EnsureUserAsync(userId, null, null, ct);
            user = await users.FindByIdAsync(userId);
            if (user is null)
            {
                throw new InvalidOperationException($"Could not record user '{userId}'.");
            }
        }

        var current = await users.GetRolesAsync(user);
        var removed = current.Except(requested, StringComparer.OrdinalIgnoreCase).ToList();
        if (removed.Count > 0)
        {
            await users.RemoveFromRolesAsync(user, removed);
        }

        await AssignAsync(user, [.. requested.Except(current, StringComparer.OrdinalIgnoreCase)]);
    }

    private async Task<bool> IsEmptyCoreAsync(CancellationToken ct) => !await users.Users.AnyAsync(ct);

    private async Task AssignAsync(IdentityUser user, IReadOnlyList<string> granted)
    {
        foreach (var role in granted)
        {
            if (!await roles.RoleExistsAsync(role))
            {
                await roles.CreateAsync(new IdentityRole(role));
            }
        }

        if (granted.Count > 0)
        {
            await users.AddToRolesAsync(user, granted);
        }
    }

    /// <summary>A member row so a new person has a name, avatar, and XP ledger like everyone else.</summary>
    private async Task EnsureProfileAsync(string userId, string displayName, string? email, CancellationToken ct)
    {
        if (await db.Set<UserProfile>().AnyAsync(p => p.UserId == userId, ct))
        {
            return;
        }

        db.Set<UserProfile>().Add(new UserProfile
        {
            UserId = userId,
            DisplayName = displayName,
            Email = email,
            CreatedByUserId = userId,
            CreatedUtc = DateTime.UtcNow,
        });
        await db.SaveChangesAsync(ct);
    }

    /// <summary>"fahad.aldhubaib@koc.com" → "Fahad Aldhubaib"; "KOC\aldhubaib" → "aldhubaib".</summary>
    public static string DisplayNameFor(string? displayName, string identifier)
    {
        if (!string.IsNullOrWhiteSpace(displayName))
        {
            return displayName.Trim();
        }

        var local = identifier.Split('@')[0];
        local = local[(local.LastIndexOf('\\') + 1)..];
        var words = local.Split(['.', '_', '-'], StringSplitOptions.RemoveEmptyEntries)
            .Select(w => w.Length == 1 ? w.ToUpperInvariant() : char.ToUpperInvariant(w[0]) + w[1..]);
        var name = string.Join(' ', words);
        return string.IsNullOrWhiteSpace(name) ? identifier : name;
    }
}
