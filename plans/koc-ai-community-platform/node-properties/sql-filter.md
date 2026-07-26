# `sql-filter` — Filter (SQL)

**Category:** Data · **Ports:** Table → Table · **Handler:** `SqlFilterHandler`

Keep only rows matching a SQL condition, e.g. `pressure > 3000 AND zone = 'north'`.

## Parameters
| `key` | Label | Type | Default | Required | Options / Range | In UI today |
|---|---|---|---|---|---|---|
| `where` | WHERE condition | Text | — | **yes** | any DuckDB boolean expr | ✅ yes |

## Panel on click
A text field for the WHERE condition.

## Notes
`replay: false` — a row op, not applied to the eval set. `SELECT *` keeps all roles.
