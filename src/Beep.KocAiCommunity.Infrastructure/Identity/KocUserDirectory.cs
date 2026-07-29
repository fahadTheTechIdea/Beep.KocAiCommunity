using Beep.KocAiCommunity.Application.Security;
using Beep.KocAiCommunity.Domain.Organization;
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
    // Two kinds of role, two different owners — as KocRoles has always documented:
    //
    //   Position (Employee … CEO)  mirrors the reporting line, and comes from the org directory.
    //   Function (PlatformAdmin …) is a platform capability an administrator grants.
    //
    // Only function roles are stored against the account. The position is derived from the user's org
    // membership on every read, so moving someone in the org tree moves their authority with them and
    // there is no second copy to drift.

    /// <summary>Function roles granted to the first person to appear on an empty installation.</summary>
    public static readonly string[] FirstAccountRoles = [KocRoles.PlatformAdmin];

    /// <summary>Function roles granted to everyone after the first — none; the position carries them.</summary>
    public static readonly string[] DefaultRoles = [];

    /// <summary>The function roles an administrator may assign. Positions are not among them.</summary>
    public static readonly string[] AssignableRoles =
        [KocRoles.PlatformAdmin, KocRoles.CompetitionAdmin, KocRoles.LearningAdmin, KocRoles.Auditor];

    public Task<bool> IsEmptyAsync(CancellationToken ct = default) => IsEmptyCoreAsync(ct);

    public async Task<IReadOnlyList<string>> EnsureUserAsync(string userId, string? displayName, string? email, CancellationToken ct = default)
    {
        var user = await users.FindByIdAsync(userId);
        if (user is not null)
        {
            // The common path — every request after the first. It must include the position, or an
            // already-known user would arrive with function roles only and fail every position policy.
            return await GetRolesAsync(userId, ct);
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

        // The position comes from the org directory, so return the effective set rather than only what
        // was just granted — otherwise a first sign-in would report no position at all.
        return [.. granted.Append(await PositionRoleAsync(userId, ct)).Distinct(StringComparer.OrdinalIgnoreCase)];
    }

    public async Task<IReadOnlyList<string>> GetRolesAsync(string userId, CancellationToken ct = default)
    {
        var user = await users.FindByIdAsync(userId);
        if (user is null)
        {
            return [];
        }

        return [.. (await users.GetRolesAsync(user)).Append(await PositionRoleAsync(userId, ct)).Distinct(StringComparer.OrdinalIgnoreCase)];
    }

    /// <summary>
    /// The position role for a user, read from their primary org membership. Everyone gets at least
    /// <see cref="KocRoles.Employee"/> — someone the org directory hasn't placed yet is still a member of
    /// the platform, and without it every authenticated page would refuse them.
    /// </summary>
    private async Task<string> PositionRoleAsync(string userId, CancellationToken ct)
    {
        var position = await db.OrgMemberships.AsNoTracking()
            .Where(m => m.UserId == userId && m.IsPrimary && m.ToUtc == null)
            .Select(m => (PositionLevel?)m.PositionLevel)
            .FirstOrDefaultAsync(ct);

        return position switch
        {
            PositionLevel.CEO => KocRoles.CEO,
            PositionLevel.DCEO => KocRoles.DCEO,
            PositionLevel.Manager => KocRoles.Manager,
            PositionLevel.TeamLeader => KocRoles.TeamLeader,
            _ => KocRoles.Employee,
        };
    }

    public async Task SetRolesAsync(string userId, IReadOnlyList<string> requested, CancellationToken ct = default)
    {
        // A position is not granted here — it follows the person's place in the org directory. Saying so
        // is better than accepting the value and having it quietly overridden on the next read.
        var positions = requested.Where(r => KocRoles.AllPositions.Contains(r, StringComparer.OrdinalIgnoreCase)).ToList();
        if (positions.Count > 0)
        {
            throw new InvalidOperationException(
                $"{string.Join(", ", positions)} is a position level, which comes from the org directory. Set the person's position on their org placement instead.");
        }

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
