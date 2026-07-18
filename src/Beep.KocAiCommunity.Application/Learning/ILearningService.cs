using Beep.KocAiCommunity.Domain.Learning;

namespace Beep.KocAiCommunity.Application.Learning;

/// <summary>A track the user is enrolled in, with progress.</summary>
public sealed record MyLearningItem(TrackEnrollment Enrollment, string Title, int CompletedLessons, int TotalLessons);

/// <summary>Learner-facing operations for guided learning tracks.</summary>
public interface ILearningService
{
    Task<IReadOnlyList<LearningTrack>> BrowseVisibleAsync(string userId, CancellationToken ct = default);
    Task<LearningTrack?> GetAsync(Guid trackId, CancellationToken ct = default);
    Task<IReadOnlyList<Lesson>> GetLessonsAsync(Guid trackId, CancellationToken ct = default);

    /// <summary>Enroll the user (idempotent — returns the existing enrollment if present).</summary>
    Task<TrackEnrollment> EnrollAsync(string userId, Guid trackId, CancellationToken ct = default);

    /// <summary>Mark a lesson complete (auto-enrolls). Completing the last lesson completes the track.</summary>
    Task<LessonProgress> CompleteLessonAsync(string userId, Guid trackId, Guid lessonId, CancellationToken ct = default);

    Task<IReadOnlyList<MyLearningItem>> GetMyLearningAsync(string userId, CancellationToken ct = default);
}
