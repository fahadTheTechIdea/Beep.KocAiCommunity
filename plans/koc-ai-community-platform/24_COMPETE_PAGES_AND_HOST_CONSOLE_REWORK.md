# Phase 24 — Compete Pages and Host Console Rework

**Status:** ✅ DONE (2026-08-13) — all four stages shipped the same day as the audit; suites green (Unit 432 / Integration 238 / Component 71). Two accepted deviations, recorded in the tracker: failure reasons live in the snackbar (failed submissions are never persisted, so there is no history row to carry them), and the four acceptance journeys are held by `CompetitionLifecycleTests` + `HostConsoleTests` + a curl smoke rather than Playwright.
**Dependencies:** Phase 13 (competitions core), Phase 05b (rewards copy), Phase 20 (scoring audit)
**Goal:** Make the competition detail page's tabs — above all **Host** — a complete, honest console, and make **Task and Evaluation one decision** everywhere instead of two half-decisions that can disagree.

> **Why now.** The arena pages were built in one pass (tracker items 29–34) around the competitor's
> path: browse → data → submit → leaderboard. That path works. The **host's** path was sketched, not
> finished: the create dialog and the Host tab each own half of the task/scorer decision, the
> lifecycle the dialog promises is not the lifecycle the service implements, and half of what a host
> needs (prizes, category, editing their own text) either has no UI, no endpoint, or is locked to
> PlatformAdmin. With 25 seeded challenges live and colleagues about to host their own, the seams
> are where the platform will tear first.

---

## 1. Audit — what is actually there today

### 1.1 The Task ↔ Evaluation seam (the core defect)

One decision — *"what kind of prediction is this, and how is it scored?"* — is stored as two fields
(`TaskType`, `ScorerCode`) and set by **two different surfaces that never talk to each other**:

| # | Finding | Evidence |
|---|---------|----------|
| T1 | The create dialog collects a task choice (5 cards) but **sends only the scorer**. `CreateCompetitionRequest` has no TaskType field at all. | `CreateCompetitionDialog.razor:449` (`CurrentTask.Scorer`), `CompetitionDtos.cs:4-11` |
| T2 | `TaskType` therefore stays at its **entity default `"BinaryClassification"`** until the first dataset upload. A Regression or Anomaly challenge created without data shows a *Binary classification* chip beside an *RMSE/AUC* metric chip — visibly inconsistent in the arena, the banner, and the home page. | `CompetitionEntities.cs:28`, `CompetitionService.cs:61-73` |
| T3 | The Host tab's Task select is **never initialised from the competition** — it resets to `BinaryClassification` (and `label`/`id`) on every page load. A host re-uploading data either silently rewrites the task or hits a late server error. | `CompetitionDetail.razor:479-481`, `LoadAllAsync` (539-559) never touches `_dsTask/_dsLabel/_dsId` |
| T4 | The Host tab offers **all five tasks** although the scorer was frozen at creation and supports only 1–2 of them. The only guard is the server's late rejection, phrased in plumbing: *"Scorer 'rmse' does not support task 'BinaryClassification'."* | `CompetitionDetail.razor:347-353` vs `CompetitionService.cs:295-305`; scorer support lists in `AccuracyScorer.cs:15`, `RmseScorer.cs:18`, `AucScorer.cs:18` |
| T5 | The scorer is **invisible and immutable** after creation. Nothing on the Host tab says what the competition is scored by, and no endpoint can change it. | endpoint inventory, `CompetitionEndpoints.cs:37-340` |
| T6 | The submit hint says **`id, label`** for every task. For AUC (anomaly) the meaningful upload is a *score/ranking* per row, for RMSE a number — the one line of guidance a competitor gets never adapts. No `sample_submission.csv` exists anywhere. | `CompetitionDetail.razor:273-275`; contrast per-task samples that exist only inside the create dialog, `CreateCompetitionDialog.razor:280-307` |

**Design decision for the fix:** task and metric are presented and stored as **one bound pair**.
The create dialog already treats them as one card; the request, the service, and the Host tab must
follow. Where they could drift (Host tab re-upload), the UI constrains the choice to
`scorer.SupportedTasks` and shows the metric read-only beside it.

### 1.2 Lifecycle says one thing and does another

| # | Finding | Evidence |
|---|---------|----------|
| L1 | The dialog's button says **Create draft**, the review step says *"Nothing goes live until you upload data and click Activate"*, the snackbar says *"Draft created"* — and `CreateAsync` hardcodes **`Status = "active"`**. Every hosted competition goes live instantly, dataless, submissions failing with a generic error. | `CreateCompetitionDialog.razor:254,354-359,467` vs `CompetitionService.cs:65` |
| L2 | `BrowseVisibleAsync` hides **all** drafts — including the host's own. "Back to draft" makes your competition vanish from your own arena; the only way back is browser history. | `CompetitionService.cs:425-428` |
| L3 | The arena's **draft filter chip** is shown to everyone and can never match anything (the list it filters never contains drafts). Dead UI. | `Compete.razor:94` |
| L4 | **Activate has no readiness guard.** One click activates a competition with no data and no answer key; competitors then get *"This competition is not open for submissions"* after building a model. The readiness chips exist but gate nothing. | `CompetitionDetail.razor:425-440`, `SetStatusAsync` (`CompetitionService.cs:317-328`), submit guard (`Status != "active" \|\| AnswerKeyArtifactId is null`) |
| L5 | Reveal handling is inconsistent: the dialog stores reveal at **12:00 UTC** of the chosen date, the Host tab converts **local midnight → UTC**; both are date-only pickers. Nothing auto-concludes at reveal. | `CreateCompetitionDialog.razor:447` vs `CompetitionDetail.razor:751-767` |

### 1.3 Host tab authorization and reach

| # | Finding | Evidence |
|---|---------|----------|
| H1 | The Host tab shows for **anyone with a create grant**, not for the host of *this* competition (`CanCreate => _maxScope is not null`). Colleague A with a grant opens colleague B's competition, sees a full console, and every save 403s with *"Only the competition creator…"*. | `CompetitionDetail.razor:487-488` vs creator checks like `CompetitionService.cs:285-288` |
| H2 | The 25 **seeded** competitions belong to `koc-platform` — no human can conclude them, set their reveal, replace data, or edit their translations. Only feature/prizes are reachable (admin-only). The admin override exists for exactly one service method (hero image takes `isPlatformAdmin`); everything else checks raw `CreatedByUserId`. | `CompetitionSeeder.cs:678`, `CompetitionEndpoints.cs:310-316` vs the creator-only methods |
| H3 | There is no **"competitions I host"** view anywhere — a host manages by memory and URL. | `Compete.razor` (no mine filter), `Home.razor` |

### 1.4 Missing host capabilities

| # | Capability | API today | UI today |
|---|-----------|-----------|----------|
| M1 | Prizes (1st/2nd/3rd) | ✅ `POST /competitions/{id}/prizes` — but **PlatformAdmin-only** | Admin console only; a host cannot set prizes on their own challenge. 0 of 25 live competitions carry prizes. |
| M2 | Category / domain | ❌ none — `SetCompetitionCategoryRequest` exists in Contracts but **no endpoint and no client method use it** (dead contract) | none. A hosted competition is invisible to the home-page area filter and the arena Domain row. |
| M3 | Edit English title/description | ❌ none | none — the Host tab edits the **Arabic** but not the English. |
| M4 | Change daily quota | ❌ none | none (slider exists only at creation). |
| M5 | Change visibility scope | ❌ none | none. |
| M6 | Sample submission file | ❌ | ❌ — trivially derivable from the evaluation set's ids + label header. |
| M7 | Quota remaining today | not on any DTO | competitor never sees "3 of 5 left". |

### 1.5 Two forms own one entity (the root of most of the above)

The create dialog (`+ Host a competition`) and the Host tab are **two divergent editors for the
same record**, each holding fields the other cannot see. Nothing keeps them consistent, and
three drifts have already happened (task, reveal, language):

| Field | Create dialog | Host tab | State |
|---|---|---|---|
| Title / description (**English**) | ✅ | ❌ | Only settable once, at create — typos are permanent |
| Title / description (**Arabic**) | ✅ (collapsed panel) | ✅ | The one overlap — and it is the *translation*, not the source |
| Task | ✅ card — **discarded** (T1) | ✅ select — **resets** (T3) | Drifted |
| Scoring metric | ✅ implied by the card | ❌ invisible | Frozen and unshown |
| Scope / audience | ✅ | ❌ | |
| Daily quota | ✅ slider | ❌ | |
| Reveal | ✅ noon-UTC conversion | ✅ local-midnight conversion | Drifted (L5) |
| Hero image | ✅ | ✅ | Two duplicate implementations |
| Data / answer key | ❌ (format samples only) | ✅ | |
| Prizes / category | ❌ | ❌ | Nowhere (M1, M2) |

The user-visible symptom is exactly as reported: **the popup speaks English, the Host tab speaks
Arabic** — an author writes their English in one place, their Arabic in another, and can never
again touch the English at all.

Compounding it, the dialog's own chrome is largely **not localized**: the step rail
(`Steps = ["Overview", "Task & data", "Rules", "Review"]`), every task card (label, help text,
metric chip), the quota hints, the reveal explanation, the review chips ("scored by …",
"submissions/day"), the description template, and its snackbars are raw English
(`CreateCompetitionDialog.razor:18,271,280-307,338-343,352-360,411-419,463-472`) — so an Arabic
reader gets a mostly-English dialog even though the rest of the arena is bilingual.

### 1.6 Smaller page defects (sweep list)

- Raw unlocalised strings on the detail page: `← All competitions` (line 40), `⚔ Competition`
  (43), the Overview's *"Submissions are scored with … against a hidden answer key."* (159-160),
  the Host upload snackbars (805, 821, 837), `(optional)` (95), the dialog's step rail and
  review chips (18, 96, 152, 167-174, 184-189).
- `LoadData` has no try/catch: if the data endpoint rejects a caller (it is employee-gated), the
  whole detail page dies with it rather than the Data tab degrading (`CompetitionDetail.razor:638-645`).
- Arena task filter says **"Classification"** where every card chip says **"Binary classification"**
  (`Compete.razor:221`).
- `_dsTraining/_dsEval` MemoryStreams are never disposed after upload (`CompetitionDetail.razor:793-813`).
- Data tab's `RowCount` counts header-stripped lines on the whole CSV held in circuit memory —
  fine at seed sizes, worth a note when hosts upload 5 MB files (`CompetitionDetail.razor:857-858`).

---

## 2. The rework, in four stages

Ordered so every stage ships alone, each one leaving the pages truthful — no stage depends on a
later one to stop lying.

### Stage 1 — One decision: task + evaluation travel together *(the defect the audit is named for)*

1. **Contract:** add `TaskType` to `CreateCompetitionRequest` (append-only, default null → server
   falls back to the scorer's first supported task, so old callers stay valid).
2. **Service:** `CreateAsync` stores the pair and **validates it once** with the same
   `scorer.SupportedTasks` check `SetDatasetsAsync` already does — creation and upload enforce the
   same rule, worded for people: *"Accuracy scores yes/no and multiclass answers; pick one of
   those, or choose a different metric."*
3. **Host tab:** initialise `_dsTask`, `_dsLabel`, `_dsId` from the loaded competition in
   `LoadAllAsync`; constrain the Task select to `scorer.SupportedTasks` (needs `ScorerCode` +
   supported tasks on the DTO — append-only again); show the metric beside it read-only with a line
   of plain-words explanation ("RMSE — average miss, lower wins").
4. **Submit tab:** per-task format panel — `id,label` for classification, `id,<number>` for
   regression/forecasting, `id,<score>` with "higher = more anomalous" for AUC — plus a
   **Download sample submission** button generated from the evaluation ids.
5. **Tests:** integration — create stores the pair, mismatched pair rejected at create;
   component — Host tab select reflects the competition and offers only compatible tasks.

### Stage 2 — An honest lifecycle

1. `CreateAsync` creates **drafts** (`Status = "draft"`), which is what every word of the dialog
   already promises. The seeder still stamps its competitions active explicitly.
2. `BrowseVisibleAsync` keeps **your own drafts** visible to you (and to PlatformAdmin), tagged so
   the arena can badge them "draft — only you see this". The arena draft chip now filters something
   real; for everyone else it stays hidden as today.
3. **Readiness gate on Activate:** the service refuses to activate without an answer key (data
   optional — upload-only competitions are legitimate, `ValidateAnswerKeyAsync` already handles
   both shapes); the Host tab turns the readiness chips into a checklist wired to the same rule,
   with the Activate button disabled and explaining itself until it passes.
4. **Reveal:** one shared conversion (date+time picker, explicit "local time" label, stored UTC) in
   both the dialog and the Host tab; an opt-in **"conclude automatically at reveal"** flag
   (append-only column, checked by the same code path that already notifies on conclusion).
5. **Tests:** draft→active refuses without key; own-draft visibility; both reveal writers produce
   the same UTC instant for the same wall-clock choice.

### Stage 3 — One console: create and update in the same place

The fix for §1.5 is not parity between two forms — it is **deleting one of them as an editor**.
Competition data gets exactly one writing surface: the Host console. The `+ Host a competition`
button shrinks to a **launcher** that asks only for what can never change afterwards, creates the
draft, and lands on the console where everything else lives.

1. **The launcher** (what remains of the dialog): title + the task-and-metric card — the two
   choices that are frozen for the record's life — then *Create draft* → navigate to
   `/compete/{id}?tab=host`. Its guidance content is not lost: the per-task file-format samples,
   help rail, and description quality checks move into the console sections where their fields
   now live. Everything the launcher keeps goes through the localizer.
2. **The console holds every field, both languages side by side:** English title/description and
   Arabic title/description in the *same* section, saved together — never English at birth and
   Arabic in a different room (M3 dies here). Plus scope (editable while draft, frozen once
   active), quota, reveal, data, key, hero image, prizes, category, lifecycle. The dialog's
   duplicate hero/reveal/Arabic implementations are deleted.
3. **Ownership, not grant:** add `CanManage` to `CompetitionDto` (creator **or PlatformAdmin**,
   computed server-side); the console keys off it. Grant-holders stop seeing consoles they cannot
   use.
4. **Admin override made uniform:** one `RequireManageAsync(userId, isPlatformAdmin, id)` helper in
   the service, used by datasets/key/status/reveal/translations/hero — the hero-image method's
   pattern promoted to policy. This is also what makes the **seeded** competitions manageable
   (H2): an admin can conclude Payroll Integrity or set its prizes without a database session.
5. **The endpoints the console needs:** `PUT /competitions/{id}` for title/description/quota/scope
   (manage-gated, title-uniqueness shared with create, scope only while draft); prizes drops from
   PlatformAdmin to *manage*; the dead category contract gets wired —
   `POST /competitions/{id}/category` (manage-gated, enabled categories only) + client method.
6. **Hosting home:** a "Hosting" filter chip on the arena (client-side once `CanManage` is on the
   DTO) — the missing route back to your own drafts.
7. **Tests:** 403 matrix (stranger / grant-holder / creator / admin) per endpoint; category
   round-trip to the arena Domain row; prize set by host renders on the card and banner; a
   component test pinning the single-writer rule — the launcher contains **no field** the console
   also has (title excepted, since the launcher is where it is born).

### Stage 4 — Competitor polish and the sweep

1. `QuotaRemainingToday` on the my-submissions response; shown beside the upload control and
   decremented live ("3 of 5 left today — resets midnight UTC").
2. Failure stories: a failed submission's reason (already thrown as `CompetitionException` text)
   lands in the History table's status cell, not just a vanished snackbar.
3. `LoadData` degrades to the Data tab's empty state instead of taking the page down.
4. The localisation sweep of §1.5, held to `LocalizationCoverageTests` like everything else, and
   the dispose/wording nits from the same list.
5. Playwright pass over the four journeys: host-from-zero (dialog → draft → data → key → activate),
   compete-from-zero, admin-rescues-seeded-competition, Arabic end-to-end on all tabs.

---

## 3. Decisions taken (so the next session doesn't relitigate)

| Decision | Choice | Rationale |
|----------|--------|-----------|
| Create vs. edit surfaces | **One editor — the Host console.** The `+` button is a launcher (title + task card → draft → console); it contains no field the console also has | Two forms for one entity have already drifted three ways (task discarded/reset, two reveal conversions, English-here/Arabic-there). Parity patching guarantees a fourth drift; a single writer cannot drift. |
| Languages | English and Arabic sit **side by side in the same console section**, saved together | An author must never lose access to half their own words; "popup speaks English, Host tab speaks Arabic" is the reported defect, verbatim. |
| Task/scorer authority | **Scorer is chosen once at create (as part of the task card); task may vary only within the scorer's family afterwards** | Changing the metric after submissions exist would reorder a live leaderboard; `RescoreAllAsync` exists for *key* replacement, not metric replacement. If a host truly picked wrong, draft → fix → reactivate is the honest path. |
| Scope after creation | Editable while draft, frozen at first activation | Widening or narrowing a live audience silently changes who can see submissions already made; while nothing has happened yet, it is a free choice. |
| Draft visibility | Own drafts visible to self + admin only | "Draft — not yet visible" must be true, but the host must never lose their own work. |
| Prizes authority | Creator or PlatformAdmin | Prize *text* is free-form and scope-visible only; no budget system exists to protect. Admin console keeps its copy for seeded content. |
| Category authority | Creator or PlatformAdmin, enabled categories only | Same shape as every other manage action; disabled categories stay unofferable everywhere. |
| New columns | Only `ConcludeAtReveal` (bool, default false) | Everything else in this phase is append-only DTO/contract work — consistent with the arena revamp's "no DB schema change" discipline except where a behavior genuinely needs state. |
| English title edits after activation | Allowed, uniqueness-checked, translation rows keyed by id so nothing orphans | The translation store was keyed by competition id for exactly this reason (`CompetitionEndpoints.cs:55`). |

## 4. Acceptance (gate for calling this phase done)

- A host can take a competition from idea to active **without ever seeing a server-worded error**:
  every rule that can reject is expressed in the UI before the click.
- `TaskType` and `ScorerCode` cannot disagree — not at create, not at re-upload, not for the
  entity default — and no page shows a task chip beside a metric chip that contradicts it.
- The word **draft** on screen is true: drafts are invisible to others, visible to their host,
  and nothing activates without an answer key.
- A grant does not open other people's consoles; an admin can manage any competition, including
  all 25 seeded ones, from the UI.
- A hosted competition can carry prizes, a category, corrected English, and a changed quota —
  all from the Host tab.
- **Exactly one form writes competition fields.** The launcher holds only title + task card; both
  languages of the title and description live in one console section; deleting the dialog's
  duplicated hero/reveal/Arabic code is part of done, not cleanup for later.
- Every new string has its Arabic (`LocalizationCoverageTests` both directions), and the global
  DoD passes: `dotnet format --verify-no-changes && dotnet build -warnaserror && dotnet test`.

## 5. Explicitly out of scope

- New scorers (F1, MAE, MAPE…) and per-competition metric plugins — Phase 20's registry is the
  place; this phase only stops the existing three from being misapplied.
- Team submissions, private sharing of submissions, code upload — unchanged positions from
  Phase 13.
- Rich dataset descriptions/column dictionaries (Phase 07 territory); the sample-submission file
  is the only data artifact this phase adds.
- Any change to how the desktop Studio submits — the bridge contract (19d) is untouched; the
  submit-format panel documents it, nothing more.
