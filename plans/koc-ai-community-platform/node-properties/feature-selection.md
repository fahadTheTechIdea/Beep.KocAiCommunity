# `feature-selection` — Feature selection
**Category:** Transform · **Ports:** Table → Table · **Handler:** `FeatureSelectionHandler` (`src/Beep.KocAiCommunity.ML/Nodes/MlTransformHandlers.cs`)

Filters numeric features by how many non-default values they contain.

## What it does
1. Reads the `count` parameter (default 1, floored with `Math.Max(1, ...)`, no upper clamp).
2. Concatenates all `NumericFeatures`.
3. Applies `SelectFeaturesBasedOnCount` (keeps features whose non-default value count meets the threshold), producing a single `Fs` vector.
4. Runs through `FitTransform`: fit on the TRAIN FOLD ONLY and records a replay for the validation/test folds.

## Parameters today
| key | UI control | type | default | range / clamp | required | column-aware |
|---|---|---|---|---|---|---|
| `count` | number field | integer | `1` | floored at 1 (`Math.Max(1, ...)`), no upper clamp | no | acts on all numeric features |

## Gaps / plan (to be complete & friendly for non-IT users)
- Add variance / mutual-information modes and top-K selection.
- Add a column selector and an upper clamp for `count`.
- Fix the messaging: the description says "near-constant", but this is actually a non-default-value count filter, not a variance filter.

## Notes
- Runs through `FitTransform` — fit on TRAIN FOLD ONLY plus recorded replay, so selection is based only on training data (no leakage).
- Concatenates numeric features into a single `Fs` vector output; label, id, and `__fold` are excluded automatically.
