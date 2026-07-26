# `split` — Train/test split

**Category:** Split · **Ports:** Table → Table · **Handler:** `SplitHandler`

Holds out a fraction of rows for honest evaluation. Place it before the model. Writes the internal `__fold` marker (0 = train, 1 = test).

## Parameters
| `key` | Label | Type | Default | Required | Options / Range | In UI today |
|---|---|---|---|---|---|---|
| `testFraction` | Test fraction | Number | `0.25` | no | clamped 0.05–0.9 | ✅ yes |

## Panel on click
One number field, pre-filled `0.25`.

## Notes
Deterministic (seed 1). In predict mode it is a no-op (trains on the full set). Row-sampling nodes after `split` are rejected (they'd drop a fold).
