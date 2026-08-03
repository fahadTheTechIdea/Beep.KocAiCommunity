using Beep.KocAiCommunity.Application.Learning;
using Beep.KocAiCommunity.Domain.Learning;
using Beep.KocAiCommunity.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Beep.KocAiCommunity.IntegrationTests;

/// <summary>
/// The quiz that ships with the entry-point track.
/// <para>
/// Seeded content is the one place a broken quiz reaches everybody at once, and the two ways to break
/// it are silent: a question nobody can answer because no option is marked correct, and one with a
/// single option to "choose" between. The admin console refuses both at authoring time; nothing was
/// checking the seeder, which does not go through it.
/// </para>
/// </summary>
public class SeededQuizTests(KocApiFactory factory) : IClassFixture<KocApiFactory>
{
    private readonly KocApiFactory _factory = factory;

    private async Task<(Quiz Quiz, List<QuizQuestion> Questions, List<QuizAnswer> Answers)> ReadAsync()
    {
        _ = _factory.CreateClientAs(sub: null);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<KocDbContext>();

        var track = await db.LearningTracks
            .FirstAsync(t => t.ContentKey == "ai-for-everyone" && t.Language == TrackLanguages.English);

        var quiz = await db.Quizzes.FirstAsync(q => q.TrackId == track.Id);
        var questions = await db.QuizQuestions.Where(q => q.QuizId == quiz.Id).OrderBy(q => q.OrderNo).ToListAsync();
        var ids = questions.Select(q => q.Id).ToList();
        var answers = await db.QuizAnswers.Where(a => ids.Contains(a.QuestionId)).ToListAsync();

        return (quiz, questions, answers);
    }

    [Fact]
    public async Task The_entry_track_ships_with_a_quiz_that_can_be_sat()
    {
        var (quiz, questions, _) = await ReadAsync();

        quiz.IsEnabled.Should().BeTrue();
        questions.Should().NotBeEmpty();
        questions.Select(q => q.OrderNo).Should().BeInAscendingOrder().And.OnlyHaveUniqueItems();
    }

    [Fact]
    public async Task It_is_optional_rather_than_required()
    {
        // A mandatory quiz arriving in a deployment would stop everybody part-way through that track
        // from finishing it, for a quiz they never agreed to sit. Requiring it is the track owner's
        // call, made in the admin console.
        var (quiz, _, _) = await ReadAsync();

        quiz.IsMandatory.Should().BeFalse();
    }

    [Fact]
    public async Task Every_question_can_actually_be_answered_correctly()
    {
        var (_, questions, answers) = await ReadAsync();

        foreach (var question in questions)
        {
            var options = answers.Where(a => a.QuestionId == question.Id).ToList();

            options.Should().HaveCountGreaterThanOrEqualTo(2, "\"{0}\" needs something to choose between", question.Text);
            options.Should().Contain(a => a.IsCorrect, "\"{0}\" has no correct answer, so nobody can score it", question.Text);
            options.Should().OnlyContain(a => !string.IsNullOrWhiteSpace(a.Text));
        }
    }

    [Fact]
    public async Task Answering_it_correctly_passes_and_answering_it_wrongly_does_not()
    {
        // Grading the real seeded content, rather than a fixture built to grade cleanly.
        var (quiz, questions, answers) = await ReadAsync();

        var graded = questions
            .Select(q => new GradedQuestion(q.Id, [.. answers.Where(a => a.QuestionId == q.Id && a.IsCorrect).Select(a => a.Id)]))
            .ToList();

        var allRight = graded.Select(g => new SubmittedAnswer(g.QuestionId, g.CorrectAnswerIds)).ToList();
        QuizScoring.Grade(graded, allRight, quiz.PassMark).ScorePercent.Should().Be(100);

        var allWrong = questions
            .Select(q => new SubmittedAnswer(q.Id,
                [.. answers.Where(a => a.QuestionId == q.Id && !a.IsCorrect).Select(a => a.Id).Take(1)]))
            .ToList();
        QuizScoring.Grade(graded, allWrong, quiz.PassMark).Passed.Should().BeFalse();
    }

    [Fact]
    public async Task Seeding_twice_does_not_produce_a_second_quiz()
    {
        // The seeder runs on every start. Rewriting the quiz each time would discard an admin's edits,
        // and adding a second one would break the one-quiz-per-track index.
        _ = _factory.CreateClientAs(sub: null);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<KocDbContext>();

        var track = await db.LearningTracks
            .FirstAsync(t => t.ContentKey == "ai-for-everyone" && t.Language == TrackLanguages.English);
        var before = await db.QuizQuestions.CountAsync(q => db.Quizzes.Any(z => z.Id == q.QuizId && z.TrackId == track.Id));

        await Beep.KocAiCommunity.Infrastructure.Learning.LearningSeeder.SeedTracksAsync(db);

        (await db.Quizzes.CountAsync(q => q.TrackId == track.Id)).Should().Be(1);
        (await db.QuizQuestions.CountAsync(q => db.Quizzes.Any(z => z.Id == q.QuizId && z.TrackId == track.Id)))
            .Should().Be(before, "a re-seed must not duplicate the questions");
    }
}
