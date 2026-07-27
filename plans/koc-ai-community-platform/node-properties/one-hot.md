# `one-hot` — One-hot encode
**Category:** Transform · **Ports:** Table → Table · **Handler:** `OneHotHandler` (`src/Beep.KocAiCommunity.ML/Nodes/MlTransformHandlers.cs`)

Turns categorical (text) columns into indicator columns the model can use.

## What it does
1. Selects all `TextFeatures` (text-typed feature columns).
2. Applies `OneHotEncoding` to every one of those text feature columns in place.
3. Runs through `FitTransform`: fit on the TRAIN FOLD ONLY and records a replay for the validation/test folds.

## Parameters today
| key | UI control | type | default | range / clamp | required | column-aware |
|---|---|---|---|---|---|---|
None — acts on all numeric/text feature columns automatically.

## Gaps / plan (to be complete & friendly for non-IT users)
- Add a column selector so users can encode specific text columns rather than all of them.
- Expose output-kind (Indicator / Bag / Key / Binary).
- Expose max-key-count and unseen-category handling.

## Notes
- Runs through `FitTransform` — fit on TRAIN FOLD ONLY plus recorded replay, so the category vocabulary comes only from training data (no leakage).
- Operates on ALL `TextFeatures`; label, id, and `__fold` are excluded automatically.
