using Beep.KocAiCommunity.Application.Learning;
using FluentAssertions;
using Xunit;

namespace Beep.KocAiCommunity.UnitTests;

/// <summary>
/// Grading a quiz.
/// <para>
/// The interesting cases are all refusals: the tick-everything strategy, the unanswered question, the
/// pass mark exactly met, and the quiz with nothing in it. A grader that only handles a right answer
/// and a wrong one passes people who did not earn it, and on a mandatory quiz that is the gate opening
/// for the wrong reason.
/// </para>
/// </summary>
public class QuizScoringTests
{
    private static readonly Guid Q1 = Guid.NewGuid();
    private static readonly Guid Q2 = Guid.NewGuid();
    private static readonly Guid A = Guid.NewGuid();
    private static readonly Guid B = Guid.NewGuid();
    private static readonly Guid C = Guid.NewGuid();

    private static GradedQuestion Single(Guid id, Guid correct) => new(id, [correct]);

    [Fact]
    public void A_correct_single_answer_scores()
    {
        var result = QuizScoring.Grade([Single(Q1, A)], [new SubmittedAnswer(Q1, [A])], passMark: 70);

        result.CorrectCount.Should().Be(1);
        result.ScorePercent.Should().Be(100);
        result.Passed.Should().BeTrue();
    }

    [Fact]
    public void Selecting_every_option_does_not_pass_a_multi_answer_question()
    {
        // The whole reason partial credit is refused. If ticking everything scored, the quiz would
        // measure willingness to tick boxes.
        var question = new GradedQuestion(Q1, [A, B]);

        var result = QuizScoring.Grade([question], [new SubmittedAnswer(Q1, [A, B, C])], passMark: 50);

        result.CorrectCount.Should().Be(0);
        result.Passed.Should().BeFalse();
    }

    [Fact]
    public void A_partly_right_multi_answer_question_earns_nothing()
    {
        var question = new GradedQuestion(Q1, [A, B]);

        QuizScoring.Grade([question], [new SubmittedAnswer(Q1, [A])], passMark: 50)
            .CorrectCount.Should().Be(0);
    }

    [Fact]
    public void An_unanswered_question_is_wrong_rather_than_skipped()
    {
        // Skipping it would shrink the denominator, so answering one question correctly and leaving the
        // rest blank would score 100%.
        var result = QuizScoring.Grade(
            [Single(Q1, A), Single(Q2, B)],
            [new SubmittedAnswer(Q1, [A])],
            passMark: 70);

        result.QuestionCount.Should().Be(2);
        result.ScorePercent.Should().Be(50);
        result.Passed.Should().BeFalse();
    }

    [Fact]
    public void An_empty_selection_for_a_question_is_not_a_correct_empty_answer()
    {
        // Set equality alone would call an empty selection correct for a question with no right option.
        QuizScoring.Grade([new GradedQuestion(Q1, [])], [new SubmittedAnswer(Q1, [])], passMark: 50)
            .CorrectCount.Should().Be(0);
    }

    [Fact]
    public void The_pass_mark_is_met_exactly_not_only_exceeded()
    {
        // 1 of 2 is 50%. A pass mark of 50 has to admit it, or every boundary reads one question short.
        QuizScoring.Grade([Single(Q1, A), Single(Q2, B)], [new SubmittedAnswer(Q1, [A])], passMark: 50)
            .Passed.Should().BeTrue();
    }

    [Fact]
    public void Two_of_three_rounds_to_sixty_seven()
    {
        var q3 = Guid.NewGuid();
        var result = QuizScoring.Grade(
            [Single(Q1, A), Single(Q2, B), Single(q3, C)],
            [new SubmittedAnswer(Q1, [A]), new SubmittedAnswer(Q2, [B])],
            passMark: 70);

        result.ScorePercent.Should().Be(67, "a learner counting two out of three does not read 66");
        result.Passed.Should().BeFalse();
    }

    [Fact]
    public void A_quiz_with_no_questions_is_not_passed()
    {
        // It gates a track when mandatory. Treating "nothing to get wrong" as a pass would open that
        // gate for everyone the moment an admin created an empty quiz.
        var result = QuizScoring.Grade([], [], passMark: 70);

        result.Passed.Should().BeFalse();
        result.QuestionCount.Should().Be(0);
    }

    [Fact]
    public void An_out_of_range_pass_mark_is_clamped_rather_than_thrown()
    {
        QuizScoring.NormalizePassMark(0).Should().Be(1, "a pass mark of zero cannot be missed");
        QuizScoring.NormalizePassMark(140).Should().Be(100, "a pass mark above full marks cannot be met");
        QuizScoring.NormalizePassMark(-5).Should().Be(1);
        QuizScoring.NormalizePassMark(70).Should().Be(70);
    }

    [Fact]
    public void A_question_with_no_correct_option_is_reported_as_unanswerable()
    {
        QuizScoring.IsAnswerable(new GradedQuestion(Q1, [])).Should().BeFalse();
        QuizScoring.IsAnswerable(Single(Q1, A)).Should().BeTrue();
    }
}
