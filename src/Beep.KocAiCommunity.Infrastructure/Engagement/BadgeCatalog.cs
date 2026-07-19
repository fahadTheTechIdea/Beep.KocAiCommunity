using Beep.KocAiCommunity.Domain.Engagement;

namespace Beep.KocAiCommunity.Infrastructure.Engagement;

/// <summary>
/// The canonical KOC badge catalog. Icon files are real assets in the shared O&amp;G icon library
/// (<c>Ui.Shared/wwwroot/icons/</c>), resolved on the client via <c>KocBrand.Icon(...)</c>.
/// A single source of truth shared by the seeder and the rule evaluator.
/// </summary>
public static class BadgeCatalog
{
    public const string FirstBarrel = "first-barrel";
    public const string FirstSubmission = "first-submission";
    public const string CompetitionWinner = "competition-winner";
    public const string Podium = "podium";
    public const string FirstTrack = "first-track";
    public const string AllTracks = "all-tracks";
    public const string Streak7 = "streak-7";
    public const string Streak30 = "streak-30";
    public const string Helper10 = "helper-10";
    public const string DiscussionStarter = "discussion-starter";
    public const string TeamPlayer = "team-player";

    public sealed record Definition(string Code, string Name, string Description, string IconFile, string Tier);

    public static readonly IReadOnlyList<Definition> All =
    [
        new(FirstBarrel, "First Barrel", "Earned your very first Barrel on the platform.", "008-oil.png", "bronze"),
        new(FirstSubmission, "Wildcatter", "Made your first competition submission.", "179-exploration.png", "bronze"),
        new(CompetitionWinner, "Gusher", "Finished first in a competition.", "039-oil-well.png", "gold"),
        new(Podium, "On the Podium", "Finished in the top three of a competition.", "100-goal.png", "silver"),
        new(FirstTrack, "Certified", "Completed your first learning track.", "132-training.png", "bronze"),
        new(AllTracks, "Master Operator", "Completed every published learning track.", "083-engineering.png", "gold"),
        new(Streak7, "Steady Pump", "Kept a 7-day activity streak.", "220-pump-jack.png", "silver"),
        new(Streak30, "Non-Stop Flow", "Kept a 30-day activity streak.", "016-pipeline.png", "gold"),
        new(Helper10, "Good Neighbor", "Received 10 kudos from colleagues.", "118-approval.png", "silver"),
        new(DiscussionStarter, "Conversation Driller", "Started 5 discussions.", "065-drilling-1.png", "bronze"),
        new(TeamPlayer, "Team Player", "Part of a team that won a team challenge.", "105-management.png", "silver"),
    ];

    public static Definition? Find(string code) => All.FirstOrDefault(b => b.Code == code);
}
