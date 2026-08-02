# 02 — Data: import, profiling, and preview

> **Status:** 🟡 Partly shipped 2026-08-02 (import, preview, delete, folder access).
> **Depends on:** 01 for logging and workspace integrity. **Blocks:** 03, 04.

## Context

Before 2026-08-02 the desktop had **no way to add a dataset**. `LocalDatasetStore` listed `*.csv` already
sitting in `%LOCALAPPDATA%\KocStudio\datasets`, so unless you knew that path and copied files there by
hand, the designer opened with an empty picker and nothing explaining why. On a fresh install the
offline Studio was effectively unusable.

That is fixed. What remains is the difference between *accepting* a file and *understanding* it.

### Shipped

- Import one or more CSVs through a file picker
- Preview header and first rows before committing to a pipeline
- Delete, with confirmation
- Open the workspace folder for files too large for the picker
- Name-collision suffixing, name sanitisation, path-traversal guard, ids stable across restart

### Not yet

Everything below.

## Scope

**In**

- Encoding and delimiter detection
- Column profiling — type, nulls, distinct, min/max/mean
- Large-file handling that never reads the whole file into memory
- Dataset rename, and ordering by recent use
- Row-count estimate without a full scan

**Out**

- Editing data in the app. This is a modelling tool, not a spreadsheet — a user who needs to fix rows
  should do it where they already know how, then re-import.
- Non-CSV formats (Parquet, Excel). Worth revisiting once someone asks; the node engine is CSV-oriented
  throughout and this would be a wider change than it looks.
- Datasets from the platform. That is Phase 06 territory and depends on classification enforcement.

## Design

### Encoding and delimiter detection

The current import copies bytes and assumes UTF-8 comma-separated. KOC data will not always be that:
exports from Excel in an Arabic locale are frequently **semicolon-separated**, and older systems produce
**Windows-1256** rather than UTF-8. Both currently produce a dataset that looks imported and then fails
oddly in the designer — one column named after the whole header row, or mojibake column names.

Detect on import, from the first 64 KB:

| Signal | Method |
|---|---|
| BOM | UTF-8, UTF-16 LE/BE from the leading bytes |
| No BOM | Try UTF-8 strict; on decode failure fall back to the system ANSI page |
| Delimiter | Count `,` `;` `\t` `\|` outside quotes across the first 20 lines; the most consistent per-line count wins |

Show what was detected in the import result and let the user override before the file is committed —
detection is a guess and should present itself as one.

> This matters more for Arabic-locale exports than the English-language literature suggests, and it is
> the single most likely first-contact failure for a KOC engineer.

### Profiling

`CsvProfiler` already exists in `Infrastructure/Datasets` and computes exactly what is wanted: row
counts, nulls, distinct values, min/max/mean, inferred type. It is used by the platform's dataset
pipeline.

Reuse it rather than writing a second profiler. `Desktop.Local` would need a reference to
`Infrastructure` — check the dependency direction first; if that is unacceptable, move `CsvProfiler`
down to `Application`, where it has no persistence dependencies anyway.

Profile **on demand**, not on import: a 200 MB file should not block the import button. Profile when the
dataset is first selected, cache the result beside the file as `{name}.profile.json`, and invalidate on
file timestamp.

### Large files

The import path currently does `content.CopyToAsync(file)`, which streams — good. The preview reads
line-by-line — also good. The remaining risks are:

- **Row count.** Counting lines means reading the file. For the card, estimate from
  `fileLength / averageLineLength` sampled over the first 200 lines, and label it as approximate.
  Compute the true count during profiling, when the user has asked for detail.
- **Preview of a file with 500 columns.** Cap the preview at the first 50 columns with a "+N more"
  indicator, or the table renders off-screen and the page becomes unusable.
- **A single-line 200 MB file.** `ReadLineAsync` will allocate the lot. Cap the read at 1 MB per line
  and report the file as malformed rather than exhausting memory.

### Rename and recency

Rename is a file rename plus an index update, both inside the store's lock. The id must not change —
workflows reference it.

Recency needs a `lastUsedUtc` per dataset, written when a pipeline runs against it. Store it in the
index alongside the id. Sort the list by it, so the dataset someone is working on stays at the top
instead of sorting alphabetically away from them.

## Files

| File | Change |
|---|---|
| `Desktop.Local/LocalDatasetStore.cs` | Detection on import; `Rename`; `lastUsedUtc`; profile cache |
| `Desktop.Local/CsvFormatDetector.cs` | New — encoding and delimiter detection |
| `Application/Datasets/CsvProfiler.cs` | Moved down from Infrastructure if the reference direction forbids reuse |
| `WinForms/Components/Datasets.razor` | Detection result with override; profile panel; rename; column cap |
| `Desktop.Local/LocalDatasetIndex.cs` | New — the index becomes a record, not a `Dictionary<string, Guid>` |

## Acceptance criteria

- [ ] A semicolon-separated file imports with the right columns
- [ ] A Windows-1256 file imports with readable Arabic column names
- [ ] The detected encoding and delimiter are shown, and can be overridden before committing
- [ ] A 200 MB CSV imports without the UI freezing and without the process exceeding ~300 MB
- [ ] Selecting it shows a profile within a few seconds; the second selection is instant (cached)
- [ ] A 500-column file previews the first 50 with a clear indicator
- [ ] A malformed single-line file reports as malformed rather than exhausting memory
- [ ] Rename keeps the id, and a workflow referencing it still resolves
- [ ] The list orders by recent use

## Tests

| Test | Level |
|---|---|
| Semicolon, tab and pipe delimiters each detected | Unit |
| UTF-8 BOM, UTF-16, and ANSI fallback each decoded | Unit |
| A delimiter inside quotes does not sway detection | Unit |
| Row-count estimate is within 10% on a uniform file | Unit |
| Profile cache invalidates when the file changes | Unit |
| Rename preserves the id and `PathFor` still resolves | Unit |
| `lastUsedUtc` updates when a pipeline runs | Unit |
| A single-line oversized file is rejected, not read | Unit |

## Risks

| Risk | Mitigation |
|---|---|
| Detection guesses wrong and the user does not notice | Show the guess and require a confirm on import; never silent |
| Profiling a huge file blocks the UI | On demand, off the UI thread, cancellable |
| Moving `CsvProfiler` breaks the platform's dataset pipeline | It is covered by existing tests; move only if the reference direction forces it |
| Two Studio instances write the index at once | The store already locks in-process; add a file lock, or accept last-writer-wins and document it |
