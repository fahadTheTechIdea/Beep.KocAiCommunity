# `hash-encode` — Hash encode
**Category:** Transform · **Ports:** Table → Table · **Handler:** `HashEncodeHandler` (`src/Beep.KocAiCommunity.ML/Nodes/MlTransformHandlers.cs`)

Hashes high-cardinality categoricals (e.g. well ids) into a fixed-width representation.

## What it does
1. Selects all `TextFeatures` (text-typed feature columns).
2. Applies `OneHotHashEncoding` to those columns using the ML.NET DEFAULT hash width.
3. Runs through `FitTransform`: fit on the TRAIN FOLD ONLY and records a replay for the validation/test folds.

## Parameters today
| key | UI control | type | default | range / clamp | required | column-aware |
|---|---|---|---|---|---|---|
None — acts on all numeric/text feature columns automatically.

## Gaps / plan (to be complete & friendly for non-IT users)
- Expose `bits` (numberOfBits, valid 1–30) so users control the hash width — the description says "fixed width" but the width is NOT actually exposed today.
- Expose the hash seed.
- Add a column selector so users can hash specific text columns rather than all of them.

## Notes
- Runs through `FitTransform` — fit on TRAIN FOLD ONLY plus recorded replay, so no leakage across folds.
- Operates on all `TextFeatures`; label, id, and `__fold` are excluded automatically.
