title: Flag the abnormal
summary: Find the readings that don't belong — HSE sensor spikes, equipment behaving unlike itself — without anyone having to label them first.
level: Advanced
order: 7

## Lesson 1 — The problem with labelled failures

Classification needs examples of what you are looking for. For rare, expensive events that is exactly
what you do not have: a handful of failures across thousands of healthy hours, and each one recorded
differently.

Anomaly detection turns the problem around. Instead of learning what failure looks like, it learns
what **normal** looks like — which you have in abundance — and flags whatever departs from it.

That reframing is why it needs no labels to train. Set the Train node's **Task** to **Anomaly
detection** and the target column becomes ground truth used *only for scoring*, never for learning.

## Lesson 2 — How RandomizedPCA decides

The Studio's anomaly algorithm is **`randomized-pca`**, and the idea behind it is worth understanding
because it explains both its strength and its blind spot.

Normal operation is not random. Five sensors on an ESP move together: as load rises, intake pressure,
motor current, and flow all shift in a coordinated way. Those five numbers really only describe two or
three independent things. PCA finds that lower-dimensional "normal subspace".

Scoring a row means projecting it onto that subspace and measuring how far off it lands — the
**reconstruction error**. A row that fits the usual pattern reconstructs almost perfectly. A row where
one sensor has broken from its companions cannot be reconstructed, and scores high.

> This is why it catches things a threshold alarm misses. Every individual reading can be inside its
> normal band while the *combination* is impossible — vibration high while current is low. No single
> limit is breached; the relationship is broken.

## Lesson 3 — The rank knob

**Normal-subspace components** (the `rank` parameter) is how many dimensions describe "normal".

- **Fewer** components — a tighter notion of normal, so more rows look anomalous. More alarms,
  including false ones.
- **More** components — a looser notion, so fewer rows stand out. Quieter, and easier to miss things.

Leave it blank and the node uses one less than your feature count. It is always clamped below the
feature count, deliberately: at full rank the reconstruction is perfect for every row and every score
collapses to zero — a detector that flags nothing, with no error to tell you why.

Start with the default. Tune only if the alarm volume is wrong for the crew who must act on it.

## Lesson 4 — Training on normal history

The most important practical point in this track:

> **Train on data you believe is normal.**

If your training rows include the failures, PCA folds those failures into its idea of normal and
stops finding them. Filter to healthy periods first, using `filter-rows` or an `sql-filter`.

This is also why anomaly detection is exempt from the split-before-fit rule. There is no label to
leak, so a `split` is not required — though you still need held-out labelled rows if you want to
*measure* how well it works.

## Lesson 5 — Scoring: AUC and detection rate

Accuracy is useless here. If 2% of rows are anomalies, flagging nothing is 98% accurate.

**AUC** asks the right question: take one true anomaly and one normal row — how often does the model
score the anomaly higher? It measures *ranking*, and it is unaffected by how rare anomalies are. 0.5
is a coin flip; above 0.9 is strong.

**Detection rate** turns that into something operational. Take the top K rows by score, where K is the
number of true anomalies present, and ask how many are real. At that cut precision and recall are the
same number, so there is no threshold to argue about: *"of the alarms a crew would actually work
through, this share were real."*

Read them together. AUC says the ranking is sound; detection rate says what that means for the people
holding the radio.

## Lesson 6 — Turning scores into alarms

The model outputs a continuous score, not a yes/no. Where you cut is a business decision:

- How many alarms can the crew investigate per shift?
- What does a missed event cost against a wasted inspection?

Sort the held-out rows by score and look at where the true anomalies sit. If most are in the top 20,
an alarm on the top 20 per period is defensible. If they are scattered throughout, the model is not
ready and no threshold will rescue it.

## Lesson 7 — Build it yourself

1. `dataset` → `convert-numeric` → `replace-missing` → `standardize` → `train` → `evaluate`
2. On `train`: Task = **Anomaly detection**. The algorithm list collapses to `randomized-pca`, which
   is the only one that applies, and the supervised knobs disappear.
3. Leave **rank** blank for the first run.
4. Run it. The evaluate line reports AUC and detection rate together.

Do standardize first. PCA measures distance, and an unscaled column measured in thousands will define
"normal" almost single-handedly.

Then halve the rank and run again. Watch the detection rate move, and decide which setting matches the
alarm volume your operation can absorb.

## Lesson 8 — Take it to a competition

The **ESP anomaly** demo competition is built for exactly this: five correlated sensors, a rare
spiked-sensor fault, scored on AUC.

Open **Compete**, press *Join & build your pipeline*, and the Studio opens on the host's data with the
task already set to anomaly detection. Train on the normal history, submit, and compare your
leaderboard AUC with what you saw locally.
