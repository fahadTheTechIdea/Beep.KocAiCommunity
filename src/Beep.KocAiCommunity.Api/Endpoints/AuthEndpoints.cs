using Beep.KocAiCommunity.Api.Security;
using Beep.KocAiCommunity.Application.Security;
using Beep.KocAiCommunity.Contracts.Identity;
using Beep.KocAiCommunity.Infrastructure.Identity;
using Beep.KocAiCommunity.ServiceDefaults.Security;
using Microsoft.AspNetCore.RateLimiting;

namespace Beep.KocAiCommunity.Api.Endpoints;

/// <summary>
/// Registration and password sign-in for the <see cref="KocAuthMode.LocalAccounts"/> mode. Mapped only
/// in that mode — with intranet SSO or Entra there is no password for this app to hold, so the endpoints
/// don't exist rather than sitting there returning errors.
/// <para>
/// These are the only anonymous write endpoints in the API, so they carry their own tighter rate limit:
/// password guessing shouldn't get the global 1000-requests-a-minute budget.
/// </para>
/// </summary>
public static class AuthEndpoints
{
    /// <summary>Rate-limit policy name for the sign-in endpoints.</summary>
    public const string RateLimitPolicy = "auth";

    public static RouteGroupBuilder MapAuthEndpoints(this RouteGroupBuilder group)
    {
        group.MapGet("/auth/state", async (LocalAccountService accounts, CancellationToken ct) =>
            Results.Ok(new RegistrationStateResponse(true, await accounts.IsFirstAccountAsync(ct))))
        .WithName("RegistrationState")
        .AllowAnonymous();

        group.MapPost("/auth/register", async (
            RegisterRequest request, LocalAccountService accounts, AccessTokenIssuer tokens, CancellationToken ct) =>
        {
            var result = await accounts.RegisterAsync(request.Email, request.Password, request.DisplayName, ct);
            return result.Succeeded
                ? Results.Ok(Token(tokens, result))
                : Results.BadRequest(new { error = result.Error });
        })
        .WithName("Register")
        .AllowAnonymous()
        .RequireRateLimiting(RateLimitPolicy);

        group.MapPost("/auth/login", async (
            LoginRequest request, LocalAccountService accounts, AccessTokenIssuer tokens, CancellationToken ct) =>
        {
            var result = await accounts.SignInAsync(request.Email, request.Password, ct);
            return result.Succeeded
                ? Results.Ok(Token(tokens, result))
                : Results.Json(new { error = result.Error }, statusCode: StatusCodes.Status401Unauthorized);
        })
        .WithName("Login")
        .AllowAnonymous()
        .RequireRateLimiting(RateLimitPolicy);

        // Inside KOC, IIS has already verified the Windows account by the time the Web sees the request.
        // The Web vouches for that person here and receives a platform token, so the API keeps one way of
        // authenticating a caller no matter how they originally signed in. The vouching is what gets
        // checked: an HMAC proving the caller holds the key both processes share (see IdentityExchange).
        group.MapPost("/auth/exchange", async (
            ExchangeIdentityRequest request, HttpContext http, KocSetupStore setup,
            IKocUserDirectory directory, AccessTokenIssuer tokens, CancellationToken ct) =>
        {
            var userId = (request.UserId ?? string.Empty).Trim();
            if (userId.Length == 0)
            {
                return Results.BadRequest(new { error = "A user id is required." });
            }

            if (!IdentityExchange.IsValid(
                    userId,
                    http.Request.Headers[IdentityExchange.TimestampHeader],
                    http.Request.Headers[IdentityExchange.SignatureHeader],
                    setup.Current.TokenSigningKey,
                    DateTimeOffset.UtcNow))
            {
                return Results.Json(new { error = "This caller may not vouch for a user." },
                    statusCode: StatusCodes.Status401Unauthorized);
            }

            var roles = await directory.EnsureUserAsync(userId, request.DisplayName, request.Email, ct);
            var displayName = string.IsNullOrWhiteSpace(request.DisplayName)
                ? KocUserDirectory.DisplayNameFor(null, request.Email ?? userId)
                : request.DisplayName!;
            var (token, expires) = tokens.Issue(userId, displayName, roles);
            return Results.Ok(new AuthTokenResponse(token, expires, userId, displayName, roles));
        })
        .WithName("ExchangeIdentity")
        .AllowAnonymous()
        .RequireRateLimiting(RateLimitPolicy);

        return group;
    }

    private static AuthTokenResponse Token(AccessTokenIssuer tokens, AccountResult result)
    {
        var (token, expires) = tokens.Issue(result.UserId!, result.DisplayName!, result.Roles);
        return new AuthTokenResponse(token, expires, result.UserId!, result.DisplayName!, result.Roles);
    }
}
