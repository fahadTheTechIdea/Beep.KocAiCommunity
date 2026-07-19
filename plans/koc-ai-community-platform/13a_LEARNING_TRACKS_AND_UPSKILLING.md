# Phase 13a — Learning Tracks and Upskilling

**Status:** ✅ DONE (2026-07-19 audit) — tracks/lessons/enrollment/progress/completion, seeding, learn↔compete tie-in, and supervisor rollups all shipped.
**Engagement follow-up (Phase 05b):** lesson and track completion award Barrels XP (`lesson.completed` 25 bbl, `track.completed` 150 bbl), earn `first-track`/`all-tracks` badges, and feed streaks — turning every lesson into visible community progress. Printable certificates remain deferred. See `05b_COMMUNITY_ENGAGEMENT_AND_GAMIFICATION.md` §1.2–§1.3, §3.5.
**Dependencies:** Phase 02 (org hierarchy, roles, visibility), Phase 07 (datasets/projects), Phase 13 (competitions)
**Goal:** Guided, level-graded **learning tracks** that take a KOC employee from their first look at data to a dependable model — the "learn" half of *learn & compete*. Tracks carry lessons, enrollment, and progress; progress rolls up to supervisors; completing a track suggests a matching competition.

> **Why this phase exists.** The platform's primary purpose is to *train and familiarize KOC employees with AI/ML* (README decision 1). Competitions (Phase 13) are where employees practice; learning tracks are where they build the skill first. Together they are the center of gravity of the product. Employees are the primary learners; supervisors watch progress through the Phase 02 rollups.

## 1. Goal and dependencies

- Three seeded tracks, beginner → advanced, on real KOC O&G data:
  1. **Getting started with data** — read, clean, and make sense of a dataset; no coding. (~6 lessons)
  2. **Solve a real problem** — build a model for a production / facilities / subsurface question. (~8 lessons)
  3. **Make it dependable** — tune, check, and package a model a team can trust and reuse. (~7 lessons)
- Lessons with ordered content, estimated minutes, and optional hands-on steps that open the Studio canvas or a dataset
- Enrollment and per-lesson progress per employee
- Supervisory rollup of track progress (Team Leader → Team, Manager → Group, DCEO → Directorate, CEO → Company)
- Learning ↔ competition tie-in: a track may recommend a competition; a competition may recommend a track (`Competition.RecommendedTrackId`, Phase 13)
- Authoring by `LearningAdmin`; consumption by every `Employee`

## 2. Existing reference behavior

- No direct analog in Beep.AI.Community / Beep.AI.MLStudio — this is a new capability for the KOC training mission.
- Content authoring reuses the markdown + artifact-store patterns from datasets (Phase 07) and help content (Phase 14).
- Progress/enrollment modeling follows the immutable-version + status patterns already used for datasets and workflows.

## 3. Architecture decisions

| Decision | Choice | Rationale |
|---|---|---|
| Track structure | `LearningTrack` → ordered `Lesson`s | Simple, linear tracks for v1; branching deferred |
| Levels | `Beginner`, `Intermediate`, `Advanced` | Matches the three seeded tracks; drives ordering and badges |
| Lesson content | Markdown stored via `IArtifactStore`, referenced by `ContentRef` | Same governed storage as datasets; no inline HTML |
| Hands-on steps | Optional deep-link to a Studio workflow template or a dataset | Turns reading into doing without leaving the platform |
| Enrollment | One `TrackEnrollment` per (user, track) | Idempotent enroll; re-enroll resumes |
| Progress | One `LessonProgress` per (enrollment, lesson) | Percent-complete derived, not stored |
| Visibility | Tracks are `Company`-scoped by default (training is for everyone); a track may be narrowed to a Directorate | Uses the Phase 02 `VisibilityScope` model |
| Supervision | Progress rolls up read-only via `IOrgScopeResolver` | Managers see who is learning, never edit progress |
| Authoring | `LearningAdmin` (or `PlatformAdmin`) creates/publishes; drafts are immutable once published | Content governance |
| Completion | A track is complete when all its lessons are complete; issues a `TrackCompletion` record | Feeds badges + competition suggestions |

## 4. Project-by-project deliverables

### 4.1 Domain

- `LearningTrack`, `Lesson`, `TrackEnrollment`, `LessonProgress`, `TrackCompletion`
- `TrackLevel` enum (`Beginner`, `Intermediate`, `Advanced`)

### 4.2 Application

- `ILearningTrackService` (browse/read, author, publish)
- `IEnrollmentService` (enroll, record lesson progress, compute completion)
- `ILearningRollupService` (supervisory progress rollups, scoped by `IOrgScopeResolver`)
- DTO ↔ entity mapping

### 4.3 Infrastructure

- EF Core configurations
- Markdown content reader over `IArtifactStore`
- Seed data for the three starter tracks and their lessons (idempotent seeder)

### 4.4 API

- Track browse/read, enroll, lesson-complete, my-progress, authoring, supervisory rollup

### 4.5 UI

- `Pages/Learn/Index.razor` — track catalog (cards with level, lesson count, hours; the example's "Learn & compete" grid)
- `Pages/Learn/Track.razor` — track detail with lesson list and progress
- `Pages/Learn/Lesson.razor` — lesson content + hands-on launch + "mark complete"
- `Pages/Learn/MyLearning.razor` — the employee's enrollments and progress
- `Pages/Supervision/LearningRollup.razor` — supervisor view of track progress across the caller's subtree
- `Components/Learn/TrackCard.razor`, `Components/Learn/ProgressBar.razor`, `Components/Learn/LessonNav.razor`
- All MudBlazor; verified against `mudBlazor_Docs/`

## 5. Entities and migrations

```csharp
public enum TrackLevel { Beginner = 0, Intermediate = 1, Advanced = 2 }

public class LearningTrack : AuditableEntity
{
    public string Title { get; set; } = default!;
    public string Summary { get; set; } = default!;
    public TrackLevel Level { get; set; }
    public int OrderNo { get; set; }
    public string Status { get; set; } = "draft";       // draft, published, archived
    public string Domain { get; set; } = "upstream";     // upstream, midstream, downstream, hse
    public VisibilityScope VisibilityScope { get; set; } = VisibilityScope.Company;
    public Guid VisibilityOrgUnitId { get; set; }        // ignored for Company scope
    public Guid? RecommendedCompetitionId { get; set; }  // tie-in to Phase 13
}

public class Lesson : AuditableEntity
{
    public Guid TrackId { get; set; }
    public int OrderNo { get; set; }
    public string Title { get; set; } = default!;
    public string ContentRef { get; set; } = default!;   // markdown artifact reference
    public int EstimatedMinutes { get; set; }
    public string? HandsOnKind { get; set; }             // null, "workflow-template", "dataset"
    public Guid? HandsOnRefId { get; set; }              // WorkflowTemplate id or Dataset id
}

public class TrackEnrollment : AuditableEntity
{
    public Guid TrackId { get; set; }
    public string UserId { get; set; } = default!;
    public string Status { get; set; } = "active";       // active, completed, abandoned
    public DateTime StartedUtc { get; set; }
    public DateTime? CompletedUtc { get; set; }
}

public class LessonProgress : AuditableEntity
{
    public Guid EnrollmentId { get; set; }
    public Guid LessonId { get; set; }
    public string Status { get; set; } = "not-started";  // not-started, in-progress, completed
    public DateTime? CompletedUtc { get; set; }
}

public class TrackCompletion : AuditableEntity
{
    public Guid TrackId { get; set; }
    public string UserId { get; set; } = default!;
    public DateTime CompletedUtc { get; set; }
}
```

Indexes: unique `TrackEnrollment(TrackId, UserId)`, unique `LessonProgress(EnrollmentId, LessonId)`, `Lesson(TrackId, OrderNo)`, `LearningTrack(Status, OrderNo)`, `TrackCompletion(UserId, CompletedUtc DESC)`.

## 6. API contracts

```http
GET    /api/v1/tracks?level=&domain=&page=              # visible tracks (visibility-filtered)
GET    /api/v1/tracks/{id}
GET    /api/v1/tracks/{id}/lessons
GET    /api/v1/tracks/{id}/lessons/{lessonId}
POST   /api/v1/tracks/{id}/enroll                       # idempotent
POST   /api/v1/tracks/{id}/lessons/{lessonId}/complete
GET    /api/v1/me/learning                              # my enrollments + progress

# Authoring (LearningAdmin)
POST   /api/v1/tracks
PUT    /api/v1/tracks/{id}
POST   /api/v1/tracks/{id}/lessons
PUT    /api/v1/tracks/{id}/lessons/{lessonId}
POST   /api/v1/tracks/{id}/publish

# Supervision (RequireSupervisor; scoped to caller subtree)
GET    /api/v1/supervision/learning?orgUnitId=          # track-progress rollup for the subtree
```

## 7. MudBlazor pages and components

- Track catalog uses `MudCard` grid; progress uses `MudProgressLinear`
- Lesson content rendered from markdown (sanitized) inside `MudPaper`
- Rollup uses `MudDataGrid`
- All components verified against `mudBlazor_Docs/` before markup

## 8. Security and authorization

- `Employee` minimum to browse/enroll/complete (subject to track visibility)
- `LearningAdmin` (or `PlatformAdmin`) required to create/edit/publish tracks and lessons
- Track/lesson content read is visibility-filtered by `IVisibilityEvaluator`
- Lesson progress and completion are writable only by the owning employee; supervisors read rollups only, scoped to their subtree by `IOrgScopeResolver`
- Published tracks are immutable; edits create a new draft version (no silent content mutation under enrolled learners)

## 9. Tests

- Unit: completion computation (all lessons → track complete), progress idempotency, level ordering
- Unit: visibility filtering of the track catalog
- Integration: enroll → complete lessons → track completion + optional competition suggestion
- Integration: supervisory rollup returns only the caller's subtree; rejects out-of-subtree `orgUnitId`
- Component: track card, progress bar, lesson nav

## 10. Verification commands

```bash
dotnet test tests/Beep.KocAiCommunity.IntegrationTests --filter "FullyQualifiedName~Learning|FullyQualifiedName~Tracks"
```

Sign in as an `Employee`, enroll in "Getting started with data", complete its lessons, and confirm the track shows complete and suggests the recommended competition. Sign in as that employee's Team Leader and confirm the learning rollup shows their progress and nothing from another Team.

## 11. Acceptance gate

- Three starter tracks seeded and browsable, level-graded
- Enroll is idempotent; progress records per lesson; track completes when all lessons complete
- Completion suggests the recommended competition (when set)
- Visibility enforced: a Directorate-scoped track is invisible outside that Directorate; Company-scoped tracks visible to all
- Supervisory rollup is read-only and scoped to the caller's subtree
- Only `LearningAdmin`/`PlatformAdmin` can author/publish; published tracks are immutable
- Tests pass

## 12. Risks and deferred work

- Branching/adaptive tracks deferred — v1 tracks are linear
- Rich interactive lessons (quizzes, in-browser notebooks) deferred; notebook execution stays disabled (Phase 07 policy)
- Certificates/badges beyond a simple `TrackCompletion` record are deferred
- Authoring UI richness (WYSIWYG) is minimal in v1; markdown only
