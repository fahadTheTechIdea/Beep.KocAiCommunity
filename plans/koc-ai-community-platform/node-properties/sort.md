# `sort` — Sort

**Category:** Data · **Ports:** Table → Table · **Handler:** `SortHandler`

Order rows by columns, e.g. `pressure DESC, well_id`.

## Parameters
| `key` | Label | Type | Default | Required | Options / Range | In UI today |
|---|---|---|---|---|---|---|
| `orderBy` | ORDER BY | Text | — | **yes** | e.g. `pressure DESC` | ✅ yes |

## Panel on click
A text field for the ORDER BY.

## Notes
`replay: false`. Appends every column as a total-order tie-break → deterministic, reproducible order.
