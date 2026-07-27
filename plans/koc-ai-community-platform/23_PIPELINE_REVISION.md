# Phase 23 — Whole-pipeline revision: one source of truth, node-driven execution

**Date:** 2026-07-27
**Status:** 🟢 23a + 23b DONE — the node graph is now the single source of truth for the control facts (the executor reads target/id/task from the Train node; a passed value is an override; the primary Dataset node no longer collides with the secondary scanner). 23c–f pending. Full end-to-end re-read below.
**Why:** Phase 22 moved the data source onto the **Dataset** node (`datasetId`) and the target/id/task onto the **Train** node. The audit confirms the **server never reads those node fields** — label/id/task/data still enter through method parameters — so the graph and the run parameters are now two independent stores of the same facts, kept in sync only by the designer's `EffectiveLabel()`/`EffectiveTask()` helpers. That's the exact "field shown but not honoured" defect we fixed for hyperparameters, re-created at the pipeline level. This phase makes **the node graph the single source of truth.**

---

## Current end-to-end data flow (as-is)

```
Designer (WorkflowDesigner.razor)
  BuildDefinition() → WorkflowDefinition { Nodes[Kind,Config(+_x/_y)], Edges }
  data source   = Dataset node Config["datasetId"]      (DatasetNodeId())
  target/task   = Train node Config["targetColumn"/"task"] (EffectiveLabel/EffectiveTask)
  id            = Train node Config["idColumn"]  ← WRITTEN, never sent anywhere
        │
        ├── Run (free)  → ExecuteWorkflowFromDatasetAsync(def, datasetId, labelColumn, task)   ← params, not the graph
        ├── Upload      → ExecuteWorkflowAsync(def, csv, labelColumn, task)
        └── Submit      → SubmitPipelineAsync(competitionId, def)                               ← def only
        │
Server
  StudioEndpoints  → IPipelineExecutor.ExecuteAsync(def, labelColumn, task, csv, …)             ← params
  CompetitionService.SubmitPipelineAsync → PredictAsync(def, competition.LabelColumn,
                                            competition.IdColumn, task, trainCsv, evalCsv)       ← competition entity
        │
  PluginNodeExecutor.Run/Predict
    PipelineContext { LabelColumn=param, IdColumn=param, Task=param, Mode }                      ← NODE CONFIG IGNORED
    table = PipelineTable.FromCsvFile(streamPath)   ← data is the injected stream, NOT datasetId
    handlers read ctx.LabelColumn / ctx.Task / ctx.IdColumn only
        │
  Scoring (submit only): ScorePublicPrivateAsync → public + private (FNV-1a holdout), leaderboard
```

**Invariants that are correct and must be preserved:** topological compile + single-chain enforcement (`WorkflowCompiler`), split-before-fit / label-integrity / id-survival / id-uniqueness guards, transform replay onto the eval set, RFC-4180 `KocCsv` everywhere, competition authoritatively overriding label/id/task at submit (anti-cheat), public/private FNV-1a holdout, single scorer for board + rescore.

---

## Findings (prioritised)

### 🔴 P1 — Source-of-truth conflict: node Config for target/id/task/datasetId is ignored server-side
`TrainHandler`/`DatasetHandler` never read `targetColumn`/`idColumn`/`task`/`datasetId`; the executor uses only the `ExecuteAsync`/`PredictAsync` parameters (`PipelineContext` init from params). The designer's `EffectiveLabel()`/`EffectiveTask()` transcribe the Train-node values into the call, so the *designer* stays consistent — but:
- The **saved `WorkflowDefinition` alone does not drive execution.** Any non-designer caller (a scheduled/durable run, a re-run from the registry, a job) must re-supply label/task or it silently uses defaults (`"label"`, `BinaryClassification`).
- **`idColumn` on the Train node is pure decoration** — no free-run path has an id parameter at all; only competition submit uses an id, and it uses `competition.IdColumn`.
- Two stores of the same fact, reconciled only by client helpers = drift waiting to happen.

### 🔴 P2 — Dataset-as-node collides with the secondary-dataset machinery (regression from Phase 22)
`WorkflowDatasetScanner.ReferencedDatasetIds` scans **every** node's `Config["datasetId"]` (intended for join/union), so the new **Dataset node's** `datasetId` is now pulled into `secondaryDatasets` and loaded into DuckDB as an unused `ds_n` table, **and** `ValidateNodeInputs` treats it as a `Dataset` param that must resolve — so:
- Free-run: the primary dataset is **double-loaded** (once as the CSV stream, once as an unused secondary).
- Competition submit passes **no** secondaries (see P3); if a Dataset node ever carries a `datasetId` there, `ValidateNodeInputs` would **throw**. (Today `ApplyCompetitionDefaults` leaves it blank, so it's latent — but fragile.)

### 🔴 P3 — Competition submit doesn't pass secondary datasets
`SubmitPipelineAsync` → `PredictAsync(...)` with **no `secondaryDatasets`**. Any pipeline containing a `join-dataset`/`union-dataset` that references a second dataset **runs in Studio but always fails on submit** (`ValidateNodeInputs` throws "dataset … could not be loaded"). A user can build a passing pipeline that can never be submitted.

### 🟠 P4 — `idColumn` inconsistent between Execute and Predict
`Execute`/`Run` has no `idColumn` (null), so `FeatureNames` treats the id column as a **feature** during preview/run; `Predict` sets `IdColumn` and excludes it. Preview metrics can differ from the scored submission because the feature set differs.

### 🟠 P5 — No client-side graph validation
The only gate is "a Dataset node with a parseable `datasetId` exists." No cycle/connectivity/"has a Train node"/required-params check before Run/Submit — malformed graphs only fail once the server compiles/executes them. Poor UX for non-IT users.

### 🟡 P6 — Submit robustness
- **Quota race:** `EnsureSubmittableAsync` counts-then-inserts with no transaction/unique constraint → two concurrent submits can exceed the daily quota.
- **No hard time budget on `PredictAsync`** (only `ExecuteAsync` has `maxSeconds`, and it isn't enforced as a cancellation) → a heavy graph ties up a request thread; a failing pipeline consumes no quota → unlimited expensive retries.
- **Tiny-key holdout degrades** to "whole key on both boards" for `<2` key rows (public == private). Fine for toy competitions; document it.
- **Direct-upload submissions** have no per-submission id-coverage check.

---

## The decision: make the node graph the single source of truth

Reconcile P1/P2 by having the **executor read control facts from the graph**, with the competition still overriding at submit (anti-cheat). Net contract:

- **Target/label, id, task** come from the **Train node** (`targetColumn`/`idColumn`/`task`). The execution entry points accept them as **optional overrides**; when absent, the executor reads them from the Train node. Competition submit passes the competition's values as the override (unchanged behaviour, now explicit).
- **The Dataset node's `datasetId`** is the **primary** input for a dataset-run; it is resolved to the training stream by the endpoint and **excluded** from the secondary-dataset scan. Only `join-dataset`/`union-dataset` datasetIds are secondaries.
- The redundant request parameters (`labelColumn`/`task` on execute, `datasetId` duplicated in `ExecuteFromDatasetRequest`) become derived-from-graph (kept only as explicit overrides where a non-graph caller needs them).

This removes the drift, makes the saved definition self-describing, and kills the "decorative field" smell — the same principle as the anti-drift guard, applied to pipeline-level facts.

---

## Phased plan

### Phase 23a — Fix the Dataset-node regression (P2) — smallest, do first
- Exclude the **primary `dataset` node** from `WorkflowDatasetScanner.ReferencedDatasetIds` (only scan `join-dataset`/`union-dataset`).
- Make `ValidateNodeInputs` skip the `dataset` node's `datasetId` (it's the primary input, resolved by the endpoint, not a secondary).
- Result: no double-load, no latent throw. Add a golden test: a graph with a Dataset-node `datasetId` runs without loading a spurious secondary.

### Phase 23b — Node graph is the source of truth for target/id/task (P1)
- Add optional reads in the executor: if the entry point's label/id/task are null/blank, read them from the Train node Config (`targetColumn`/`idColumn`/`task`). Keep explicit params as overrides (competition passes its own).
- Free-run endpoints derive label/id/task from the graph when not supplied; competition submit keeps passing the competition's (authoritative).
- Update the designer to stop sending redundant params (or send them only as the graph mirror). Remove the now-dead `idColumn` decoration OR wire it through the free-run path.
- Extend the anti-drift guard spirit: a test asserting the Train node's declared `targetColumn`/`task` are actually consumed by the execution path (no decorative pipeline fields).

### Phase 23c — Competition submit passes secondary datasets (P3)
- `SubmitPipelineAsync` resolves the pipeline's referenced secondary datasets (competition-scoped / participant-visible) and passes them to `PredictAsync`; OR explicitly reject join/union-with-external-data at submit with a clear message. Decide policy (likely: allow only datasets the participant may use; document).
- Golden/integration test: a join-dataset pipeline submits and scores (or is rejected with a clear reason).

### Phase 23d — Execute/Predict feature-set parity (P4)
- Give `ExecuteAsync` an `idColumn` (optional) so preview excludes the id from features exactly like predict; or document that preview always excludes a configured id column. Ensure `FeatureNames` is identical across modes for the same graph.

### Phase 23e — Client-side graph validation (P5)
- Before Run/Submit, validate in the designer: DAG has a Dataset node with data, has a Train node, no orphan/dangling required inputs, single chain (mirror `WorkflowCompiler` rules) — surface inline errors instead of a server round-trip. Keep the server compile as the authority.

### Phase 23f — Submit robustness (P6)
- Quota: reserve via a transaction / unique `(competition,user,day,seq)` or optimistic retry so concurrent submits can't exceed the cap.
- Time budget: thread a real `CancellationToken`/deadline into `PredictAsync`; fail fast on runaway graphs.
- Document tiny-key holdout degradation; optionally require a minimum key size for a true hidden split.
- Direct-upload: id-coverage check vs the answer key.

---

## Acceptance criteria
- Executing a **saved `WorkflowDefinition` alone** (no side parameters) reproduces exactly what the designer shows — target/id/task/data all come from the nodes.
- No decorative pipeline fields: every node field the designer shows is consumed by the executor (guarded by a test).
- A Dataset-node `datasetId` does not create a spurious secondary or throw; join/union secondaries still work — including on competition submit.
- Preview (Execute) and scored (Predict) use the same feature set for the same graph.
- Competition submit remains anti-cheat (host label/id/task authoritative) and quota-safe.
- Full gate stays green (build -warnaserror, format, all five test suites).

## Ordering & risk
23a (contained bug-fix, do immediately) → 23b (the core reconciliation, medium, touches executor + endpoints + designer) → 23c (submit secondaries, medium) → 23d (parity, small) → 23e (client validation, small/medium, UI-only) → 23f (robustness, medium, service-layer). Each phase ships behind the green gate with its own tests; 23a/23b are the priority because they remove the source-of-truth conflict this phase exists to fix.
