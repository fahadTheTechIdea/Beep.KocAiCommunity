# Phase 13 — Competitions and Leaderboards

**Status:** ✅ DONE (2026-07-19 audit) — entities, `CompetitionService`, trusted scorers, hidden answer key, quotas, reveal, `LeaderboardHub`, supervisor rollups all shipped.
**Engagement follow-up (Phase 05b):** the fun layer lands on top of this working core — gold/silver/bronze medal styling on top-3 leaderboard rows, animated rank-change arrows on SignalR updates, a team-standings tab (org-unit average Barrels), Barrels XP for scored submissions, `competition-winner`/`podium` badges at reveal, and confetti for winners. See `05b_COMMUNITY_ENGAGEMENT_AND_GAMIFICATION.md` §1.2–§1.3, §3.5, §3.9.
**Dependencies:** Phase 02, Phase 07, Phase 08, Phase 12
**Goal:** Internal, Kaggle-style KOC competitions — **the primary way employees practice the AI/ML skills they build in the learning tracks** — with trusted scoring, hidden evaluation data, org-scoped visibility, and real-time leaderboards.

> **Center of gravity.** Competitions (with [Learning Tracks](13a_LEARNING_TRACKS_AND_UPSKILLING.md)) are the core of the platform, not a peripheral feature. Employees "have the most fun" here; supervisors watch participation and standings through the rollup dashboards (Phase 02 `IOrgScopeResolver`). Anyone — an employee, a Team Leader, or a `CompetitionAdmin` — can create a competition and choose who can see it.
>
> **CompetitionAdmin is the organizer role.** A `CompetitionAdmin` generates competitions **and prepares the material that backs them**: they can create and manage the **datasets** and **projects** a competition uses (Phase 07), set the hidden evaluation data, wire the scoring plugin, and publish. This lets an organizer stand up a complete challenge end-to-end without waiting on a PlatformAdmin.

## 1. Goal and dependencies

- Internal KOC competitions only (no external sign-ups)
- Prediction-file and trusted ML.NET model submissions
- Declarative scoring metrics and trusted scorer plugins
- Concealed-vs-live leaderboard splits, submission quotas, reveal dates
- **Org-scoped visibility**: creator picks Team/Group/Directorate/Company audience (Phase 02 model)
- **Supervision rollups**: participation and standings surfaced to supervisors for their subtree
- **Learning tie-in**: a competition can reference a recommended learning track; completing a track can suggest a matching competition
- Real-time leaderboard SignalR updates

## 2. Existing reference behavior

- Beep.AI.Community: `app/services/competition_service.py` (2362 lines), `app/services/scoring_service.py`, `app/services/submission_evaluator.py`.
- Beep.AI.Community scored user-uploaded Python scripts in subprocess — explicitly NOT copied.

## 3. Architecture decisions

| Decision | Choice | Rationale |
|---|---|---|
| Audience | KOC employees only | Per KOC focus |
| Visibility | `VisibilityScope` (Team/Group/Directorate/Company) + `VisibilityOrgUnitId` chosen at creation | Creator decides who can see and enter — same model as datasets/projects |
| Who can create | Any Employee, or a `CompetitionAdmin` | Employees can run friendly team challenges; admins run wider ones |
| Submission types | Prediction file (CSV/Parquet) + trusted ML.NET model | Avoid arbitrary code execution |
| Scoring | Trusted scorer plugins registered server-side | Standard |
| Leaderboard | Live (visible) score on a hidden subset + concealed final score | Standard Kaggle-style split; both stay within the competition's visibility scope |
| Quotas | Per-user per-day; configurable | Standard |
| Reveal | Concealed final leaderboard hidden until reveal date | Standard |
| Supervision | Standings and participation roll up to supervisors via `IOrgScopeResolver` | Managers see how their people/teams are doing |

## 4. Project-by-project deliverables

### 4.1 Domain

- `Competition`, `CompetitionPhase`, `CompetitionEligibility`
- `Submission`, `SubmissionFile`, `SubmissionResult`
- `LeaderboardEntry`, `LeaderboardSnapshot`
- `ScoringPlugin` (registry, not user-uploaded)

### 4.2 Application

- `ICompetitionService`, `ISubmissionService`, `ILeaderboardService`, `IScoringPlugin`
- DTO ↔ entity mapping

### 4.3 Infrastructure

- EF Core configurations
- Scorer plugin loader (assembly scanning)

### 4.4 API

- Endpoints for competition CRUD, submission, leaderboard

### 4.5 UI

- `Pages/Competitions/Index.razor`
- `Pages/Competitions/Detail.razor`
- `Pages/Competitions/New.razor` — includes the shared `VisibilityScopePicker` ("who can see/enter this") with audience preview
- `Pages/Competitions/Leaderboard.razor`
- `Pages/Competitions/Submissions.razor`
- `Pages/Supervision/CompetitionRollup.razor` — supervisor view of participation and standings across the caller's subtree (Team/Group/Directorate/Company depending on position)
- `Components/Competitions/SubmissionForm.razor`
- `Components/Competitions/LeaderboardTable.razor`
- `Components/Competitions/CountdownTimer.razor`

## 5. Entities and migrations

```csharp
public class Competition : AuditableEntity
{
    public string Name { get; set; } = default!;
    public string Description { get; set; } = default!;
    public string Status { get; set; } = "draft"; // draft, active, judging, concluded, archived
    public DateTime? ActiveFromUtc { get; set; }
    public DateTime? ActiveToUtc { get; set; }
    public DateTime? RevealUtc { get; set; }        // concealed final leaderboard revealed at this time
    public string SponsorUserId { get; set; } = default!;
    public VisibilityScope VisibilityScope { get; set; } = VisibilityScope.Team;  // who can see/enter
    public Guid VisibilityOrgUnitId { get; set; }
    public Guid? RecommendedTrackId { get; set; }   // optional learning-track tie-in (Phase 13a)
    public Guid? DatasetId { get; set; }
    public Guid? DatasetVersionId { get; set; }
    public KocDataClassification Classification { get; set; }
    public string? RulesMarkdown { get; set; }
    public int SubmissionQuotaPerUser { get; set; } = 5;
    public int SubmissionQuotaPerDay { get; set; } = 2;
    public string ScoringPluginCode { get; set; } = default!;
    public string ScoringConfigJson { get; set; } = default!;
}

public class CompetitionPhase : AuditableEntity
{
    public Guid CompetitionId { get; set; }
    public string Name { get; set; } = default!;
    public DateTime FromUtc { get; set; }
    public DateTime ToUtc { get; set; }
    public string Type { get; set; } = default!; // training, evaluation
}

public class Submission : AuditableEntity
{
    public Guid CompetitionId { get; set; }
    public string SubmitterUserId { get; set; } = default!;
    public string Status { get; set; } = "pending"; // pending, scored, failed, disqualified
    public string Type { get; set; } = default!; // prediction-file, model
    public Guid? PredictionFileArtifactId { get; set; }
    public Guid? ModelId { get; set; }
    public Guid? ModelVersionId { get; set; }
    public DateTime SubmittedUtc { get; set; }
    public string? Notes { get; set; }
    public double? PublicScore { get; set; }
    public double? PrivateScore { get; set; }
    public string? ScoreBreakdownJson { get; set; }
}

public class LeaderboardEntry : AuditableEntity
{
    public Guid CompetitionId { get; set; }
    public string SubmitterUserId { get; set; } = default!;
    public Guid? BestSubmissionId { get; set; }
    public double Score { get; set; }
    public int Rank { get; set; }
    public bool IsPublic { get; set; }
}
```

## 6. API contracts

```http
GET    /api/v1/competitions?status=&scope=&page=      # results filtered by the caller's visibility subtree
POST   /api/v1/competitions                           # body includes visibilityScope + visibilityOrgUnitId
GET    /api/v1/competitions/{id}
PUT    /api/v1/competitions/{id}
DELETE /api/v1/competitions/{id}
POST   /api/v1/competitions/{id}/activate
POST   /api/v1/competitions/{id}/conclude
POST   /api/v1/competitions/{id}/reveal               # unveils the concealed final leaderboard
GET    /api/v1/competitions/{id}/leaderboard?board=live|final   # 'final' returns 403 before RevealUtc
POST   /api/v1/competitions/{id}/submissions
GET    /api/v1/competitions/{id}/submissions?page=
GET    /api/v1/competitions/{id}/submissions/{submissionId}
POST   /api/v1/competitions/{id}/submissions/{submissionId}/rescore
GET    /api/v1/supervision/competitions?orgUnitId=    # supervisor rollup; scoped to caller's subtree
```

## 7. MudBlazor pages and components

- All competition pages use MudBlazor; leaderboard uses `MudDataGrid` with custom ranking column

## 8. Security and authorization

- `Employee` minimum to view/enter (subject to the competition's visibility scope)
- Any Employee may create a competition; only the creator, a `CompetitionAdmin`, or `PlatformAdmin` may edit/activate/reveal/rescore it
- Visibility enforced by `IVisibilityEvaluator` (Phase 02): a competition is listed and enterable only within its `VisibilityScope` subtree; `Company` scope is open to all KOC
- Create form defaults `VisibilityOrgUnitId` to the creator's own unit; non-admins cannot pick a unit they don't belong to
- Supervisor rollup endpoints are gated to the caller's own subtree (`IOrgScopeResolver`); a supervisor cannot read standings outside their org
- Submissions can be uploaded by any eligible participant within scope
- Hidden evaluation data is never accessible via the API
- Concealed final score and breakdown are opaque to submitters until `RevealUtc`
- Quotas enforced server-side

## 9. Tests

- Unit: quota enforcement, ranking calculation with ties, scoring plugin contract
- Integration: competition lifecycle, submission scoring, leaderboard refresh
- Component: leaderboard table, submission form

## 10. Verification commands

```bash
dotnet test tests/Beep.KocAiCommunity.IntegrationTests --filter "FullyQualifiedName~Competitions"
```

## 11. Acceptance gate

- Hidden evaluation data is never accessible via the API
- Scoring is reproducible
- Quotas work
- Leaderboard ties are deterministic
- Reveal date hides the concealed final leaderboard until reached (`board=final` returns 403 before `RevealUtc`)
- Org-scoped visibility enforced: a Team-scoped competition is invisible to another Team; a Company-scoped one is visible to all KOC
- Supervisor rollup returns only the caller's subtree and rejects an out-of-subtree `orgUnitId`
- Tests pass

## 12. Risks and deferred work

- Trusted scorer plugins must be registered at startup; new plugins need a deploy
- Anti-leakage controls: ensure evaluation data path never leaks into upload paths
- Future: external competition mode is explicitly out of scope
