title: Forecast over time
summary: Production decline, pressure trends, demand next quarter. Forecasting is regression with one rule you cannot break — and the Studio enforces it with a single node.
level: Intermediate
order: 6

## Lesson 1 — Forecasting is regression that respects time

A forecast predicts a number, so it runs on the regression engine and everything from *Predict a
number* applies: the same algorithms, the same metrics, the same diagnosis order.

One thing changes, and it changes everything: **the rows are ordered, and the order carries the
answer**. You are asking about the future, so the model must be trained only on the past.

Set the Train node's **Task** to **Regression**, and use a **`time-split`** rather than a `split`.
That is the whole difference in the graph. It is also the difference between a forecast you can act on
and one that is quietly measuring its own memory.

## Lesson 2 — Why a random split lies

`split` shuffles rows and holds back a random quarter. On time-ordered data that scatters *future*
rows into the training set. The model learns from October while being tested on September, scores
beautifully, and fails the moment it meets a month nobody has seen.

Nothing looks wrong when this happens. There is no error, no warning — the metric simply improves.
That is what makes it dangerous.

**`time-split`** orders rows by a column you name and holds back the most recent fraction. The model
trains on the past and is scored on the future, which is the situation it will actually face.

> Use `time-split` whenever the question contains the word *next*: next month's rate, next quarter's
> decline, days until the next failure.

## Lesson 3 — The time-split node

Two settings:

- **Time / order column** — the column that orders the series. A date, a timestamp, or a plain
  sequence number all work. The node sorts by whichever type the column actually holds — dates as
  dates, numbers as numbers — rather than sorting text and putting `10` before `9`.
- **Test fraction (most recent)** — how much of the tail to hold back. 0.25 is a sensible default.

Blank rows in the order column sort first, treated as earliest. If your time column has gaps, fix them
before you split rather than letting the node guess.

## Lesson 4 — Features that exist at prediction time

This is where most forecasts break, and the split does not protect you.

Every feature must be knowable **at the moment the prediction is made**. A monthly average that
includes the month you are forecasting is a leak. Cumulative production up to and including the target
period is a leak. A field-wide total computed across the whole file is a leak.

The test for any feature: *would I have had this value, with this content, on the day I needed the
forecast?* If not, remove it — however much it helps the score. Especially then.

Safe and useful features for a decline forecast:

- Values from **previous** periods (last month's rate, the month before)
- Time elapsed since first production
- Static well properties — completion type, zone, depth
- Operating settings in force during the period being predicted

## Lesson 5 — Choosing an algorithm

Start with **`fasttree`**. Decline curves bend, and boosted trees capture bends that a straight line
cannot.

**`sdca`** and **`lbfgs`** give a straight-line relationship. That is sometimes exactly right — a
gentle linear decline over a short horizon — and it is far easier to explain.

**`gam`** sits between them: it fits a curve per feature and lets you show the shape to a reviewer.

**`ogd`** processes rows one at a time and suits very long histories that will not fit in memory.

Remember that trees do not extrapolate. A tree trained on rates between 200 and 900 barrels will never
predict 1,100 — it can only recombine values it has seen. For a long-horizon forecast heading well
outside the historical range, a linear model may be more honest even if it scores worse.

## Lesson 6 — Reading a forecast's metrics

RMSE and MAE are in the units of what you are forecasting, which makes them easy to judge: *"typically
wrong by 35 barrels a day"* is a sentence an operations engineer can act on.

R² is less useful here than in ordinary regression. A series with a strong trend gives a flattering R²
almost automatically, because predicting the trend alone explains most of the variation. Look at the
error in real units instead.

Compare against the dullest possible baseline: **next month equals this month**. If your model cannot
beat that, it is not yet earning its place.

## Lesson 7 — Build it yourself

1. `dataset` → `convert-numeric` → `time-split` → `train` → `evaluate`
2. On the `time-split` node set the order column to your date column, test fraction 0.25.
3. On `train`, Task = Regression, target = the quantity you are forecasting, algorithm = `fasttree`.
4. Run it and note the RMSE.
5. Now swap `time-split` for a plain `split` and run again.

The second number will be better. It is also fiction. Sitting with that gap for a minute teaches more
about evaluation than any amount of reading.

Then take it to the **Production Decline** demo competition, where the hidden test set is genuinely in
the future and no amount of tuning against your own rows can flatter it.
