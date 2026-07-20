namespace Beep.KocAiCommunity.Ui.Shared.Branding;

/// <summary>
/// KOC brand constants shared across every surface. Colors are sampled from the KOC eagle
/// logo (petroleum blue). Static brand assets (logo, O&amp;G icons) are served from this RCL's
/// wwwroot under <c>_content/Beep.KocAiCommunity.Ui.Shared/</c>.
/// </summary>
public static class KocBrand
{
    /// <summary>The product name as it appears in the app bar, page titles, and emails.</summary>
    public const string ProductName = "KOC Training and Career Development";

    /// <summary>Short form for tight spaces (app bar on small screens, chips, breadcrumbs).</summary>
    public const string ShortName = "KOC T&CD";

    public const string Company = "Kuwait Oil Company";

    /// <summary>Where this app sits in the company — shown under the product name.</summary>
    public const string Department = "Training and Career Development Group";

    public const string Tagline = "Build AI skills, compete with colleagues, and grow your career at KOC.";

    /// <summary>KOC petroleum blue (accent 700 / primary).</summary>
    public const string Accent = "#1466A5";
    public const string Accent300 = "#5FA3D4";
    public const string Accent500 = "#2A7CBE";
    public const string Accent700 = "#1466A5";
    public const string Accent900 = "#0B2E4C";

    private const string ContentRoot = "_content/Beep.KocAiCommunity.Ui.Shared";

    public const string LogoPath = ContentRoot + "/brand/KOC_Logo.png";

    /// <summary>Resolves an O&amp;G domain icon by file name (e.g. "009-pump.png", "PAD.png").</summary>
    public static string Icon(string fileName) => $"{ContentRoot}/icons/{fileName}";
}
