# 03 — Designer UX parity with the field's best

> **Depends on:** 02 for profiling. **Blocks:** nothing, but multiplies the value of 04.
> **Shared surface:** every change here lands in `Ui.Studio`, so the **Web gets it too**.

## Context

The designer is the app. It is shared verbatim between Web and desktop, which means improvements are
paid for once and delivered twice — and also that regressions are felt twice.

Measured against Azure ML Designer (Phase 00, §3), two capabilities are missing that change how a
pipeline gets debugged, and three more are ordinary desktop expectations we do not meet.

| Capability | Azure ML Designer | Us |
|---|---|---|
| Preview the table flowing out of any node | ✅ right-click → Preview Data | ❌ one log line per node |
| Searchable node palette | ✅ | ❌ 38 nodes, browse only |
| Validation before running | ✅ on the canvas | 🟡 compiler errors, in the log |
| Undo / redo | — | ❌ |
| Run results that survive navigation | ✅ | ❌ lost |

## Scope

**In**

- Per-node data preview
- Palette search and keyboard-driven add
- Validation surfaced on the canvas
- Undo / redo
- A run pane that persists

**Out**

- Rewriting the canvas library. `Z.Blazor.Diagrams` stays.
- Collaborative editing. One person, one workflow.
- Node authoring in the UI. Nodes are code; the catalogue is the contract.

## Design

### Per-node data preview — the highest-value item

Today, when a pipeline produces an odd metric, the only recourse is to reason about what each node
*probably* did. Being able to look at the table between two nodes turns that from deduction into
observation.

The executor already materialises a `PipelineTable` between nodes. It is discarded. The change is to
retain a **bounded sample** — first 100 rows, first 50 columns — per node for the last run, and show it
when the node is selected.

```
PipelineExecutionResult
  └─ NodeResults[]
       ├─ NodeId, Kind, Status, Message        ← exists
       ├─ RowsIn, RowsOut, ElapsedMs           ← new
       └─ Sample: { Columns[], Rows[][] }      ← new, bounded
```

**Memory is the constraint.** A 40-node pipeline retaining 100×50 cells each is fine; retaining full
tables is not. Bound at construction, never after.

This is worth doing on the desktop first: it runs in-process, so the sample never crosses a wire. The
Web gets the same thing at the cost of a larger response, which is a reason to keep the bound tight.

### Palette search

A text box over the node catalogue filtering on display name, category and description. Sorted by
category, matches highlighted. `Ctrl+K` focuses it; `Enter` adds the top match at the canvas centre.

The catalogue is already in memory via `INodeRegistry`. This is a filter, not a lookup.

### Validation on the canvas

The compiler already knows what is wrong — a missing target column, no split before a supervised
model, an unreachable node. Today that arrives as text in the log after pressing Run.

Run the same validation **as the graph changes**, debounced, and mark the offending nodes: a warning
badge on the node, the reason in the property panel, and a count in the toolbar. Pressing Run with
errors outstanding should say what they are rather than failing at the first one.

> The split-before-fit rule is the one users hit most, and it currently surfaces at publish time. It
> should be visible while building — that is a leakage guard, and finding out late is finding out after
> you have already reasoned about a wrong number.

### Undo / redo

`Ctrl+Z` / `Ctrl+Y` over a bounded stack of graph snapshots — the definition JSON is small and the
simplest correct thing. 50 entries; cleared on load. Covers add, delete, move, connect, disconnect and
property edits.

Property edits need debouncing so typing into a text field is one undo entry, not one per keystroke.

### Persistent run pane

Move run results out of component state into a scoped `RunSession` service holding the last N runs for
the open workflow. Navigating away and back keeps them. On the desktop, persist to the workspace so they
survive a restart — that is Phase 04's run history, and this pane becomes its viewer.

## Files

| File | Change |
|---|---|
| `Ui.Studio/Pages/WorkflowDesigner.razor` | Palette search; validation badges; undo/redo; preview panel |
| `Ui.Studio/Services/GraphHistory.cs` | New — undo/redo stack |
| `Ui.Studio/Services/RunSession.cs` | New — run results outliving the page |
| `Ui.Shared/Components/DataPreview.razor` | New — the bounded table viewer, shared |
| `Application/ML/PipelineExecutionResult.cs` | `RowsIn`, `RowsOut`, `ElapsedMs`, bounded `Sample` |
| `ML/Nodes/PluginNodeExecutor.cs` | Capture the bounded sample per node |
| `Api/Endpoints/StudioEndpoints.cs` | Carry the new fields |

## Acceptance criteria

- [ ] Selecting a node after a run shows the table that flowed out of it
- [ ] The sample is bounded — a million-row dataset does not grow the response or the process materially
- [ ] Typing in the palette filters within a keystroke; `Enter` adds the top match
- [ ] A supervised model with no split ahead of it is badged **before** Run is pressed
- [ ] The toolbar shows an outstanding-problem count
- [ ] `Ctrl+Z` reverses add, delete, move, connect and property edits
- [ ] Typing a value produces one undo entry, not one per character
- [ ] Navigating away from the designer and back preserves the last run's results
- [ ] All of the above work identically in the Web

## Tests

| Test | Level |
|---|---|
| Sample capture respects the row and column bounds | Unit |
| `RowsIn`/`RowsOut` match the executor's actual counts | Unit |
| Palette filter matches on name, category and description | Component |
| Validation flags a missing split, missing target, unreachable node | Unit |
| Undo restores the previous graph exactly | Unit |
| Undo stack is bounded and does not grow without limit | Unit |
| `RunSession` survives a navigation | Component |

## Risks

| Risk | Mitigation |
|---|---|
| Samples make the API response large | Bound hard; consider omitting samples over HTTP and keeping them desktop-only if payloads bite |
| Live validation on every keystroke costs performance | Debounce; validate the graph, not the property values, on edit |
| Undo snapshots of a large graph cost memory | Definitions are JSON and small; cap at 50 and measure |
| A shared change regresses the Web | The Web's component tests run in the same suite; this is why they exist |
