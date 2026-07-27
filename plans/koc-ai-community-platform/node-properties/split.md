# `split` — Train/test split
**Category:** Split · **Ports:** Table → Table · **Handler:** `MlModelHandlers` (`src/Beep.KocAiCommunity.ML/Nodes/MlModelHandlers.cs`, factories in `MlModelOps.cs`)

Holds out a fraction of rows for honest evaluation by tagging each row with an internal `__fold` marker.

## What it does
1. In predict mode → no split (trains on the full set).
2. Sets `ctx.HasSplit = true`.
3. Clamps `testFraction` to the allowed range.
4. Runs `TrainTestSplit(seed: 1)` and writes `__fold = 0` (train) / `__fold = 1` (test).

## Parameters today
| key | UI control | type | default | range / clamp | required | column-aware |
|---|---|---|---|---|---|---|
| `testFraction` | Number | number | `0.25` | `Math.Clamp(..., 0.05, 0.9)` | no | no |

## Gaps / plan (to be complete & friendly for non-IT users)
- No major gaps.
- Could expose a **stratify-by-label** toggle so class balance is preserved across folds.
- Could expose a **seed** field for users who want reproducible-but-varied splits.

## Notes
- Deterministic — fixed seed `1`.
- Enables the downstream `__fold` leakage guard: nodes that would drop a fold after `split` are rejected.
