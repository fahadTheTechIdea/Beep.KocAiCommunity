namespace Beep.KocAiCommunity.Contracts.Learning;

/// <summary>A learning track summary card.</summary>
public sealed record TrackDto(
    Guid Id, string Title, string Summary, string Level, int OrderNo, string Domain, int LessonCount,
    /// <summary>The competition this track prepares you for, when an admin has linked one.</summary>
    Guid? RecommendedCompetitionId = null,
    string? RecommendedCompetitionTitle = null,
    /// <summary>The language this track is written in — which may not be the one that was asked for.</summary>
    string Language = "en");

/// <summary>A lesson within a track.</summary>
public sealed record LessonDto(Guid Id, int OrderNo, string Title, int EstimatedMinutes, string? HandsOnKind, string? Content);

/// <summary>A track with its lessons.</summary>
public sealed record TrackDetailDto(
    Guid Id, string Title, string Summary, string Level, string Domain, IReadOnlyList<LessonDto> Lessons,
    string Language = "en",
    /// <summary>The same material in other languages, keyed by language code. Empty when untranslated.</summary>
    IReadOnlyDictionary<string, Guid>? Translations = null);

/// <summary>The learner's enrollment + progress in one track.</summary>
public sealed record MyLearningDto(Guid TrackId, string Title, string Status, int CompletedLessons, int TotalLessons);

/// <summary>Enrollment state.</summary>
public sealed record EnrollmentDto(Guid TrackId, string Status, DateTime StartedUtc, DateTime? CompletedUtc);

// ── Quizzes ─────────────────────────────────────────────────────────────────────────────────────
// The learner shape and the admin shape are separate records rather than one record with the answers
// nulled out on the way to a learner. A shared shape means the field exists on the wire and staying
// secret depends on every endpoint remembering to blank it; separate types make leaking it a compile
// error instead of a code review.

/// <summary>One option as a learner sees it. There is deliberately no "is this the right one" here.</summary>
public sealed record QuizAnswerDto(Guid Id, int OrderNo, string Text);

/// <summary>One question as a learner sees it, before answering.</summary>
public sealed record QuizQuestionDto(Guid Id, int OrderNo, string Text, IReadOnlyList<QuizAnswerDto> Answers);

/// <summary>The quiz a learner is about to sit.</summary>
public sealed record QuizDto(
    Guid Id,
    Guid TrackId,
    string Intro,
    int PassMark,
    bool IsMandatory,
    IReadOnlyList<QuizQuestionDto> Questions,
    /// <summary>The learner's best score so far, or null if they have not sat it.</summary>
    int? BestScorePercent = null,
    bool HasPassed = false,
    int AttemptCount = 0);

/// <summary>One question's answers, as submitted. An unanswered question is simply absent.</summary>
public sealed record QuizResponseDto(Guid QuestionId, IReadOnlyList<Guid> SelectedAnswerIds);

public sealed record SubmitQuizRequest(IReadOnlyList<QuizResponseDto> Responses);

/// <summary>
/// How one question went, returned only after the attempt is graded. This is the one place the correct
/// answers are disclosed, and only for a quiz the caller has just sat.
/// </summary>
public sealed record QuizQuestionResultDto(
    Guid QuestionId, string Text, bool WasCorrect, IReadOnlyList<Guid> CorrectAnswerIds,
    IReadOnlyList<Guid> SelectedAnswerIds, string Explanation);

/// <summary>The result of a sitting.</summary>
public sealed record QuizAttemptResultDto(
    Guid AttemptId, int AttemptNo, int CorrectCount, int QuestionCount, int ScorePercent, bool Passed,
    DateTime SubmittedUtc,
    IReadOnlyList<QuizQuestionResultDto> Questions,
    /// <summary>True when passing this attempt is what completed the track.</summary>
    bool CompletedTrack = false);

/// <summary>A past sitting, for the history list. No per-question detail.</summary>
public sealed record QuizAttemptSummaryDto(
    Guid Id, int AttemptNo, int ScorePercent, bool Passed, DateTime SubmittedUtc);

// ---- Admin shapes: these carry the correct answers, and only admin endpoints return them ----

public sealed record AdminQuizAnswerDto(Guid Id, int OrderNo, string Text, bool IsCorrect);

public sealed record AdminQuizQuestionDto(
    Guid Id, int OrderNo, string Text, string Explanation, IReadOnlyList<AdminQuizAnswerDto> Answers);

public sealed record AdminQuizDto(
    Guid Id, Guid TrackId, string Intro, int PassMark, bool IsMandatory, bool IsEnabled,
    IReadOnlyList<AdminQuizQuestionDto> Questions);

/// <summary>Creates the quiz on first save, updates it thereafter.</summary>
public sealed record UpsertQuizRequest(string Intro, int PassMark, bool IsMandatory, bool IsEnabled);

public sealed record UpsertQuizAnswerRequest(Guid? Id, string Text, bool IsCorrect);

/// <summary>A question and all of its answers, saved together — a question is not useful half-saved.</summary>
public sealed record UpsertQuizQuestionRequest(
    Guid? Id, string Text, string Explanation, IReadOnlyList<UpsertQuizAnswerRequest> Answers);
