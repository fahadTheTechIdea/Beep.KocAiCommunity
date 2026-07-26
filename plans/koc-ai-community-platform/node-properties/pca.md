# `pca` — PCA

**Category:** Transform · **Ports:** Table → Table · **Handler:** `PcaHandler`

Reduce numeric features to N principal components.

## Parameters
| `key` | Label | Type | Default | Required | Options / Range | In UI today |
|---|---|---|---|---|---|---|
| `rank` | Components | Number | `2` | no | clamped 1–(#numeric features) | ✅ yes |

## Panel on click
One number field, pre-filled `2`.

## Notes
Needs ≥ 2 numeric features or it skips. Outputs a `Pca` vector, drops the originals.
