namespace Beep.KocAiCommunity.Ui.Shared.Branding;

/// <summary>
/// KOC brand constants shared across every surface. Colors are sampled from the KOC eagle
/// logo (petroleum blue). Static brand assets (logo, O&amp;G icons) are served from this RCL's
/// wwwroot under <c>_content/Beep.KocAiCommunity.Ui.Shared/</c>.
/// </summary>
public static class KocBrand
{
    public const string ProductName = "KocAiCommunity";
    public const string Company = "Kuwait Oil Company";
    public const string Tagline = "KOC's internal workspace for AI collaboration and ML.";

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
