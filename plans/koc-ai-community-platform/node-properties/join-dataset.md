# `join-dataset` — Join another dataset
**Category:** Data · **Ports:** Table → Table · **Handler:** `JoinDatasetHandler` (`src/Beep.KocAiCommunity.ML/Nodes/DuckNodeHandlers.cs`)

Brings columns from a second attached dataset into the working table via a LEFT JOIN on a shared key.

## What it does
1. Reads `datasetId` (a GUID) and resolves the attached dataset to its `ds_N` table via `SecondaryTable(id)`; if it does not resolve, the node is skipped.
2. Reads `on` (the join key column, which must exist in BOTH tables) and `columns` (which columns of the joined dataset to bring in; blank means all).
3. Builds a LEFT JOIN of `working` (`w`) to the other table (`o`) on `w."on" = o."on"`, selecting `w.*` plus the selected other columns (excluding the join key itself).
4. Marked `replay:true` — it is treated as a column-adding op and is replayed onto the fixed eval set at predict time.

## Parameters today
| key | UI control | type | default | required | column/dataset-aware |
|---|---|---|---|---|---|
| datasetId | Dataset | GUID | (empty) | required (unresolved → skip) | Yes — must resolve or throw |
| on | Text | string | (empty) | required | Semantically a column (declared as free Text today; must exist in both tables) |
| columns | Columns | string (multi) | (empty = all) | optional | Yes — scope is the JOINED dataset's columns |

## Gaps / plan (to be complete & friendly for non-IT users)
- Add a join-type dropdown (Inner / Left / Right / Full); today only LEFT JOIN is supported.
- Make `on` a single column picker constrained to the intersection of both schemas (or split into separate leftKey / rightKey).
- Add a collision prefix/suffix option for brought columns that share a name with existing columns.

## Notes
- LEFT JOIN only.
- Duplicate keys in the other table multiply rows and can fail the id-uniqueness submission guard.
- A brought column with the same name as an existing column collides.
- `columns` validation scope at runtime is the JOINED dataset's columns, not the working table's.
