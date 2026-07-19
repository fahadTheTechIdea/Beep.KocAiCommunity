# Phase 06 — Collaboration and Discussions

**Status:** ✅ DONE (2026-07-19) — discussions/replies + notifications shipped earlier; this session added **emoji reactions, @mentions (+autocomplete), moderation (lock/pin/delete, audited), and attachments**.

## Implementation notes (2026-07-19)

- **Emoji reactions.** A single polymorphic `Reaction` table (target = "discussion" | "reply", unique per
  user+emoji) toggles a curated set (👍 ❤️ 🎉 💡 🚀 ✅ — `CommunityEmojis.Allowed`). Tallies carry a
  `Mine` flag. `POST /discussions/{id}/react` and `…/replies/{replyId}/react` return the new tallies;
  reusable `ReactionBar.razor` renders them.
- **@mentions.** On create/reply the body is scanned for `@token`; tokens resolve against KOC
  `UserProfile`s by normalized display name **or** user id, capped at 10 per post (mass-mention
  guard), and each mentioned user gets a `mention` notification. `GET /community/mention-candidates?q=`
  backs a `MudAutocomplete` that only suggests KOC users.
- **Moderation.** `IsLocked`/`IsPinned` on `Discussion`; lock (blocks replies), pin (sorts to top),
  and soft-delete of discussions/replies. Moderator = `PlatformAdmin` **or** an org-unit leader
  (`LedOrgUnitId`); authors can delete their own. Every action writes an `IAuditEnvelope` entry.
- **Attachments.** `DiscussionAttachment` stores files via `IArtifactService` (classification
  `Internal`); `POST /discussions/{id}/attachments` (multipart) + `GET /community/attachments/{id}`
  download, both visibility-scoped. Malware scanning remains a documented follow-up (see §12).
- **Migrations.** Dual-provider `AddCommunityInteractions` (Reactions/Mentions/DiscussionAttachments
  tables + IsLocked/IsPinned columns).
- **Tests.** 5 integration tests (`CommunityInteractionsTests`: reaction toggle, moderation authz +
  lock, author delete, mention notification + autocomplete, attachment upload/list/download). Whole
  solution builds `-warnaserror` clean; 87 unit + 58 integration tests pass.
**Scope change:** `UserProfile` (avatar/bio/skills) and the activity feed moved to Phase 05b, which also adds the fun layer on top of this phase: kudos buttons on discussion cards, Barrels XP for creating/replying (daily-capped), and the Community page tabs (Discussions / Activity / Kudos board / Team leaderboard) — see `05b_COMMUNITY_ENGAGEMENT_AND_GAMIFICATION.md`.
**Dependencies:** Phase 02, Phase 03, Phase 04
**Goal:** Build the internal collaboration surface scoped to KOC employees only.

## 1. Goal and dependencies

- Profiles for KOC employees (skills, interests, organization, avatar, contact)
- Discussions, replies, votes, attachments, moderation
- Notifications and mentions scoped to KOC employees only
- Activity feed scoped to projects the user belongs to
- No public profile visibility, no external followers, no public activity feed

## 2. Existing reference behavior

- Beep.AI.Community has `app/services/discussion_service.py`, `app/models/discussion.py`, `app/services/auth_service.py:30-119`.
- The new app narrows the surface to internal collaboration.

## 3. Architecture decisions

| Decision | Choice | Rationale |
|---|---|---|
| Profile source | First sign-in creates profile from Entra (`oid`, `name`, `email`, `department`, `jobTitle`) | Single source of truth |
| Profile edit | Department, job title are read-only from Entra; only skills/interests/avatar editable | KOC directory is authoritative |
| Discussion scope | Project or asset (well, field, reservoir, facility, HSE event) | Domain-aligned |
| Mention syntax | `@displayName` resolved server-side; only KOC users can be mentioned | Security |
| Notifications | In-app + email (configurable per user) | Standard |
| Moderation | The `discussion.moderate` permission can lock/pin/delete | Tiered permissions (granted to designated moderators, e.g. Team Leaders or PlatformAdmin) |
| Activity feed | Per-user, scoped to projects they belong to | Privacy |

## 4. Project-by-project deliverables

### 4.1 Domain

- `UserProfile`, `UserSkill`, `UserInterest`, `OrganizationUnit`
- `Discussion`, `DiscussionReply`, `DiscussionVote`, `DiscussionAttachment`
- `Notification`, `Mention`
- `ActivityEvent`

### 4.2 Application

- `IProfileService`, `IDiscussionService`, `INotificationService`, `IActivityService`
- DTO ↔ entity mapping

### 4.3 Infrastructure

- EF Core configurations for the entities
- Indexes for search (full-text on title/body), vote totals, and feed queries

### 4.4 API

- Endpoints for profiles, discussions, replies, votes, attachments, notifications, activity

### 4.5 UI

- `Pages/Profile/Index.razor`, `Pages/Profile/Edit.razor`
- `Pages/Discussions/Index.razor`, `Pages/Discussions/Thread.razor`, `Pages/Discussions/New.razor`
- `Pages/Notifications/Index.razor`
- `Pages/Activity/Index.razor`
- `Components/Discussion/ReplyEditor.razor`
- `Components/Discussion/MentionAutocomplete.razor`
- `Components/Discussion/AttachmentList.razor`

## 5. Entities and migrations

```csharp
public class UserProfile : AuditableEntity
{
    public string UserId { get; set; } = default!;
    public string DisplayName { get; set; } = default!;
    public string Email { get; set; } = default!;
    public string? Department { get; set; }
    public string? JobTitle { get; set; }
    public string? AvatarBlobId { get; set; }
    public string? Bio { get; set; }
    public string Status { get; set; } = "active";  // active, inactive, suspended
}

public class Discussion : AuditableEntity
{
    public string Title { get; set; } = default!;
    public string Body { get; set; } = default!;
    public string AuthorUserId { get; set; } = default!;
    public string ScopeType { get; set; } = default!;  // "project", "well", "field", "facility", "hse"
    public Guid ScopeId { get; set; }
    public string? Tags { get; set; }  // comma-separated
    public bool IsLocked { get; set; }
    public bool IsPinned { get; set; }
    public int ReplyCount { get; set; }
    public int VoteCount { get; set; }
}

public class DiscussionReply : AuditableEntity { /* ... */ }
public class DiscussionVote { /* UserId, DiscussionId, Value +1/-1, CreatedUtc */ }
public class DiscussionAttachment { /* DiscussionId, ArtifactReferenceId */ }
public class Notification : AuditableEntity { /* UserId, Type, PayloadJson, ReadUtc */ }
public class Mention : /* ReplyId, MentionedUserId */ { }
public class ActivityEvent : AuditableEntity { /* ActorUserId, Type, ResourceType, ResourceId, PayloadJson, Visibility */ }
```

## 6. API contracts

```http
GET    /api/v1/profiles/{userId}
PUT    /api/v1/profiles/me
GET    /api/v1/discussions?scopeType=&scopeId=&page=
POST   /api/v1/discussions
GET    /api/v1/discussions/{id}
PUT    /api/v1/discussions/{id}
DELETE /api/v1/discussions/{id}
POST   /api/v1/discussions/{id}/lock  (discussion.moderate)
POST   /api/v1/discussions/{id}/pin   (discussion.moderate)
POST   /api/v1/discussions/{id}/vote
DELETE /api/v1/discussions/{id}/vote
POST   /api/v1/discussions/{id}/replies
GET    /api/v1/discussions/{id}/replies
POST   /api/v1/discussions/{id}/replies/{replyId}/vote
GET    /api/v1/notifications?unreadOnly=
PUT    /api/v1/notifications/{id}/read
GET    /api/v1/activity?page=
```

## 7. MudBlazor pages and components

- `Pages/Discussions/Thread.razor` uses `MudGrid` + `MudList` + `MudTextField` + `MudFileUpload`
- Mention autocomplete uses `MudAutocomplete`
- Notifications use `MudBadge` + `MudMenu`

## 8. Security and authorization

- Only KOC employees can view, post, reply, vote, attach
- Holders of the `discussion.moderate` permission can lock/pin/delete
- @mention resolves to Entra `oid`; unknown users cannot be mentioned
- Attachments inherit classification from the discussion scope
- Activity visibility: project members, dataset collaborators, model owners

## 9. Tests

- Unit: vote aggregation, mention parsing, notification routing
- Integration: thread create + reply + vote + mention round-trip
- Integration: activity feed filtering
- Integration: attachment classification enforcement
- Component: reply editor, mention picker

## 10. Verification commands

```bash
dotnet test tests/Beep.KocAiCommunity.IntegrationTests --filter "FullyQualifiedName~Discussions"
```

## 11. Acceptance gate

- KOC user can post, reply, vote, attach
- Mention autocomplete only suggests KOC users
- Notifications appear in real time (SignalR)
- Activity feed scoped correctly
- Moderator actions are audited
- Tests pass

## 12. Risks and deferred work

- @mention abuse (mass mention) needs rate limiting; document
- Attachment scanning requires server-side malware check; defer to a follow-up
- Activity feed growth: archive older events
