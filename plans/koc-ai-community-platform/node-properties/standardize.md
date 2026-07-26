# `standardize` — Standardize (z-score)

**Category:** Transform · **Ports:** Table → Table

Rescale numeric features to mean 0, variance 1.

## Parameters
_None._ Operates on all numeric features automatically.

## Panel on click
"This node has no settings."

## Notes
Fits on the train fold and replays on the evaluation set at predict time; roles (numeric features only) are preserved via `FeatureNames`.
