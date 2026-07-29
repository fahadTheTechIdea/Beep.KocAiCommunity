title: Predict a number
summary: Estimate a production rate, a pressure, a remaining life. Regression end to end, with every algorithm the Studio offers and the metrics that tell you the truth.
level: Intermediate
order: 5

## Lesson 1 — Questions with a numeric answer

Regression predicts a quantity rather than a category.

- *What rate will this well produce next month?*
- *What is the bottom-hole pressure, given surface readings?*
- *How many days of useful life remain on this component?*

The setup mirrors classification: one column holding the number you want (the **label**), and columns
that might explain it. Set the Train node's **Task** to **Regression**.

If your question involves *next month* specifically, read the *Forecast over time* track as well —
time-ordered data needs a different split, and getting that wrong is the most flattering mistake in
this field.

## Lesson 2 — The linear algorithms

**`sdca`** — the default. A weighted sum of your features, fitted quickly. **L1** drives useless
feature weights to zero; **L2** keeps all weights modest. Leave both blank to start.

**`lbfgs`** — Poisson regression here, not ordinary least squares. That detail matters: Poisson
assumes the target is a **count or a rate that cannot go negative**, which fits production volumes,
failure counts, and event rates rather well. Do not use it for a quantity that can legitimately be
negative, such as a temperature difference.

**`ogd`** — online gradient descent. Processes rows one at a time, so it handles datasets too large to
hold in memory. Sensitive to **learning rate** and **iterations**; expect to tune both.

Scale your numeric columns before any of these. A feature measured in thousands will otherwise
dominate one measured in tenths purely because of its units.

## Lesson 3 — The tree algorithms

**`fasttree`** — gradient-boosted regression trees, and usually the strongest option on sensor data.
It captures relationships that bend: pressure that matters enormously below a threshold and hardly at
all above it, which a straight line cannot express.

**`fastforest`** — bagged trees. Steadier and usually a little weaker; a sensible pick on small
datasets where FastTree overfits.

Both take the same knobs: **trees**, **leaves per tree**, **min rows per leaf**, and (FastTree only)
**learning rate**. If your model is excellent on training data and poor on held-out data, raise *min
rows per leaf* and lower *leaves* before anything else.

Trees ignore scale entirely — skip `standardize` if you are using them.

## Lesson 4 — When you have to explain it

**`gam`** — a generalised additive model fits a separate curve for each feature and adds them up. You
lose a little accuracy against FastTree and gain something often worth more: you can show a reviewer
the curve for choke position and say *"here is exactly how this drives the estimate"*.

For anything heading to an operations decision or a regulatory conversation, start here.

## Lesson 5 — Reading R², RMSE and MAE

The Evaluate node reports three numbers, and they answer different questions.

**R²** — the share of variation the model explains. 1.0 is perfect, 0 means no better than always
guessing the average. Negative means *worse* than guessing the average, which is a real result and
usually means something is wrong with the features rather than the algorithm.

**RMSE** — root mean squared error, in the units of your target. Because errors are squared before
averaging, one badly wrong prediction hurts far more than several slightly wrong ones.

**MAE** — mean absolute error, also in your units. Every error counts in proportion to its size.

Which to optimise is a business question, not a statistical one:

> If being wrong by 100 barrels once is worse than being wrong by 10 barrels ten times, use **RMSE**.
> If they cost you the same, use **MAE**.

Say the number out loud with its units — *"typically wrong by 40 barrels a day"* — and you will know
immediately whether the model is useful. R² alone can look respectable while the error is far too
large to act on.

## Lesson 6 — When the model is poor

Work through these in order, because the cheapest fixes come first:

1. **Is the target actually predictable from these features?** No algorithm invents information that
   isn't there. This is the most common cause and the least often checked.
2. **Are there leaks?** A suspiciously excellent R² usually means a feature encodes the answer.
3. **Is the relationship curved?** Linear algorithms cannot bend. Try `fasttree`.
4. **Are the scales wild?** For linear models, `standardize`, or `log-normalize` when values span
   orders of magnitude.
5. **Are outliers dominating?** Try `robust-scale`, or `filter-rows` to drop physically impossible
   readings.
6. **Only now**, tune hyperparameters.

## Lesson 7 — Build and compare

1. `dataset` → `convert-numeric` → `replace-missing` → `standardize` → `split` → `train` → `evaluate`
2. Baseline with `sdca`, defaults. Record R², RMSE, MAE.
3. Switch to `fasttree`, record again. Then `fastforest`, then `gam`.
4. Take the winner and tune one hyperparameter at a time.
5. Confirm with `cross-validate` if two candidates are close.

The split is seeded, so every run is scored on the same held-out rows and the comparison is fair.

## Lesson 8 — Take it to a competition

Open **Compete** and look for a competition scored on **RMSE — lower wins**. Press *Join & build your
pipeline*; the Studio opens on the host's data with the target and task already set.

Build your pipeline, run it to see your local error, then submit. You will be scored on a hidden test
set — and the difference between your local number and the leaderboard number tells you how much of
your performance was real and how much was you, gradually and unintentionally, tuning against your own
held-out rows.
