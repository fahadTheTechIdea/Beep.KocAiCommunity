# `compute-column` — Compute column

**Category:** Prepare · **Ports:** Table → Table · **Handler:** `ComputeColumnHandler`

Create a new column from a formula; params bind to the input columns in order.

## Parameters
| `key` | Label | Type | Default | Required | Options / Range | In UI today |
|---|---|---|---|---|---|---|
| `output` | New column name | Text | — | **yes** | — | ✅ yes |
| `inputs` | Input columns | Columns | — | **yes** | feature columns (not label/id) | ✅ yes |
| `expression` | Formula | Text | — | **yes** | e.g. `(gas, oil) => gas / (oil + 1)` | ✅ yes |

## Panel on click
Output name (text), inputs (column multi-select), expression (text).

## Notes
**Rejects the label/id as inputs** (target-leakage guard) — deriving a feature from the target fails loudly.
