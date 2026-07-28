using Beep.KocAiCommunity.Api.Security;
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

        return group;
    }

    private static AuthTokenResponse Token(AccessTokenIssuer tokens, AccountResult result)
    {
        var (token, expires) = tokens.Issue(result.UserId!, result.DisplayName!, result.Roles);
        return new AuthTokenResponse(token, expires, result.UserId!, result.DisplayName!, result.Roles);
    }
}
