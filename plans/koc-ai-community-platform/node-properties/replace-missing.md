# `replace-missing` — Replace missing

**Category:** Transform · **Ports:** Table → Table · **Handler:** `ReplaceMissingHandler`

Impute missing numeric values (common in PI sensor gaps).

## Parameters
| `key` | Label | Type | Default | Required | Options / Range | In UI today |
|---|---|---|---|---|---|---|
| `mode` | Replace with | Select | `mean` | no | `mean`, `min`, `max` | ✅ yes |

## Panel on click
One dropdown, pre-selected `mean`.
