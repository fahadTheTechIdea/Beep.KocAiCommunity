using Beep.KocAiCommunity.Application.Learning;
using Beep.KocAiCommunity.Domain.Engagement;
using Beep.KocAiCommunity.Domain.Learning;
using Beep.KocAiCommunity.Infrastructure.Engagement;
using Beep.KocAiCommunity.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Beep.KocAiCommunity.IntegrationTests;

/// <summary>
/// What a mandatory quiz actually blocks.
/// <para>
/// "Mandatory" is the whole point of the admin switch, so the behaviour has to be exact: reading every
/// lesson is no longer finishing the track. These run against a real database and the real service,
/// because the rule spans three tables and the interesting cases are about what is <b>not</b> written —
/// no completion row, no Barrels, no badge — which a mocked service cannot show.
/// </para>
/// </summary>
public class QuizGateTests(KocApiFactory factory) : IClassFixture<KocApiFactory>
{
    private readonly KocApiFactory _factory = factory;

    private sealed record Fixture(Guid TrackId, Guid LessonId, Guid? QuizId);

    /// <summary>A published one-lesson track, optionally with a quiz of a given kind.</summary>
    private async Task<Fixture> SeedAsync(bool withQuiz = false, bool mandatory = false, bool enabled = true, int questions = 1)
    {
        // The factory starts its host lazily and migrates the database as it starts, so taking a scope
        // before any client exists reaches a provider with no tables in it.
        _ = _factory.CreateClientAs(sub: null);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<KocDbContext>();

        var track = new LearningTrack
        {
            Title = "Quiz gate " + Guid.NewGuid().ToString("N")[..6],
            Summary = "for the gate tests",
            Status = "published",
            Domain = "ml",
            Level = TrackLevel.Beginner,
            CreatedUtc = DateTime.UtcNow,
        };
        db.LearningTracks.Add(track);

        var lesson = new Lesson
        {
            TrackId = track.Id,
            OrderNo = 1,
            Title = "The only lesson",
            ContentRef = "none",
            Content = "body",
            EstimatedMinutes = 5,
            CreatedUtc = DateTime.UtcNow,
        };
        db.Lessons.Add(lesson);

        Guid? quizId = null;
        if (withQuiz)
        {
            var quiz = new Quiz
            {
                TrackId = track.Id,
                PassMark = 70,
                IsMandatory = mandatory,
                IsEnabled = enabled,
                CreatedUtc = DateTime.UtcNow,
            };
            db.Quizzes.Add(quiz);
            quizId = quiz.Id;

            for (var i = 0; i < questions; i++)
            {
                var question = new QuizQuestion
                {
                    QuizId = quiz.Id,
                    OrderNo = i + 1,
                    Text = $"Question {i + 1}?",
                    CreatedUtc = DateTime.UtcNow,
                };
                db.QuizQuestions.Add(question);
                db.QuizAnswers.Add(new QuizAnswer
                {
                    QuestionId = question.Id, OrderNo = 1, Text = "Right", IsCorrect = true, CreatedUtc = DateTime.UtcNow,
                });
            }
        }

        await db.SaveChangesAsync();
        return new Fixture(track.Id, lesson.Id, quizId);
    }

    private async Task FinishTheReadingAsync(string userId, Fixture f)
    {
        using var scope = _factory.Services.CreateScope();
        var learning = scope.ServiceProvider.GetRequiredService<ILearningService>();
        await learning.EnrollAsync(userId, f.TrackId);
        await learning.CompleteLessonAsync(userId, f.TrackId, f.LessonId);
    }

    private async Task PassTheQuizAsync(string userId, Guid quizId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<KocDbContext>();
        db.QuizAttempts.Add(new QuizAttempt
        {
            QuizId = quizId,
            UserId = userId,
            AttemptNo = 1,
            SubmittedUtc = DateTime.UtcNow,
            CorrectCount = 1,
            QuestionCount = 1,
            ScorePercent = 100,
            Passed = true,
            CreatedUtc = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();
    }

    private async Task<(string Status, bool Completed)> StateAsync(string userId, Guid trackId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<KocDbContext>();
        var status = await db.TrackEnrollments.AsNoTracking()
            .Where(e => e.TrackId == trackId && e.UserId == userId).Select(e => e.Status).FirstAsync();
        var completed = await db.TrackCompletions.AsNoTracking()
            .AnyAsync(c => c.TrackId == trackId && c.UserId == userId);
        return (status, completed);
    }

    [Fact]
    public async Task With_no_quiz_finishing_the_lessons_still_finishes_the_track()
    {
        // The regression guard. Most tracks have no quiz, and they must behave exactly as before.
        var f = await SeedAsync(withQuiz: false);
        await FinishTheReadingAsync("gate-none", f);

        var (status, completed) = await StateAsync("gate-none", f.TrackId);

        status.Should().Be(TrackEnrollmentStatus.Completed);
        completed.Should().BeTrue();
    }

    [Fact]
    public async Task An_optional_quiz_blocks_nothing()
    {
        var f = await SeedAsync(withQuiz: true, mandatory: false);
        await FinishTheReadingAsync("gate-optional", f);

        var (status, completed) = await StateAsync("gate-optional", f.TrackId);

        status.Should().Be(TrackEnrollmentStatus.Completed, "an optional quiz is a self-check, not a gate");
        completed.Should().BeTrue();
    }

    [Fact]
    public async Task A_disabled_mandatory_quiz_blocks_nothing()
    {
        // Withdrawing a quiz must not strand everybody mid-track behind a quiz they cannot open.
        var f = await SeedAsync(withQuiz: true, mandatory: true, enabled: false);
        await FinishTheReadingAsync("gate-disabled", f);

        (await StateAsync("gate-disabled", f.TrackId)).Completed.Should().BeTrue();
    }

    [Fact]
    public async Task A_mandatory_quiz_holds_the_track_at_awaiting_quiz()
    {
        var f = await SeedAsync(withQuiz: true, mandatory: true);
        await FinishTheReadingAsync("gate-owed", f);

        var (status, completed) = await StateAsync("gate-owed", f.TrackId);

        status.Should().Be(TrackEnrollmentStatus.AwaitingQuiz, "the reading is done and the quiz is not");
        completed.Should().BeFalse("no completion row, so no Barrels and no badge either");
    }

    [Fact]
    public async Task Passing_the_quiz_completes_the_track_from_the_quiz_side()
    {
        // The case that a lesson-only re-check would miss entirely: nothing on the lesson path ever runs
        // again, so a passed quiz would leave the track owed forever.
        var f = await SeedAsync(withQuiz: true, mandatory: true);
        await FinishTheReadingAsync("gate-passed", f);
        await PassTheQuizAsync("gate-passed", f.QuizId!.Value);

        using (var scope = _factory.Services.CreateScope())
        {
            await scope.ServiceProvider.GetRequiredService<ILearningService>()
                .ReevaluateCompletionAsync(f.TrackId, "gate-passed");
        }

        var (status, completed) = await StateAsync("gate-passed", f.TrackId);

        status.Should().Be(TrackEnrollmentStatus.Completed);
        completed.Should().BeTrue();
    }

    [Fact]
    public async Task A_mandatory_quiz_with_no_questions_keeps_the_gate_shut()
    {
        // Nobody can pass what has nothing in it. Treating it as satisfied would let a misconfigured
        // quiz quietly wave everyone through, which is the failure worth being loud about.
        var f = await SeedAsync(withQuiz: true, mandatory: true, questions: 0);
        await FinishTheReadingAsync("gate-empty", f);

        (await StateAsync("gate-empty", f.TrackId)).Status.Should().Be(TrackEnrollmentStatus.AwaitingQuiz);
    }

    [Fact]
    public async Task Making_a_quiz_mandatory_later_does_not_take_back_a_finished_track()
    {
        // Somebody finished the track under the old rules. Revoking a completion they earned — and the
        // Barrels and badge with it — because an admin changed a setting afterwards would be indefensible.
        var f = await SeedAsync(withQuiz: true, mandatory: false);
        await FinishTheReadingAsync("gate-grandfathered", f);

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<KocDbContext>();
            var quiz = await db.Quizzes.FirstAsync(q => q.Id == f.QuizId!.Value);
            quiz.IsMandatory = true;
            await db.SaveChangesAsync();

            await scope.ServiceProvider.GetRequiredService<ILearningService>()
                .ReevaluateCompletionAsync(f.TrackId, "gate-grandfathered");
        }

        var (status, completed) = await StateAsync("gate-grandfathered", f.TrackId);

        status.Should().Be(TrackEnrollmentStatus.Completed, "it was finished before the rule changed");
        completed.Should().BeTrue();
    }

    [Fact]
    public async Task Finishing_a_track_awards_the_badge_that_track_carries()
    {
        // The catalogue row is created here, the first time anybody finishes, rather than being
        // something an admin has to remember — a track that quietly awards nothing is the failure
        // this is designed against.
        var f = await SeedAsync();
        await FinishTheReadingAsync("badge-earner", f);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<KocDbContext>();
        var code = BadgeCatalog.ForTrack(f.TrackId);

        (await db.Set<Badge>().AnyAsync(b => b.Code == code))
            .Should().BeTrue("the badge's catalogue row is created on first completion");
        (await db.Set<UserBadge>().AnyAsync(b => b.UserId == "badge-earner" && b.BadgeCode == code))
            .Should().BeTrue();
    }

    [Fact]
    public async Task A_second_person_finishing_reuses_the_same_badge_row()
    {
        var f = await SeedAsync();
        await FinishTheReadingAsync("badge-one", f);
        await FinishTheReadingAsync("badge-two", f);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<KocDbContext>();
        var code = BadgeCatalog.ForTrack(f.TrackId);

        (await db.Set<Badge>().CountAsync(b => b.Code == code))
            .Should().Be(1, "a second completion must not create a second catalogue row");
        (await db.Set<UserBadge>().CountAsync(b => b.BadgeCode == code)).Should().Be(2);
    }

    [Fact]
    public async Task No_badge_while_a_mandatory_quiz_is_still_owed()
    {
        // The badge follows the completion, so the gate has to hold it back too. Awarding it at the end
        // of the lessons would hand out the reward the gate exists to withhold.
        var f = await SeedAsync(withQuiz: true, mandatory: true);
        await FinishTheReadingAsync("badge-owed", f);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<KocDbContext>();

        (await db.Set<UserBadge>().AnyAsync(b => b.UserId == "badge-owed" && b.BadgeCode == BadgeCatalog.ForTrack(f.TrackId)))
            .Should().BeFalse();
    }
}
