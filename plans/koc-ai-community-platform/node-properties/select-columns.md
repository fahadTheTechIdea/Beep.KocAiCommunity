# `select-columns` — Select columns

**Category:** Transform · **Ports:** Table → Table · **Handler:** `SelectColumnsHandler`

Keep only the chosen feature columns; drop the rest.

## Parameters
| `key` | Label | Type | Default | Required | Options / Range | In UI today |
|---|---|---|---|---|---|---|
| `columns` | Columns to keep | Columns | — | **yes** | feature columns | ✅ yes |

## Panel on click
A column multi-select. Column names validated against the table.

## Notes
Only drops **features** (roles auto-protected even if unlisted).
