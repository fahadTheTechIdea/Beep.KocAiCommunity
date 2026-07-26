# Phase 20 — Pipeline & Scoring End-to-End Audit + Remediation

**Date:** 2026-07-26
**Scope:** every node → workflow → executor → scoring path — how data moves, whether it
follows standards, and whether scoring is consistent and correct.
**Status:** 🟡 IN PROGRESS — audit complete; contained fixes shipped; larger items sequenced below.

This document is the durable record of the audit. It supersedes the scattered "remaining
follow-ups" note in tracker item 37 and defines the remediation program (tracker item 38).

---

## How data moves (the two data planes)

```
AUTHORING            EXECUTION                                      SCORING
WorkflowDefinition ─┬─► PluginNodeExecutor (graph)  ─► id,prediction CSV ─► IScoringPlugin ─► LeaderboardEntry
 (compiler+guards)  └─► AutoMlTrainer   (AutoML)     ─► model.zip + metrics   (accuracy / rmse)
```

- **Uniform contract:** every node speaks `PipelineTable` (physically a header CSV). DuckDB and
  ML.NET nodes read/write the same CSV, so they interleave freely.
- **Two engines run the SAME `WorkflowDefinition`:** `IPipelineExecutor`/`PluginNodeExecutor` runs
  the real graph; `IMlTrainer`/`AutoMlTrainer` runs AutoML and ignores the graph. Competition
  scoring and `/studio/workflows/execute` use the graph; **`/studio/workflows/run`, `workflow.run`,
  `experiment.train`, `model.train` all substitute AutoML** (`WorkflowService.RunAsync:24` compiles
  the graph only to validate, then trains AutoML with a hardcoded `BinaryClassification`).

---

## Findings (ranked)

### Tier 1 — silent wrong results
- **T1 — two engines, graph ignored on run/train.** A participant "runs" their published workflow,
  sees AutoML metrics, submits, and the leaderboard score comes from the *graph* — a different
  computation. `WorkflowVersion.DefinitionJson` is read only for `Status`. *(design change; open)*
- **T2 — numeric id corruption across the typeless-CSV crossing.** `PipelineTable` carried names but
  no types; every load re-inferred, so a zero-padded/large id round-tripped `00123 → 123` and broke
  the id-aligned answer-key join → score collapses to ~0. *(FIXED, phase E — id pinned to text on
  both crossings; root-cause fix H2 pending, see below)*
- **T3 — branching graphs mis-thread.** The executor threads one `table` in topo order; the compiler
  never rejects fan-in/fan-out, so a diamond/branch feeds the wrong upstream table into a node.
  *(open — compiler guard)*
- **T4 — union-after-split silently dropped rows.** Appended rows had `NULL __fold` and were filtered
  out of both train and test. *(FIXED, phase E — appended rows get `__fold = 0`)*

### Tier 2 — scoring integrity
- **S1 — "concealed final" board is cosmetic.** `CompetitionEndpoints.cs:190` returns the same live
  data for every `board` value; only literal `board=final` is gated by `RevealUtc`. No public/private
  holdout exists — scorers score the whole key, so live == final. *(open)*
- **S2 — answer-key swap doesn't rescore, no lifecycle guard.** `SetAnswerKeyAsync` replaces the key at
  any status but leaves frozen `Submission`/`LeaderboardEntry` scores. *(open)*
- **S3 — accuracy boolean folding collides for coded multiclass.** `t/f/y/n/1/0` fold together, so a
  multiclass class literally named `T`/`F`/`Y`/`N` can match a wrong prediction. *(open — scope folding
  to binary tasks)*
- **S4 — accuracy vs RMSE diverge on edge cases.** RMSE returns `0.0` (best) on a degenerate key while
  accuracy's `0.0` is worst; RMSE self-skips a phantom header row while accuracy counts it; `CompetitionCsv`
  reads id/value by position while validation reads by header name. *(open — unify)*
- **S5 — quota + leaderboard concurrency-unsafe.** TOCTOU on the daily-quota count; `RowVersion` exists
  but isn't configured as a concurrency token. *(open)*

### Tier 3 — standards / hygiene (mostly FIXED)
- **M5/M6/M7 — three naive-CSV sites** (`AutoMlPredictionPool`, `AutoMlTrainer.ComputeFeatureStats`,
  `CsvProfiler`) bypassed the `KocCsv` mandate. *(FIXED, phase E — all route through `KocCsv`)*
- **H3 — DateTime column crashed `MlCsv`.** *(FIXED, phase E — ISO-8601 branch)*
- **L10 — FastTree/FastForest non-deterministic** (multi-threaded FP reductions). *(FIXED — pinned
  `NumberOfThreads = 1` via `.Options`)*
- **H2 — typeless `PipelineTable`** is the root cause of T2 and precision/date drift. *(PATCH READY,
  see below)*
- **Minor (open/low):** provenance hash is order-sensitive (`WorkflowSerializer`); `__fold` name
  collision unguarded; compiler "needs a model node" check is case-sensitive for `cluster`.

### What was already solid
`KocCsv` RFC-4180 codec; id↔prediction positional alignment + count guard; `FeaturizationGuard` graph
BFS; `__fold` dropped-marker leakage guard; deterministic leaderboard tie-break + `HigherIsBetter`;
temp-file cleanup on success and exception (no leaks).

---

## Remediation status

| Item | Status | Commit |
|---|---|---|
| Phase C — predict id-alignment + fold-leakage guard | ✅ | `896506e` |
| Phase D — determinism + node validation + label-token echo | ✅ | `ddd3dd4` |
| Phase E — id/type text-pinning (T2/H3/T4) + RFC-4180 (M5/M6/M7) | ✅ | `151b3aa` |
| L10 — FastTree/FastForest thread-pinning | ✅ | `82025c2` |
| H2 — schema-carrying `PipelineTable` (root-cause of T2) | ✅ | `4b0d9f5` |
| T3 — compiler branch guard (reject fan-in/fan-out) | ✅ | `c167586` |
| S3 — boolean-folding scoped to binary keys | ✅ | `e0c705c` |
| S4 — accuracy/RMSE parity (name-based id, degenerate-key reject) | ✅ | `e0c705c` |
| S2 — answer-key rescore + concluded-key lock | ✅ | `6a7d788` |
| Fan-out join duplicate-id guard (ratified) | ✅ | `0eff2e5` |
| S5 — leaderboard optimistic-concurrency token + retry | ✅ | `6f68df2` |
| Data-class contract guards (label/id integrity, target leakage, fold integrity, union label, filter typo) | ✅ | `af6fe5f` |
| AutoML must not train on the id as a feature | ✅ | `5f53118` |
| T1 path A — interactive run executes the node graph (was AutoML) | ✅ | `21be420` |
| **T1 path B — durable `workflow.run` job** | ⏸ BLOCKED (see note) — stays on AutoML | — |
| S1 — real public/private holdout (deterministic split + final board + dual-provider migration) | ✅ | `00f33e0` |
| S5 residual — quota TOCTOU | ⬜ open (needs an atomic per-user/day counter + migration) | — |
| ColumnRoles refactor (X/y/id/fold typed roles) | ⏸ DISCARDED (reverted `39f5026`) — parked | `scratchpad/columnroles-*` |

**T1 path B blocker:** the `workflow.run` job produces a *registerable, inference-ready* model
(`CapturedModel` = a self-contained ML.NET `.zip` → `ModelRunRecorder` → inference pool). The node graph
cannot produce one: its `ctx.Model` is only the `train` node's fitted pipeline, while preprocessing is
applied as separate replay steps — and some of those are **DuckDB SQL**, which cannot be serialized into
an ML.NET `.zip`. So any graph using a DuckDB node can't be frozen into a single servable artifact.
Path B therefore stays on AutoML (which does produce a servable model); T1's user-facing goal — the
*interactive* run matching the graph (and competition-submit) — is delivered at path A. The proper
long-term fix is to **serve inference by re-running the graph's `PredictAsync`** (store the definition as
the "model", no `.zip`), which is a rework of the whole inference path (`AutoMlPredictionPool` /
`IInferenceService` / registry), tracked as future work — not a job-handler swap.

**H2 patch note:** a background remediation agent authored a coherent schema-carrying `PipelineTable`
(`PipelineColumnType` enum; `Types` carried across both crossings; DuckDB `read_csv_auto(types=…)` +
`ColumnTypes`; `MlCsv.DescribeColumns`). It was set aside (not committed) because it was unrequested,
unvalidated, and arrived via an uncontrolled process. It is the *proper* fix that would let the phase-E
text-pinning band-aid be removed. Requires a full test pass before adoption.

---

## How to prove correctness (assurance layer)

Add a **pipeline/competition correctness suite**, gated in CI alongside the global DoD:
- **Golden end-to-end fixtures:** zero-padded ids, large-magnitude ids, quoted comma/newline fields,
  `1/0` vs `true/false` labels, dates → assert the submission joins 1:1 to the key and scores as expected.
- **Round-trip invariant:** a no-op transform pipeline reproduces the id column byte-for-byte.
- **Engine-equivalence (post-T1):** the same workflow via "run" and via "submit" must agree.
- **Linearity property (until T3):** any compiler-valid graph the executor accepts is actually linear.
- **Determinism:** re-running any seeded pipeline yields identical metrics (L10 test is the first).

---

## Recommended order

`H2 (decision) → T3 → S1–S5 → T1`. T3 is a contained compiler guard; S1–S5 are several small scoring
fixes; T1 is the largest (design change to route run/train through the graph).
