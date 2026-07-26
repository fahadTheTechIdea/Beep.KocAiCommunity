# `normalize` — Normalize (min-max)

**Category:** Transform · **Ports:** Table → Table

Scale numeric features to 0–1.

## Parameters
_None._ Operates on all numeric features automatically.

## Panel on click
"This node has no settings."

## Notes
Fits on the train fold and replays on the evaluation set at predict time; roles (numeric features only) are preserved via `FeatureNames`.
