# `binning` — Bin values

**Category:** Transform · **Ports:** Table → Table · **Handler:** `BinningHandler`

Quantile-bin numeric features into buckets.

## Parameters
| `key` | Label | Type | Default | Required | Options / Range | In UI today |
|---|---|---|---|---|---|---|
| `bins` | Max bins | Number | `10` | no | clamped 2–255 | ✅ yes |

## Panel on click
One number field, pre-filled `10`.
