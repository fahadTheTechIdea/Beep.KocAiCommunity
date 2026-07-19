namespace Beep.KocAiCommunity.Domain.Engagement;

/// <summary>The KOC O&amp;G career ladder — a pure lookup driven by lifetime Barrels (bbl). No DB.</summary>
public static class KocLevels
{
    /// <summary>Level ordinal, title, and the minimum lifetime bbl to reach it.</summary>
    public static readonly (int Level, string Title, int MinXp)[] Ladder =
    [
        (1, "Roustabout", 0),
        (2, "Roughneck", 100),
        (3, "Derrickhand", 300),
        (4, "Driller", 700),
        (5, "Toolpusher", 1_500),
        (6, "Field Engineer", 3_000),
        (7, "Reservoir Analyst", 6_000),
        (8, "Chief Geoscientist", 12_000),
    ];

    /// <summary>The current level for a bbl total, plus the threshold of the next level (null at the top).</summary>
    public static (int Level, string Title, int MinXp, int? NextXp) ForXp(int xp)
    {
        var current = Ladder[0];
        int? next = Ladder.Length > 1 ? Ladder[1].MinXp : null;

        for (var i = 0; i < Ladder.Length; i++)
        {
            if (xp >= Ladder[i].MinXp)
            {
                current = Ladder[i];
                next = i + 1 < Ladder.Length ? Ladder[i + 1].MinXp : null;
            }
        }

        return (current.Level, current.Title, current.MinXp, next);
    }

    /// <summary>The level ordinal for a bbl total.</summary>
    public static int LevelForXp(int xp) => ForXp(xp).Level;

    /// <summary>The title for a level ordinal (clamped to the ladder).</summary>
    public static string TitleForLevel(int level) =>
        Ladder[Math.Clamp(level - 1, 0, Ladder.Length - 1)].Title;
}
