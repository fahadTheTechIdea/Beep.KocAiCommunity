using Beep.KocAiCommunity.Contracts.Competitions;

namespace Beep.KocAiCommunity.Infrastructure.Engagement;

/// <summary>Stable Barrels (bbl) source discriminators and their award values. One source of truth.</summary>
public static class XpSources
{
    public const string LessonCompleted = "lesson.completed";
    public const string TrackCompleted = "track.completed";

    /// <summary>Passing a track's quiz. Awarded once per quiz — a retake earns the badge, not the Barrels again.</summary>
    public const string QuizPassed = "quiz.passed";
    public const string SubmissionScored = "submission.scored";
    public const string SubmissionFirst = "submission.first";     // one-time bonus, awarded internally
    public const string CompetitionTop3 = "competition.top3";
    public const string CompetitionWin = "competition.win";       // 0-bbl marker for the winner badge
    public const string DiscussionCreated = "discussion.created";
    public const string DiscussionReplied = "discussion.replied";
    public const string KudosReceived = "kudos.received";
    public const string StreakWeek = "streak.week";

    /// <summary>bbl awarded per source. Unknown sources award 0.</summary>
    public static int Points(string source) => source switch
    {
        LessonCompleted => 25,
        TrackCompleted => 150,
        // Between a lesson and a whole track: passing is more than reading one page and less than
        // finishing the material.
        QuizPassed => 60,
        // Competition awards come from the shared CompetitionRewards constants so the arena UI's
        // prize copy is guaranteed to match what is actually granted.
        SubmissionScored => CompetitionRewards.ScoredSubmissionBarrels,
        SubmissionFirst => CompetitionRewards.FirstSubmissionBonusBarrels,
        CompetitionTop3 => CompetitionRewards.PodiumBarrels,
        CompetitionWin => 0,
        DiscussionCreated => 10,
        DiscussionReplied => 5,
        KudosReceived => 15,
        StreakWeek => 70,
        _ => 0,
    };

    /// <summary>Sources that count against the anti-spam daily cap.</summary>
    public static bool IsDailyCapped(string source) =>
        source is DiscussionCreated or DiscussionReplied;

    /// <summary>Combined daily bbl cap for <see cref="IsDailyCapped"/> sources.</summary>
    public const int DailyCappedCeiling = 50;
}
