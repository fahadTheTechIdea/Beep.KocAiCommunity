# `convert-numeric` — Cast to number

**Category:** Prepare · **Ports:** Table → Table · **Handler:** `ConvertNumericHandler`

Convert text/typed columns to numbers so they can be used as features.

## Parameters
| `key` | Label | Type | Default | Required | Options / Range | In UI today |
|---|---|---|---|---|---|---|
| `columns` | Columns (blank = all text) | Columns | — | no | feature columns | ✅ yes |

## Panel on click
A column multi-select (blank = every text feature). Column names validated against the table.

## Notes
Filtered through `FeatureNames`, so it can never target the label/id.
