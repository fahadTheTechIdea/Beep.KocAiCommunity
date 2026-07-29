namespace Beep.KocAiCommunity.Contracts.Localization;

/// <summary>
/// The languages the platform speaks. KOC's workforce reads both, and the half of the platform that is
/// open to everyone — the learning catalogue and the community — is the half that most needs Arabic.
/// <para>
/// This lives in Contracts because it is needed on both sides of the wire: the Web renders a switcher
/// and sets a direction, the API resolves a request language. <c>TrackLanguages</c> in the domain says
/// the same thing about <i>content</i>, and a test holds the two lists to each other so they can't drift.
/// </para>
/// </summary>
public static class KocLanguages
{
    public const string English = "en";
    public const string Arabic = "ar";

    /// <summary>The cookie the culture is remembered in — readable before anyone has an account.</summary>
    public const string CookieName = ".KocAiCommunity.Culture";

    /// <summary>Every supported language, in the order a switcher should offer them.</summary>
    public static readonly string[] All = [English, Arabic];

    /// <summary>
    /// Formatting culture for numbers and dates, pinned deliberately and identical in both languages.
    /// <para>
    /// Only the <i>words</i> change with the interface language. A leaderboard score, an AUC of 0.93, and
    /// a competition deadline must read the same either way — a metric that renders as ٠٫٩٣ for one
    /// colleague and 0.93 for another, in a screenshot pasted into the same chat, is a support ticket.
    /// </para>
    /// </summary>
    public const string FormattingCulture = "en-GB";

    /// <summary>The name of a language in that language, so a switcher reads naturally to the person looking for it.</summary>
    public static string NativeName(string language) => language switch
    {
        Arabic => "العربية",
        _ => "English",
    };

    /// <summary>Arabic is read right to left; the page shell needs to know before it renders.</summary>
    public static bool IsRightToLeft(string? language) =>
        string.Equals(language, Arabic, StringComparison.OrdinalIgnoreCase);

    /// <summary>The <c>dir</c> attribute for a language.</summary>
    public static string Direction(string? language) => IsRightToLeft(language) ? "rtl" : "ltr";

    /// <summary>Falls back to English for anything unrecognised, so a stale cookie reads normally.</summary>
    public static string Normalize(string? language) =>
        All.FirstOrDefault(l => string.Equals(l, language, StringComparison.OrdinalIgnoreCase)) ?? English;
}
