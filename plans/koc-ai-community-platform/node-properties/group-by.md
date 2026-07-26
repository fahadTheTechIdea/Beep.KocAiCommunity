# `group-by` — Group & aggregate

**Category:** Data · **Ports:** Table → Table · **Handler:** `GroupByHandler`

Aggregate rows: pick group-by columns and aggregate expressions.

## Parameters
| `key` | Label | Type | Default | Required | Options / Range | In UI today |
|---|---|---|---|---|---|---|
| `groupBy` | Group-by columns | Columns | — | **yes** | existing columns | ✅ yes |
| `aggregations` | Aggregates | Text | — | **yes** | e.g. `AVG(pressure) AS avg_p, MAX(vibration) AS max_v` | ✅ yes |

## Panel on click
Group-by (column multi-select) + aggregations (text).

## Notes
**Reshapes the column set** — roles survive only if in the keys/aggregates, so it doesn't fit before the model in a supervised pipeline. For a group-derived *feature*, use a `sql` window function (`AVG(fare) OVER (PARTITION BY pclass)`) instead.
