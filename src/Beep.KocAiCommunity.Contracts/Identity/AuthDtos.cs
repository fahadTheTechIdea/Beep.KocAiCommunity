namespace Beep.KocAiCommunity.Contracts.Identity;

/// <summary>Create an account on this site (the LocalAccounts sign-in mode).</summary>
public sealed record RegisterRequest(string Email, string Password, string? DisplayName = null);

/// <summary>Sign in with an email and password.</summary>
public sealed record LoginRequest(string Email, string Password);

/// <summary>
/// A successful sign-in: the access token the caller sends on subsequent API requests, when it expires,
/// and who the token says you are (so the Web can build its cookie without a second round trip).
/// </summary>
public sealed record AuthTokenResponse(
    string AccessToken,
    DateTime ExpiresUtc,
    string UserId,
    string DisplayName,
    IReadOnlyList<string> Roles);

/// <summary>Whether this installation still needs a first account, so the UI can say "create the admin account".</summary>
public sealed record RegistrationStateResponse(bool AcceptsRegistration, bool IsFirstAccount);
