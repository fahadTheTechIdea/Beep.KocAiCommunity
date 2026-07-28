using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Configuration;

namespace Beep.KocAiCommunity.ServiceDefaults.Security;

/// <summary>
/// What the first-run wizard decided. Persisted as JSON so the Web and the API — separate processes —
/// boot into the same authentication mode.
/// </summary>
public sealed record KocSetupState
{
    /// <summary>Schema version of this file, so a future release can migrate it knowingly.</summary>
    public int Version { get; init; } = 1;

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public KocAuthMode Mode { get; init; } = KocAuthMode.Unconfigured;

    /// <summary>When setup was completed (null while unconfigured).</summary>
    public DateTime? CompletedUtc { get; init; }

    /// <summary>Who completed setup, when that is known (the first admin's email).</summary>
    public string? CompletedBy { get; init; }

    /// <summary>
    /// The symmetric key the API signs its access tokens with, base64. Generated during setup and shared
    /// with the Web through this file — the two processes must agree or every API call is rejected.
    /// </summary>
    public string? TokenSigningKey { get; init; }

    // ---- Entra ID (only when Mode is EntraId) ----
    public string? EntraTenantId { get; init; }
    public string? EntraClientId { get; init; }
    public string? EntraClientSecret { get; init; }

    public bool IsConfigured => Mode != KocAuthMode.Unconfigured;
}

/// <summary>
/// Reads and writes the first-run setup file. The path comes from <c>Setup:File</c> when set; otherwise
/// it lands in the machine's local application data under <c>KocAiCommunity</c>, which the Web and API
/// both resolve to the same place when they run on one host.
/// <para>
/// Configuration still wins: a deployment that sets <c>Auth:Mode</c> (or the legacy <c>AzureAd</c> /
/// <c>WindowsAuth</c> / <c>DevAuth</c> keys) is already configured and never sees the wizard. That keeps
/// existing appsettings-driven environments — including the test host — working untouched.
/// </para>
/// </summary>
public sealed class KocSetupStore(IConfiguration configuration)
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { WriteIndented = true };
    private static readonly object WriteLock = new();

    private KocSetupState? _cached;

    /// <summary>Where the setup file lives for this host.</summary>
    public string FilePath { get; } = ResolvePath(configuration);

    /// <summary>
    /// The effective setup: explicit configuration first (so appsettings/environment deployments and the
    /// test host bypass the wizard), then the setup file, then "unconfigured".
    /// </summary>
    public KocSetupState Current => _cached ??= Load();

    /// <summary>Re-reads from disk — used after the wizard writes, so the same process sees the change.</summary>
    public KocSetupState Reload()
    {
        _cached = null;
        return Current;
    }

    public bool IsConfigured => Current.IsConfigured;

    public KocAuthMode Mode => Current.Mode;

    /// <summary>Persists the wizard's answers, filling in a fresh signing key when one is needed.</summary>
    public KocSetupState Save(KocSetupState state)
    {
        var completed = state with
        {
            Version = 1,
            CompletedUtc = state.CompletedUtc ?? DateTime.UtcNow,
            TokenSigningKey = state.Mode == KocAuthMode.LocalAccounts
                ? (string.IsNullOrWhiteSpace(state.TokenSigningKey) ? NewSigningKey() : state.TokenSigningKey)
                : state.TokenSigningKey,
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

    /// <summary>A 256-bit signing key, base64 — enough for HMAC-SHA256 access tokens.</summary>
    public static string NewSigningKey() => Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));

    private KocSetupState Load()
    {
        // 1. An explicit mode in configuration is authoritative and skips the wizard entirely.
        if (Enum.TryParse<KocAuthMode>(configuration["Auth:Mode"], ignoreCase: true, out var configured)
            && configured != KocAuthMode.Unconfigured)
        {
            return FromConfiguration(configured);
        }

        // 2. Legacy/explicit keys mean this deployment was configured before the wizard existed.
        if (SecurityExtensions.IsEntraConfigured(configuration))
        {
            return FromConfiguration(KocAuthMode.EntraId);
        }

        if (SecurityExtensions.IsWindowsAuthEnabled(configuration))
        {
            return FromConfiguration(KocAuthMode.WindowsIntranet);
        }

        if (configuration.GetValue("DevAuth:Enabled", false))
        {
            return FromConfiguration(KocAuthMode.DemoPersonas);
        }

        // 3. Otherwise the setup file decides — and its absence means "ask the user".
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

    private KocSetupState FromConfiguration(KocAuthMode mode) => new()
    {
        Mode = mode,
        CompletedUtc = null,
        TokenSigningKey = configuration["Auth:TokenSigningKey"],
        EntraTenantId = configuration["AzureAd:TenantId"],
        EntraClientId = configuration["AzureAd:ClientId"],
        EntraClientSecret = configuration["AzureAd:ClientSecret"],
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
