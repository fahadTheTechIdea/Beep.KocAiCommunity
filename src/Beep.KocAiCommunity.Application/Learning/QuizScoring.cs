namespace Beep.KocAiCommunity.Application.Learning;

/// <summary>One question as the grader sees it: its id and the set of options that are correct.</summary>
public sealed record GradedQuestion(Guid QuestionId, IReadOnlyCollection<Guid> CorrectAnswerIds);

/// <summary>What the learner selected for one question. An unanswered question is simply absent.</summary>
public sealed record SubmittedAnswer(Guid QuestionId, IReadOnlyCollection<Guid> SelectedAnswerIds);

/// <summary>The outcome of a sitting, before it is written down.</summary>
public sealed record QuizResult(int CorrectCount, int QuestionCount, int ScorePercent, bool Passed);

/// <summary>
/// Grading, kept away from the database so it can be reasoned about and tested directly.
/// <para>
/// The rule that matters is what counts as a correct response to a question with several right options:
/// every correct one selected and no incorrect one. Awarding part marks for a partly-right answer is
/// the intuitive alternative and the wrong one here — it lets somebody pass by ticking every box, which
/// is precisely the behaviour a quiz exists to distinguish from knowing the material.
/// </para>
/// </summary>
public static class QuizScoring
{
    /// <summary>A pass mark outside 1–100 cannot be met or cannot be missed; clamp rather than throw.</summary>
    public static int NormalizePassMark(int passMark) => Math.Clamp(passMark, 1, 100);

    public static QuizResult Grade(
        IReadOnlyCollection<GradedQuestion> questions,
        IReadOnlyCollection<SubmittedAnswer> submitted,
        int passMark)
    {
        // A quiz with no questions is not a quiz anyone can fail. Returning "passed" on an empty set
        // would silently unlock a mandatory gate; refusing to score it keeps the gate shut and makes
        // the misconfiguration visible instead.
        if (questions.Count == 0)
        {
            return new QuizResult(0, 0, 0, Passed: false);
        }

        var byQuestion = submitted
            .GroupBy(a => a.QuestionId)
            .ToDictionary(g => g.Key, g => g.SelectMany(a => a.SelectedAnswerIds).ToHashSet());

        var correct = questions.Count(q =>
            byQuestion.TryGetValue(q.QuestionId, out var chosen)
            && chosen.Count > 0
            && chosen.SetEquals(q.CorrectAnswerIds));

        // Rounded away from zero, so 2/3 reads as 67 rather than 66 and matches what a learner counts.
        var percent = (int)Math.Round(100.0 * correct / questions.Count, MidpointRounding.AwayFromZero);

        return new QuizResult(correct, questions.Count, percent, percent >= NormalizePassMark(passMark));
    }

    /// <summary>
    /// Whether a question can actually be answered correctly. A question with no correct option — easy
    /// to create by unticking one in the editor — is unanswerable, and every attempt at the quiz would
    /// be capped below full marks with no way for the learner to know why.
    /// </summary>
    public static bool IsAnswerable(GradedQuestion question) => question.CorrectAnswerIds.Count > 0;
}
