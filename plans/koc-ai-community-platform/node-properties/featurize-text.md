# `featurize-text` — Featurize text
**Category:** Transform · **Ports:** Table → Table · **Handler:** `FeaturizeTextHandler` (`src/Beep.KocAiCommunity.ML/Nodes/MlTransformHandlers.cs`)

Turns free text (e.g. HSE reports) into numeric feature vectors.

## What it does
1. Selects all `TextFeatures` (text-typed feature columns).
2. Applies `FeaturizeText` in place for each text feature column (output name == input name).
3. Consumes those columns: because they are replaced in place, downstream nodes no longer see them as text.
4. Runs through `FitTransform`: fit on the TRAIN FOLD ONLY and records a replay for the validation/test folds.

## Parameters today
| key | UI control | type | default | range / clamp | required | column-aware |
|---|---|---|---|---|---|---|
None — acts on all numeric/text feature columns automatically.

## Gaps / plan (to be complete & friendly for non-IT users)
- Add a per-column selector so users can featurize specific text columns.
- Expose n-gram length, word-vs-char grams, stopword removal, casing/diacritics handling, TF-IDF weighting, and vocabulary size — all `TextFeaturizingEstimator.Options` are hidden today.

## Notes
- Runs through `FitTransform` — fit on TRAIN FOLD ONLY plus recorded replay, so the learned vocabulary comes only from training data (no leakage).
- Featurizes in place (output name == input name), so the original text columns are consumed and not visible to later nodes as text.
