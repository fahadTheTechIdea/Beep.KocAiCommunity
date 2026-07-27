# Phase 22 — Full node revision: friendly property panels, column pickers, per-algorithm hyperparameters

**Date:** 2026-07-27
**Status:** 🟡 IN PROGRESS — every node re-read from source and documented (37 per-node docs); panel column pickers + dropdown-label display shipped (build-verified); train ML-properties + per-algorithm hyperparameters + node friendliness gaps planned below.
**Goal:** make the Studio designer usable by **non-IT people** — every node exposes **all** its real properties, with the right control for each (column pickers, dropdowns that show friendly labels, numeric fields with ranges), and the Train node exposes **per-algorithm** hyperparameters. Competition runs pre-fill the fixed columns; free runs let the user set everything.

This plan is the result of reading **every** handler's actual `Execute` code (not the descriptors) — see the per-node docs in [`node-properties/`](node-properties/), one file per node (all 37), each with: what it does, its real parameters (exact defaults/clamps from code), column-awareness, gaps, and notes.

---

## What shipped already (build-verified; tests pending — app was running/locked)

- **Column pickers.** `Column` fields render a **dropdown of the dataset's real columns**; `Columns` fields render a **multi-select checklist** of real columns (loaded from the dataset schema via `GetDatasetVersionAsync().Schema`). Falls back to a text box only when no columns are known. (`NodePropertyPanel.razor`, `WorkflowDesigner.LoadColumns`.)
- **Dropdown display fix.** Every `MudSelect` now uses `ToStringFunc`, so the selected value shows its **friendly label** (e.g. "SDCA (linear)", the dataset **name**) instead of the raw key/GUID. Verified against `mudBlazor_Docs/Select.txt` (Value-presentation section).

## The decision (from the user)

- **Option A** — one **Train** node with an **algorithm dropdown**; the visible hyperparameters **change with the selected algorithm** (each algorithm has its own properties).
- **Custom per-node property windows are allowed** where a flat field list can't express the control (e.g. a group-by aggregate builder, a join configurator). The generic `NodePropertyPanel` stays the default; custom editors plug in per node kind.

---

## Phases

### Phase 22a — panel controls (DONE)
Column pickers + dropdown-label display (above). Verify with the Web app **closed** so the ComponentTests can run.

### Phase 22b — Train node shows all ML properties (Target / ID / Task / Features)
Today label, id, and task are **pipeline-level** (`ctx.LabelColumn` / `ctx.IdColumn` / `ctx.Task`) set in the separate "Run pipeline" box — not on the node. Plan:
- Add to the **Train** node (and surface consistently): `targetColumn` (Column picker), `idColumn` (Column picker), `task` (Select: Binary / Multiclass / Regression), optional `featureColumns` (Columns picker, blank = all numeric except target/id).
- **Free mode:** the run reads these from the node; the "Run pipeline" boxes are driven by / merged with the node.
- **Competition mode:** pre-fill `targetColumn` / `idColumn` / `task` from the competition (`CompetitionDto.LabelColumn` / `IdColumn` / `Task`) and **lock** them (the server enforces the competition's anyway).
- The algorithm dropdown must follow `task` (regression hides perceptron/naivebayes, etc.) — already task-filtered via `LookupOption.AppliesTo`, but `task` must be readable on the node.

### Phase 22c — per-algorithm hyperparameters (Option A) + more algorithms
Per [`train.md`](node-properties/train.md) the trainer factory (`MlModelOps.Trainer` / `MulticlassTrainer`) currently reads a **flat** set (`trees`/`leaves`/`learningRate`/`l2`) with help-text hints. Make the panel show the fields **for the selected algorithm** (custom Train editor, or `VisibleWhen` on `NodeParameter`):
- **SDCA:** `l2` ✅, `l1` 🆕, `maxIterations` 🆕.
- **L-BFGS:** `l2` ✅ (default 1), `l1` 🆕, `historySize` 🆕.
- **FastTree** (binary/regression): `trees` ✅, `leaves` ✅, `minLeaf` 🆕 (hardcoded 10 today), `learningRate` ✅, + optional `featureFraction`/`l1`/`l2`/`maxBins` 🆕.
- **FastForest** (binary/regression): `trees` ✅, `leaves` ✅, `minLeaf` 🆕, `featureFraction` 🆕; **no** learningRate.
- **AveragedPerceptron** (binary): `learningRate` 🆕, `numberOfIterations` 🆕, `l2` 🆕 — **nothing is set today**.
- **NaiveBayes** (multiclass): no hyperparameters.
- **New algorithms to add:** LightGBM (binary/reg/multiclass), GAM (binary/reg), SgdCalibrated (binary), **OneVersusAll** (multiclass — lets FastTree/FastForest actually work for multiclass instead of silently falling back to SDCA), Ols / OnlineGradientDescent (regression). Each = new `algo` key + switch arm in `MlModelOps` + `MlAlgorithms.All` tag + its hyperparameter fields.
- **K-Means** (`cluster`): expose `maxIterations`, `initialization` (KMeansYinyang/Random/KMeansPlusPlus).
- Wire the new keys through the **anti-drift guard** (every read key must be a declared field).

### Phase 22d — Transform / Prepare friendliness
From the per-node docs' gap sections:
- **Scalers** (`standardize`, `normalize`, `log-normalize`, `robust-scale`, `binning`): optional `columns` picker (blank = all numeric).
- **`hash-encode`:** expose `bits` (numberOfBits 1–30) — the description promises "fixed width" but it's hidden today.
- **`one-hot`:** column selector + `outputKind` (Indicator/Bag/Key/Binary).
- **`featurize-text`:** n-gram length, word/char grams, stopword removal, casing — currently all hidden.
- **`replace-missing`:** add **Median** mode; optional column selector.
- **`pca`:** note `rank` upper bound is data-dependent (numeric feature count).
- **`filter-rows`:** `column` → numeric-column dropdown (done via column picker); keep min/max optional.
- **`compute-column`:** inputs picker excludes label/id (hard leakage guard already fails otherwise) and preserves order.

### Phase 22e — Data / DuckDB friendliness (custom editors)
- **`join-dataset`:** join-type dropdown (Inner/Left/Right/Full — LEFT-only today); `on` → single column picker over the intersection of both schemas (free text today); optional collision prefix/suffix.
- **`group-by`:** repeatable **aggregate builder** (function COUNT/SUM/AVG/MIN/MAX/MEDIAN/STDDEV + column + output name) generating the SQL; raw SQL kept as "advanced".
- **`sort`:** repeatable sort keys (column + ASC/DESC) instead of raw `ORDER BY` text.
- **`sql-filter`:** visual condition builder (column + operator + value, AND/OR); raw predicate as "advanced".
- **`sql`:** schema panel + validate/preview.
- **`union-dataset`:** append-all vs distinct; column-mapping preview so NULL-fills are visible.

---

## Acceptance criteria
- Selecting any node shows **every** real property with the right control; nothing the executor reads is hidden (anti-drift guard stays green).
- Column/Columns fields are pickers of the dataset's real columns; every dropdown shows friendly labels.
- Train shows target/id/task (+ per-algorithm hyperparameters); competition pre-fills & locks the fixed columns, free mode is fully editable.
- New algorithms are real (backed by an `MlModelOps` arm), not fake dropdown entries.
- The golden pipeline + submission suites stay green; ComponentTests cover the panel controls.

## Per-node docs index
All 37 kinds documented in [`node-properties/`](node-properties/): Source (`dataset`); Split (`split`); Model (`train`, `cluster`, `cross-validate`); Evaluate (`score`, `evaluate`); Prepare (`rename-column`, `convert-numeric`, `compute-column`, `combine-columns`, `lp-normalize`, `global-contrast`); Shape (`take-rows`, `shuffle`, `sample`, `filter-rows`); Transform (`select-columns`, `drop-columns`, `standardize`, `normalize`, `log-normalize`, `robust-scale`, `binning`, `replace-missing`, `one-hot`, `hash-encode`, `featurize-text`, `pca`, `feature-selection`); Data (`sql`, `sql-filter`, `group-by`, `sort`, `distinct`, `join-dataset`, `union-dataset`).
