# Phase 21 — Complete node property panel (every parameter, every node)

**Date:** 2026-07-26
**Status:** 🟢 Phase 21a DONE — model-node hyperparameters declared + anti-drift guard shipped; 21b/21c pending.
**Scope:** the Studio designer property panel — expose **every** settable parameter of **every** node, with sensible defaults shown on click.

---

## The problem

The designer's property inspector (`WorkflowDesigner.razor:104-140`) is **descriptor-driven**: it renders exactly each node's declared `Parameters` (Dataset → picker, Select → dropdown, else → text field). That is complete *for what's declared* — but the **model nodes read hyperparameters the executor honours yet never declared**, so those knobs are **invisible** in the UI:

- **`train`** declares only `algorithm`, but the executor reads **`trees` (100)**, **`leaves` (20)**, **`learningRate` (0.2)**, **`l2`** from config (`MlModelOps.cs:54-57`). Users can pick the algorithm but **cannot tune it** in the designer.
- **`cross-validate`** declares only `folds`; it reads `algorithm` + the same hyperparameters → all hidden.
- The `algorithm` option list `[sdca, lbfgs, fasttree, fastforest]` **omits** `perceptron` (binary `AveragedPerceptron`) and `naivebayes` (multiclass) which the trainer factories actually accept.

Net effect: the "tune your model to differentiate" story is limited — hyperparameter tuning is only reachable by editing config JSON / the API, not the UI.

## Why they're hidden — root cause (this is a defect, not a design choice)

The hyperparameters aren't hidden on purpose. They are the result of **descriptor ↔ executor drift**:

- The **descriptor** (`NodeDescriptor.Parameters`) is the single source of truth for three things at once — the UI property panel, the API node catalog (`GET /api/v1/ml/nodes`), and parameter validation.
- The **executor** reads model settings straight from the node's raw `Config` dictionary — `MlModelOps.cs`: `HpInt(node,"trees")`, `HpInt(node,"leaves")`, `ReadDouble(Cfg(node,"learningRate"))`, `HpFloat(node,"l2")`, `Algo(node)` — **without** the matching `P("trees", …)` entries ever being added to the descriptor.

So the engine grew tunable knobs while the descriptor stayed frozen at `algorithm`/`folds`. **Nothing checks that the two agree**, so those keys became silent, undeclared config — reachable only by hand-editing JSON or the API, invisible in the UI.

**Why "hidden + fixed" is wrong.** Because they're undeclared, the panel can't render them, so every user is locked to the hardcoded defaults (`trees=100`, `leaves=20`, `learningRate=0.2`). A real capability that exists in the engine is **unreachable** — dead functionality — and it removes the single biggest source of competition differentiation (model tuning). Hidden ⇒ fixed ⇒ nobody can tune ⇒ everyone's model converges to the same result. That is a **bug to fix**, not intended behaviour.

**Prevent recurrence — the real fix.** Adding four fields once isn't enough; the fix is making the descriptor the *enforced* single source of truth so this drift can't happen again. A test walks every handler and **fails if it reads a `Config` key (`Cfg`/`HpInt`/`HpFloat`/`Algo`) its descriptor doesn't declare.** Then any future knob added to the executor must also be declared — so it can never be silently hidden. This guard is a **Phase 21a deliverable**, not optional.

## The goal

On clicking any node, the property panel shows **all** its parameters, each with its default filled in and a clear label/type. Nothing the executor reads is hidden. This is a **descriptor completeness** task — the inspector already renders whatever the descriptor declares and the executor already reads the config keys, so the fix is to make the descriptors the complete, single source of truth.

## Per-node documentation

Every node kind is documented in its own file under [`node-properties/`](node-properties/) — one file per node, each listing the node's purpose, its full parameter set (`key`, label, type, default, required, options/range) and whether each is exposed in the UI today. See the index below.

### Index (37 node kinds)

| Category | Nodes |
|---|---|
| Source | [`dataset`](node-properties/dataset.md) |
| Split | [`split`](node-properties/split.md) |
| Model | [`train`](node-properties/train.md) ✅, [`cluster`](node-properties/cluster.md), [`cross-validate`](node-properties/cross-validate.md) ✅ |
| Evaluate | [`score`](node-properties/score.md), [`evaluate`](node-properties/evaluate.md) |
| Prepare | [`rename-column`](node-properties/rename-column.md), [`convert-numeric`](node-properties/convert-numeric.md), [`compute-column`](node-properties/compute-column.md), [`combine-columns`](node-properties/combine-columns.md), [`lp-normalize`](node-properties/lp-normalize.md), [`global-contrast`](node-properties/global-contrast.md) |
| Shape | [`take-rows`](node-properties/take-rows.md), [`shuffle`](node-properties/shuffle.md), [`sample`](node-properties/sample.md), [`filter-rows`](node-properties/filter-rows.md) |
| Transform | [`select-columns`](node-properties/select-columns.md), [`drop-columns`](node-properties/drop-columns.md), [`standardize`](node-properties/standardize.md), [`normalize`](node-properties/normalize.md), [`log-normalize`](node-properties/log-normalize.md), [`robust-scale`](node-properties/robust-scale.md), [`binning`](node-properties/binning.md), [`replace-missing`](node-properties/replace-missing.md), [`one-hot`](node-properties/one-hot.md), [`hash-encode`](node-properties/hash-encode.md), [`featurize-text`](node-properties/featurize-text.md), [`pca`](node-properties/pca.md), [`feature-selection`](node-properties/feature-selection.md) |
| Data / DuckDB | [`sql`](node-properties/sql.md), [`sql-filter`](node-properties/sql-filter.md), [`group-by`](node-properties/group-by.md), [`sort`](node-properties/sort.md), [`distinct`](node-properties/distinct.md), [`join-dataset`](node-properties/join-dataset.md), [`union-dataset`](node-properties/union-dataset.md) |

✅ = the executor's parameters are now fully declared and rendered (Phase 21a); no hidden knobs remain.

## Design

1. **Add the missing parameters to the descriptors** (`NodeDescriptor.Parameters`) — the inspector renders them automatically and the executor already reads the keys, so no executor change is needed:
   - `train`: add `trees` (Number, 100), `leaves` (Number, 20), `learningRate` (Number, 0.2), `l2` (Number, blank = trainer default).
   - `cross-validate`: add `algorithm` (Select, same options) + the four hyperparameters.
   - Extend the `algorithm` options to include `perceptron` and `naivebayes`.
2. **Algorithm-conditional relevance.** The hyperparameters only apply to some algorithms (`trees`/`leaves`/`learningRate` → tree trainers; `l2` → linear). Two options:
   - **A (ship first):** declare all four flatly with helper text noting which algorithm each affects; the executor already ignores the irrelevant ones. Zero UI-engine change.
   - **B (enhancement):** add optional `NodeParameter.VisibleWhen` (param → value) so the inspector shows a hyperparameter only for the relevant `algorithm`. Requires a small inspector + descriptor change.
3. **Defaults on click.** The inspector already seeds each field from `NodeParameter.Default` (`CfgOr(p)`), so every parameter shows its default when the node is selected — this just needs the descriptors completed.
4. **Keep the node catalog the single source of truth** — the API `GET /api/v1/ml/nodes`, the executor, and the parameter validator all read the descriptor, so completing it fixes the UI, the docs, and validation together.

## Phases

- **Phase 21a — descriptor completion + anti-drift guard (option A). ✅ DONE.**
  1. ✅ Added the hidden hyperparameters (`trees`/`leaves`/`learningRate`/`l2`) + missing algorithm options (`perceptron`, `naivebayes`) to the `train`/`cross-validate` descriptors (`MlModelHandlers.cs`). The UI renders them with **no `.razor` change** (the inspector is descriptor-driven), pre-filled with the defaults.
  2. ✅ Shipped the drift guard (`NodePropertyDriftTests`): scans `MlModelOps` (the shared model-training path) for `Config` reads (`Cfg`/`HpInt`/`HpFloat`/`Algo`) and **fails if any read key is not declared on the `train`/`cross-validate` descriptors.** Any future knob added to the executor must also be declared to keep it green — a hidden knob can't reappear.
  3. ✅ Extended `NodeCatalogTests` to assert the full parameter set + algorithm options for both model nodes.
- **Phase 21b — conditional rendering (option B, optional).** `VisibleWhen` on `NodeParameter`; inspector shows hyperparameters per selected algorithm; number fields get min/step hints.
- **Phase 21c — polish.** Per-parameter help tooltips (from the per-node docs), range hints (clamps: `bins` 2–255, `cluster` 2–20, `folds` 2–10, `pca rank` 1–#features), and a "reset to defaults" action.

## Acceptance criteria

- Selecting any node shows **every** parameter the executor reads, each pre-filled with its default.
- `train`/`cross-validate` expose `algorithm` + `trees`/`leaves`/`learningRate`/`l2`; `algorithm` offers all trainers (`sdca`, `lbfgs`, `fasttree`, `fastforest`, `perceptron`, `naivebayes`).
- A test asserts the descriptor's declared parameters ⊇ the config keys the handler/`MlModelOps` reads (no hidden knobs) for every node.
- No executor behaviour change (it already reads these keys); pure descriptor + UI completeness.
