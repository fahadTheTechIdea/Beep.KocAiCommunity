# `hash-encode` — Hash encode

**Category:** Transform · **Ports:** Table → Table

Hash high-cardinality categoricals (e.g. well ids) into a fixed width.

## Parameters
_None._ Operates on all text features automatically.

## Panel on click
"This node has no settings."

## Notes
Fits on the train fold and replays on the evaluation set at predict time; roles (text features only) are preserved via `FeatureNames`.
