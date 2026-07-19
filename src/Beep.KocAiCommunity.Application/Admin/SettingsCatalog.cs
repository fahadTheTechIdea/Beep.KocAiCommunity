namespace Beep.KocAiCommunity.Application.Admin;

/// <summary>
/// A code-first definition of one platform setting: its identity, category, help text, whether it is a
/// secret, and a default. The database stores only the current value — never the definition — so the
/// set of settings and their semantics live in source, not in mutable data.
/// </summary>
public sealed record SettingDefinition(
    string Key, string Category, string DisplayName, string Description, bool IsSecret, string DefaultValue);

/// <summary>The authoritative, code-first registry of platform settings.</summary>
public static class SettingsCatalog
{
    public static readonly IReadOnlyList<SettingDefinition> All =
    [
        new("general.platformName", "General", "Platform name", "Shown in the app bar and emails.", false, "KOC AI Community"),
        new("general.supportEmail", "General", "Support email", "Where employees send help requests.", false, "ai-support@koc.com.kw"),
        new("security.sessionLifetimeMinutes", "Security", "Session lifetime (minutes)", "Idle sign-in lifetime.", false, "60"),
        new("security.mfaRequired", "Security", "Require MFA", "Whether MFA is enforced at sign-in.", false, "false"),
        new("email.smtpHost", "Email", "SMTP host", "Outbound mail server hostname.", false, ""),
        new("email.smtpPassword", "Email", "SMTP password", "Outbound mail server password.", true, ""),
        new("storage.provider", "Storage", "Artifact store provider", "Local or AzureBlob.", false, "Local"),
        new("integrations.connectorApiKey", "Integrations", "Connector API key", "Shared key for KOC connectors.", true, ""),
    ];

    public static SettingDefinition? Find(string key) =>
        All.FirstOrDefault(s => string.Equals(s.Key, key, StringComparison.OrdinalIgnoreCase));

    public static IReadOnlyList<string> Categories =>
        All.Select(s => s.Category).Distinct().ToList();
}
