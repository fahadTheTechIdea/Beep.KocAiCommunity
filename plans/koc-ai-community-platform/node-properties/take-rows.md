# `take-rows` — Take first N

**Category:** Shape · **Ports:** Table → Table · **Handler:** `TakeRowsHandler`

Keep only the first N rows (quick experiments on big data).

## Parameters
| `key` | Label | Type | Default | Required | Options / Range | In UI today |
|---|---|---|---|---|---|---|
| `count` | Rows to keep | Number | `1000` | no | ≥ 1 | ✅ yes |

## Panel on click
One number field, pre-filled `1000`.

## Notes
**Rejected after `split`** — it would drop the held-out test fold (split lays out train rows then test rows). Place it before the split.
