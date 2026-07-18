using Beep.KocAiCommunity.Domain.Learning;
using Beep.KocAiCommunity.Domain.Organization;
using Beep.KocAiCommunity.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Beep.KocAiCommunity.Infrastructure.Learning;

/// <summary>
/// Seeds the three company-wide starter tracks (idempotent). Runs at dev startup and from tests.
/// </summary>
public static class LearningSeeder
{
    public static async Task SeedTracksAsync(KocDbContext db, CancellationToken ct = default)
    {
        if (await db.LearningTracks.AnyAsync(ct))
        {
            return;
        }

        var stamp = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        AddTrack(db, stamp, order: 1, TrackLevel.Beginner,
            "Getting started with data",
            "Read, clean, and make sense of a dataset. For anyone curious about AI — no coding needed.",
            ["What is a dataset?", "Loading well data", "Spotting gaps and outliers", "Simple summaries", "Charts that tell the truth", "Your first insight"]);

        AddTrack(db, stamp, order: 2, TrackLevel.Intermediate,
            "Solve a real problem",
            "Build a model for a production, facilities, or subsurface question using your own data.",
            ["Framing the question", "Features from sensor tags", "Train / test split by time", "Fit your first model", "Read the metrics", "Avoid leakage", "Compare two models", "Ship a prediction"]);

        AddTrack(db, stamp, order: 3, TrackLevel.Advanced,
            "Make it dependable",
            "Tune, check, and package a model your team can trust and reuse day to day.",
            ["Reproducible runs", "Tuning with AutoML", "Validation that holds up", "Explainability basics", "Versioning a model", "Rollback and approvals", "Hand-over to the team"]);

        await db.SaveChangesAsync(ct);
    }

    private static void AddTrack(KocDbContext db, DateTime stamp, int order, TrackLevel level, string title, string summary, string[] lessonTitles)
    {
        var track = new LearningTrack
        {
            Title = title,
            Summary = summary,
            Level = level,
            OrderNo = order,
            Status = "published",
            Domain = "upstream",
            VisibilityScope = VisibilityScope.Company,
            CreatedUtc = stamp,
        };
        db.LearningTracks.Add(track);

        for (var i = 0; i < lessonTitles.Length; i++)
        {
            db.Lessons.Add(new Lesson
            {
                TrackId = track.Id,
                OrderNo = i + 1,
                Title = lessonTitles[i],
                ContentRef = $"seed://{level}/{order}/{i + 1}",
                Content = Body(lessonTitles[i], level, i + 1),
                EstimatedMinutes = 20,
                CreatedUtc = stamp,
            });
        }
    }

    // A real, structured markdown lesson body — KOC-flavoured so the content feels like the job.
    private static string Body(string title, TrackLevel level, int order)
    {
        var intro = level switch
        {
            TrackLevel.Beginner => "No maths, no code — just the ideas you need to read data like an engineer reads a gauge.",
            TrackLevel.Intermediate => "You'll build something that works on real KOC-shaped data and understand *why* it works.",
            _ => "This is about trust: models a team can run tomorrow, next quarter, and after you've moved on.",
        };

        return $"""
        # {title}

        {intro}

        ## Why it matters at KOC
        Every well, pump, and separator produces a stream of numbers. Turning that stream into a
        decision — *intervene now* or *hold* — is exactly what this lesson builds toward. Good data
        habits here save real intervention cost downstream.

        ## Key ideas
        - **Start from the question.** A model is only as useful as the decision it informs.
        - **Look before you model.** Ranges, gaps, and outliers tell you what the sensors were doing.
        - **Keep it honest.** Whatever you measure on training data, prove it again on data the model never saw.

        ## Try it in the Studio
        > Open the **Workflow** node editor and build: `dataset → normalize → split → train → evaluate`.
        > Watch each node report its status. Change the algorithm and see the score move.

        ```text
        dataset  →  normalize  →  split  →  train (FastTree)  →  evaluate
        ```

        ## Checkpoint {order}
        You're ready to move on when you can explain, in one sentence, what this step contributes to a
        trustworthy prediction — and show it running on your own CSV.
        """;
    }
}
