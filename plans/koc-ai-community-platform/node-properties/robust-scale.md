# `robust-scale` — Robust scale

**Category:** Transform · **Ports:** Table → Table

Scale by median and IQR — tolerant of outliers.

## Parameters
_None._ Operates on all numeric features automatically.

## Panel on click
"This node has no settings."

## Notes
Fits on the train fold and replays on the evaluation set at predict time; roles (numeric features only) are preserved via `FeatureNames`.
