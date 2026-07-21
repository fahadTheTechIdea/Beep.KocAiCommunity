# 19d — Competitions bridge + packaging (Stages 5–6)

Connect the offline desktop designer to the online competition surface, degrade
gracefully when offline, and package the app. See `19_WINFORMS_DESKTOP_STUDIO.md`.

## Stage 5 — Competitions bridge

**Goal:** from the desktop, browse competitions, submit a locally-built pipeline, and
see leaderboards — via the API — while staying offline-safe.

**Work**
- `LocalKocApiClient` routes the competition methods (`GetCompetitionsAsync`,
  `SubmitPipelineAsync`, `GetLeaderboard*Async`, `GetMeAsync`) to an **inner HTTP
  `KocApiClient`** built from the configured API base URL + identity. All other
  Studio calls stay local (Stage 4).
- **Submit flow:** the reused designer already has a "Submit to competition" card;
  on the desktop it posts the current `WorkflowDefinition` (built locally) to
  `POST /competitions/{id}/submit-pipeline`. A **Competitions** screen (reuse
  `Compete.razor` / a trimmed browse+leaderboard component) lists visible
  competitions and their live/final boards.
- **Offline-graceful:** a connectivity probe (cheap `GET /me` or a ping). When the
  API is unreachable, the Competitions screen shows "Connect to the KOC network to
  compete" and the designer's Submit button is disabled with a tooltip; a failed call
  never crashes the circuit (reuse the existing `ReadErrorAsync` handling).
- **Settings:** a small dialog for **API base URL** and **identity**. v1 uses the
  existing dev-header persona (`DevIdentity` + `DevIdentityHandler`) against a
  dev/staging API; **production Entra interactive sign-in is a follow-up** (record as
  a future enhancement, not v1).

**DoD / verification**
- Build `-warnaserror`; format clean.
- Integration-style test (against the in-memory API factory) or a manual E2E: with a
  running API, submit a locally-built pipeline to a seeded competition → a scored
  submission appears on the leaderboard; with the API down, the app stays usable and
  shows the offline state. Commit.

## Stage 6 — Packaging, config, docs

**Goal:** a distributable desktop build and updated documentation.

**Work**
- First-run config (`appsettings.json` or a settings file): API base URL, workspace
  path, default persona. App icon + product name (`KOC Training & Career Development —
  Studio`).
- `dotnet publish -c Release -r win-x64` (self-contained) profile; verify the ML.NET
  and DuckDB **native `win-x64` assets** ship and a local run works from the published
  output.
- Docs: a `README` section for the desktop app (build, run, offline behaviour,
  competitions); update `plans/.../README.md` and `MASTER_TODO_TRACKER.md`; write a
  memory note. Note the WebView2 runtime prerequisite.

**DoD / verification**
- `dotnet publish` produces a runnable app; launching it offline builds+runs a
  pipeline; online it can submit to a competition. Commit.

## Cross-cutting notes

- **CI:** the WinForms + Desktop.Local projects are Windows-only (`net10.0-windows` for
  the shell). Keep them out of the Linux build/test set (solution filter) so the
  shared-library suite stays green cross-platform; build the desktop app on Windows.
- **Security:** the desktop app runs pipelines locally with the user's own
  permissions; the only outbound surface is the competition API, authenticated the
  same way the Web client is.
