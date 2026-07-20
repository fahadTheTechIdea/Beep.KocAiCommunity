# Phase 17 — DuckDB engine core + engine crossing

Part of the DuckDB-integration initiative (`../peppy-strolling-yao.md`). Adds DuckDB as an **additive
data-prep engine** alongside the ML.NET modelling engine — DuckDB does the SQL/ETL, ML.NET does the
training. It does **not** replace any ML.NET node.

## Architecture

DuckDB nodes are the **data-prep front-end**; ML.NET nodes do the modelling. In one graph:

```
dataset ─▶ [DuckDB: sql / group-by / filter / sort] ─▶ split ─▶ train ─▶ evaluate
           └────────── SQL data prep (Duck) ─────────┘        └──── ML.NET (unchanged) ────┘
```

- **`DuckDbSession`** — a short-lived in-memory DuckDB connection: `Execute/Scalar`, `LoadCsv`
  (`read_csv_auto`), `ExportCsv` (`COPY … TO`), `CreateTableAs`, `ReplaceTable` (safe in-place
  transform), `Columns`, `RowCount`, with identifier/literal quoting and BigInteger-safe coercion.
- **Engine crossing** (in `PluginNodeExecutor`): each node declares a `NodeEngine` (`Ml`, `Duck`, or
  `Source`). The executor lazily materializes the working data into the representation the next node
  needs, **via a temp CSV** (reusing the existing `InferColumns`/`CreateTextLoader` load path):
  - Pure-ML pipelines (no Duck node) → eager load + split exactly as before (**parity preserved**).
  - Mixed pipelines → the source loads into DuckDB first; Duck nodes transform the `working` table;
    the first ML node triggers `EnsureMl` which exports `working` → CSV → loads into ML.NET and
    performs the train/test split **at the crossing** (so the split sees the prepped data).
  - `EnsureMl` tolerates a dropped label column (unsupervised group-by → cluster).
  - ML→Duck crossing is rejected with a clear error: DuckDB nodes must precede the ML nodes.
- **`PipelineContext`** gains the DuckDB session, the `working`-table state, `Current` location,
  temp-file tracking (disposed with the context), and `SecondaryTables` (for Phase 18 join/union).
- **`IPipelineExecutor`** gains an optional `IReadOnlyDictionary<Guid, Stream> secondaryDatasets`
  (read to bytes, registered as DuckDB tables). Callers resolve these in Phase 18.

## Nodes added (category "Data")

`sql` (arbitrary SELECT over `working`), `sql-filter` (WHERE), `group-by` (GROUP BY + aggregates),
`sort` (ORDER BY), `distinct` (dedupe). Each replaces the `working` table via `ReplaceTable`.

## Done (2026-07-20)

- ✅ 17a: `DuckDB.NET.Data.Full` (MIT, native bundled) + `DuckDbSession` + probe tests (native engine
  loads; CSV round-trip with a SQL filter+derive).
- ✅ 17b: engine crossing + the five Data nodes; end-to-end tests — a `sql` prep → ML train/evaluate
  pipeline and a `group-by`/`sort`/`cluster` chain. Parity suite unchanged (all green). `-warnaserror`,
  format clean, 130 unit + 100 integration tests.

## Next (Phase 18)

- `join-dataset` / `union-dataset` (need secondary-dataset resolution wired into the callers via a
  `WorkflowDatasetScanner`), `pivot`/`unpivot`, `limit`, `summarize`.
- Dataset param type end-to-end + API-driven designer catalog (retire the hardcoded Web catalog).
