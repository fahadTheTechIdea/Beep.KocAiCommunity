title: Evaluate honestly
summary: A model that scores well on data it has already seen tells you nothing. Learn the split, cross-validation, and evaluation nodes — and the mistakes that quietly inflate a score.
level: Advanced
order: 11

## Lesson 1 — The only question that matters

A model's job is to be right about data it has never seen. Every evaluation technique here exists to
answer one question honestly:

> **How will this perform on next month's data?**

The temptation is to measure on the rows you trained with. It always looks excellent, because the
model has effectively memorised them. This is not a subtle statistical concern — it is the single
most common way an ML project fails after it reaches production, and it is entirely preventable.

## Lesson 2 — The train/test split

**`split`** holds back a fraction of rows — 25% by default — and the Train node never sees them. The
Evaluate node then scores on exactly that held-back set.

Place it **before** the model. The platform enforces this: a graph where a supervised model is
reachable from a dataset without a split in between is rejected at publish time, with the message
*"would be fit without a train/test split"*.

Two nodes are exempt, for a real reason rather than convenience:

- **`cluster`** — k-means has no labels to leak.
- **`train`** configured for **anomaly detection** — RandomizedPCA learns "normal" from the shape of
  the features alone.

The split is seeded, so the same graph on the same data gives the same rows every time. Reproducible
results are not a nicety when a competition result is on the line.

## Lesson 3 — When a random split lies to you

If your rows are ordered in time, a random split scatters future rows into the training set. The
model then "predicts" September using knowledge of October, scores brilliantly, and collapses in
production.

**`time-split`** fixes this: it orders by a column you name and holds back the most recent fraction.
The model trains on the past and is scored on the future — the situation it will actually face.

Use it whenever the question contains the word *next*: next month's rate, next quarter's decline,
time to next failure.

> A random split on time-ordered data is the most flattering mistake in machine learning. It is also
> the easiest to make, because nothing looks wrong — the number just goes up.

## Lesson 4 — Cross-validation

One split gives one estimate, and that estimate depends on which rows happened to land in the test
set. **`cross-validate`** splits the data K ways (5 by default), trains K times, and averages the
result. It is a more honest number, especially on small datasets.

It costs K times the training time, and it does not produce a model you can deploy — it produces a
*better estimate of how well your approach works*. Use it while choosing between approaches, then
train once properly.

Cross-validation has no meaning for unsupervised anomaly detection — there is no label to fold on —
and the node says so and skips rather than quietly folding your ground truth as if it were a
classification target.

## Lesson 5 — Reading the metrics

**`score`** applies the trained model to the held-out rows. **`evaluate`** turns those predictions
into numbers, and which numbers depends on the task:

| Task | Reported | Watch for |
|---|---|---|
| Binary classification | Accuracy, AUC | Accuracy is meaningless on rare events — see below |
| Multiclass | MicroAccuracy, MacroAccuracy | A large gap means small classes are being ignored |
| Regression | R², RMSE, MAE | RMSE punishes large errors; MAE treats all equally |
| Anomaly detection | AUC, detection rate | Accuracy would be ~1.0 for a model that flags nothing |

**The rare-event trap.** If 2% of pumps fail, a model that predicts "no failure" every single time is
98% accurate and completely worthless. That is why anomaly detection is scored on AUC — which asks
whether the truly abnormal rows are *ranked* above the normal ones — and reported alongside a
detection rate: of the alarms you would actually act on, how many were real.

## Lesson 6 — Leakage, the quiet killer

Leakage is any information in your features that would not exist at prediction time. The split
protects you from the obvious form. These are the ones it does not catch:

- **A column derived from the answer.** `days_since_failure` cannot be known before the failure.
- **A future aggregate.** A monthly average that includes the month you are predicting.
- **An identifier that encodes the outcome.** A work-order number issued only for failed equipment.
- **Preparation fitted on everything.** Scaling computed across train *and* test leaks the test set's
  distribution. Keep column-shaping nodes where the platform replays them per fold.

The test for any feature: *would I have had this value, with this content, at the moment I needed the
prediction?* If not, it is leakage — however much it improves the score. Especially then.

## Lesson 7 — Build it yourself

Prove the whole lesson to yourself in one sitting:

1. Build `dataset` → `train` → `evaluate` with no split. Note the metric — it will look excellent.
2. Insert a `split` before the train node. Run again. The metric drops. **That drop is the truth**;
   the first number was measuring memory, not skill.
3. Swap `split` for `time-split` on a dataset with a date column. Compare again.
4. Add `cross-validate` and compare its averaged figure with your single split.

Then take that pipeline into a competition. The hidden test set is the final honest evaluation, and
nothing you do to your own split can flatter it.
