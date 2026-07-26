# `featurize-text` — Featurize text

**Category:** Transform · **Ports:** Table → Table

Turn free-text (e.g. HSE reports) into numeric vectors.

## Parameters
_None._ Operates on all text features automatically.

## Panel on click
"This node has no settings."

## Notes
Fits on the train fold and replays on the evaluation set at predict time; roles (text features only) are preserved via `FeatureNames`.
