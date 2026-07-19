# Phase 05b — Community Engagement and Gamification

**Status:** ✅ DONE (implemented 2026-07-19) — full vertical slice shipped: domain + service + seeder + dual-provider `AddEngagement` migrations + 10 API endpoints + celebration hub routing + typed client + Profile page, Home greeting/standing, app-bar Barrels/streak chip, Community tabs (Leaderboards + Activity), kudos dialog, confetti. Builds `-warnaserror` clean; 22 engagement unit tests + 5 integration tests pass (60 + 42 total, no regressions).
**Dependencies:** Phase 02 (RBAC/org tree — DONE), Phase 03 (EF Core — DONE), Phase 04 (API/SignalR/outbox — DONE), Phase 05 (shell — DONE), Phase 06 (discussions — PARTIAL), Phase 13 (competitions — DONE), Phase 13a (learning — DONE)
**Goal:** Make the platform feel like a fun, social, KOC-flavored community — not an enterprise tool. Employees earn **Barrels (bbl)** for learning and competing, climb an O&G career ladder, collect badges drawn from the existing O&G icon library, give each other kudos, and see their Team/Group/Directorate battle on team leaderboards.

> This phase deliberately builds ONLY on code that already exists and is verified working:
> `KocDbContext`, `AuditableEntity`, `VisibilityScope`, `OrgUnit`/`OrgMembership`, `OutboxWriter`,
> `LeaderboardHub`, `NotificationService` + `NotificationBell.razor`, `KocBrand.Icon(...)`,
> `LearningService`, `CompetitionService`, `CommunityService`.

---

## 1. Design principles (the "fun" contract)

| Principle | How it shows up |
|---|---|
| **KOC identity, not generic gamification** | Points are **Barrels (bbl)**; levels are an O&G career ladder; badge art comes from `Ui.Shared/wwwroot/icons/` (236 O&G icons already shipped) |
| **Team pride** | Org-unit leaderboards (Team vs Team, Group vs Group) computed from the existing `OrgMembership` tree — Kuwaiti workplace culture is team-oriented; rivalry between Directorates drives adoption |
| **Celebrate, never shame** | Leaderboards show top 10 + "your rank"; no bottom-of-list view; streak loss is silent |
| **Everything already earns** | XP hooks go into the three services that already work: `LearningService` (lessons), `CompetitionService` (submissions), `CommunityService` (discussions/replies) — no new user behavior required to start earning |
| **Warm, personal shell** | Home greets by first name with time-of-day greeting (incl. Arabic "صباح الخير" variant), streak flame in the app bar, XP ring on the avatar |
| **Micro-delight** | Confetti burst on badge earn / competition win, animated rank-change arrows on the live leaderboard, snackbar toasts with badge art |

### 1.1 The career ladder (levels)

Barrels thresholds and titles (constant table, no DB):

| Level | Title | bbl needed |
|---|---|---|
| 1 | Roustabout | 0 |
| 2 | Roughneck | 100 |
| 3 | Derrickhand | 300 |
| 4 | Driller | 700 |
| 5 | Toolpusher | 1,500 |
| 6 | Field Engineer | 3,000 |
| 7 | Reservoir Analyst | 6,000 |
| 8 | Chief Geoscientist | 12,000 |

### 1.2 XP award table (single source of truth)

| Source code | Trigger (existing code path) | bbl |
|---|---|---|
| `lesson.completed` | `LearningService` lesson progress completion | 25 |
| `track.completed` | `LearningService` track completion row | 150 |
| `submission.scored` | `CompetitionService` successful scoring | 20 |
| `submission.first` | first-ever scored submission (badge check) | 50 bonus |
| `competition.top3` | rank ≤ 3 at reveal | 300 |
| `discussion.created` | `CommunityService.CreateDiscussion` | 10 |
| `discussion.replied` | `CommunityService` reply | 5 |
| `kudos.received` | `EngagementService.GiveKudosAsync` | 15 |
| `streak.week` | 7 consecutive active days | 70 |

Daily earn cap for `discussion.*` sources: 50 bbl/day (anti-spam).

### 1.3 Badge catalog (seeded, icon = existing file in `Ui.Shared/wwwroot/icons/`)

| Code | Name | Icon file | Awarded when |
|---|---|---|---|
| `first-barrel` | First Barrel | `008-oil.png` | first XP event |
| `first-submission` | Wildcatter | `179-exploration.png` | first competition submission scored |
| `competition-winner` | Gusher | `039-oil-well.png` | rank 1 at competition reveal |
| `podium` | On the Podium | `100-goal.png` | rank ≤ 3 at reveal |
| `first-track` | Certified | `132-training.png` | first learning track completed |
| `all-tracks` | Master Operator | `083-engineering.png` | all published tracks completed |
| `streak-7` | Steady Pump | `220-pump-jack.png` | 7-day streak |
| `streak-30` | Non-Stop Flow | `016-pipeline.png` | 30-day streak |
| `helper-10` | Good Neighbor | `118-approval.png` | 10 kudos received |
| `discussion-starter` | Conversation Driller | `065-drilling-1.png` | 5 discussions created |
| `team-player` | Team Player | `105-management.png` | member of a team that wins a team challenge |

---

## 2. Existing code reference (verified)

| What | File | Notes |
|---|---|---|
| Base entity | `src/Beep.KocAiCommunity.Domain/Common/AuditableEntity.cs` | all new entities inherit |
| Visibility | `src/Beep.KocAiCommunity.Domain/Organization/OrgEnums.cs` | `VisibilityScope` enum (Team/Group/Directorate/Company) |
| Org tree | `src/Beep.KocAiCommunity.Domain/Organization/OrgUnit.cs`, `OrgMembership.cs` | team leaderboard joins on these |
| DbContext | `src/Beep.KocAiCommunity.Infrastructure/Persistence/KocDbContext.cs` | add `DbSet`s here |
| DI | `src/Beep.KocAiCommunity.Infrastructure/DependencyInjection.cs` | register `IEngagementService` |
| Outbox | `src/Beep.KocAiCommunity.Infrastructure/Messaging/OutboxWriter.cs` | badge/kudos events → SignalR |
| Notifications | `src/Beep.KocAiCommunity.Infrastructure/Notifications/NotificationService.cs`, `src/Beep.KocAiCommunity.Web/Components/Layout/NotificationBell.razor` | reuse for kudos/badge toasts |
| Realtime hub | `LeaderboardHub` (mapped in `src/Beep.KocAiCommunity.Api/Program.cs`) | pattern to copy for `CommunityHub` |
| Brand/icons | `src/Beep.KocAiCommunity.Ui.Shared/Branding/KocBrand.cs` — `KocBrand.Icon("009-pump.png")` | badge art |
| Current user | `src/Beep.KocAiCommunity.Application/Security/IKocCurrentUser.cs` | user id + display name |
| Hook: learning | `src/Beep.KocAiCommunity.Infrastructure/Learning/LearningService.cs` | award on lesson/track completion |
| Hook: competitions | `src/Beep.KocAiCommunity.Infrastructure/Competitions/CompetitionService.cs` | award on scored submission + reveal |
| Hook: community | `src/Beep.KocAiCommunity.Infrastructure/Community/CommunityService.cs` | award on create/reply |
| Pages | `src/Beep.KocAiCommunity.Web/Components/Pages/` (`Home.razor`, `Community.razor`, `Compete.razor`, `Learn.razor`) | surfaces to upgrade |

---

## 3. Implementation specs

### 3.1 Domain entities

**File**: `src/Beep.KocAiCommunity.Domain/Engagement/EngagementEntities.cs`
**Action**: New

```csharp
using Beep.KocAiCommunity.Domain.Common;
using Beep.KocAiCommunity.Domain.Organization;

namespace Beep.KocAiCommunity.Domain.Engagement;

/// <summary>A KOC employee's community profile. Created lazily on first engagement read.</summary>
public class UserProfile : AuditableEntity
{
    public string UserId { get; set; } = default!;          // Entra oid — same key style as OrgMembership
    public string DisplayName { get; set; } = default!;
    public string? Bio { get; set; }                        // max 280
    public string AvatarIcon { get; set; } = "185-worker.png"; // file name resolved via KocBrand.Icon()
    public string? SkillsCsv { get; set; }                  // "ML.NET,Python,Reservoir"
    public int XpTotal { get; set; }
    public int Level { get; set; } = 1;
    public int CurrentStreakDays { get; set; }
    public int LongestStreakDays { get; set; }
    public DateOnly? LastActiveDate { get; set; }
}

/// <summary>Append-only XP ledger. XpTotal on UserProfile is a maintained rollup.</summary>
public class XpEvent : AuditableEntity
{
    public string UserId { get; set; } = default!;
    public string Source { get; set; } = default!;          // "lesson.completed", "submission.scored", ...
    public int Points { get; set; }
    public string? RefType { get; set; }                    // "lesson", "submission", "discussion", "kudos"
    public Guid? RefId { get; set; }
}

/// <summary>Seeded badge catalog row.</summary>
public class Badge : AuditableEntity
{
    public string Code { get; set; } = default!;            // "first-submission"
    public string Name { get; set; } = default!;
    public string Description { get; set; } = default!;
    public string IconFile { get; set; } = default!;        // "179-exploration.png"
    public string Tier { get; set; } = "bronze";            // bronze, silver, gold
}

public class UserBadge : AuditableEntity
{
    public string UserId { get; set; } = default!;
    public string BadgeCode { get; set; } = default!;
    public Guid? RefId { get; set; }
}

/// <summary>Peer-to-peer recognition. Both sides must be KOC employees.</summary>
public class Kudos : AuditableEntity
{
    public string FromUserId { get; set; } = default!;
    public string ToUserId { get; set; } = default!;
    public string Message { get; set; } = default!;         // max 200
    public string Emoji { get; set; } = "👏";               // curated set: 👏 🚀 🛢️ 🌟 🤝
    public string? RefType { get; set; }                    // optional link: "submission", "discussion"
    public Guid? RefId { get; set; }
}

/// <summary>Org-scoped activity feed row, written alongside domain actions.</summary>
public class ActivityEvent : AuditableEntity
{
    public string ActorUserId { get; set; } = default!;
    public string Type { get; set; } = default!;            // "badge.earned", "competition.joined", ...
    public string? RefType { get; set; }
    public Guid? RefId { get; set; }
    public string? PayloadJson { get; set; }
    public VisibilityScope VisibilityScope { get; set; } = VisibilityScope.Team;
    public Guid VisibilityOrgUnitId { get; set; }
}
```

### 3.2 Level table

**File**: `src/Beep.KocAiCommunity.Domain/Engagement/KocLevels.cs`
**Action**: New

```csharp
namespace Beep.KocAiCommunity.Domain.Engagement;

/// <summary>The O&amp;G career ladder. Pure lookup — no DB.</summary>
public static class KocLevels
{
    public static readonly (int Level, string Title, int MinXp)[] Ladder =
    [
        (1, "Roustabout", 0), (2, "Roughneck", 100), (3, "Derrickhand", 300),
        (4, "Driller", 700), (5, "Toolpusher", 1500), (6, "Field Engineer", 3000),
        (7, "Reservoir Analyst", 6000), (8, "Chief Geoscientist", 12000),
    ];

    public static (int Level, string Title, int MinXp, int? NextXp) ForXp(int xp)
    {
        var current = Ladder[0];
        int? next = null;
        for (var i = 0; i < Ladder.Length; i++)
        {
            if (xp >= Ladder[i].MinXp) { current = Ladder[i]; next = i + 1 < Ladder.Length ? Ladder[i + 1].MinXp : null; }
        }
        return (current.Level, current.Title, current.MinXp, next);
    }
}
```

### 3.3 Application interface

**File**: `src/Beep.KocAiCommunity.Application/Engagement/IEngagementService.cs`
**Action**: New

```csharp
namespace Beep.KocAiCommunity.Application.Engagement;

public interface IEngagementService
{
    Task<ProfileDto> GetProfileAsync(string userId, CancellationToken ct);
    Task<ProfileDto> UpdateMyProfileAsync(UpdateProfileRequest request, CancellationToken ct);

    /// <summary>Idempotent per (userId, source, refId). Applies daily caps, rolls up XpTotal,
    /// recomputes Level, touches the streak, runs badge rules, writes ActivityEvent + outbox.</summary>
    Task AwardXpAsync(string userId, string source, string? refType, Guid? refId, CancellationToken ct);

    Task<IReadOnlyList<XpLeaderboardRow>> GetXpLeaderboardAsync(LeaderboardPeriod period, CancellationToken ct);
    Task<IReadOnlyList<TeamLeaderboardRow>> GetTeamLeaderboardAsync(LeaderboardPeriod period, CancellationToken ct);
    Task<IReadOnlyList<BadgeDto>> GetBadgesAsync(string userId, CancellationToken ct);
    Task GiveKudosAsync(GiveKudosRequest request, CancellationToken ct);
    Task<IReadOnlyList<ActivityDto>> GetActivityFeedAsync(int page, CancellationToken ct);
}

public enum LeaderboardPeriod { Week, Month, AllTime }
```

DTOs live in `src/Beep.KocAiCommunity.Contracts/Engagement/EngagementDtos.cs` (New) following the shape of the existing contracts project.

### 3.4 Infrastructure service

**File**: `src/Beep.KocAiCommunity.Infrastructure/Engagement/EngagementService.cs`
**Action**: New (~350 lines)

Key rules (mirror the tone of `CompetitionService.cs`):
- `AwardXpAsync` is **idempotent**: unique index on `XpEvent(UserId, Source, RefId)`; a duplicate insert is a no-op (catch `DbUpdateException` on the unique index, return).
- Streak: if `LastActiveDate == today` → no change; `== yesterday` → `CurrentStreakDays++`; else reset to 1. Award `streak.week` XP + `streak-7`/`streak-30` badges at thresholds.
- Team leaderboard: `XpEvent` (period-filtered) `JOIN OrgMembership ON UserId` `GROUP BY OrgUnitId`, average bbl per member (average, not sum — fair to small teams), top 10, resolved to `OrgUnit.Name`.
- Every badge earn: `NotificationService` row + `OutboxWriter` message (`type: "engagement.badge"`) so the dispatcher pushes it over SignalR — same path `OutboxDispatcher` already uses for the leaderboard.

**File**: `src/Beep.KocAiCommunity.Infrastructure/Engagement/BadgeRules.cs`
**Action**: New — one static rule class evaluating the §1.3 table given the user's ledger; returns newly earned codes.

**File**: `src/Beep.KocAiCommunity.Infrastructure/Engagement/EngagementSeeder.cs`
**Action**: New — idempotent badge catalog seeding (same pattern as `Learning/LearningSeeder.cs` and `Competitions/CompetitionSeeder.cs`).

### 3.5 XP hooks in existing services

| File | Action | Hook |
|---|---|---|
| `src/Beep.KocAiCommunity.Infrastructure/Learning/LearningService.cs` | Modify | after lesson-progress completion → `AwardXpAsync(userId, "lesson.completed", "lesson", lessonId)`; after track completion → `"track.completed"` |
| `src/Beep.KocAiCommunity.Infrastructure/Competitions/CompetitionService.cs` | Modify | after a submission is scored → `"submission.scored"`; at reveal, ranks ≤ 3 → `"competition.top3"` + `podium`/`competition-winner` badges |
| `src/Beep.KocAiCommunity.Infrastructure/Community/CommunityService.cs` | Modify | on create → `"discussion.created"`; on reply → `"discussion.replied"` |

Hooks call `IEngagementService` injected via constructor; failures in XP award must never fail the parent operation (log warning, continue).

### 3.6 Persistence

**File**: `src/Beep.KocAiCommunity.Infrastructure/Persistence/Configurations/EngagementConfigurations.cs`
**Action**: New — indexes:
- `UserProfile.UserId` unique
- `XpEvent (UserId, Source, RefId)` unique; `XpEvent (CreatedUtc)` for period queries
- `Badge.Code` unique; `UserBadge (UserId, BadgeCode)` unique
- `Kudos (ToUserId, CreatedUtc)`; `ActivityEvent (VisibilityOrgUnitId, CreatedUtc)`

**File**: `src/Beep.KocAiCommunity.Infrastructure/Persistence/KocDbContext.cs`
**Action**: Modify — add `DbSet<UserProfile>`, `DbSet<XpEvent>`, `DbSet<Badge>`, `DbSet<UserBadge>`, `DbSet<Kudos>`, `DbSet<ActivityEvent>`.

**Migrations**: `AddEngagement` in both `Infrastructure/Persistence/Migrations` (SQLite) and `Infrastructure.SqlServerMigrations` — same dual-provider flow as `AddProjects`.

### 3.7 API endpoints

**File**: `src/Beep.KocAiCommunity.Api/Endpoints/EngagementEndpoints.cs`
**Action**: New — mapped in `Program.cs` beside the existing 11 endpoint groups.

```http
GET  /api/v1/profiles/me
GET  /api/v1/profiles/{userId}
PUT  /api/v1/profiles/me                       (bio, avatarIcon, skillsCsv only)
GET  /api/v1/engagement/leaderboard?period=week|month|all
GET  /api/v1/engagement/teams?period=week|month|all
GET  /api/v1/engagement/badges/{userId}
GET  /api/v1/engagement/badges/catalog
POST /api/v1/engagement/kudos                  { toUserId, message, emoji, refType?, refId? }
GET  /api/v1/engagement/activity?page=
```

All require the Employee policy (`KocPolicies`); no anonymous surface. `avatarIcon` is validated against an allowlist derived from `Ui.Shared/wwwroot/icons/` file names — never a free-form path.

### 3.8 Realtime

**File**: `src/Beep.KocAiCommunity.Api/Hubs/CommunityHub.cs`
**Action**: New — same shape as `LeaderboardHub`; groups per org unit. `OutboxDispatcher` (Modify) routes `engagement.*` outbox messages to it:
- `engagement.badge` → `{ userId, badgeCode, badgeName, iconFile }`
- `engagement.kudos` → `{ toUserId, fromDisplayName, emoji }`
- `engagement.levelup` → `{ userId, level, title }`

### 3.9 Web UI (the fun layer)

| File | Action | Content |
|---|---|---|
| `src/Beep.KocAiCommunity.Web/Components/Pages/Profile.razor` | New | `@page "/profile"` + `@page "/profile/{UserId}"` — avatar picker (`MudGrid` of O&G icons via `KocBrand.Icon`), XP ring (`MudProgressCircular` around avatar), level title chip, badge wall (`MudTooltip` per badge), streak flame, kudos wall, recent activity |
| `src/Beep.KocAiCommunity.Web/Components/Pages/Home.razor` | Modify | personalized hero: time-of-day greeting (incl. "صباح الخير" / "مساء الخير" when culture is `ar`), streak flame + bbl count, "continue where you left off" (active track / open competition), **Spotlight carousel** (`MudCarousel`): latest competition winner, newest badges in your org, top team of the week |
| `src/Beep.KocAiCommunity.Web/Components/Layout/MainLayout.razor` | Modify | app-bar chip: 🔥 streak + bbl total (click → `/profile`); subscribes to `CommunityHub` and fires celebration on `engagement.badge` / `engagement.levelup` |
| `src/Beep.KocAiCommunity.Web/Components/Pages/Community.razor` | Modify | tabs: Discussions / **Activity feed** / **Kudos board** / **Team leaderboard**; give-kudos button on every discussion card |
| `src/Beep.KocAiCommunity.Web/Components/Pages/Compete.razor` | Modify | medal styling for top-3 rows (gold/silver/bronze row tint), animated rank-change arrows on SignalR update, team-standings tab |
| `src/Beep.KocAiCommunity.Web/Components/Dialogs/GiveKudosDialog.razor` | New | recipient (from context), emoji picker (curated 5), 200-char message, POST `/api/v1/engagement/kudos` |
| `src/Beep.KocAiCommunity.Web/Components/Shared/BadgeChip.razor` | New | badge icon + name + tier ring; reused on Profile, Home spotlight, notifications |
| `src/Beep.KocAiCommunity.Web/Components/Shared/XpRing.razor` | New | avatar + circular progress toward next level + level tooltip ("340 / 700 bbl to Driller") |
| `src/Beep.KocAiCommunity.Web/wwwroot/js/celebrations.js` | New | `window.kocCelebrate(kind)` — canvas confetti in KOC palette (`#1466A5`, `#5FA3D4`, gold); respects `prefers-reduced-motion` |
| `src/Beep.KocAiCommunity.Ui.Shared/Theme/KocTheme.cs` | Modify | add success/celebration tokens: gold `#D4A017`, flame `#E8590C`; keep petroleum blue primary |

### 3.10 Notifications integration

`NotificationBell.razor` (Modify): kudos and badge notifications render with the badge icon / sender emoji instead of the generic icon; clicking navigates to `/profile`.

---

## 4. Files summary

| Action | File | Est. |
|--------|------|------|
| New | `Domain/Engagement/EngagementEntities.cs` | ~90 |
| New | `Domain/Engagement/KocLevels.cs` | ~30 |
| New | `Application/Engagement/IEngagementService.cs` | ~40 |
| New | `Contracts/Engagement/EngagementDtos.cs` | ~60 |
| New | `Infrastructure/Engagement/EngagementService.cs` | ~350 |
| New | `Infrastructure/Engagement/BadgeRules.cs` | ~80 |
| New | `Infrastructure/Engagement/EngagementSeeder.cs` | ~60 |
| New | `Infrastructure/Persistence/Configurations/EngagementConfigurations.cs` | ~70 |
| Modify | `Infrastructure/Persistence/KocDbContext.cs` | +8 |
| New | migrations `AddEngagement` (SQLite + SqlServer) | gen |
| Modify | `Infrastructure/Learning/LearningService.cs` | +15 |
| Modify | `Infrastructure/Competitions/CompetitionService.cs` | +25 |
| Modify | `Infrastructure/Community/CommunityService.cs` | +12 |
| Modify | `Infrastructure/DependencyInjection.cs` | +3 |
| New | `Api/Endpoints/EngagementEndpoints.cs` | ~120 |
| New | `Api/Hubs/CommunityHub.cs` | ~30 |
| Modify | `Api/Program.cs` + `OutboxDispatcher.cs` | +20 |
| New | `Web/Components/Pages/Profile.razor` | ~250 |
| Modify | `Web/Components/Pages/Home.razor` | +120 |
| Modify | `Web/Components/Layout/MainLayout.razor` | +40 |
| Modify | `Web/Components/Pages/Community.razor` | +100 |
| Modify | `Web/Components/Pages/Compete.razor` | +60 |
| New | `Web/Components/Dialogs/GiveKudosDialog.razor` | ~80 |
| New | `Web/Components/Shared/BadgeChip.razor` | ~40 |
| New | `Web/Components/Shared/XpRing.razor` | ~50 |
| New | `Web/wwwroot/js/celebrations.js` | ~60 |
| Modify | `Ui.Shared/Theme/KocTheme.cs` | +6 |
| Modify | `Web/Components/Layout/NotificationBell.razor` | +20 |

---

## 5. Security and authorization

- All engagement endpoints require the Employee policy; kudos/profile views are company-internal only.
- `AwardXpAsync` is server-side only — no endpoint lets a client grant XP.
- Kudos rate limit: max 10 given per user per day (server-enforced, mirrors the submission quota pattern in `CompetitionService`).
- `avatarIcon` allowlist prevents path traversal; `Bio`/`Message` are length-capped and HTML-encoded on render (Blazor default).
- Team leaderboards show org-unit aggregates only — no individual rows outside the caller's own visibility scope (reuse `IOrgScopeResolver`).
- XP ledger is auditable (`AuditableEntity`); admin can void an `XpEvent` via a future 14a admin page (documented, not built here).

## 6. Tests

- Unit: `KocLevels.ForXp` boundaries; `BadgeRules` for each §1.3 rule; streak transitions (same-day, next-day, gap).
- Unit: XP idempotency — same `(userId, source, refId)` twice awards once.
- Integration: complete a lesson → XP row + profile rollup + `first-barrel` badge + notification + outbox message.
- Integration: scored submission → XP; reveal with rank 1 → `competition-winner` + `competition.top3`.
- Integration: kudos round-trip incl. daily cap at 10; discussion XP daily cap at 50.
- Integration: team leaderboard = average-per-member across `OrgMembership`, visibility-scoped.
- Component: `XpRing` renders progress; `GiveKudosDialog` posts; Home greeting varies with time and `ar` culture.

## 7. Verification commands

```bash
dotnet build --no-restore -warnaserror
dotnet test tests/Beep.KocAiCommunity.UnitTests --filter "FullyQualifiedName~Engagement"
dotnet test tests/Beep.KocAiCommunity.IntegrationTests --filter "FullyQualifiedName~Engagement"
```

## 8. Acceptance gate

- A fresh employee who completes one lesson sees: bbl counter appear in the app bar, `first-barrel` badge toast with confetti, and their row on the weekly leaderboard.
- A scored competition submission raises the streak flame the same day.
- Kudos sent from a discussion card arrives as a real-time bell notification for the recipient.
- Team leaderboard ranks org units by average bbl and never leaks individual names across scope.
- `prefers-reduced-motion` disables confetti; all new UI passes the Phase 05 accessibility smoke tests.
- All four global commands pass (`restore`, `format`, `build -warnaserror`, `test`).

## 9. Risks and deferred work

- Emoji rendering on older corporate browsers — curated set only, with PNG fallback if needed.
- XP economy tuning (inflation) — values in §1.2 are constants in one file; revisit after 1 month of telemetry.
- Deferred: seasonal resets ("Quarters"), printable certificates (13a), admin XP void page (14a), Arabic full localization (shell strings only in this phase).
