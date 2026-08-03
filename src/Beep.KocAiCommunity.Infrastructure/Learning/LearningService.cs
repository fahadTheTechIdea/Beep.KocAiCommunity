using Beep.KocAiCommunity.Application.Authorization;
using Beep.KocAiCommunity.Application.Engagement;
using Beep.KocAiCommunity.Application.Learning;
using Beep.KocAiCommunity.Domain.Learning;
using Beep.KocAiCommunity.Infrastructure.Engagement;
using Beep.KocAiCommunity.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Beep.KocAiCommunity.Infrastructure.Learning;

public sealed class LearningService(KocDbContext db, IVisibilityEvaluator visibility, IEngagementService engagement) : ILearningService
{
    public async Task<IReadOnlyList<LearningTrack>> BrowseVisibleAsync(string userId, string language, CancellationToken ct = default)
    {
        var wanted = TrackLanguages.Normalize(language);

        var published = await db.LearningTracks
            .AsNoTracking()
            .Where(t => t.Status == "published")
            .OrderBy(t => t.OrderNo)
            .ToListAsync(ct);

        // Choose one row per piece of material: the reader's language when it exists, otherwise whatever
        // the material was written in. Filtering on language in the query instead would silently drop
        // every untranslated track, which for a catalogue that is mostly English reads as a broken page.
        var translated = published
            .Where(t => t.ContentKey.Length > 0 && t.Language == wanted)
            .Select(t => t.ContentKey)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var chosen = published.Where(t =>
            t.Language == wanted || t.ContentKey.Length == 0 || !translated.Contains(t.ContentKey));

        var visible = new List<LearningTrack>(published.Count);
        foreach (var track in chosen)
        {
            if (await visibility.CanSeeAsync(userId, track.VisibilityScope, track.VisibilityOrgUnitId, ct))
            {
                visible.Add(track);
            }
        }

        return visible;
    }

    public async Task<IReadOnlyDictionary<string, Guid>> GetTranslationsAsync(Guid trackId, CancellationToken ct = default)
    {
        var key = await db.LearningTracks.AsNoTracking()
            .Where(t => t.Id == trackId)
            .Select(t => t.ContentKey)
            .FirstOrDefaultAsync(ct);

        if (string.IsNullOrEmpty(key))
        {
            return new Dictionary<string, Guid>();
        }

        var siblings = await db.LearningTracks.AsNoTracking()
            .Where(t => t.ContentKey == key && t.Status == "published")
            .Select(t => new { t.Language, t.Id })
            .ToListAsync(ct);

        return siblings.ToDictionary(s => s.Language, s => s.Id, StringComparer.OrdinalIgnoreCase);
    }

    public Task<LearningTrack?> GetAsync(Guid trackId, CancellationToken ct = default) =>
        db.LearningTracks.AsNoTracking().FirstOrDefaultAsync(t => t.Id == trackId, ct);

    public async Task<IReadOnlyList<Lesson>> GetLessonsAsync(Guid trackId, CancellationToken ct = default) =>
        await db.Lessons.AsNoTracking()
            .Where(l => l.TrackId == trackId)
            .OrderBy(l => l.OrderNo)
            .ToListAsync(ct);

    public async Task<TrackEnrollment> EnrollAsync(string userId, Guid trackId, CancellationToken ct = default)
    {
        var existing = await db.TrackEnrollments.FirstOrDefaultAsync(e => e.TrackId == trackId && e.UserId == userId, ct);
        if (existing is not null)
        {
            return existing;
        }

        var enrollment = new TrackEnrollment
        {
            TrackId = trackId,
            UserId = userId,
            Status = TrackEnrollmentStatus.Active,
            StartedUtc = DateTime.UtcNow,
            CreatedUtc = DateTime.UtcNow,
        };
        db.TrackEnrollments.Add(enrollment);
        await db.SaveChangesAsync(ct);
        return enrollment;
    }

    public async Task<LessonProgress> CompleteLessonAsync(string userId, Guid trackId, Guid lessonId, CancellationToken ct = default)
    {
        var enrollment = await EnrollAsync(userId, trackId, ct);

        var progress = await db.LessonProgress
            .FirstOrDefaultAsync(p => p.EnrollmentId == enrollment.Id && p.LessonId == lessonId, ct);

        if (progress is null)
        {
            progress = new LessonProgress
            {
                EnrollmentId = enrollment.Id,
                LessonId = lessonId,
                CreatedUtc = DateTime.UtcNow,
            };
            db.LessonProgress.Add(progress);
        }

        var wasCompleted = progress.Status == "completed";
        progress.Status = "completed";
        progress.CompletedUtc = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);

        // Barrels for completing a lesson (idempotent per lesson — re-completing awards nothing more).
        if (!wasCompleted)
        {
            await AwardSafelyAsync(userId, XpSources.LessonCompleted, "lesson", lessonId, ct);
        }

        await MaybeCompleteTrackAsync(enrollment, trackId, userId, ct);
        return progress;
    }

    public async Task<IReadOnlyList<MyLearningItem>> GetMyLearningAsync(string userId, CancellationToken ct = default)
    {
        var enrollments = await db.TrackEnrollments.AsNoTracking()
            .Where(e => e.UserId == userId)
            .ToListAsync(ct);

        var items = new List<MyLearningItem>(enrollments.Count);
        foreach (var enrollment in enrollments)
        {
            var title = await db.LearningTracks.Where(t => t.Id == enrollment.TrackId).Select(t => t.Title).FirstOrDefaultAsync(ct) ?? "";
            var total = await db.Lessons.CountAsync(l => l.TrackId == enrollment.TrackId, ct);
            var completed = await db.LessonProgress.CountAsync(p => p.EnrollmentId == enrollment.Id && p.Status == "completed", ct);
            items.Add(new MyLearningItem(enrollment, title, completed, total));
        }

        return items;
    }

    /// <summary>
    /// Re-checks whether a track is finished, from whichever side just changed.
    /// <para>
    /// Called after a lesson is completed and again after a quiz is passed, because with a mandatory
    /// quiz either one can be the last thing standing: somebody can finish the lessons and still owe
    /// the quiz, or pass the quiz having finished the lessons weeks ago. Doing this only on the lesson
    /// path would leave a passed quiz never completing the track.
    /// </para>
    /// </summary>
    public async Task ReevaluateCompletionAsync(Guid trackId, string userId, CancellationToken ct = default)
    {
        var enrollment = await db.TrackEnrollments.AsNoTracking()
            .FirstOrDefaultAsync(e => e.TrackId == trackId && e.UserId == userId, ct);

        if (enrollment is not null)
        {
            await MaybeCompleteTrackAsync(enrollment, trackId, userId, ct);
        }
    }

    /// <summary>
    /// Whether the quiz gate is open for this person: no quiz, a quiz that is off, an optional quiz, or
    /// a mandatory one they have passed. A mandatory quiz with no questions stays shut — nobody can
    /// pass what has nothing in it, and reporting it as satisfied would hide the misconfiguration.
    /// </summary>
    private async Task<bool> QuizGateSatisfiedAsync(Guid trackId, string userId, CancellationToken ct)
    {
        var quiz = await db.Quizzes.AsNoTracking()
            .FirstOrDefaultAsync(q => q.TrackId == trackId && q.IsEnabled && q.IsMandatory, ct);

        return quiz is null
            || await db.QuizAttempts.AsNoTracking().AnyAsync(a => a.QuizId == quiz.Id && a.UserId == userId && a.Passed, ct);
    }

    private async Task MaybeCompleteTrackAsync(TrackEnrollment enrollment, Guid trackId, string userId, CancellationToken ct)
    {
        var total = await db.Lessons.CountAsync(l => l.TrackId == trackId, ct);
        if (total == 0)
        {
            return;
        }

        var completed = await db.LessonProgress.CountAsync(p => p.EnrollmentId == enrollment.Id && p.Status == "completed", ct);
        if (completed < total)
        {
            return;
        }

        // A track already finished stays finished. An admin turning a quiz mandatory afterwards must not
        // reach back and un-complete work somebody has done — and half-revoking it, which is what
        // demoting the enrollment while leaving the completion row behind amounts to, is worse than
        // either taking it all back or leaving it alone.
        if (await db.TrackCompletions.AnyAsync(c => c.TrackId == trackId && c.UserId == userId, ct))
        {
            return;
        }

        // Every lesson is read, so the only thing that can still be owed is a mandatory quiz. Say so on
        // the enrollment rather than leaving it "in-progress": the learner has finished the reading, and
        // a progress bar at 8/8 next to "in progress" reads as a bug rather than as one step left.
        if (!await QuizGateSatisfiedAsync(trackId, userId, ct))
        {
            var awaiting = await db.TrackEnrollments.FirstAsync(e => e.Id == enrollment.Id, ct);
            if (awaiting.Status != TrackEnrollmentStatus.AwaitingQuiz)
            {
                awaiting.Status = TrackEnrollmentStatus.AwaitingQuiz;
                await db.SaveChangesAsync(ct);
            }

            return;
        }

        var tracked = await db.TrackEnrollments.FirstAsync(e => e.Id == enrollment.Id, ct);
        tracked.Status = TrackEnrollmentStatus.Completed;
        tracked.CompletedUtc = DateTime.UtcNow;

        var alreadyRecorded = await db.TrackCompletions.AnyAsync(c => c.TrackId == trackId && c.UserId == userId, ct);
        if (!alreadyRecorded)
        {
            db.TrackCompletions.Add(new TrackCompletion
            {
                TrackId = trackId,
                UserId = userId,
                CompletedUtc = DateTime.UtcNow,
                CreatedUtc = DateTime.UtcNow,
            });
        }

        await db.SaveChangesAsync(ct);

        // Barrels for finishing the whole track (idempotent per track).
        if (!alreadyRecorded)
        {
            await AwardSafelyAsync(userId, XpSources.TrackCompleted, "track", trackId, ct);
        }
    }

    // Engagement is a side effect: a failure here must never fail the learning action.
    private async Task AwardSafelyAsync(string userId, string source, string refType, Guid refId, CancellationToken ct)
    {
        try
        {
            await engagement.AwardXpAsync(userId, source, refType, refId, ct);
        }
        catch (Exception)
        {
            // Swallow — the lesson/track completion already committed.
        }
    }
}
