# `dataset` — Dataset
**Category:** Source · **Ports:** None → Table · **Handler:** `MlModelHandlers` (`src/Beep.KocAiCommunity.ML/Nodes/MlModelHandlers.cs`, factories in `MlModelOps.cs`)

The pipeline's data entry point — surfaces the loaded table and its row/column counts to everything downstream.

## What it does
1. Acts as a pure passthrough — does not transform, filter, or copy the data.
2. Returns `"done"` with detail `"{rows} rows · {feature count} columns"`.
3. Feature count is derived from `FeatureNames` = all columns **except** `LabelColumn`, `IdColumn`, and the internal `__fold` column.

## Parameters today
| key | UI control | type | default | required | column-aware |
|---|---|---|---|---|---|
| `datasetId` | Dataset picker (of the caller's file datasets) | GUID | — | no (free mode) | Selecting it drives the whole pipeline's column pickers |

## How it works
- **FREE mode:** the user picks the **training dataset** on this node. The designer reads it, runs the graph on it, and loads its schema so every downstream `Column`/`Columns` field becomes a real picker. Defaults to the user's first dataset for convenience.
- **COMPETITION mode:** the data is fixed by the host, so this is left empty — the server injects the competition's training set. (The executor's dataset node is a passthrough; `datasetId` is a designer-level selector, not read by the handler.)
- The old "Run pipeline" box no longer has its own dataset/label/task pickers — data comes from this node, target/task from the **Train** node.

## Notes
- Pure passthrough at execution — introduces no ordering or leakage concerns of its own.
- `__fold` is internal bookkeeping and is deliberately excluded from the reported feature count.
