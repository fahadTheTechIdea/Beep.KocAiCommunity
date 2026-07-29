title: Prepare the data
summary: Real KOC data arrives messy — gaps, text where numbers should be, columns on wildly different scales. Learn every preparation node and when to reach for it.
level: Beginner
order: 9

## Lesson 1 — Why preparation decides the result

A model learns from what you give it. Give it a column where pressure is recorded as `"1,240 psi"`
and it learns nothing, because it cannot read that as a number. Give it one column in pascals and
another in barrels per day and the pascals will drown out the barrels, not because they matter more
but because their numbers are bigger.

Preparation is not tidying for its own sake. Every node below exists because a specific thing goes
wrong without it.

The Studio groups them into three palettes:

| Palette | What it changes | Example |
|---|---|---|
| **Prepare** | Column names, types, and derived values | `rename-column`, `compute-column` |
| **Shape** | Which *rows* you keep | `filter-rows`, `sample` |
| **Transform** | The *values* inside numeric and text columns | `standardize`, `one-hot` |

A useful habit: shape first (fewer rows to process), prepare second (correct types and names), then
transform (scale and encode). It is not a rule, but it saves work.

## Lesson 2 — Fixing types and names

**`rename-column`** — gives a column a new name. Sensor exports arrive with tags like `TI-4021.PV`.
Rename it to `wellhead_temp` once and every later node, chart, and error message reads clearly.

**`convert-numeric`** — turns text into numbers. This is the single most common fix on KOC exports:
a column looks numeric to you but arrives as text because one row contained `"N/A"` or a comma.
Leave the column list blank and it converts every text column it can.

**`compute-column`** — makes a new column from existing ones with a small formula, for example
`(gas, oil) => gas / (oil + 1)` for a gas-oil ratio. The `+ 1` is deliberate: it keeps the division
safe when oil is zero, which happens on shut-in wells.

**`combine-columns`** — merges several numeric columns into one feature vector. Most pipelines never
need it, because the Train node combines features for you.

## Lesson 3 — Choosing which rows to keep

**`filter-rows`** — keeps rows where a column falls in a range. Use it to drop physically impossible
readings: a wellhead pressure of −5 is an instrument fault, not a measurement.

**`take-rows`** — keeps the first N rows. Good for trying a pipeline quickly on a big dataset; bad
for training, because "the first N" is rarely a fair sample of anything.

**`sample`** — keeps a random fraction. This *is* a fair sample, and it is the right way to shrink a
dataset while you experiment.

**`shuffle`** — reorders rows randomly. Matters when the file arrived sorted (all failures at the
bottom, say) and you are about to split it.

> **Careful with time.** If your question is about the future, shuffling and random sampling destroy
> the very thing you are trying to learn. See the *Forecast over time* track.

## Lesson 4 — Filling gaps and taming outliers

**`replace-missing`** — fills empty numeric cells with the column's mean, minimum, or maximum. Mean
is the usual choice. Think about whether missing really means "average": a missing flow reading
during a shutdown means *zero*, not *typical*, and filling it with the mean invents production that
never happened.

**`binning`** — turns a continuous column into buckets. Useful when the exact value matters less than
the band: choke position 0–10%, 10–20%, and so on. Also blunts extreme outliers, since everything
above the top bucket is treated alike.

## Lesson 5 — Putting columns on the same scale

Four nodes do closely related things, and choosing between them is mostly about outliers.

| Node | What it does | Reach for it when |
|---|---|---|
| **`standardize`** | Centres on 0, scales to unit variance | The default. Sensible for most sensor data |
| **`normalize`** | Squeezes into 0–1 | You need a bounded range, e.g. for display |
| **`log-normalize`** | Takes logarithms first | Values span orders of magnitude — permeability, particle counts |
| **`robust-scale`** | Scales using percentiles, not the mean | A handful of extreme outliers would otherwise dominate |

Two more, less common: **`lp-normalize`** scales each *row* to unit length (used with text features),
and **`global-contrast`** centres and scales each row — both act along rows rather than columns.

Scaling matters enormously for the linear algorithms (`sdca`, `lbfgs`, `sgd`, `perceptron`) and
barely at all for the tree ones (`fasttree`, `fastforest`), which only compare values within a
column. If you are using trees and short on time, skip scaling.

## Lesson 6 — Turning categories into numbers

A model cannot read `"Ahmadi"`. It needs numbers, and how you convert changes what it can learn.

**`one-hot`** — one new column per category, holding 0 or 1. The honest default for a handful of
categories. Four output kinds are available: *indicator* (0/1, the usual), *bag* (counts), *key*
(a single integer id), and *binary* (a compact bit encoding).

> Do not use *key* to feed a linear model. It says field 3 is three times field 1, which is
> meaningless, and the model will believe you.

**`hash-encode`** — maps categories into a fixed number of buckets (2^bits). Use it when there are
thousands of distinct values — well ids, equipment tags — and one-hot would produce an unusable
number of columns. Collisions are the price; more bits means fewer.

**`featurize-text`** — turns free text into numeric features. This is what to use on maintenance
notes and shift reports.

## Lesson 7 — Reducing the number of columns

**`select-columns`** and **`drop-columns`** — keep or remove named columns. The blunt instruments,
and often the right ones.

**`feature-selection`** — drops columns that are almost always the same value. A sensor that reads
zero for 99% of rows tells the model nothing and adds noise.

**`pca`** — replaces many correlated columns with a few combined ones. Useful when a dozen sensors
all move together; the cost is interpretability, because the new columns no longer correspond to
anything you can point at on a rig.

> **Watch the id column.** Dropping columns is where pipelines quietly break: if the id disappears,
> predictions cannot be matched back to rows, and a competition submission is rejected. The Train
> node never uses the id as a feature, so there is no reason to drop it.

## Lesson 8 — Build it yourself

Open the Studio and build this, using a dataset of your own:

1. `dataset` → `convert-numeric` → `replace-missing` → `standardize` → `split` → `train` → `evaluate`

Run it. Then remove `standardize`, run again, and compare the metric with a linear algorithm such as
`sdca`. Then switch the algorithm to `fasttree` and try both again.

You should see scaling matter for the linear model and barely register for the tree. That difference
is the whole lesson: preparation is not ritual, it is a response to how a particular algorithm reads
numbers.
