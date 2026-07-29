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
}
