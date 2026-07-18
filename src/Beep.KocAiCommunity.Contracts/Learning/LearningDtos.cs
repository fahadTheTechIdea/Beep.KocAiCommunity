namespace Beep.KocAiCommunity.Contracts.Learning;

/// <summary>A learning track summary card.</summary>
public sealed record TrackDto(Guid Id, string Title, string Summary, string Level, int OrderNo, string Domain, int LessonCount);

/// <summary>A lesson within a track.</summary>
public sealed record LessonDto(Guid Id, int OrderNo, string Title, int EstimatedMinutes, string? HandsOnKind, string? Content);

/// <summary>A track with its lessons.</summary>
public sealed record TrackDetailDto(Guid Id, string Title, string Summary, string Level, string Domain, IReadOnlyList<LessonDto> Lessons);

/// <summary>The learner's enrollment + progress in one track.</summary>
public sealed record MyLearningDto(Guid TrackId, string Title, string Status, int CompletedLessons, int TotalLessons);

/// <summary>Enrollment state.</summary>
public sealed record EnrollmentDto(Guid TrackId, string Status, DateTime StartedUtc, DateTime? CompletedUtc);
