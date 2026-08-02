# 05 — Local model registry and inference

> **Depends on:** 04 — there is nothing to register until training produces models locally.

## Context

Phase 04 leaves trained models sitting in run folders. That is enough to *have* a model and not enough
to *use* one. The Web has a registry with versions, two-person approval, promotion and inference; the
desktop has none of it.

The desktop does not need the governance — approvals are a platform concern, and a model on one
engineer's laptop has no audience to protect. What it needs is the ability to **keep a model, use it, and
hand it over**.

## Scope

**In**

- Save a run's model into a named local registry entry with its metrics and lineage
- Predict against a saved model — single row and batch CSV
- Export a model bundle; import one
- Promote a local model to the platform registry when online

**Out**

- Approval workflow. Two-person approval on a single-user machine is theatre.
- Deployment / serving. The desktop is not a serving tier.
- Drift monitoring. Needs production traffic, which the desktop does not have.
- Model explainability. Worth wanting, out of scope here; it would be its own phase.

## Design

### Local registry

```
workspace/
  models/
    esp-failure/                       ← the name the user chose
      v1/
        model.zip
        model.json    ← metrics, task, target, features, source run id, dataset hash, created
      v2/
        …
      latest.txt      ← which version is current
```

A folder per name, a folder per version. Versions are integers, never reused. `model.json` carries
enough to answer *what is this and where did it come from* without the run it came from still existing.

The registry is **not** the run history. A run is an experiment; a model is a thing you decided to keep.
Registering copies the file rather than referencing it, so pruning runs does not gut the registry.

### Prediction

Two shapes, matching the two ways a person checks a model:

- **Single row** — fields derived from the model's feature list, typed from the schema. Fastest way to
  ask *"what would it say about this well?"*
- **Batch CSV** — a file in, predictions appended as a column, saved next to the input. This is the one
  people will actually use for anything real.

`AutoMlPredictionPool` already exists and caches loaded models. Register it in desktop DI (Phase 04
does this) and reuse — loading a model per prediction would be slow and pointless.

**Schema mismatch is the common failure.** A batch CSV missing a feature column must fail with *which*
column is missing, not a framework exception. Validate against `model.json` before predicting.

### Export / import

A `.kocmodel` file — a zip of `model.zip` + `model.json`. This is how an engineer sends a colleague a
model, or moves one between machines.

Import validates the manifest and refuses a bundle whose ML.NET version is newer than the host's, with
a clear message. Silently loading an incompatible model produces a crash far from the cause.

> **Trust boundary.** An imported model is executable content from outside. It should be treated as
> such: the import dialog must name where the file came from and warn plainly. We are not sandboxing
> ML.NET model loading, and pretending otherwise would be worse than saying so.

### Promotion to the platform

When online and signed in, a local model can be pushed to the platform registry — the same
`RegisterModelDialog` flow the Web uses. The platform then applies its own governance: approvals,
promotion, deployment. Local models are drafts; the platform is the system of record.

The upload carries the model, its metrics and its lineage. It does **not** carry the training dataset —
that may be Restricted, and the platform's classification rules apply to datasets uploaded deliberately,
not to whatever happened to be on someone's laptop.

## Files

| File | Change |
|---|---|
| `Desktop.Local/LocalModelStore.cs` | New — register, list, read, delete, version |
| `Desktop.Local/ModelBundle.cs` | New — export/import `.kocmodel` |
| `Desktop.Local/LocalKocApiClient.cs` | Override model listing and inference to hit local |
| `WinForms/Components/Models.razor` | New — registry, predict, export/import, promote |
| `WinForms/Components/Runs.razor` | "Keep this model" action into the registry |
| `Application/ML/IPredictionPool.cs` | Confirm it is reusable as-is from the desktop |

## Acceptance criteria

- [ ] A run's model can be kept into a named registry entry
- [ ] Registering the same name twice creates v2, and v1 is still readable
- [ ] Single-row prediction builds its form from the model's own feature list
- [ ] Batch prediction appends a column and writes the result beside the input
- [ ] A CSV missing a feature names the missing column
- [ ] Export produces a `.kocmodel` that imports on another machine
- [ ] Importing a newer-ML.NET bundle refuses with a clear message
- [ ] The import dialog states the trust implication
- [ ] Promotion to the platform works when online and fails honestly when not
- [ ] Deleting a run leaves any model kept from it intact

## Tests

| Test | Level |
|---|---|
| Versions increment and are never reused | Unit |
| `model.json` round-trips with lineage intact | Unit |
| Schema validation names the missing column | Unit |
| Bundle export/import round-trips | Unit |
| An incompatible bundle version is refused | Unit |
| Deleting a source run leaves the registered model readable | Unit |
| The prediction pool caches rather than reloading per call | Unit |

## Risks

| Risk | Mitigation |
|---|---|
| Model files fill the disk | Size shown per entry and in total; retention offered |
| An imported model is malicious | Stated plainly at import. Not sandboxed — say so rather than imply safety |
| ML.NET version skew between machines | Version in the manifest; refuse newer with a message |
| Users treat local models as approved | The UI calls them **local drafts**, and promotion is the explicit step |
