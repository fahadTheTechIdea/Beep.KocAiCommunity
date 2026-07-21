# 19c — Local execution engine (Stage 4)

Make the designer **work fully offline**: build and run pipelines with no web server
by binding `IKocApiClient` to a local, in-process implementation. This is the stage
that delivers the offline-first guarantee. See `19_WINFORMS_DESKTOP_STUDIO.md`.

## Goal

`Beep.KocAiCommunity.Desktop.Local` provides `LocalKocApiClient : IKocApiClient` that
serves the Studio surface from in-process engine libraries and local files; the
WinForms host binds to it so the reused `WorkflowDesigner` runs offline unchanged.

## Work

- New project `src/Beep.KocAiCommunity.Desktop.Local` (`net10.0`), referencing
  **ML** (`PluginNodeRegistry`, `PluginNodeExecutor`, `IPipelineNodeHandler`s),
  **Workflow** (compiler), **Application** (`INodeRegistry`/`IPipelineExecutor`
  abstractions + `WorkflowDefinition`), **Client**, **Contracts**.
- `LocalKocApiClient : IKocApiClient` implements the methods the designer uses:
  - **Node catalog** `GetMlNodesAsync` / `GetMlNodeAsync` ← `PluginNodeRegistry.All`
    mapped to `NodeDescriptorDto` (same mapping the API's `MlNodeEndpoints` uses).
  - **Datasets** `GetDatasetsAsync` ← a **local dataset folder** (user-added CSVs):
    each file → a `DatasetDto` (`HasFile = true`, name = file name, a synthesized id
    that maps back to the path). A "Add local CSV…" action copies/points a file into
    the workspace.
  - **Run** `ExecuteWorkflowOnDatasetAsync` (and the upload variant) ← open the local
    CSV as a stream and call `PluginNodeExecutor.ExecuteAsync(definition, stream, …)`
    in-process, returning the same `PipelineExecutionResult` DTO the designer renders.
  - **Workflow registry** create/list/detail/versions/save-draft/publish ← a **local
    JSON store** (a workspace folder of `{guid}.json` envelopes: definition + version
    metadata). "Publish" freezes a copy; enough to power the designer's Save/Publish/
    Open flow offline.
  - **Competition methods** → delegate to an inner HTTP `KocApiClient` (Stage 5).
  - Everything else → `throw new NotSupportedException(...)` (desktop shows only the
    Studio + competition screens).
- Register a DI helper `AddKocLocalStudio(this IServiceCollection, LocalWorkspace
  options)` wiring: all `IPipelineNodeHandler`s + `PluginNodeRegistry` +
  `PluginNodeExecutor` (mirroring `Infrastructure/DependencyInjection.cs`), the local
  dataset/workflow stores, and `IKocApiClient → LocalKocApiClient`.
- WinForms host: swap `AddKocHttpClient(...)` for `AddKocLocalStudio(...)` (the inner
  HTTP client for competitions is still configured with the API base URL).

## Local workspace layout

```
%LOCALAPPDATA%/KocStudio/
  datasets/   ← user CSVs (GetDatasetsAsync enumerates these)
  workflows/  ← {guid}.json  (definition + versions; the local "registry")
  temp/       ← PipelineTemp scratch (koc-pipe-*), swept on startup
```

## DoD / verification

- Build `-warnaserror`; format clean.
- **Unit tests** (`Desktop.Local` in the existing test project or a new one, `net10.0`):
  - `GetMlNodesAsync` returns the full catalog including the DuckDB nodes.
  - A round-trip: create a workflow, save a draft, reload it, list versions.
  - A run: given a small CSV + a `dataset→split→train→evaluate` definition,
    `ExecuteWorkflowOnDatasetAsync` returns a successful `PipelineExecutionResult`
    with a metric — **no HTTP, no server**.
- **Manual (the guarantee):** with the network **disconnected**, launch the WinForms
  app, add a local CSV, build and Run a pipeline → node-by-node results + a trained
  metric. Commit.

## Notes

- Reuses the engine libraries verbatim — no fork of executor/registry logic.
- DuckDB + ML.NET run natively in-process on Windows; ensure the `win-x64` native
  assets resolve when run from the built app (fully validated in Stage 6 publish).
