using Beep.KocAiCommunity.Domain.Learning;
using Beep.KocAiCommunity.Domain.Organization;
using Beep.KocAiCommunity.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Beep.KocAiCommunity.Infrastructure.Learning;

/// <summary>
/// Seeds the company-wide starter tracks. Idempotent <b>per track</b> (a missing track is added even
/// when other tracks already exist), so new tracks appear without resetting the database. Lesson
/// content is markdown and is rendered richly in the app (headings, lists, tables, images, inline
/// SVG diagrams, and embedded/placeholder video).
/// </summary>
public static class LearningSeeder
{
    private const string Icons = "_content/Beep.KocAiCommunity.Ui.Shared/icons";

    public static async Task SeedTracksAsync(KocDbContext db, CancellationToken ct = default)
    {
        var stamp = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        // Order 0: the true entry point for people with no AI background — images and video.
        // refreshContent keeps this authored track's lessons current on existing databases.
        await EnsureTrackAsync(db, stamp, order: 0, TrackLevel.Beginner,
            "AI for Everyone — Start Here",
            "Never touched AI before? Start here. Plain words, pictures, and a first challenge — no maths, no code.",
            BeginnerLessons(), ct, refreshContent: true, contentKey: "ai-for-everyone");

        await EnsureTrackAsync(db, stamp, order: 1, TrackLevel.Beginner,
            "Getting started with data",
            "Read, clean, and make sense of a dataset. For anyone curious about AI — no coding needed.",
            Simple("What is a dataset?", "Loading well data", "Spotting gaps and outliers", "Simple summaries", "Charts that tell the truth", "Your first insight"), ct,
            contentKey: "getting-started-with-data");

        await EnsureTrackAsync(db, stamp, order: 2, TrackLevel.Intermediate,
            "Solve a real problem",
            "Build a model for a production, facilities, or subsurface question using your own data.",
            Simple("Framing the question", "Features from sensor tags", "Train / test split by time", "Fit your first model", "Read the metrics", "Avoid leakage", "Compare two models", "Ship a prediction"), ct,
            contentKey: "solve-a-real-problem");

        await EnsureTrackAsync(db, stamp, order: 3, TrackLevel.Advanced,
            "Make it dependable",
            "Tune, check, and package a model your team can trust and reuse day to day.",
            Simple("Reproducible runs", "Tuning with AutoML", "Validation that holds up", "Explainability basics", "Versioning a model", "Rollback and approvals", "Hand-over to the team"), ct,
            contentKey: "make-it-dependable");

        // Authored tracks live as markdown documents (see TrackDocument). refreshContent keeps their
        // lesson text current on databases that already have them, matched by order so nobody's progress
        // is lost when the training team edits a lesson. A translation is another document with the same
        // content key and a different language, so it seeds as its own track through the same path.
        foreach (var document in TrackDocument.All())
        {
            await EnsureTrackAsync(db, stamp, document.Order, document.Level, document.Title, document.Summary,
                [.. document.Lessons], ct, refreshContent: true,
                contentKey: document.ContentKey, language: document.Language);
        }

        await db.SaveChangesAsync(ct);

        await SeedEntryTrackQuizAsync(db, stamp, ct);
    }

    /// <summary>
    /// A quiz on the entry-point track, so the feature is visible without an admin having to author one
    /// before anybody can see what it does.
    /// <para>
    /// Seeded <b>optional</b> on purpose. A mandatory quiz appearing on an existing database would stop
    /// everybody currently part-way through that track from finishing it, for a quiz they never agreed
    /// to sit. Making it required is a decision for whoever owns the track, in the admin console.
    /// </para>
    /// <para>
    /// Written once and then left alone: it is skipped entirely if the track already has a quiz, so an
    /// admin's edits survive the next deployment rather than being overwritten by this.
    /// </para>
    /// </summary>
    private static async Task SeedEntryTrackQuizAsync(KocDbContext db, DateTime stamp, CancellationToken ct)
    {
        var track = await db.LearningTracks
            .FirstOrDefaultAsync(t => t.ContentKey == "ai-for-everyone" && t.Language == TrackLanguages.English, ct);

        if (track is null || await db.Quizzes.AnyAsync(q => q.TrackId == track.Id, ct))
        {
            return;
        }

        var quiz = new Quiz
        {
            TrackId = track.Id,
            Intro = "Five questions on what you have just read. It takes a couple of minutes, nothing is timed, and you can retake it as often as you like.",
            PassMark = 70,
            IsMandatory = false,
            IsEnabled = true,
            CreatedUtc = stamp,
        };
        db.Quizzes.Add(quiz);

        // (question, explanation, [(answer, isCorrect)])
        (string Q, string Why, (string Text, bool Correct)[] Answers)[] questions =
        [
            ("What does a machine learning model actually learn from?",
             "A model learns patterns from examples it is shown. Nobody writes the rules by hand — that is the difference from ordinary software.",
             [("Examples in data it has been shown", true),
              ("Rules an engineer wrote out by hand", false),
              ("The internet, continuously", false)]),

            ("Why is a model tested on data it has never seen?",
             "Scoring on data it trained on measures memory, not skill. The held-out data is the only honest estimate of how it will behave on tomorrow's readings.",
             [("To find out how it does on new readings", true),
              ("To make training finish faster", false),
              ("Because the training data runs out", false)]),

            ("A pump-failure dataset has 998 healthy rows and 2 failures. A model predicts \"healthy\" every time. What is its accuracy?",
             "99.8% — and it is useless, because it never finds the thing you care about. This is why accuracy is the wrong metric on imbalanced data.",
             [("About 99.8%, and it is still useless", true),
              ("About 50%, because there are two classes", false),
              ("About 0.2%, because it misses both failures", false)]),

            ("What is a label?",
             "The label is the answer for each row — the thing you want the model to work out for rows where you do not have it yet.",
             [("The answer you want the model to predict", true),
              ("The name of the file the data came from", false),
              ("The units a sensor reports in", false)]),

            ("You have a year of daily readings and want next month's production. What is wrong with splitting the data at random?",
             "A random split lets the model see the future while it trains, so it scores far better in testing than it ever will in use. Time-series data is split chronologically.",
             [("The model would train on days that come after the ones it is tested on", true),
              ("Random splits are slower to compute", false),
              ("Nothing — random is always the right split", false)]),
        ];

        var order = 1;
        foreach (var (text, why, answers) in questions)
        {
            var question = new QuizQuestion
            {
                QuizId = quiz.Id,
                OrderNo = order++,
                Text = text,
                Explanation = why,
                CreatedUtc = stamp,
            };
            db.QuizQuestions.Add(question);

            var answerOrder = 1;
            foreach (var (answerText, isCorrect) in answers)
            {
                db.QuizAnswers.Add(new QuizAnswer
                {
                    QuestionId = question.Id,
                    OrderNo = answerOrder++,
                    Text = answerText,
                    IsCorrect = isCorrect,
                    CreatedUtc = stamp,
                });
            }
        }

        await db.SaveChangesAsync(ct);
    }

    private static async Task EnsureTrackAsync(
        KocDbContext db, DateTime stamp, int order, TrackLevel level, string title, string summary,
        (string Title, string? Content)[] lessons, CancellationToken ct, bool refreshContent = false,
        string contentKey = "", string language = TrackLanguages.English)
    {
        // Identity is (content key, language): the same material in two languages is two tracks, and a
        // translator changing a title must not orphan the row. Databases seeded before content keys
        // existed are matched by title once and backfilled, so an upgrade doesn't duplicate the
        // catalogue — and doesn't lose anyone's progress against the original rows.
        var existing = await db.LearningTracks
            .FirstOrDefaultAsync(t => t.ContentKey == contentKey && t.Language == language, ct);

        if (existing is null && contentKey.Length > 0)
        {
            existing = await db.LearningTracks.FirstOrDefaultAsync(t => t.ContentKey == "" && t.Title == title, ct);
            if (existing is not null)
            {
                existing.ContentKey = contentKey;
                existing.Language = language;
            }
        }

        if (existing is not null)
        {
            // Authored tracks (e.g. the beginner track) refresh their lesson text in place so content
            // edits ship without a database reset. Lesson rows are matched by order, preserving progress.
            if (refreshContent)
            {
                // Matched by key rather than title, so the title itself is content that can be edited.
                existing.Title = title;
                existing.Summary = summary;

                var rows = await db.Lessons.Where(l => l.TrackId == existing.Id).OrderBy(l => l.OrderNo).ToListAsync(ct);
                foreach (var lesson in rows)
                {
                    var index = lesson.OrderNo - 1;
                    if (index >= 0 && index < lessons.Length)
                    {
                        lesson.Title = lessons[index].Title;
                        lesson.Content = lessons[index].Content ?? Body(lessons[index].Title, level, lesson.OrderNo);
                    }
                }
            }

            return;
        }

        var track = new LearningTrack
        {
            Title = title,
            Summary = summary,
            Level = level,
            OrderNo = order,
            Language = language,
            ContentKey = contentKey,
            Status = "published",
            Domain = "upstream",
            VisibilityScope = VisibilityScope.Company,
            CreatedUtc = stamp,
        };
        db.LearningTracks.Add(track);

        for (var i = 0; i < lessons.Length; i++)
        {
            db.Lessons.Add(new Lesson
            {
                TrackId = track.Id,
                OrderNo = i + 1,
                Title = lessons[i].Title,
                ContentRef = $"seed://{language}/{level}/{order}/{i + 1}",
                Content = lessons[i].Content ?? Body(lessons[i].Title, level, i + 1),
                EstimatedMinutes = level == TrackLevel.Beginner ? 8 : 20,
                CreatedUtc = stamp,
            });
        }
    }

    // Lessons whose body is generated from the generic template.
    private static (string Title, string? Content)[] Simple(params string[] titles) =>
        [.. titles.Select(t => (t, (string?)null))];

    // ---------------------------------------------------------------- beginner track content

    private static (string Title, string? Content)[] BeginnerLessons() =>
    [
        ("What is AI, really?", """
        # What is AI, really?

        You do not need any background to understand this. **Artificial intelligence (AI)** simply means
        teaching a computer to **learn from past examples** so it can make **useful predictions** about new
        situations.

        <figure class="koc-figure"><svg viewBox="0 0 640 150" width="640" role="img" aria-label="Examples flow into Learn, which produces Predict" xmlns="http://www.w3.org/2000/svg">
          <defs><marker id="ai-ar1" markerWidth="10" markerHeight="8" refX="8" refY="3" orient="auto"><path d="M0,0 L8,3 L0,6 Z" fill="#a9761a"/></marker></defs>
          <rect x="18" y="45" width="170" height="62" rx="10" fill="#eaf2f9" stroke="#1466A5" stroke-width="2"/>
          <text x="103" y="72" text-anchor="middle" font-family="system-ui" font-size="19" font-weight="600" fill="#0E4F80">Examples</text>
          <text x="103" y="93" text-anchor="middle" font-family="system-ui" font-size="12" fill="#586a7b">what happened before</text>
          <line x1="192" y1="76" x2="242" y2="76" stroke="#a9761a" stroke-width="3" marker-end="url(#ai-ar1)"/>
          <rect x="246" y="45" width="150" height="62" rx="10" fill="#eaf2f9" stroke="#1466A5" stroke-width="2"/>
          <text x="321" y="72" text-anchor="middle" font-family="system-ui" font-size="19" font-weight="600" fill="#0E4F80">Learn</text>
          <text x="321" y="93" text-anchor="middle" font-family="system-ui" font-size="12" fill="#586a7b">find the pattern</text>
          <line x1="400" y1="76" x2="450" y2="76" stroke="#a9761a" stroke-width="3" marker-end="url(#ai-ar1)"/>
          <rect x="454" y="45" width="168" height="62" rx="10" fill="#0E4F80"/>
          <text x="538" y="72" text-anchor="middle" font-family="system-ui" font-size="19" font-weight="600" fill="#ffffff">Predict</text>
          <text x="538" y="93" text-anchor="middle" font-family="system-ui" font-size="12" fill="#cfe0ee">on something new</text>
        </svg><figcaption>AI in one picture: learn the pattern from past examples, then predict on new data.</figcaption></figure>

        ## An everyday example
        By learning from the history of our pumps — their pressure, temperature, and vibration — a computer
        can **warn us that a pump is likely to fail before it does**, so we can fix it on plan instead of in an
        emergency.

        <div class="koc-callout"><strong>The big idea:</strong> AI does not "think". It spots patterns in
        numbers, then applies them. If the pattern is real, the prediction is useful.</div>

        You will build exactly this kind of thing on this platform — starting with a friendly challenge, no
        experience required.
        """),

        ("How a computer learns from examples", """
        # How a computer learns from examples

        Imagine a new engineer who has never seen a pump. You show them **hundreds of past pumps** and, for
        each, whether it later failed. After enough examples, they start to notice: *hot motor + heavy
        vibration + long runtime often ends in failure.*

        That is precisely what a computer does — only faster, and across many more numbers at once.

        ## Three simple steps
        - **Show examples.** Give the computer past cases where we already know the answer.
        - **Learn the pattern.** It adjusts itself until it can reproduce those known answers well.
        - **Predict.** Now give it a *new* case with no answer — it fills in its best guess.

        <div class="koc-callout"><strong>Why "hidden" tests matter:</strong> to know if the computer truly
        learned (and didn't just memorise), we check it on examples it has <em>never seen</em>. That is how
        competitions on this platform score you — fairly, on hidden data.</div>

        <p><img src="{ICONS}/074-predictive-chart.png" alt="Predictive chart" width="72" height="72"/></p>

        You do not need to know *how* it adjusts itself. You need to know what to feed it and how to read the
        result — which is what the rest of these lessons cover.
        """.Replace("{ICONS}", Icons)),

        ("What is a dataset? (rows, columns, and the answer)", """
        # What is a dataset?

        A **dataset** is just a table — like a spreadsheet. Nothing scary.

        - Each **row** is one example (one pump, one well, one passenger).
        - Each **column** is one detail we know about it (a *feature*).
        - One special column is the **answer** we want to predict (the *label*).

        Here is a tiny dataset for our first challenge — did a Titanic passenger survive?

        | Age | Class | Sex | Fare | **Survived (answer)** |
        |----:|:-----:|:------|-----:|:----------------------:|
        | 29 | 1 | female | 211 | **Yes** |
        | 40 | 3 | male | 7 | **No** |
        | 8 | 2 | female | 30 | **Yes** |

        The computer studies the first four columns and learns to predict the last one. Later, we give it rows
        **without** the answer and ask it to fill in "Survived — Yes or No?".

        <div class="koc-callout"><strong>Remember:</strong> features go in, the answer comes out. Choosing good
        features is most of the skill — and it is something anyone can get better at with practice.</div>
        """),

        ("Watch: the big idea in a few minutes", """
        # Watch: the big idea

        Sometimes a short video explains it best. Here is a plain-language introduction to what AI is and
        how it learns from examples — no background needed.

        <div class="koc-video"><iframe src="https://www.youtube.com/embed/ukzFI9rgwfU" title="What is AI — a plain-language introduction" allowfullscreen loading="lazy"></iframe></div>

        <div class="koc-callout"><strong>On the offline intranet?</strong> If the video doesn't load, don't
        worry — the written lessons cover everything you need. The video is a bonus, not a requirement.</div>
        """),

        ("Your journey here: Learn → Build → Compete", """
        # Your journey on the AI Digital Campus

        This platform turns learning into doing. You follow one simple loop:

        <figure class="koc-figure"><svg viewBox="0 0 660 130" width="660" role="img" aria-label="Learn then Build then Compete then get Recognised" xmlns="http://www.w3.org/2000/svg">
          <defs><marker id="jr-ar" markerWidth="10" markerHeight="8" refX="8" refY="3" orient="auto"><path d="M0,0 L8,3 L0,6 Z" fill="#a9761a"/></marker></defs>
          <rect x="10" y="40" width="140" height="52" rx="26" fill="#eaf2f9" stroke="#1466A5" stroke-width="2"/>
          <text x="80" y="72" text-anchor="middle" font-family="system-ui" font-size="17" font-weight="600" fill="#0E4F80">Learn</text>
          <line x1="154" y1="66" x2="196" y2="66" stroke="#a9761a" stroke-width="3" marker-end="url(#jr-ar)"/>
          <rect x="200" y="40" width="140" height="52" rx="26" fill="#eaf2f9" stroke="#1466A5" stroke-width="2"/>
          <text x="270" y="72" text-anchor="middle" font-family="system-ui" font-size="17" font-weight="600" fill="#0E4F80">Build</text>
          <line x1="344" y1="66" x2="386" y2="66" stroke="#a9761a" stroke-width="3" marker-end="url(#jr-ar)"/>
          <rect x="390" y="40" width="150" height="52" rx="26" fill="#eaf2f9" stroke="#1466A5" stroke-width="2"/>
          <text x="465" y="72" text-anchor="middle" font-family="system-ui" font-size="17" font-weight="600" fill="#0E4F80">Compete</text>
          <line x1="544" y1="66" x2="586" y2="66" stroke="#a9761a" stroke-width="3" marker-end="url(#jr-ar)"/>
          <rect x="590" y="40" width="60" height="52" rx="26" fill="#a9761a"/>
          <text x="620" y="71" text-anchor="middle" font-family="system-ui" font-size="22" fill="#ffffff">★</text>
        </svg><figcaption>Learn the idea, build a solution with guided tools, enter a challenge, and earn recognition.</figcaption></figure>

        - **Learn** — short lessons like this one.
        - **Build** — in the **Studio**, you connect simple blocks (or let AutoML do it) to make a model. No code.
        - **Compete** — enter a challenge, submit your model, and watch the live scoreboard.
        - **Get recognised** — earn points and badges as you go.

        You are already on step one. Ready to try step three?
        """),

        ("Try your first challenge: Titanic", """
        # Try your first challenge

        The best way to understand this platform is to *use* it. We picked the world's most famous starter
        problem so **anyone** can join in — no oil-&-gas knowledge needed.

        <p><img src="{ICONS}/072-analytics.png" alt="Analytics" width="64" height="64"/></p>

        ## The Titanic challenge
        From a passenger's details — travel class, sex, age, fare, family aboard — predict **who survived**.
        It sounds simple, and the ideas you use here are the very same ones behind predicting a pump failure or
        an oil rate.

        ## What to do
        - Open **Compete** in the left menu and choose **"Titanic — Who Survives?"**.
        - Read the challenge page, then open the **Studio** and let **AutoML** build a first model for you.
        - **Submit** it and see your score on the leaderboard. Then try to improve it.

        <div class="koc-callout"><strong>You cannot break anything.</strong> Experiment freely — every attempt
        teaches you something, and only your best score counts.</div>

        When you're comfortable, move on to **"Getting started with data"** and then a real KOC challenge like
        **ESP Pump Failure** or **Production Forecast**.
        """),
    ];

    // ---------------------------------------------------------------- generic lesson template

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
