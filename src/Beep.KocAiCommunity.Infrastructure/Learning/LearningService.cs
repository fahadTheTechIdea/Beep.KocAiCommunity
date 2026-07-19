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
    public async Task<IReadOnlyList<LearningTrack>> BrowseVisibleAsync(string userId, CancellationToken ct = default)
    {
        var published = await db.LearningTracks
            .AsNoTracking()
            .Where(t => t.Status == "published")
            .OrderBy(t => t.OrderNo)
            .ToListAsync(ct);

        var visible = new List<LearningTrack>(published.Count);
        foreach (var track in published)
        {
            if (await visibility.CanSeeAsync(userId, track.VisibilityScope, track.VisibilityOrgUnitId, ct))
            {
                visible.Add(track);
            }
        }

        return visible;
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
            Status = "active",
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

        var tracked = await db.TrackEnrollments.FirstAsync(e => e.Id == enrollment.Id, ct);
        tracked.Status = "completed";
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
