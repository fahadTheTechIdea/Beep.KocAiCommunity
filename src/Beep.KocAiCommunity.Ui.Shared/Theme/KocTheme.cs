using Beep.KocAiCommunity.Ui.Shared.Branding;
using MudBlazor;

namespace Beep.KocAiCommunity.Ui.Shared.Theme;

/// <summary>
/// The KOC MudBlazor theme. Anchors on KOC petroleum blue with white surfaces and neutral
/// grays — conservative, structured, trustworthy. Dark mode is not a v1 requirement but the
/// palette is defined so it can be added without a redesign.
/// </summary>
public static class KocTheme
{
    public static MudTheme Build() => new()
    {
        PaletteLight = new PaletteLight
        {
            Primary = KocBrand.Accent700,
            PrimaryDarken = KocBrand.Accent900,
            PrimaryLighten = KocBrand.Accent500,
            Secondary = "#1F8A8C",           // KOC teal accent
            AppbarBackground = "#FFFFFF",
            AppbarText = "#0B2E4C",
            Background = "#FFFFFF",
            Surface = "#F6F8FA",
            DrawerBackground = "#FFFFFF",
            TextPrimary = "#10222F",
            TextSecondary = "#5A6B78",
            Divider = "#E1E7EC",
        },
        Typography = new Typography
        {
            Default = new DefaultTypography { FontFamily = ["Inter", "IBM Plex Sans", "Segoe UI", "sans-serif"] },
            H1 = new H1Typography { FontWeight = "700", LetterSpacing = "0.005em" },
            H2 = new H2Typography { FontWeight = "600", LetterSpacing = "0.01em" },
            H3 = new H3Typography { FontWeight = "600" },
        },
        LayoutProperties = new LayoutProperties
        {
            DefaultBorderRadius = "4px",
            AppbarHeight = "64px",
        },
    };
}
