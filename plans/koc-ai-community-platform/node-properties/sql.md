# `sql` — SQL query

**Category:** Data · **Ports:** Table → Table · **Handler:** `SqlHandler`

Transform the data with a SQL SELECT over the table `working` — full DuckDB SQL (joins, aggregations, window functions, CASE, cross-column math). Keep the label column for downstream training.

## Parameters
| `key` | Label | Type | Default | Required | Options / Range | In UI today |
|---|---|---|---|---|---|---|
| `sql` | SELECT … FROM working | Text | — | **yes** | any DuckDB SELECT | ✅ yes |

## Panel on click
A multi-line text area for the SQL.

## Notes
`replay: true` — re-applied to the eval set at predict. A column-adding `SELECT *, expr …` is safe; dropping the label/id/`__fold` is caught downstream. A `SELECT` that references the label to derive a feature is target leakage (fails at predict on the label-less eval set).
