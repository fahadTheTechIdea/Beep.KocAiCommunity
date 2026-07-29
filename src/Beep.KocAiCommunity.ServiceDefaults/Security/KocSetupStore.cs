using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Configuration;

namespace Beep.KocAiCommunity.ServiceDefaults.Security;

/// <summary>
/// The first run's single answer, persisted so the Web and the API — separate processes — agree on where
/// people sign in. Deliberately tiny: anything that can be decided once the site is running belongs in
/// the site's admin settings, not here.
/// </summary>
public sealed record KocSetupState
{
    /// <summary>Schema version of this file, so a future release can migrate it knowingly.</summary>
    public int Version { get; init; } = 2;

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public KocSignInSource SignInWith { get; init; } = KocSignInSource.Unconfigured;

    /// <summary>When setup was completed (null while unconfigured).</summary>
    public DateTime? CompletedUtc { get; init; }

    /// <summary>
    /// The key the API signs its access tokens with, base64 — and the shared secret the Web proves itself
    /// with when exchanging a corporate identity for one. Not a choice; generated at setup. The two
    /// processes must agree or every API call is rejected.
    /// </summary>
    public string? TokenSigningKey { get; init; }

    public bool IsConfigured => SignInWith != KocSignInSource.Unconfigured;
}

/// <summary>
/// Reads and writes the first-run answer. The path comes from <c>Setup:File</c> when set; otherwise it
/// lands in the machine's local application data under <c>KocAiCommunity</c>, which the Web and API both
/// resolve to the same place when they run on one host.
/// <para>
/// Configuration still wins: a deployment that sets <c>Auth:SignInWith</c> (or the legacy <c>AzureAd</c> /
/// <c>WindowsAuth</c> keys) is already configured and never sees the wizard. That keeps existing
/// appsettings-driven environments — including the test host — working untouched.
/// </para>
/// </summary>
public sealed class KocSetupStore(IConfiguration configuration)
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { WriteIndented = true };
    private static readonly object WriteLock = new();

    private KocSetupState? _cached;

    /// <summary>Where the setup file lives for this host.</summary>
    public string FilePath { get; } = ResolvePath(configuration);

    public KocSetupState Current => _cached ??= WithConfiguredKey(Load());

    /// <summary>
    /// A signing key given in configuration always wins. Deployments that inject it (and the test host)
    /// must not depend on a file having been written first — without this the two processes would each
    /// invent their own key and reject every token the other issued.
    /// </summary>
    private KocSetupState WithConfiguredKey(KocSetupState state) =>
        configuration["Auth:TokenSigningKey"] is { Length: > 0 } key ? state with { TokenSigningKey = key } : state;

    /// <summary>Re-reads from disk — used after the wizard writes, so the same process sees the change.</summary>
    public KocSetupState Reload()
    {
        _cached = null;
        return Current;
    }

    public bool IsConfigured => Current.IsConfigured;

    public KocSignInSource SignInWith => Current.SignInWith;

    /// <summary>
    /// Demo personas: no real authentication, the visitor picks who to be. A development and
    /// demonstration convenience set in configuration — never a first-run choice, and refused in
    /// Production. Roles still come from the database like everyone else's.
    /// </summary>
    public bool DemoPersonasEnabled =>
        configuration.GetValue("Auth:DemoPersonas", false) || configuration.GetValue("DevAuth:Enabled", false);

    /// <summary>Persists the wizard's answer, generating the signing key the two processes share.</summary>
    public KocSetupState Save(KocSetupState state)
    {
        var completed = state with
        {
            Version = 2,
            CompletedUtc = state.CompletedUtc ?? DateTime.UtcNow,
            TokenSigningKey = string.IsNullOrWhiteSpace(state.TokenSigningKey) ? NewSigningKey() : state.TokenSigningKey,
        };

        lock (WriteLock)
        {
            var directory = Path.GetDirectoryName(FilePath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.WriteAllText(FilePath, JsonSerializer.Serialize(completed, Json));
        }

        _cached = completed;
        return completed;
    }

    /// <summary>A 256-bit key, base64 — enough for HMAC-SHA256 tokens and the identity exchange.</summary>
    public static string NewSigningKey() => Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));

    private KocSetupState Load()
    {
        // 1. An explicit answer in configuration is authoritative and skips the wizard entirely.
        if (Enum.TryParse<KocSignInSource>(configuration["Auth:SignInWith"], ignoreCase: true, out var configured)
            && configured != KocSignInSource.Unconfigured)
        {
            return FromConfiguration(configured);
        }

        // 2. Keys from before the wizard existed mean this deployment was already configured.
        if (SecurityExtensions.IsEntraConfigured(configuration) || SecurityExtensions.IsWindowsAuthEnabled(configuration))
        {
            return FromConfiguration(KocSignInSource.KocEnvironment);
        }

        // 3. Otherwise the setup file decides — and its absence means "ask".
        try
        {
            if (File.Exists(FilePath)
                && JsonSerializer.Deserialize<KocSetupState>(File.ReadAllText(FilePath), Json) is { } saved)
            {
                return saved;
            }
        }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException)
        {
            // An unreadable or corrupt file must not take the app down: fall through to the wizard,
            // which rewrites it.
        }

        return new KocSetupState();
    }

    private KocSetupState FromConfiguration(KocSignInSource source) => new()
    {
        SignInWith = source,
        CompletedUtc = null,
        TokenSigningKey = configuration["Auth:TokenSigningKey"],
    };

    private static string ResolvePath(IConfiguration configuration)
    {
        if (configuration["Setup:File"] is { Length: > 0 } configured)
        {
            return Path.GetFullPath(configured);
        }

        var root = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        if (string.IsNullOrEmpty(root))
        {
            root = Path.GetTempPath();
        }

        return Path.Combine(root, "KocAiCommunity", "setup.json");
    }
}
