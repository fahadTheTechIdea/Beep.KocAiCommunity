# `sample` — Sample rows
**Category:** Shape · **Ports:** Table → Table · **Handler:** `SampleHandler` (`src/Beep.KocAiCommunity.ML/Nodes/MlPrepareHandlers.cs`)

Keeps a random fraction of the rows.

## What it does
1. Reads `fraction` (default 0.5); there is no hard range clamp on the fraction itself.
2. HARD FAILS if the pipeline has already been split (positional take after split would drop / alter the held-out fold).
3. Shuffles the rows deterministically (`ShuffleRows(seed: 1)`).
4. Takes the resulting row count, floored to at least 1 (`Math.Max(1, ...)`).

## Parameters today
| key | UI control | type | default | range / clamp | required | column-aware |
|---|---|---|---|---|---|---|
| fraction | Number | number | 0.5 | no hard range clamp on fraction; resulting row count floored to >= 1 | no (has default) | no |

## Gaps / plan (to be complete & friendly for non-IT users)
- Enforce a 0–1 range for `fraction` in the UI (the handler does not clamp it), so users cannot enter nonsensical values.
- Show the resulting row count as the fraction is adjusted.

## Notes
- HARD FAIL after split (same reason as `take-rows`): sampling positionally after the split would alter the held-out set. Place sample before the split.
- Randomization is a deterministic shuffle (`seed: 1`) followed by a take, so results are reproducible.
- Not column-aware; operates on whole rows.
