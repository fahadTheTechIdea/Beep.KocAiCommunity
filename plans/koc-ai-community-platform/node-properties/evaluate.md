# `evaluate` — Evaluate
**Category:** Evaluate · **Ports:** Table → Metrics · **Handler:** `MlModelHandlers` (`src/Beep.KocAiCommunity.ML/Nodes/MlModelHandlers.cs`, factories in `MlModelOps.cs`)

Computes the headline metric on the held-out test fold and records it as the run's primary value.

## What it does
1. In predict mode, or when the model is null → skip.
2. `RequireLabel` — fails if no label column is set.
3. Reapplies `ctx.LabelMap` (the multiclass key map).
4. Reports: regression → R² / RMSE; multiclass → Micro / Macro accuracy; binary → Accuracy / AUC (NonCalibrated).
5. Writes `ctx.PrimaryValue`.

## Parameters today
| key | UI control | type | default | range / clamp | required | column-aware |
|---|---|---|---|---|---|---|
| — | None — acts on all flowing columns. | | | | | |

## Gaps / plan (to be complete & friendly for non-IT users)
- None.

## Notes
- Requires the label (`RequireLabel`).
- Reapplies `ctx.LabelMap` so multiclass predictions map back to original class keys.
- Sets `ctx.PrimaryValue` — this is the run's headline metric.
