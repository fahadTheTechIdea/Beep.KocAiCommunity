# `union-dataset` — Append another dataset

**Category:** Data · **Ports:** Table → Table · **Handler:** `UnionDatasetHandler`

Add the rows of a second dataset to the current data (columns aligned by name; missing ones become null).

## Parameters
| `key` | Label | Type | Default | Required | Options / Range | In UI today |
|---|---|---|---|---|---|---|
| `datasetId` | Dataset to append | Dataset | — | **yes** | a resolvable dataset (must carry the label) | ✅ yes (picker) |

## Panel on click
Dataset picker.

## Notes
`replay: false` — augments **training** rows only. Rejects a secondary missing the label (would train NULL-label rows). Appended rows get `__fold = 0` when a split has run.
