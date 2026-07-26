# `rename-column` — Rename column

**Category:** Prepare · **Ports:** Table → Table · **Handler:** `RenameColumnHandler`

Give a feature a clearer name (e.g. `WHP` → `wellhead_pressure`).

## Parameters
| `key` | Label | Type | Default | Required | Options / Range | In UI today |
|---|---|---|---|---|---|---|
| `from` | From column | Text | — | **yes** | existing column | ✅ yes |
| `to` | New name | Text | — | **yes** | — | ✅ yes |

## Panel on click
Two text fields. Skips (with a message) if the `from` column is absent.

## Notes
Renaming the **label** column orphans the role → caught downstream by `RequireLabel`. Prefer renaming features only.
