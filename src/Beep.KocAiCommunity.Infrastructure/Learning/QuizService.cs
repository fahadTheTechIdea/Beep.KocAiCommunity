using Beep.KocAiCommunity.Application.Engagement;
using Beep.KocAiCommunity.Application.Learning;
using Beep.KocAiCommunity.Contracts.Learning;
using Beep.KocAiCommunity.Domain.Learning;
using Beep.KocAiCommunity.Infrastructure.Engagement;
using Beep.KocAiCommunity.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Beep.KocAiCommunity.Infrastructure.Learning;

public sealed class QuizService(
    KocDbContext db,
    ILearningService learning,
    IEngagementService engagement) : IQuizService
{
    // ---- Learner ------------------------------------------------------------------------------

    public async Task<QuizDto?> GetForLearnerAsync(Guid trackId, string userId, CancellationToken ct = default)
    {
        var quiz = await db.Quizzes.AsNoTracking().FirstOrDefaultAsync(q => q.TrackId == trackId && q.IsEnabled, ct);
        if (quiz is null)
        {
            return null;
        }

        var questions = await LoadQuestionsAsync(quiz.Id, ct);

        // Attempts are only meaningful for somebody with an account. An anonymous reader can still see
        // that a quiz exists and what passing it takes, which is part of deciding to sign in.
        var attempts = string.IsNullOrEmpty(userId)
            ? []
            : await db.QuizAttempts.AsNoTracking()
                .Where(a => a.QuizId == quiz.Id && a.UserId == userId)
                .ToListAsync(ct);

        return new QuizDto(
            quiz.Id,
            quiz.TrackId,
            quiz.Intro,
            quiz.PassMark,
            quiz.IsMandatory,
            [.. questions.Select(q => new QuizQuestionDto(
                q.Question.Id, q.Question.OrderNo, q.Question.Text,
                [.. q.Answers.Select(a => new QuizAnswerDto(a.Id, a.OrderNo, a.Text))]))],
            attempts.Count == 0 ? null : attempts.Max(a => a.ScorePercent),
            attempts.Any(a => a.Passed),
            attempts.Count);
    }

    public async Task<QuizAttemptResultDto> SubmitAsync(
        Guid trackId, string userId, SubmitQuizRequest request, CancellationToken ct = default)
    {
        var quiz = await db.Quizzes.FirstOrDefaultAsync(q => q.TrackId == trackId && q.IsEnabled, ct)
            ?? throw new QuizException("This track has no quiz open at the moment.");

        var questions = await LoadQuestionsAsync(quiz.Id, ct);
        if (questions.Count == 0)
        {
            // Refusing beats recording a 0-of-0 attempt: a stored empty attempt would sit in the
            // learner's history looking like a failure they earned.
            throw new QuizException("This quiz has no questions yet. Please try again later.");
        }

        var graded = questions
            .Select(q => new GradedQuestion(q.Question.Id, [.. q.Answers.Where(a => a.IsCorrect).Select(a => a.Id)]))
            .ToList();

        // Only options that belong to the question they were sent for. Without this, a response naming
        // an answer id from a different question could satisfy the set comparison.
        var allowed = questions.ToDictionary(q => q.Question.Id, q => q.Answers.Select(a => a.Id).ToHashSet());
        var submitted = request.Responses
            .Where(r => allowed.ContainsKey(r.QuestionId))
            .Select(r => new SubmittedAnswer(r.QuestionId, [.. r.SelectedAnswerIds.Where(id => allowed[r.QuestionId].Contains(id))]))
            .ToList();

        var result = QuizScoring.Grade(graded, submitted, quiz.PassMark);

        var attemptNo = 1 + await db.QuizAttempts.CountAsync(a => a.QuizId == quiz.Id && a.UserId == userId, ct);
        var attempt = new QuizAttempt
        {
            QuizId = quiz.Id,
            UserId = userId,
            AttemptNo = attemptNo,
            SubmittedUtc = DateTime.UtcNow,
            CorrectCount = result.CorrectCount,
            QuestionCount = result.QuestionCount,
            ScorePercent = result.ScorePercent,
            Passed = result.Passed,
            CreatedByUserId = userId,
            CreatedUtc = DateTime.UtcNow,
        };
        db.QuizAttempts.Add(attempt);

        foreach (var response in submitted)
        {
            foreach (var answerId in response.SelectedAnswerIds)
            {
                db.QuizAttemptAnswers.Add(new QuizAttemptAnswer
                {
                    AttemptId = attempt.Id,
                    QuestionId = response.QuestionId,
                    AnswerId = answerId,
                    CreatedByUserId = userId,
                    CreatedUtc = DateTime.UtcNow,
                });
            }
        }

        await db.SaveChangesAsync(ct);

        // Passing may be the last thing the track was waiting for. Re-check before reporting, so the
        // result screen can say the track is finished in the same breath.
        var completedTrack = false;
        if (result.Passed)
        {
            await AwardSafelyAsync(userId, XpSources.QuizPassed, "quiz", quiz.Id, ct);
            await learning.ReevaluateCompletionAsync(trackId, userId, ct);
            completedTrack = await db.TrackCompletions.AsNoTracking()
                .AnyAsync(c => c.TrackId == trackId && c.UserId == userId, ct);
        }

        var chosen = submitted.ToDictionary(s => s.QuestionId, s => s.SelectedAnswerIds);

        return new QuizAttemptResultDto(
            attempt.Id, attempt.AttemptNo, result.CorrectCount, result.QuestionCount, result.ScorePercent,
            result.Passed, attempt.SubmittedUtc,
            [.. questions.Select(q =>
            {
                var correct = q.Answers.Where(a => a.IsCorrect).Select(a => a.Id).ToList();
                var picked = chosen.TryGetValue(q.Question.Id, out var p) ? p.ToList() : [];
                return new QuizQuestionResultDto(
                    q.Question.Id, q.Question.Text,
                    picked.Count > 0 && picked.ToHashSet().SetEquals(correct),
                    correct, picked, q.Question.Explanation);
            })],
            completedTrack);
    }

    public async Task<IReadOnlyList<QuizAttemptSummaryDto>> GetMyAttemptsAsync(
        Guid trackId, string userId, CancellationToken ct = default)
    {
        var quizId = await db.Quizzes.AsNoTracking()
            .Where(q => q.TrackId == trackId).Select(q => (Guid?)q.Id).FirstOrDefaultAsync(ct);

        if (quizId is null)
        {
            return [];
        }

        return await db.QuizAttempts.AsNoTracking()
            .Where(a => a.QuizId == quizId && a.UserId == userId)
            .OrderByDescending(a => a.AttemptNo)
            .Select(a => new QuizAttemptSummaryDto(a.Id, a.AttemptNo, a.ScorePercent, a.Passed, a.SubmittedUtc))
            .ToListAsync(ct);
    }

    // ---- Admin --------------------------------------------------------------------------------

    public async Task<AdminQuizDto?> GetForAdminAsync(Guid trackId, CancellationToken ct = default)
    {
        // Not filtered on IsEnabled: a switched-off quiz is exactly the one an admin needs to open.
        var quiz = await db.Quizzes.AsNoTracking().FirstOrDefaultAsync(q => q.TrackId == trackId, ct);
        return quiz is null ? null : await ToAdminDtoAsync(quiz, ct);
    }

    public async Task<AdminQuizDto> UpsertAsync(
        Guid trackId, UpsertQuizRequest request, string adminUserId, CancellationToken ct = default)
    {
        if (!await db.LearningTracks.AnyAsync(t => t.Id == trackId, ct))
        {
            throw new QuizException("That track no longer exists.");
        }

        var quiz = await db.Quizzes.FirstOrDefaultAsync(q => q.TrackId == trackId, ct);
        if (quiz is null)
        {
            quiz = new Quiz { TrackId = trackId, CreatedByUserId = adminUserId, CreatedUtc = DateTime.UtcNow };
            db.Quizzes.Add(quiz);
        }

        quiz.Intro = (request.Intro ?? string.Empty).Trim();
        quiz.PassMark = QuizScoring.NormalizePassMark(request.PassMark);
        quiz.IsEnabled = request.IsEnabled;
        quiz.IsMandatory = request.IsMandatory;

        await db.SaveChangesAsync(ct);
        return await ToAdminDtoAsync(quiz, ct);
    }

    public async Task<AdminQuizDto> SaveQuestionAsync(
        Guid trackId, UpsertQuizQuestionRequest request, string adminUserId, CancellationToken ct = default)
    {
        var quiz = await db.Quizzes.FirstOrDefaultAsync(q => q.TrackId == trackId, ct)
            ?? throw new QuizException("Create the quiz before adding questions to it.");

        if (string.IsNullOrWhiteSpace(request.Text))
        {
            throw new QuizException("A question needs some text.");
        }

        // Refused at the point of saving rather than at the point of sitting. A question with fewer than
        // two options is not a question, and one with no correct option is unanswerable — every attempt
        // would be capped below full marks with nothing to show the learner why.
        if (request.Answers.Count < 2)
        {
            throw new QuizException("A question needs at least two answers to choose between.");
        }

        if (!request.Answers.Any(a => a.IsCorrect))
        {
            throw new QuizException("Mark at least one answer as correct, or nobody can answer this question.");
        }

        if (request.Answers.Any(a => string.IsNullOrWhiteSpace(a.Text)))
        {
            throw new QuizException("Every answer needs some text.");
        }

        QuizQuestion question;
        if (request.Id is { } id && await db.QuizQuestions.FirstOrDefaultAsync(q => q.Id == id && q.QuizId == quiz.Id, ct) is { } existing)
        {
            question = existing;

            // Replace the options wholesale. Matching them up one by one would keep attempt rows pointing
            // at an option whose meaning had been edited underneath them.
            var old = await db.QuizAnswers.Where(a => a.QuestionId == question.Id).ToListAsync(ct);
            db.QuizAnswers.RemoveRange(old);
        }
        else
        {
            question = new QuizQuestion
            {
                QuizId = quiz.Id,
                OrderNo = 1 + await db.QuizQuestions.CountAsync(q => q.QuizId == quiz.Id, ct),
                CreatedByUserId = adminUserId,
                CreatedUtc = DateTime.UtcNow,
            };
            db.QuizQuestions.Add(question);
        }

        question.Text = request.Text.Trim();
        question.Explanation = (request.Explanation ?? string.Empty).Trim();

        var order = 1;
        foreach (var answer in request.Answers)
        {
            db.QuizAnswers.Add(new QuizAnswer
            {
                QuestionId = question.Id,
                OrderNo = order++,
                Text = answer.Text.Trim(),
                IsCorrect = answer.IsCorrect,
                CreatedByUserId = adminUserId,
                CreatedUtc = DateTime.UtcNow,
            });
        }

        await db.SaveChangesAsync(ct);
        return await ToAdminDtoAsync(quiz, ct);
    }

    public async Task<AdminQuizDto> DeleteQuestionAsync(
        Guid trackId, Guid questionId, string adminUserId, CancellationToken ct = default)
    {
        var quiz = await db.Quizzes.FirstOrDefaultAsync(q => q.TrackId == trackId, ct)
            ?? throw new QuizException("That track has no quiz.");

        var question = await db.QuizQuestions.FirstOrDefaultAsync(q => q.Id == questionId && q.QuizId == quiz.Id, ct);
        if (question is not null)
        {
            db.QuizQuestions.Remove(question);
            await db.SaveChangesAsync(ct);

            // Close the gap so the numbering a learner sees stays 1, 2, 3.
            var remaining = await db.QuizQuestions.Where(q => q.QuizId == quiz.Id).OrderBy(q => q.OrderNo).ToListAsync(ct);
            for (var i = 0; i < remaining.Count; i++)
            {
                remaining[i].OrderNo = i + 1;
            }

            await db.SaveChangesAsync(ct);
        }

        return await ToAdminDtoAsync(quiz, ct);
    }

    // ---- Shared -------------------------------------------------------------------------------

    private sealed record LoadedQuestion(QuizQuestion Question, IReadOnlyList<QuizAnswer> Answers);

    /// <summary>Questions with their options, in order, in two queries rather than one per question.</summary>
    private async Task<IReadOnlyList<LoadedQuestion>> LoadQuestionsAsync(Guid quizId, CancellationToken ct)
    {
        var questions = await db.QuizQuestions.AsNoTracking()
            .Where(q => q.QuizId == quizId).OrderBy(q => q.OrderNo).ToListAsync(ct);

        var ids = questions.Select(q => q.Id).ToList();
        var answers = await db.QuizAnswers.AsNoTracking()
            .Where(a => ids.Contains(a.QuestionId)).OrderBy(a => a.OrderNo).ToListAsync(ct);

        var byQuestion = answers.GroupBy(a => a.QuestionId).ToDictionary(g => g.Key, g => (IReadOnlyList<QuizAnswer>)[.. g]);
        return [.. questions.Select(q => new LoadedQuestion(q, byQuestion.GetValueOrDefault(q.Id, [])))];
    }

    private async Task<AdminQuizDto> ToAdminDtoAsync(Quiz quiz, CancellationToken ct)
    {
        var questions = await LoadQuestionsAsync(quiz.Id, ct);
        return new AdminQuizDto(
            quiz.Id, quiz.TrackId, quiz.Intro, quiz.PassMark, quiz.IsMandatory, quiz.IsEnabled,
            [.. questions.Select(q => new AdminQuizQuestionDto(
                q.Question.Id, q.Question.OrderNo, q.Question.Text, q.Question.Explanation,
                [.. q.Answers.Select(a => new AdminQuizAnswerDto(a.Id, a.OrderNo, a.Text, a.IsCorrect))]))]);
    }

    // Engagement is a side effect: a failure awarding Barrels must never fail the attempt itself.
    private async Task AwardSafelyAsync(string userId, string source, string refType, Guid refId, CancellationToken ct)
    {
        try
        {
            await engagement.AwardXpAsync(userId, source, refType, refId, ct);
        }
        catch (Exception)
        {
            // Swallow — the attempt is already recorded.
        }
    }
}
