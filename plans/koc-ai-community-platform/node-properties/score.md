# `score` — Score
**Category:** Evaluate · **Ports:** Model → Table · **Handler:** `MlModelHandlers` (`src/Beep.KocAiCommunity.ML/Nodes/MlModelHandlers.cs`, factories in `MlModelOps.cs`)

Applies the trained model to the held-out test fold and returns the scored rows.

## What it does
1. In predict mode, or when the model is null → skip.
2. Scores `FoldTestView` (the held-out test fold).
3. Reports the scored row count.

## Parameters today
| key | UI control | type | default | range / clamp | required | column-aware |
|---|---|---|---|---|---|---|
| — | None — acts on all flowing columns. | | | | | |

## Gaps / plan (to be complete & friendly for non-IT users)
- None.

## Notes
- Does **not** call `RequireLabel` — scoring only needs the model and features.
- Operates on `FoldTestView`, keeping evaluation on genuinely held-out rows.
