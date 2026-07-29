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
