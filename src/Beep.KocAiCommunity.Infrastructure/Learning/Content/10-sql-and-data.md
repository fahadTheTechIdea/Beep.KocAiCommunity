title: Query and combine data
summary: Aggregate, join, filter, and reshape with SQL nodes that run inside your pipeline — for when preparation needs more than a checkbox.
level: Intermediate
order: 10

## Lesson 1 — SQL inside the pipeline

The Prepare and Transform nodes handle common jobs with a setting or two. Some jobs need more: a
monthly average per well, a join to a completions table, a filter with two conditions and an OR.

The Data palette runs **DuckDB** inside your pipeline. The rows flowing in are a table named
`working`, your query runs, and the result flows on to the next node. Same graph, same replay on
evaluation data, same reproducibility — you are not stepping outside the pipeline to use it.

Seven nodes. Three are visual builders over generated SQL that stays editable underneath; four are
structural.

## Lesson 2 — Filtering and sorting

**`sql-filter`** — keeps rows matching a WHERE condition. Use it when `filter-rows` isn't enough,
because you need more than one column or an OR:

```sql
wellhead_pressure > 500 AND (status = 'flowing' OR status = 'testing')
```

The condition builder writes this for you from dropdowns when the columns are known; the text stays
editable for anything the builder can't express.

**`sort`** — orders rows by an ORDER BY clause, `pressure DESC` or `well_id, reading_date`. Ordering
rarely changes what a model learns, but it matters before `take-rows`, and it makes a preview
readable.

**`distinct`** — removes duplicate rows. Exports that stitch together overlapping periods often carry
duplicates, and duplicated rows quietly weight those readings more heavily during training.

## Lesson 3 — Aggregating

**`group-by`** collapses many rows into one per group — the workhorse of this palette.

- **Group-by columns**: `well_id`, or `well_id, month`
- **Aggregates**: `AVG(pressure) AS avg_p, MAX(temp) AS peak_t, COUNT(*) AS readings`

This is how you turn a sensor stream into one row per well per month, which is usually the shape a
model needs. Raw readings every thirty seconds are rarely the right grain for a question asked
monthly.

Available aggregates are the usual set: `AVG`, `SUM`, `MIN`, `MAX`, `COUNT`, `STDDEV`. Always alias
with `AS` — the alias becomes the column name downstream, and `avg_p` is easier to live with than
`AVG(pressure)`.

> **Aggregating with time in mind.** A monthly average that includes the month you are predicting is
> leakage. If you are forecasting, aggregate only over periods that precede the target.

## Lesson 4 — Combining datasets

**`join-dataset`** brings columns from a second dataset alongside the current rows — well headers onto
readings, completions onto production.

- **Dataset to join**: chosen from datasets you can see
- **Join type**: *left* keeps all current rows (the safe default), *inner* keeps only matches, *right*
  and *full* keep the joined side
- **Key column**: must exist in both
- **Columns to bring**: blank brings everything

**`union-dataset`** appends another dataset's rows below the current ones — stacking this year onto
last year. The columns should match; mismatched columns produce gaps.

> **The fan-out trap.** If the joined dataset has more than one row per key, every match multiplies
> your rows. Your dataset silently grows, readings get double-counted, and a competition submission
> is rejected for duplicate ids. Before joining, check the key is unique on the other side — a
> `group-by` on the joined dataset first is often the fix.

## Lesson 5 — Arbitrary SQL

**`sql`** runs a full `SELECT … FROM working` and passes the result on. Everything the other nodes do,
plus CASE expressions, window functions, and subqueries:

```sql
SELECT well_id, reading_date, rate,
       AVG(rate) OVER (PARTITION BY well_id ORDER BY reading_date
                       ROWS BETWEEN 6 PRECEDING AND 1 PRECEDING) AS rate_7d_avg
FROM working
```

That window function builds a rolling average **excluding the current row** — a genuinely useful
forecasting feature, and note the `1 PRECEDING`: including the current row would leak the value you're
trying to predict into its own feature.

Power comes with responsibility. A hand-written SELECT that drops the id column breaks prediction
alignment, and one that filters rows changes the row count in ways later nodes may not expect. The
pipeline will tell you — it fails loudly rather than emitting misaligned predictions — but it is
easier not to.

## Lesson 6 — Keep the id and the fold marker

Two columns must survive every SQL node:

**The id column.** Predictions are matched back to rows by id. `SELECT *` keeps it; an explicit column
list must name it. If it disappears, the pipeline fails with a clear message rather than guessing.

**The fold marker.** After a `split`, rows carry a marker saying train or test. A SELECT that drops it
would silently merge the two, so the model trains on its own test set. The platform detects this and
refuses to run — one of the few places it will stop you outright, because the alternative is a result
that looks excellent and means nothing.

The safe habit: put SQL nodes **before** the split, and prefer `SELECT *` with a WHERE over a
hand-picked column list.

## Lesson 7 — Build it yourself

A realistic preparation chain:

1. `dataset` — raw sensor readings
2. `sql-filter` — `status = 'flowing' AND rate > 0`
3. `group-by` — group `well_id, month`, aggregate `AVG(rate) AS avg_rate, COUNT(*) AS readings`
4. `join-dataset` — bring well header columns on `well_id` (left join)
5. `sql` — a CASE expression to band wells by completion type
6. `time-split` → `train` → `evaluate`

Run it and check the row count after each node. Watching thirty-second readings become one row per
well per month, then gain header columns, is the clearest way to understand what these nodes do — and
the fastest way to notice a join that multiplied your rows when it shouldn't have.
