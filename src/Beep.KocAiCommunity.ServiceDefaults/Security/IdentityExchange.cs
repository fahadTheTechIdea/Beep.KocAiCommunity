using System.Security.Cryptography;
using System.Text;

namespace Beep.KocAiCommunity.ServiceDefaults.Security;

/// <summary>
/// Lets the Web turn an identity the KOC environment already verified into a platform access token.
/// <para>
/// The API never sees the corporate credential — by the time this is called the intranet or Entra has
/// already established who the person is, and the Web is vouching for them. That vouching is what has to
/// be authenticated, so the caller proves it holds the signing key both processes share: an HMAC over
/// the user id and a timestamp. Without that anyone able to reach the API could name themselves.
/// </para>
/// <para>The timestamp bounds replay to a short window; the key never travels.</para>
/// </summary>
public static class IdentityExchange
{
    /// <summary>Header carrying the proof.</summary>
    public const string SignatureHeader = "X-Koc-Exchange";

    /// <summary>Header carrying the moment the proof was made (round-trip UTC).</summary>
    public const string TimestampHeader = "X-Koc-Exchange-At";

    /// <summary>How far apart the two clocks may be before a proof is refused.</summary>
    public static readonly TimeSpan MaxSkew = TimeSpan.FromMinutes(5);

    /// <summary>The proof for a given user id at a given moment.</summary>
    public static string Sign(string userId, DateTimeOffset at, string? signingKeyBase64)
    {
        using var hmac = new HMACSHA256(ServiceDefaults.SecurityExtensions.SigningKey(signingKeyBase64).Key);
        var payload = Encoding.UTF8.GetBytes($"{userId}|{at.ToUnixTimeSeconds()}");
        return Convert.ToBase64String(hmac.ComputeHash(payload));
    }

    /// <summary>True when the proof matches and is recent enough to trust.</summary>
    public static bool IsValid(string userId, string? timestamp, string? signature, string? signingKeyBase64, DateTimeOffset now)
    {
        if (string.IsNullOrWhiteSpace(signature)
            || !long.TryParse(timestamp, out var unix))
        {
            return false;
        }

        var at = DateTimeOffset.FromUnixTimeSeconds(unix);
        if ((now - at).Duration() > MaxSkew)
        {
            return false;
        }

        var expected = Encoding.UTF8.GetBytes(Sign(userId, at, signingKeyBase64));
        var supplied = Encoding.UTF8.GetBytes(signature);

        // Fixed-time comparison: a length or early-byte difference must not be measurable.
        return supplied.Length == expected.Length && CryptographicOperations.FixedTimeEquals(supplied, expected);
    }
}
