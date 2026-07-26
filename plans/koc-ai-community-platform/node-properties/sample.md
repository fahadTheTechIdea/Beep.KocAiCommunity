# `sample` — Sample rows

**Category:** Shape · **Ports:** Table → Table · **Handler:** `SampleHandler`

Take a random fraction of the rows (shuffle-then-take, seed 1).

## Parameters
| `key` | Label | Type | Default | Required | Options / Range | In UI today |
|---|---|---|---|---|---|---|
| `fraction` | Fraction to keep | Number | `0.5` | no | 0–1 | ✅ yes |

## Panel on click
One number field, pre-filled `0.5`.

## Notes
**Rejected after `split`** (would alter the held-out set).
