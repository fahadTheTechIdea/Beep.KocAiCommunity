# `compute-column` — Compute column
**Category:** Prepare · **Ports:** Table → Table · **Handler:** `ComputeColumnHandler` (`src/Beep.KocAiCommunity.ML/Nodes/MlPrepareHandlers.cs`)

Creates a new numeric column from an ML.NET expression over one or more input columns.

## What it does
1. Reads `output` (new column name), `inputs` (input columns, in order), and `expression` (an ML.NET expression, e.g. `gor = gas/(oil+1)`).
2. Enforces a HARD leakage guard: fails if `inputs` include the label or the id column.
3. Binds the expression parameters to `inputs` IN ORDER (first parameter = first input column, etc.).
4. Evaluates the expression per row and writes the result into `output`.
5. Runs through FitTransform (fit on TRAIN FOLD only, then replay).

## Parameters today
| key | UI control | type | default | range / clamp | required | column-aware |
|---|---|---|---|---|---|---|
| output | Text | text | — | — | yes | no (free text, new name) |
| inputs | Columns | column list | — | must exclude label/id (hard fail) | yes | yes (multiple, order-significant) |
| expression | Text | text | — | valid ML.NET expression; params bind to inputs in order | yes | no (free text) |

## Gaps / plan (to be complete & friendly for non-IT users)
- `inputs` picker should exclude the label/id columns up front (so the hard guard never surprises the user) and preserve selection order.
- A guided expression editor with parameter hints (which name maps to which input) and a live preview of computed values.

## Notes
- HARD leakage guard: including the label or id column in `inputs` fails the run loudly — you cannot build a feature from the target.
- Parameter binding is positional: reordering `inputs` changes which column each expression parameter refers to.
- The computed column is numeric; it participates in downstream NumericFeatures.
