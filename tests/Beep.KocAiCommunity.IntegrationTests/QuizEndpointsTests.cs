using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Beep.KocAiCommunity.Contracts.Learning;
using Beep.KocAiCommunity.Domain.Learning;
using Beep.KocAiCommunity.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Beep.KocAiCommunity.IntegrationTests;

/// <summary>
/// The quiz over the wire.
/// <para>
/// One test here matters more than the rest: the correct answers must never reach a learner before they
/// have answered. That is not enforced by remembering to blank a field — the learner-facing records have
/// no such field — and this checks the raw JSON rather than a deserialised DTO, because deserialising
/// into the learner shape would silently discard exactly the leak being looked for.
/// </para>
/// </summary>
public class QuizEndpointsTests(KocApiFactory factory) : IClassFixture<KocApiFactory>
{
    private readonly KocApiFactory _factory = factory;

    private sealed record Seeded(Guid TrackId, Guid QuestionId, Guid RightAnswerId, Guid WrongAnswerId);

    private async Task<Seeded> SeedAsync(bool mandatory = false, int passMark = 70)
    {
        _ = _factory.CreateClientAs(sub: null);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<KocDbContext>();

        var track = new LearningTrack
        {
            Title = "Quiz api " + Guid.NewGuid().ToString("N")[..6],
            Summary = "s", Status = "published", Domain = "ml",
            Level = TrackLevel.Beginner, CreatedUtc = DateTime.UtcNow,
        };
        db.LearningTracks.Add(track);
        db.Lessons.Add(new Lesson
        {
            TrackId = track.Id, OrderNo = 1, Title = "L1", ContentRef = "none",
            Content = "b", EstimatedMinutes = 5, CreatedUtc = DateTime.UtcNow,
        });

        var quiz = new Quiz
        {
            TrackId = track.Id, PassMark = passMark, IsMandatory = mandatory,
            IsEnabled = true, Intro = "A short check.", CreatedUtc = DateTime.UtcNow,
        };
        db.Quizzes.Add(quiz);

        var question = new QuizQuestion
        {
            QuizId = quiz.Id, OrderNo = 1, Text = "Which metric suits imbalanced data?",
            Explanation = "AUC is insensitive to the class balance.", CreatedUtc = DateTime.UtcNow,
        };
        db.QuizQuestions.Add(question);

        var right = new QuizAnswer { QuestionId = question.Id, OrderNo = 1, Text = "AUC", IsCorrect = true, CreatedUtc = DateTime.UtcNow };
        var wrong = new QuizAnswer { QuestionId = question.Id, OrderNo = 2, Text = "Accuracy", IsCorrect = false, CreatedUtc = DateTime.UtcNow };
        db.QuizAnswers.AddRange(right, wrong);

        await db.SaveChangesAsync();
        return new Seeded(track.Id, question.Id, right.Id, wrong.Id);
    }

    [Fact]
    public async Task The_learner_quiz_never_carries_which_answer_is_correct()
    {
        // Read as raw JSON on purpose. Deserialising into QuizAnswerDto would drop an IsCorrect field
        // that was present on the wire, and the test would pass while the answers leaked.
        var seeded = await SeedAsync();
        var client = _factory.CreateClientAs("quiz-learner", "Employee");

        var json = await client.GetStringAsync($"/api/v1/tracks/{seeded.TrackId}/quiz");

        json.Should().NotContain("isCorrect", "the learner shape has no such field")
            .And.NotContain("IsCorrect");
        json.Should().NotContain("Explanation", "the explanation is for after the attempt, not before");
        json.Should().Contain("AUC", "the options themselves are of course sent");
    }

    [Fact]
    public async Task A_visitor_can_see_that_a_track_ends_in_a_quiz()
    {
        // Part of deciding whether to start a track. There is nothing to withhold: no answers, and no
        // attempt history for somebody who is not anybody yet.
        var seeded = await SeedAsync(mandatory: true);
        var guest = _factory.CreateClientAs(sub: null);

        var response = await guest.GetAsync($"/api/v1/tracks/{seeded.TrackId}/quiz");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var quiz = await response.Content.ReadFromJsonAsync<QuizDto>();
        quiz!.IsMandatory.Should().BeTrue();
        quiz.AttemptCount.Should().Be(0);
        quiz.BestScorePercent.Should().BeNull();
    }

    [Fact]
    public async Task A_track_with_no_quiz_is_a_404_rather_than_an_error()
    {
        _ = _factory.CreateClientAs(sub: null);
        Guid trackId;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<KocDbContext>();
            var track = new LearningTrack
            {
                Title = "No quiz here", Summary = "s", Status = "published", Domain = "ml",
                Level = TrackLevel.Beginner, CreatedUtc = DateTime.UtcNow,
            };
            db.LearningTracks.Add(track);
            await db.SaveChangesAsync();
            trackId = track.Id;
        }

        var client = _factory.CreateClientAs("quiz-none", "Employee");

        (await client.GetAsync($"/api/v1/tracks/{trackId}/quiz")).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Submitting_grades_the_attempt_and_discloses_the_answers_afterwards()
    {
        var seeded = await SeedAsync();
        var client = _factory.CreateClientAs("quiz-right", "Employee");

        var response = await client.PostAsJsonAsync(
            $"/api/v1/tracks/{seeded.TrackId}/quiz/attempts",
            new SubmitQuizRequest([new QuizResponseDto(seeded.QuestionId, [seeded.RightAnswerId])]));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<QuizAttemptResultDto>();

        result!.Passed.Should().BeTrue();
        result.ScorePercent.Should().Be(100);
        result.AttemptNo.Should().Be(1);

        // Only now: the review needs the right answer and the reason, or it teaches nothing.
        var reviewed = result.Questions.Should().ContainSingle().Subject;
        reviewed.WasCorrect.Should().BeTrue();
        reviewed.CorrectAnswerIds.Should().ContainSingle().Which.Should().Be(seeded.RightAnswerId);
        reviewed.Explanation.Should().Contain("class balance");
    }

    [Fact]
    public async Task A_wrong_answer_fails_and_the_attempt_is_still_recorded()
    {
        var seeded = await SeedAsync();
        var client = _factory.CreateClientAs("quiz-wrong", "Employee");

        var result = await (await client.PostAsJsonAsync(
            $"/api/v1/tracks/{seeded.TrackId}/quiz/attempts",
            new SubmitQuizRequest([new QuizResponseDto(seeded.QuestionId, [seeded.WrongAnswerId])])))
            .Content.ReadFromJsonAsync<QuizAttemptResultDto>();

        result!.Passed.Should().BeFalse();
        result.ScorePercent.Should().Be(0);

        // Kept, because "passed first time" is measured against the attempt count.
        var attempts = await client.GetFromJsonAsync<List<QuizAttemptSummaryDto>>(
            $"/api/v1/tracks/{seeded.TrackId}/quiz/attempts");
        attempts.Should().ContainSingle().Which.Passed.Should().BeFalse();
    }

    [Fact]
    public async Task Retaking_counts_up_and_the_best_score_is_remembered()
    {
        var seeded = await SeedAsync();
        var client = _factory.CreateClientAs("quiz-retake", "Employee");

        await client.PostAsJsonAsync($"/api/v1/tracks/{seeded.TrackId}/quiz/attempts",
            new SubmitQuizRequest([new QuizResponseDto(seeded.QuestionId, [seeded.WrongAnswerId])]));
        var second = await (await client.PostAsJsonAsync($"/api/v1/tracks/{seeded.TrackId}/quiz/attempts",
            new SubmitQuizRequest([new QuizResponseDto(seeded.QuestionId, [seeded.RightAnswerId])])))
            .Content.ReadFromJsonAsync<QuizAttemptResultDto>();

        second!.AttemptNo.Should().Be(2);

        var quiz = await client.GetFromJsonAsync<QuizDto>($"/api/v1/tracks/{seeded.TrackId}/quiz");
        quiz!.BestScorePercent.Should().Be(100);
        quiz.HasPassed.Should().BeTrue();
        quiz.AttemptCount.Should().Be(2);
    }

    [Fact]
    public async Task An_answer_id_from_another_question_cannot_be_used_to_score()
    {
        // Two quizzes, and a response naming an option that belongs to neither this question nor this
        // quiz. Without filtering to the question's own options, a set comparison could be satisfied by
        // an id the learner was never shown.
        var mine = await SeedAsync();
        var other = await SeedAsync();
        var client = _factory.CreateClientAs("quiz-foreign", "Employee");

        var result = await (await client.PostAsJsonAsync(
            $"/api/v1/tracks/{mine.TrackId}/quiz/attempts",
            new SubmitQuizRequest([new QuizResponseDto(mine.QuestionId, [other.RightAnswerId])])))
            .Content.ReadFromJsonAsync<QuizAttemptResultDto>();

        result!.Passed.Should().BeFalse();
        result.CorrectCount.Should().Be(0);
    }

    [Fact]
    public async Task Passing_a_mandatory_quiz_reports_that_it_finished_the_track()
    {
        var seeded = await SeedAsync(mandatory: true);
        var client = _factory.CreateClientAs("quiz-completes", "Employee");

        // Read the lessons first, so the quiz is the only thing outstanding.
        var track = await client.GetFromJsonAsync<TrackDetailDto>($"/api/v1/tracks/{seeded.TrackId}");
        await client.PostAsync($"/api/v1/tracks/{seeded.TrackId}/enroll", null);
        foreach (var lesson in track!.Lessons)
        {
            await client.PostAsync($"/api/v1/tracks/{seeded.TrackId}/lessons/{lesson.Id}/complete", null);
        }

        var result = await (await client.PostAsJsonAsync(
            $"/api/v1/tracks/{seeded.TrackId}/quiz/attempts",
            new SubmitQuizRequest([new QuizResponseDto(seeded.QuestionId, [seeded.RightAnswerId])])))
            .Content.ReadFromJsonAsync<QuizAttemptResultDto>();

        result!.Passed.Should().BeTrue();
        result.CompletedTrack.Should().BeTrue("the result screen should be able to say so in the same breath");
    }

    [Fact]
    public async Task Sitting_a_quiz_requires_an_account()
    {
        var seeded = await SeedAsync();
        var guest = _factory.CreateClientAs(sub: null);

        (await guest.PostAsJsonAsync($"/api/v1/tracks/{seeded.TrackId}/quiz/attempts",
            new SubmitQuizRequest([new QuizResponseDto(seeded.QuestionId, [seeded.RightAnswerId])])))
            .StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Only_an_admin_can_read_the_quiz_with_its_answers()
    {
        var seeded = await SeedAsync();

        (await _factory.CreateClientAs("quiz-nosy", "Employee")
            .GetAsync($"/api/v1/admin/tracks/{seeded.TrackId}/quiz"))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);

        var admin = _factory.CreateClientAs("quiz-admin", "Employee", "PlatformAdmin");
        var quiz = await admin.GetFromJsonAsync<AdminQuizDto>($"/api/v1/admin/tracks/{seeded.TrackId}/quiz");

        quiz!.Questions.Should().ContainSingle()
            .Which.Answers.Should().Contain(a => a.IsCorrect, "the admin view is the one that shows it");
    }

    [Fact]
    public async Task A_question_with_no_correct_answer_is_refused_when_it_is_saved()
    {
        // Refused at authoring time rather than discovered by a learner who cannot score full marks.
        var seeded = await SeedAsync();
        var admin = _factory.CreateClientAs("quiz-admin2", "Employee", "PlatformAdmin");

        var response = await admin.PutAsJsonAsync(
            $"/api/v1/admin/tracks/{seeded.TrackId}/quiz/questions",
            new UpsertQuizQuestionRequest(null, "Unanswerable?", "",
                [new UpsertQuizAnswerRequest(null, "A", false), new UpsertQuizAnswerRequest(null, "B", false)]));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var body = await response.Content.ReadAsStringAsync();
        JsonDocument.Parse(body).RootElement.GetProperty("error").GetString()
            .Should().Contain("correct");
    }

    [Fact]
    public async Task A_question_needs_something_to_choose_between()
    {
        var seeded = await SeedAsync();
        var admin = _factory.CreateClientAs("quiz-admin3", "Employee", "PlatformAdmin");

        (await admin.PutAsJsonAsync(
            $"/api/v1/admin/tracks/{seeded.TrackId}/quiz/questions",
            new UpsertQuizQuestionRequest(null, "Only one option?", "",
                [new UpsertQuizAnswerRequest(null, "The only one", true)])))
            .StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task An_admin_can_create_a_quiz_on_a_track_that_has_none()
    {
        _ = _factory.CreateClientAs(sub: null);
        Guid trackId;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<KocDbContext>();
            var track = new LearningTrack
            {
                Title = "Fresh track", Summary = "s", Status = "published", Domain = "ml",
                Level = TrackLevel.Beginner, CreatedUtc = DateTime.UtcNow,
            };
            db.LearningTracks.Add(track);
            await db.SaveChangesAsync();
            trackId = track.Id;
        }

        var admin = _factory.CreateClientAs("quiz-admin4", "Employee", "PlatformAdmin");

        // Nothing there yet reads as 204, not as a failure.
        (await admin.GetAsync($"/api/v1/admin/tracks/{trackId}/quiz")).StatusCode.Should().Be(HttpStatusCode.NoContent);

        var created = await (await admin.PutAsJsonAsync($"/api/v1/admin/tracks/{trackId}/quiz",
            new UpsertQuizRequest("Check yourself", 80, IsMandatory: true, IsEnabled: true)))
            .Content.ReadFromJsonAsync<AdminQuizDto>();

        created!.PassMark.Should().Be(80);
        created.IsMandatory.Should().BeTrue();
        created.Questions.Should().BeEmpty();
    }

    [Fact]
    public async Task Deleting_a_question_closes_the_gap_in_the_numbering()
    {
        // A learner counts 1, 2, 3. Leaving a hole would show them question 1 then question 3.
        var seeded = await SeedAsync();
        var admin = _factory.CreateClientAs("quiz-admin5", "Employee", "PlatformAdmin");

        foreach (var text in new[] { "Second?", "Third?" })
        {
            await admin.PutAsJsonAsync($"/api/v1/admin/tracks/{seeded.TrackId}/quiz/questions",
                new UpsertQuizQuestionRequest(null, text, "",
                    [new UpsertQuizAnswerRequest(null, "Right", true), new UpsertQuizAnswerRequest(null, "Wrong", false)]));
        }

        var after = await (await admin.DeleteAsync(
            $"/api/v1/admin/tracks/{seeded.TrackId}/quiz/questions/{seeded.QuestionId}"))
            .Content.ReadFromJsonAsync<AdminQuizDto>();

        after!.Questions.Select(q => q.OrderNo).Should().Equal(1, 2);
    }
}
