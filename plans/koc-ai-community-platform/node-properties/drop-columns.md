# `drop-columns` — Drop columns

**Category:** Transform · **Ports:** Table → Table · **Handler:** `DropColumnsHandler`

Remove the listed columns (ids, noise).

## Parameters
| `key` | Label | Type | Default | Required | Options / Range | In UI today |
|---|---|---|---|---|---|---|
| `columns` | Columns to drop | Columns | — | **yes** | any column | ✅ yes |

## Panel on click
A column multi-select.

## Notes
Drops **any** listed column; dropping the label/id/`__fold` is caught downstream (`RequireLabel` / predict id-guard / leakage guard).
