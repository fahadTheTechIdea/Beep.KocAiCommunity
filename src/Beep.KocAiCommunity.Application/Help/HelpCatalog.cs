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

        new("choosing-a-task", "Choosing an ML task", "Studio",
            "Classification, regression, forecasting, or anomaly detection — which fits your problem, and the gotchas for each.",
            """
            # Which ML task?

            Pick the task that matches the *shape of the answer* you want.

            | You want to predict… | Task | Scored on |
            |---|---|---|
            | A yes/no outcome (fails / doesn't) | **Binary classification** | Accuracy |
            | One of several classes (rock facies) | **Multiclass classification** | Accuracy |
            | A number (production rate) | **Regression** | RMSE (lower wins) |
            | A number **over time** (decline curve) | **Time-series forecasting** | RMSE (lower wins) |
            | The **rare abnormal** rows (sensor faults) | **Anomaly detection** | AUC (higher wins) |

            ## Time-series forecasting

            Forecasting is regression with one crucial rule: you must **train on the past and test on the
            future**. A random split leaks later readings into training and flatters your score.

            - Add a **Chronological split** node (`time-split`) and point it at your **date/sequence
              column**. It holds out the most-recent rows instead of random ones.
            - Build features the model can actually use for future rows — lags, rolling averages, days
              online, seasonality — not the raw timestamp alone.

            ## Anomaly detection

            Anomaly detection is **unsupervised**: it learns what *normal* looks like and flags rows that
            deviate. You don't label anomalies to train.

            - **Train on normal history only.** If you mix labelled anomalies into training, they pollute
              the "normal" model and detection gets worse.
            - No train/test split is needed — the detector uses every row. (The publish check knows this
              and won't ask for a split on an anomaly model.)
            - It's scored on **AUC**, not accuracy: when 99% of rows are normal, a model that flags nothing
              is 99% "accurate" but useless. AUC measures whether the true anomalies rank above the rest.
            """,
            ["task", "forecasting", "anomaly", "regression", "classification", "time-split", "auc"]),

        // ---- Node & algorithm reference ----
        // One article per node palette, so every node kind in the catalog is documented somewhere and the
        // property panel can link straight to the right page. DocumentationCoverageTests enforces that.

        new("nodes-source", "Nodes: Source", "Reference",
            "Where a pipeline's rows come from.",
            """
            # Source nodes

            ## `dataset` — Dataset
            The rows flowing into the pipeline. Every pipeline needs exactly one.

            - **Training dataset** — pick one of your datasets. In a **competition** this is left empty:
              the host's data is injected by the server and shown on the node instead.

            The Dataset node is also where the Studio reads column names from, which is what turns the
            Column pickers on every other node into dropdowns rather than free text.
            """,
            ["nodes", "dataset", "source", "reference"]),

        new("nodes-split", "Nodes: Split", "Reference",
            "Holding rows back so your score means something.",
            """
            # Split nodes

            A supervised model must not be fitted on the rows it is judged on. The platform enforces
            this: publishing a graph where `train` or `cross-validate` is reachable from `dataset`
            without a split in between is rejected.

            ## `split` — Train/test split
            Holds back a random fraction of rows.

            - **Test fraction** — share held out for evaluation (0.05–0.9, default 0.25).

            Seeded, so the same graph on the same data always holds back the same rows.

            ## `time-split` — Chronological split
            Orders rows by a time column and holds back the **most recent** fraction, so the model
            trains on the past and is scored on the future.

            - **Time / order column** — a date or a sequence number (required).
            - **Test fraction (most recent)** — share of the tail held out.

            Use this whenever the question contains the word *next*. A random split on time-ordered
            data leaks the future into training and flatters the score.

            **Exempt from the split rule:** `cluster`, and `train` set to anomaly detection — neither
            has a label to leak.
            """,
            ["nodes", "split", "time-split", "leakage", "reference"]),

        new("nodes-model", "Nodes: Model", "Reference",
            "Fitting a model, or grouping without one.",
            """
            # Model nodes

            ## `train` — Train model
            Fits a model on the training rows. The **Task** set here drives the whole run.

            - **Target (label) column** — what to predict. For anomaly detection it is ground truth used
              only for scoring, never trained on. Fixed by the competition when submitting.
            - **ID column** — row identifier carried to the submission. Never used as a feature.
            - **Feature columns** — blank means everything except target and id.
            - **Task** — Binary, Multiclass, Regression, or Anomaly detection.
            - **Algorithm** — filtered to those that apply to the chosen task. See the algorithm reference.
            - Hyperparameters appear only for the algorithm selected: **trees**, **leaves per tree**,
              **min rows per leaf**, **learning rate**, **iterations**, **L1**, **L2**, **max iterations**,
              **history size**, and **normal-subspace components** (anomaly only).

            ## `cluster` — Cluster (k-means)
            Groups rows without labels. No target, no split needed.

            - **Clusters** — how many groups (2–20).
            - **Feature columns** — blank means all numeric.

            Reports average distance and the Davies–Bouldin index (lower is better).

            ## `cross-validate` — Cross-validate
            Splits K ways, trains K times, averages the metric — a steadier estimate than one split.

            - **Folds** (2–10), plus the same algorithm and hyperparameter set as `train`.

            It produces an *estimate*, not a deployable model. Skipped for anomaly detection, which has
            no label to fold on.
            """,
            ["nodes", "train", "cluster", "cross-validate", "model", "reference"]),

        new("nodes-evaluate", "Nodes: Evaluate", "Reference",
            "Applying the model and reading the result.",
            """
            # Evaluate nodes

            ## `score` — Score
            Applies the trained model to the held-out rows. No settings. Skipped during prediction runs,
            where scoring is handled by the submission step.

            ## `evaluate` — Evaluate
            Computes metrics on the held-out rows. What it reports depends on the task:

            | Task | Reported |
            |---|---|
            | Binary classification | Accuracy, AUC |
            | Multiclass | MicroAccuracy, MacroAccuracy |
            | Regression / forecasting | R², RMSE, MAE |
            | Anomaly detection | AUC, detection rate |

            **Accuracy is misleading on rare events.** If 2% of rows are failures, flagging nothing is
            98% accurate. AUC measures whether the true positives *rank* above the rest, which is why
            anomaly detection is scored on it.
            """,
            ["nodes", "score", "evaluate", "metrics", "auc", "reference"]),

        new("nodes-prepare", "Nodes: Prepare", "Reference",
            "Fixing names, types, and derived values.",
            """
            # Prepare nodes

            ## `rename-column` — Rename column
            **From column** → **New name**. Sensor tags like `TI-4021.PV` become `wellhead_temp` once,
            and every later node reads clearly.

            ## `convert-numeric` — Convert to numeric
            Turns text columns into numbers. Blank means all text columns. The most common fix on KOC
            exports, where one `"N/A"` makes a whole column arrive as text.

            ## `compute-column` — Compute column
            A new column from a small formula, e.g. `(gas, oil) => gas / (oil + 1)`.

            - **New column name**, **Input columns**, **Formula** — all required.

            ## `combine-columns` — Combine columns
            Merges numeric columns into one feature vector. Rarely needed — `train` combines features
            for you.

            ## `lp-normalize` — Lp-normalize
            Scales each **row** to unit length (rather than each column). Used with text features.

            ## `global-contrast` — Global contrast normalize
            Centres and scales each **row**. Also a row-wise operation, from image and signal work.
            """,
            ["nodes", "prepare", "rename", "convert", "compute", "reference"]),

        new("nodes-shape", "Nodes: Shape", "Reference",
            "Choosing which rows you keep.",
            """
            # Shape nodes

            ## `filter-rows` — Filter rows
            Keeps rows where a column falls in a range: **Column**, **Keep ≥ min**, **Keep < max**.
            Use it to drop impossible readings — a wellhead pressure of −5 is an instrument fault.

            ## `take-rows` — Take rows
            Keeps the first N rows. Fine for trying a pipeline quickly; poor for training, because
            "the first N" is rarely a fair sample.

            ## `sample` — Sample
            Keeps a random fraction. This *is* a fair sample, and the right way to shrink a dataset
            while experimenting.

            ## `shuffle` — Shuffle
            Reorders rows randomly. Matters when the file arrived sorted and you are about to split it.

            **On time-ordered data**, shuffling and random sampling destroy the ordering your question
            depends on. Use `time-split` and leave the order alone.
            """,
            ["nodes", "shape", "filter", "sample", "shuffle", "take-rows", "reference"]),

        new("nodes-transform", "Nodes: Transform", "Reference",
            "Changing the values inside columns — scaling, encoding, selecting.",
            """
            # Transform nodes

            ## Selecting columns
            - **`select-columns`** — keep only the named columns (required).
            - **`drop-columns`** — remove the named columns (required).
            - **`feature-selection`** — drop columns that are almost always the same value.
              **Min non-default count** sets the threshold.
            - **`pca`** — replace correlated columns with a few combined ones. **Components** is clamped
              to the number of numeric features. Costs interpretability.

            > Never drop the id column. Predictions are matched back by it, and a submission without it
            > is rejected.

            ## Scaling numbers
            | Node | What it does | Reach for it when |
            |---|---|---|
            | **`standardize`** | Centres on 0, unit variance | The default for sensor data |
            | **`normalize`** | Squeezes into 0–1 | You need a bounded range |
            | **`log-normalize`** | Takes logarithms first | Values span orders of magnitude |
            | **`robust-scale`** | Scales on percentiles | A few extreme outliers would dominate |

            All four take a **Columns** list; blank means all numeric. Scaling matters greatly for the
            linear algorithms and barely at all for trees.

            ## Encoding categories
            - **`one-hot`** — a column per category. **Output kind**: *indicator* (0/1, the usual), *bag*
              (counts), *key* (integer id), *binary* (bit encoding). Don't feed *key* to a linear model —
              it implies category 3 is three times category 1.
            - **`hash-encode`** — maps categories into 2^**bits** buckets. For thousands of distinct
              values (well ids, tags) where one-hot would explode. More bits, fewer collisions.
            - **`featurize-text`** — turns free text into numeric features. For maintenance notes and
              shift reports.

            ## Fixing values
            - **`replace-missing`** — fills empty numeric cells with the **mean**, **min**, or **max**.
              Consider whether missing really means "average" — a missing flow during a shutdown means
              *zero*.
            - **`binning`** — buckets a continuous column into at most **Max bins** bands. Blunts
              outliers and captures "which band" when the exact value matters less.
            """,
            ["nodes", "transform", "standardize", "normalize", "one-hot", "pca", "binning", "reference"]),

        new("nodes-data", "Nodes: Data & SQL", "Reference",
            "Aggregating, joining, and querying with DuckDB inside the pipeline.",
            """
            # Data nodes

            These run **DuckDB** inside your pipeline. The incoming rows are a table called `working`.

            ## `sql` — SQL
            A full `SELECT … FROM working`. Window functions, CASE expressions, subqueries.

            ## `sql-filter` — SQL filter
            A **WHERE condition** — for filters `filter-rows` can't express, like two columns with an OR.
            A visual condition builder writes it when columns are known; the text stays editable.

            ## `group-by` — Group by
            Collapses rows into one per group.
            - **Group-by columns** and **Aggregates**, e.g. `AVG(pressure) AS avg_p, COUNT(*) AS n`.

            Always alias with `AS` — the alias becomes the downstream column name.

            ## `sort` — Sort
            An **ORDER BY** clause, e.g. `pressure DESC`. Matters before `take-rows`.

            ## `distinct` — Distinct
            Removes duplicate rows. Stitched exports often carry them, and duplicates quietly weight
            those readings more heavily in training.

            ## `join-dataset` — Join dataset
            Brings columns from a second dataset alongside the current rows.
            - **Dataset to join**, **Join type** (left / inner / right / full), **Key column**,
              **Columns to bring**.

            > **Fan-out.** If the joined dataset has more than one row per key, every match multiplies
            > your rows and ids stop being unique — a submission is then rejected. Check the key is
            > unique on the other side first.

            ## `union-dataset` — Append dataset
            Appends another dataset's rows below the current ones. Columns should match.

            ## Two columns must survive
            **The id column** — predictions are matched back by it. **The fold marker** — added by a
            split to mark train and test; a SELECT that drops it would merge the two, so the platform
            refuses to run rather than produce a flattering, meaningless result.

            Safest habit: put SQL nodes **before** the split, and prefer `SELECT *` with a WHERE.
            """,
            ["nodes", "sql", "duckdb", "group-by", "join", "union", "distinct", "sort", "reference"]),

        new("algorithms", "Algorithm reference", "Reference",
            "Every trainer the Studio offers, which tasks it applies to, and when to reach for it.",
            """
            # Algorithms

            The Train node filters this list to the algorithms that apply to the chosen **Task**, so you
            only ever see the relevant ones.

            ## Linear
            | Algorithm | Tasks | Reach for it when |
            |---|---|---|
            | **`sdca`** | Binary, Multiclass, Regression | The default. Fast, solid, a good baseline |
            | **`lbfgs`** | Binary, Multiclass, Regression | Smaller datasets; often a little better than SDCA. Regression here is **Poisson** — for counts and rates that cannot go negative |
            | **`sgd`** | Binary | Very large datasets. Sensitive to learning rate |
            | **`perceptron`** | Binary | Trains almost instantly; a useful sanity check |
            | **`ogd`** | Regression | Online gradient descent, for data too large to hold in memory |

            Linear algorithms care about scale — `standardize` first.

            ## Trees
            | Algorithm | Tasks | Reach for it when |
            |---|---|---|
            | **`fasttree`** | Binary, Regression | Usually the strongest on tabular sensor data |
            | **`fastforest`** | Binary, Regression | Small datasets, or FastTree is overfitting |
            | **`ova-fasttree`** | Multiclass | Tree strength on a multiclass problem (one model per class) |

            Trees ignore scale — you can skip `standardize` entirely.

            ## Interpretable and specialised
            | Algorithm | Tasks | Reach for it when |
            |---|---|---|
            | **`gam`** | Binary, Regression | You must *explain* the model — it fits a visible curve per feature |
            | **`naivebayes`** | Multiclass | Text, and as a fast first look |
            | **`randomized-pca`** | Anomaly detection | The only anomaly trainer. Learns the "normal" subspace and scores reconstruction error |

            ## Tuning order
            Compare algorithms on defaults first, holding the split constant, and only then tune the
            winner. If a model is excellent on training data and poor on held-out data, raise **min rows
            per leaf** and lower **leaves** before anything else.
            """,
            ["algorithms", "sdca", "fasttree", "gam", "reference", "hyperparameters"]),

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
            it. Add a `split` node (or a `time-split` for forecasting) between your dataset and the model.
            Anomaly-detection models are unsupervised and don't need one.

            **Can I download a Confidential dataset?** Only if you own it or are a platform admin.
            """,
            ["faq", "help", "questions"]),
    ];

    public static IReadOnlyList<string> Categories => All.Select(a => a.Category).Distinct().ToList();

    public static HelpArticle? Find(string slug) =>
        All.FirstOrDefault(a => string.Equals(a.Slug, slug, StringComparison.OrdinalIgnoreCase));
}
