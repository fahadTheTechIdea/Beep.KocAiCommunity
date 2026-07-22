namespace Beep.KocAiCommunity.Contracts.Competitions;

/// <summary>
/// The competition reward structure, shared by the server (actual awards) and the UI (prize copy)
/// so what the arena promises can never drift from what the engagement engine grants.
/// </summary>
public static class CompetitionRewards
{
    /// <summary>Barrels awarded to each of the top-3 finishers when a competition concludes.</summary>
    public const int PodiumBarrels = 300;

    /// <summary>Barrels awarded for every scored submission.</summary>
    public const int ScoredSubmissionBarrels = 20;

    /// <summary>One-time bonus for a user's first-ever scored submission.</summary>
    public const int FirstSubmissionBonusBarrels = 50;

    public const string WinnerBadge = "Gusher";
    public const string PodiumBadge = "On the Podium";
    public const string FirstSubmissionBadge = "Wildcatter";
}
