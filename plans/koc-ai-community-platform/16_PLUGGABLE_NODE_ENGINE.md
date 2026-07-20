# Phase 16 — Pluggable node engine (foundation for DuckDB)

Part of the DuckDB-integration initiative (see `../peppy-strolling-yao.md` for the full plan).
This phase replaces the monolithic ML pipeline executor with a plug-and-play node engine, so
adding a node kind — including the coming DuckDB/SQL nodes — is one handler class.

## Why

Adding a node used to touch ~4 places (backend catalog, a separate hardcoded Web catalog, the
executor's `switch`, and the compiler's static known-kinds). The executor was one 760-line
`switch` over ~32 node kinds. That is not extensible enough to add a whole second engine (DuckDB).

## What changed

- **`IPipelineNodeHandler`** (`src/Beep.KocAiCommunity.ML/Nodes/PipelineContext.cs`) — one handler
  per node kind. Each owns its `NodeDescriptor` (the single source of truth for the catalog) and an
  `Engine` (`Ml` today; `Duck` in Phase 17) and mutates a shared `PipelineContext`.
- **`PipelineContext`** — the state threaded through a run: the ML.NET working view, feature
  columns, preprocessors, model, label map, split info, mode (Execute vs Predict), and the shared
  numeric/text helpers moved verbatim from the old executor. Reserved fields for the DuckDB engine
  (`SecondaryTables`) are present but unused until Phase 17.
- **Handlers** (`Nodes/MlTransformHandlers.cs`, `MlPrepareHandlers.cs`, `MlModelHandlers.cs`) — all
  ~30 existing ML nodes migrated behaviour-preserving; bodies are ports of the old `switch` cases.
- **`PluginNodeRegistry`** implements the existing `Application.ML.INodeRegistry` from the handler
  descriptors (catalog + parameter validation), and exposes `Handler(kind)` + `Kinds`.
- **`PluginNodeExecutor`** implements `IPipelineExecutor` by dispatching each node (topo order) to
  its handler over the context — replacing `MlPipelineExecutor` (deleted).
- **DI** (`Infrastructure/DependencyInjection.cs`) auto-registers every `IPipelineNodeHandler` in
  the ML assembly, so a new handler needs no wiring.
- The old `MlNodeRegistry` (Application) and `MlPipelineExecutor` (ML) are removed.

## Deviations from the plan (deliberate, low-risk)

- `WorkflowCompiler.KnownKinds` stays a static set rather than being injected from the registry
  (to avoid threading the registry through 5 `Compile` call sites). A unit test
  (`NodeCatalogTests.Registry_and_compiler_known_kinds_match_exactly`) asserts the two are
  identical, so drift is caught at build/test time.
- Retiring the hardcoded Web `Diagrams/NodeCatalog.cs` (making the designer API-driven) is deferred
  to just before Phase 18, so new DuckDB nodes only need a backend definition. The designer still
  renders correctly because both catalogs remain in sync for the current node set.

## Done (2026-07-20)

- ✅ Parity: the existing executor test suite (`MlPipelineExecutorTests`, 18 unit scenarios incl.
  binary/multiclass execute + predict + expanded catalog + data-management nodes) retargeted to
  `PluginNodeExecutor` — all green. Integration tests (Studio execute + competition submit-pipeline)
  exercise it through DI — all green.
- ✅ `dotnet build -warnaserror`, `dotnet format --verify-no-changes`, full `dotnet test`
  (125 unit + 100 integration + 12 others).

## Next

- Phase 17 — DuckDB engine core + secondary-dataset plumbing (`17_DUCKDB_ENGINE.md`).
- Phase 18 — DuckDB node handlers + Dataset picker (`18_DUCKDB_NODES.md`).
