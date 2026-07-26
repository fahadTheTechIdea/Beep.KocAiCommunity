# `join-dataset` — Join another dataset

**Category:** Data · **Ports:** Table → Table · **Handler:** `JoinDatasetHandler`

Bring in columns from a second dataset by matching a shared key column (a left join).

## Parameters
| `key` | Label | Type | Default | Required | Options / Range | In UI today |
|---|---|---|---|---|---|---|
| `datasetId` | Dataset to join | Dataset | — | **yes** | a resolvable dataset | ✅ yes (picker) |
| `on` | Key column (in both) | Text | — | **yes** | a column in both tables | ✅ yes |
| `columns` | Columns to bring (blank = all) | Columns | — | no | columns of the **joined** dataset | ✅ yes |

## Panel on click
Dataset picker + key (text) + columns (multi-select, validated against the joined dataset's schema).

## Notes
`w.*` preserves the primary's roles (X/y/id/`__fold`); only new feature columns come from the join. Fan-out on a duplicate key is caught at predict (duplicate-id guard). In a **competition submission** the secondary must be attached — participant-private datasets aren't (integrity).
