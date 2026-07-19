namespace Beep.KocAiCommunity.Application.Help;

/// <summary>
/// A code-first help article. Content is authored in source (admin-only authoring is a follow-up);
/// the body is Markdown. Articles power the in-app help, FAQ, and guided walkthroughs.
/// </summary>
public sealed record HelpArticle(string Slug, string Title, string Category, string Summary, string BodyMarkdown, IReadOnlyList<string> Tags);

/// <summary>The code-first catalog of KOC AI Community help content.</summary>
public static class HelpCatalog
{
    public static readonly IReadOnlyList<HelpArticle> All =
    [
        new("getting-started", "Getting started", "Basics",
            "Sign in, find your team, and take your first steps on the platform.",
            """
            # Welcome to KOC AI Community

            This is KOC's internal home for learning AI/ML and competing with your colleagues — all
            inside KOC.

            1. **Sign in** with your KOC account. Your team and role come from the KOC directory.
            2. **Learn** — open the *Learn* tab and enrol in a starter track.
            3. **Earn Barrels (bbl)** — completing lessons, entering competitions, and helping colleagues
               all pay Barrels that raise your level on the O&G career ladder.
            4. **Compete** — join an internal, Kaggle-style competition and climb the leaderboard.

            Everything you create carries a *visibility* (Team / Group / Directorate / Company) and an
            information-security *classification*.
            """,
            ["start", "onboarding", "basics"]),

        new("earning-barrels", "Earning Barrels & levelling up", "Community",
            "How the Barrels (bbl) reward system and the O&G career ladder work.",
            """
            # Barrels & the career ladder

            **Barrels (bbl)** are the platform's experience points. You earn them by completing lessons,
            making competition submissions, starting discussions, and receiving kudos.

            Your total Barrels sets your **level** on the O&G career ladder — from *Roustabout* up to
            *Chief Geoscientist*. Daily caps keep things fair.

            - Give a colleague **kudos** to recognise good work (10 per day).
            - Keep a **streak** by showing up on consecutive days.
            - Team standings rank org units by *average* Barrels per member — so every teammate counts.
            """,
            ["barrels", "xp", "gamification", "kudos", "levels"]),

        new("how-competitions-work", "How competitions work", "Competitions",
            "Submissions, the hidden answer key, quotas, and the concealed final reveal.",
            """
            # Competitions

            Internal competitions are Kaggle-style: submit predictions against a hidden answer key and a
            trusted scorer ranks you on a live leaderboard.

            - **Quota** — a daily submission limit keeps the field even.
            - **Concealed final** — near the deadline the leaderboard freezes; the final standings are
              revealed at `RevealUtc`.
            - **Scoring** — accuracy for classification, RMSE for regression, chosen by the organiser.

            Competitions are scoped to a Team, Group, Directorate, or the whole Company.
            """,
            ["competition", "leaderboard", "submission", "scoring"]),

        new("build-a-workflow", "Build & publish a workflow", "Studio",
            "Design an ML pipeline, validate it, and publish an immutable version.",
            """
            # Workflows

            A workflow is a small graph: **dataset → transforms → split → train → evaluate**.

            1. Start from an **O&G template** (ESP failure, production rate, well-log facies) or a blank
               canvas.
            2. Save a **draft**. Drafts are editable.
            3. **Publish** to freeze an immutable version with a snapshot hash. Publishing runs the
               compiler *and* the **split-before-fit** check — a model that would be fit on the full
               dataset (train + test) is rejected to prevent leakage.
            4. **Export / import** a workflow as a portable JSON envelope.

            Once published, a version never changes; the next edit opens a new draft.
            """,
            ["workflow", "pipeline", "publish", "template", "studio"]),

        new("datasets-and-classification", "Datasets, versions & classification", "Data",
            "Upload data, read its profile, and understand who can download it.",
            """
            # Datasets

            Upload a CSV and the platform infers a **schema** and computes a **profile** (row counts,
            null counts, distinct values, min/max/mean) — reproducibly.

            - Datasets are **versioned**: publishing freezes a version; a new upload opens a new draft.
            - **Classification** is enforced on download. *Public* and *Internal* files are open to anyone
              who can see the dataset; *Confidential* and *Restricted* files require the owner or a
              platform admin.
            - You can import a CSV **from a URL** — internal/private addresses are blocked.
            """,
            ["dataset", "profile", "classification", "version", "import"]),

        new("faq", "Frequently asked questions", "FAQ",
            "Quick answers to common questions.",
            """
            # FAQ

            **Who can see what I create?** Whatever visibility you choose — your Team, Group,
            Directorate, or the whole Company. The owner always retains access.

            **Is anything shared outside KOC?** No. The platform is single-tenant and internal only.

            **How do I become a platform admin?** Admin roles come from the KOC directory; ask your
            administrator.

            **My model won't publish — why?** A supervised model must have a *train/test split* before
            it. Add a `split` node between your dataset and the model.

            **Can I download a Confidential dataset?** Only if you own it or are a platform admin.
            """,
            ["faq", "help", "questions"]),
    ];

    public static IReadOnlyList<string> Categories => All.Select(a => a.Category).Distinct().ToList();

    public static HelpArticle? Find(string slug) =>
        All.FirstOrDefault(a => string.Equals(a.Slug, slug, StringComparison.OrdinalIgnoreCase));
}
