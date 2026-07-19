namespace Beep.KocAiCommunity.Infrastructure.Engagement;

/// <summary>
/// The server-authoritative allowlist of avatar icons a user may choose. Every entry is a real file
/// in the shared O&amp;G icon library (<c>Ui.Shared/wwwroot/icons/</c>). Validating against this list
/// (never a free-form string) keeps avatar selection safe from path traversal.
/// </summary>
public static class IconLibrary
{
    public static readonly IReadOnlyList<string> Avatars =
    [
        "185-worker.png", "186-engineer.png", "083-engineering.png", "105-management.png",
        "106-planning.png", "110-thinking.png", "129-innovation.png", "133-creativity.png",
        "179-exploration.png", "063-oil-rig.png", "057-oil-platform.png", "220-pump-jack.png",
        "039-oil-well.png", "137-oil-refinery.png", "144-industry.png", "011-oil-industry.png",
    ];

    private static readonly HashSet<string> AllowedSet = new(Avatars, StringComparer.Ordinal);

    public static bool IsAllowed(string iconFile) => AllowedSet.Contains(iconFile);
}
