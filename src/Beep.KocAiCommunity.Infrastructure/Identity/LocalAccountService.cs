using Beep.KocAiCommunity.Application.Security;
using Beep.KocAiCommunity.Domain.Engagement;
using Beep.KocAiCommunity.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Beep.KocAiCommunity.Infrastructure.Identity;

/// <summary>The outcome of a registration or sign-in attempt.</summary>
public sealed record AccountResult(bool Succeeded, string? UserId, string? DisplayName, IReadOnlyList<string> Roles, string? Error)
{
    public static AccountResult Fail(string error) => new(false, null, null, [], error);

    public static AccountResult Ok(string userId, string displayName, IReadOnlyList<string> roles) =>
        new(true, userId, displayName, roles, null);
}

/// <summary>
/// Passwords — and only passwords — for the <c>LocalAccounts</c> sign-in mode. Everything that isn't a
/// credential (which roles a person holds, their profile, whether they are the first arrival) belongs to
/// <see cref="KocUserDirectory"/>, so it works the same for someone who signed in through the corporate
/// intranet instead.
/// </summary>
public sealed class LocalAccountService(
    UserManager<IdentityUser> users,
    IKocUserDirectory directory,
    KocDbContext db)
{
    /// <summary>True when no account exists yet — the next registration claims the platform.</summary>
    public Task<bool> IsFirstAccountAsync(CancellationToken ct = default) => directory.IsEmptyAsync(ct);

    public async Task<AccountResult> RegisterAsync(string email, string password, string? displayName, CancellationToken ct = default)
    {
        email = (email ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(email))
        {
            return AccountResult.Fail("Enter an email address.");
        }

        if (await users.FindByEmailAsync(email) is not null || await users.FindByNameAsync(email) is not null)
        {
            return AccountResult.Fail("An account with that email already exists. Sign in instead.");
        }

        // The person is recorded first (which decides whether they are the platform's first arrival),
        // then given the password that makes the account usable on a login page.
        var userId = Guid.NewGuid().ToString();
        var granted = await directory.EnsureUserAsync(userId, displayName, email, ct);

        var user = await users.FindByIdAsync(userId);
        if (user is null)
        {
            return AccountResult.Fail("Could not create the account.");
        }

        user.UserName = email;
        var withPassword = await users.AddPasswordAsync(user, password ?? string.Empty);
        if (!withPassword.Succeeded)
        {
            // Roll back the half-made account so the email stays free and the next attempt is clean.
            await users.DeleteAsync(user);
            await RemoveProfileAsync(userId, ct);
            return AccountResult.Fail(string.Join(" ", withPassword.Errors.Select(e => e.Description)));
        }

        await users.UpdateAsync(user);
        return AccountResult.Ok(userId, await DisplayNameAsync(userId, displayName, email, ct), granted);
    }

    public async Task<AccountResult> SignInAsync(string email, string password, CancellationToken ct = default)
    {
        email = (email ?? string.Empty).Trim();
        var user = await users.FindByEmailAsync(email) ?? await users.FindByNameAsync(email);

        // One message for "no such account" and "wrong password" — don't reveal which emails exist.
        if (user is null || !await users.CheckPasswordAsync(user, password ?? string.Empty))
        {
            return AccountResult.Fail("That email and password don't match an account.");
        }

        if (await users.IsLockedOutAsync(user))
        {
            return AccountResult.Fail("This account is temporarily locked after too many failed attempts. Try again later.");
        }

        var granted = await directory.GetRolesAsync(user.Id, ct);
        return AccountResult.Ok(user.Id, await DisplayNameAsync(user.Id, null, user.Email ?? email, ct), granted);
    }

    private async Task<string> DisplayNameAsync(string userId, string? displayName, string email, CancellationToken ct) =>
        await db.Set<UserProfile>().AsNoTracking().Where(p => p.UserId == userId)
            .Select(p => p.DisplayName).FirstOrDefaultAsync(ct)
        ?? KocUserDirectory.DisplayNameFor(displayName, email);

    private async Task RemoveProfileAsync(string userId, CancellationToken ct)
    {
        var profile = await db.Set<UserProfile>().FirstOrDefaultAsync(p => p.UserId == userId, ct);
        if (profile is not null)
        {
            db.Set<UserProfile>().Remove(profile);
            await db.SaveChangesAsync(ct);
        }
    }
}

public static class LocalIdentityRegistration
{
    /// <summary>
    /// The app's user + role store, over the existing <see cref="KocDbContext"/> (already an
    /// <c>IdentityDbContext</c>, so the tables are in place). Registered in <b>every</b> sign-in mode:
    /// authentication may come from the corporate network or Entra, but the platform's own record of
    /// who someone is and what they may do always lives here.
    /// </summary>
    public static IServiceCollection AddKocUserDirectory(this IServiceCollection services)
    {
        services.AddIdentityCore<IdentityUser>(options =>
            {
                options.User.RequireUniqueEmail = false;   // corporate accounts may have no email

                // A corporate account name is whatever the directory calls it — "KOC\aldhubaib" — and the
                // default validator rejects the backslash, which would silently fail to record the person
                // and leave them with no roles. The name is an identifier we are given, not user input.
                options.User.AllowedUserNameCharacters = string.Empty;

                options.Password.RequiredLength = 8;
                options.Password.RequireNonAlphanumeric = false;
                options.Lockout.MaxFailedAccessAttempts = 10;
                options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
            })
            .AddRoles<IdentityRole>()
            .AddEntityFrameworkStores<KocDbContext>();

        services.AddScoped<IKocUserDirectory, KocUserDirectory>();
        return services;
    }

    /// <summary>Password registration and sign-in — only where this app holds the credentials.</summary>
    public static IServiceCollection AddKocLocalAccounts(this IServiceCollection services)
    {
        services.AddScoped<LocalAccountService>();
        return services;
    }
}
