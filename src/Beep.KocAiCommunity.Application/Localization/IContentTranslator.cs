namespace Beep.KocAiCommunity.Application.Localization;

/// <summary>
/// Reads translated platform content — the names and descriptions KOC ships, in the language the caller
/// asked for.
/// <para>
/// Every method falls back to what it was given. An untranslated category shows its English name rather
/// than a blank chip, which is the same bargain the interface strings make: a gap reads as English, not
/// as a hole in the page.
/// </para>
/// </summary>
public interface IContentTranslator
{
    /// <summary>
    /// Translations for one entity type and field, keyed by entity key. One query for a whole list, so
    /// rendering a catalogue does not turn into a query per row.
    /// </summary>
    Task<IReadOnlyDictionary<string, string>> LookupAsync(
        string entityType, string field, string language, CancellationToken ct = default);

    /// <summary>Convenience for a single value, falling back to <paramref name="fallback"/>.</summary>
    Task<string> TranslateAsync(
        string entityType, string entityKey, string field, string language, string fallback, CancellationToken ct = default);

    /// <summary>
    /// Records what a person wrote for one field in one language, replacing whatever was there.
    /// <para>
    /// Everything translated used to ship with the platform, so this interface only read. A member
    /// writing their own competition in Arabic is the first case where the translation comes from a
    /// person, and theirs must win over anything seeded — unlike the seeders, which never overwrite.
    /// </para>
    /// <para>Blank clears the translation, so the field falls back to the original text.</para>
    /// </summary>
    Task SetAsync(
        string entityType, string entityKey, string field, string language, string? value, CancellationToken ct = default);
}
