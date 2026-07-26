# `filter-rows` — Filter rows

**Category:** Shape · **Ports:** Table → Table · **Handler:** `FilterRowsHandler`

Keep rows where a numeric column falls in a range.

## Parameters
| `key` | Label | Type | Default | Required | Options / Range | In UI today |
|---|---|---|---|---|---|---|
| `column` | Column | Text | — | **yes** | existing numeric column | ✅ yes |
| `min` | Keep ≥ min | Number | −∞ | no | — | ✅ yes |
| `max` | Keep < max | Number | +∞ | no | — | ✅ yes |

## Panel on click
Column (text), min (number), max (number).

## Notes
`column` is free-text (not a `Columns` picker), so the handler checks presence and fails with a clear message on a typo. **Idea:** promote `column` to a `Columns`-typed single-select.
