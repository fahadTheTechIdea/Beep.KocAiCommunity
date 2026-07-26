# `evaluate` — Evaluate

**Category:** Evaluate · **Ports:** Table → Metrics · **Handler:** `EvaluateHandler`

Computes the headline metric on the held-out (test-fold) set — Accuracy/AUC (binary), Micro/Macro-Acc (multiclass), R²/RMSE (regression).

## Parameters
_None._

## Panel on click
"This node has no settings."

## Notes
Requires the label (`RequireLabel`). Skipped in predict mode. Sets the run's primary metric.
