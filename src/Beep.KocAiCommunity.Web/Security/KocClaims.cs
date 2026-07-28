namespace Beep.KocAiCommunity.Web.Security;

/// <summary>Private claim types the Web stores in its own auth cookie.</summary>
public static class KocClaims
{
    /// <summary>
    /// The API access token issued at sign-in. Held in the cookie (encrypted at rest by Data Protection)
    /// so it is available on every render — including inside a Blazor circuit, where no HTTP request
    /// exists to read it from.
    /// </summary>
    public const string ApiAccessToken = "koc:api_token";
}
