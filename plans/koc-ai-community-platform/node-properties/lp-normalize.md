# `lp-normalize` — Lp-normalize
**Category:** Prepare · **Ports:** Table → Table · **Handler:** `LpNormalizeHandler` (`src/Beep.KocAiCommunity.ML/Nodes/MlPrepareHandlers.cs`)

Unit-normalizes each row's numeric feature vector (Lp norm) so every row has comparable scale.

## What it does
1. Assembles the numeric feature vector for each row.
2. Skips with a "no numeric columns" result if there are no numeric features.
3. Applies `NormalizeLpNorm` to unit-normalize the vector per row.
4. Runs through FitTransform (fit on TRAIN FOLD only, then replay).

## Parameters today
None.

## Gaps / plan (to be complete & friendly for non-IT users)
- None required. Optionally add a columns picker so users can normalize a chosen subset of numeric features rather than all of them.

## Notes
- Row-wise normalization: each row is scaled independently, so this carries essentially no cross-row fitted state, but it still runs through the fit/replay path for consistency.
- No-op (skip) when there are no numeric feature columns.
- Operates on the numeric feature subset only; label/id/__fold are excluded via FeatureNames.
