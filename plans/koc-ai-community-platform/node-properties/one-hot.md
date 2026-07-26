# `one-hot` — One-hot encode

**Category:** Transform · **Ports:** Table → Table

Turn categorical (text) columns into indicator columns.

## Parameters
_None._ Operates on all text features automatically.

## Panel on click
"This node has no settings."

## Notes
Fits on the train fold and replays on the evaluation set at predict time; roles (text features only) are preserved via `FeatureNames`.
