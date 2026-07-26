# `combine-columns` — Merge columns

**Category:** Prepare · **Ports:** Table → Table · **Handler:** `CombineColumnsHandler`

Combine several numeric columns into one feature vector (`Combined`).

## Parameters
| `key` | Label | Type | Default | Required | Options / Range | In UI today |
|---|---|---|---|---|---|---|
| `columns` | Columns (blank = all numeric) | Columns | — | no | numeric features | ✅ yes |

## Panel on click
A column multi-select (blank = all numeric features).

## Notes
Needs ≥ 2 columns or it skips. Filtered through `FeatureNames` (roles safe).
