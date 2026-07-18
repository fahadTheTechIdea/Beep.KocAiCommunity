# Phase 02 — Entra ID, Security, RBAC, and KOC Org Hierarchy

**Status:** 🟡 PLANNING
**Dependencies:** Phase 01
**Goal:** Configure Microsoft Entra ID authentication, KOC **position-level** app roles, the **KOC org hierarchy** (Team/Group/Directorate/Company), **supervisory rollup scoping**, the **org-scoped visibility** model, EF-backed resource permissions, and the audit envelope.

## 1. Goal and dependencies

- Single-tenant KOC Entra: only the configured KOC tenant ID is accepted
- Web OIDC sign-in flow (server-side)
- API JWT bearer validation (downstream API)
- App roles → ASP.NET Core role claims → authorization policies
- **KOC org hierarchy**: `OrgUnit` tree Team ⊂ Group ⊂ Directorate ⊂ Company (KOC); every person resolves to a home Team and a position level
- **Position levels**: `Employee → TeamLeader → Manager → DCEO → CEO` (Team Leader→Team, Manager→Group, DCEO→Directorate, CEO→Company)
- **Supervisory scope**: each level resolves the set of org units (and people) in the subtree beneath it, for read-only rollup dashboards
- **Org-scoped visibility**: `VisibilityScope` (Team/Group/Directorate/Company) + `VisibilityOrgUnitId` on competitions, datasets, and projects; a "who can see this" selector at creation
- Resource permissions per business entity
- First-admin bootstrap
- Audit envelope middleware

## 2. Existing reference behavior

- Beep.OilandGas.Web uses OIDC scheme + cookie scheme, OnTokenValidated handler, TokenProvider singleton, TokenHandler delegating handler (`Program.cs:83-92, 109-331`).
- Beep.AI.Server uses `@admin_required` decorator (`Beep.AI.Server/Beep.AI.Server/app/utils/permissions.py:185`).
- Microsoft Entra tutorial: [Prepare a web application for authentication](https://learn.microsoft.com/en-us/entra/identity-platform/tutorial-web-app-dotnet-prepare-app).

## 3. Architecture decisions

| Decision | Choice | Rationale |
|---|---|---|
| Library | Microsoft.Identity.Web 4.13.2 | Microsoft-blessed Entra integration |
| Token storage | Server-side only (cookie + backend cache) | Tokens never in browser storage |
| Tenant | KOC tenant only, enforced | Single-tenant KOC deployment |
| Position roles | `Employee`, `TeamLeader`, `Manager`, `DCEO`, `CEO` from Entra app roles → ASP.NET `ClaimTypes.Role` | Mirrors the KOC reporting line |
| Function roles | `PlatformAdmin`, `CompetitionAdmin`, `LearningAdmin`, `Auditor` (additive) | Platform capabilities granted on top of a position |
| Org hierarchy | `OrgUnit` self-referencing tree (Company→Directorate→Group→Team) | Single source of truth for supervision and visibility |
| Position ↔ org unit | `TeamLeader` leads a Team, `Manager` a Group, `DCEO` a Directorate, `CEO` the Company | Determines the supervisory subtree |
| Supervisory scope | `IOrgScopeResolver` returns the org-unit subtree + member set for the current user | Read-only rollups; never write access to reports' work |
| Visibility | `VisibilityScope` + `VisibilityOrgUnitId` on competition/dataset/project; `IVisibilityEvaluator` checks subtree membership | Creator picks Team/Group/Directorate/Company at creation |
| Policies | `RequireEmployee`, `RequirePlatformAdmin`, `RequireCompetitionAdmin`, supervisory + resource policies | DRY policy registration |
| Permissions table | EF-backed `UserEntityPermission` table | Defense-in-depth on top of coarse roles and visibility |
| First-admin bootstrap | First Entra user to sign in becomes PlatformAdmin if DB has zero admins | Avoids impossible initial state |
| Audit envelope | Middleware that wraps every request, captures ActorUserId, Action, Resource | Required by Phase 14a |

## 4. Project-by-project deliverables

### 4.1 Application/Auth

- `KocAuthOptions` (TenantId, ClientId, Audience, Scopes, AppRoleMap)
- `IKocCurrentUser` interface returning `UserId`, `DisplayName`, `Roles`, `PositionLevel`, `HomeOrgUnitId`, `LedOrgUnitId`, `Scopes`
- `KocCurrentUser` implementation backed by `IHttpContextAccessor` and the org directory
- `RequireEmployeeAttribute` and `RequirePlatformAdminAttribute` (resource-based)

### 4.1a Application/Org

- `IOrgDirectory` — resolves a person to their `HomeOrgUnit`, position level, and (for leaders) the `OrgUnit` they lead
- `IOrgScopeResolver` — given the current user, returns the set of descendant `OrgUnit` ids and member user ids in their supervisory subtree (Employee → self only)
- `IVisibilityEvaluator` — `CanSee(user, VisibilityScope scope, Guid visibilityOrgUnitId)` returns true when the user's home org unit is within the visibility subtree (Company scope = always true for KOC users)
- `OrgSyncService` — reconciles `OrgUnit`, person position level, and org membership from the KOC HR/Entra source (seeded manually in dev/test)

### 4.2 Web/Auth

- `AddKocAuthentication(builder.Configuration)` extension
- Microsoft.Identity.Web OIDC + cookie scheme
- OnTokenValidated handler maps Entra app roles to `ClaimTypes.Role`
- Tenant ID claim validation
- `MapInboundClaims = false`
- `NameClaimType = "name"`, `RoleClaimType = "role"`
- `Cookie.SameSite = Lax`, `HttpOnly`, `SecurePolicy = Always`

### 4.3 API/Auth

- `AddKocJwtBearer(builder.Configuration)` extension
- Validates tenant, audience, issuer, signing key
- Maps Entra app roles to `ClaimTypes.Role`

### 4.4 Application/Audit

- `IAuditEnvelope` interface
- `AuditEnvelopeMiddleware` (HTTP)
- `AuditEnvelope` (background)

### 4.5 Domain/Entities

- `OrgUnit(Id, Name, Type: Company|Directorate|Group|Team, ParentId, Path, LeaderUserId?)`
- `OrgMembership(Id, UserId, OrgUnitId, PositionLevel, IsPrimary, FromUtc, ToUtc?)`
- `UserEntityPermission(Id, UserId, ResourceType, ResourceId, PermissionKey, GrantedUtc, GrantedByUserId, ExpiresUtc)`
- `UserAuditSummary` view (read model)
- Shared `VisibilityScope` enum (`Team`, `Group`, `Directorate`, `Company`) reused by dataset/project/competition entities

### 4.6 Infrastructure/Audit

- `AuditEnvelopeService` writes to `AdminAuditLog` table inside the request transaction where possible

## 5. Entities and migrations

```csharp
public class UserEntityPermission
{
    public Guid Id { get; set; }
    public string UserId { get; set; } = default!;
    public string ResourceType { get; set; } = default!;  // "project", "dataset", "workflow", etc.
    public Guid ResourceId { get; set; }
    public string PermissionKey { get; set; } = default!;  // e.g. "project.write"
    public DateTime GrantedUtc { get; set; }
    public string GrantedByUserId { get; set; } = default!;
    public DateTime? ExpiresUtc { get; set; }
}
```

Index on `(UserId, ResourceType, ResourceId)`. Compound unique index on `(UserId, ResourceType, ResourceId, PermissionKey)`.

### 5.1 Org hierarchy and position

```csharp
public enum OrgUnitType { Company = 0, Directorate = 1, Group = 2, Team = 3 }

public enum PositionLevel { Employee = 0, TeamLeader = 1, Manager = 2, DCEO = 3, CEO = 4 }

public enum VisibilityScope { Team = 0, Group = 1, Directorate = 2, Company = 3 }

public class OrgUnit : AuditableEntity
{
    public string Name { get; set; } = default!;
    public OrgUnitType Type { get; set; }
    public Guid? ParentId { get; set; }              // null only for the single Company root
    public string Path { get; set; } = default!;     // materialized path, e.g. "/koc/exploration/subsurface/reservoir-analytics"
    public string? LeaderUserId { get; set; }        // TeamLeader / Manager / DCEO / CEO of this unit
}

public class OrgMembership : AuditableEntity
{
    public string UserId { get; set; } = default!;
    public Guid OrgUnitId { get; set; }              // usually a Team; leaders also lead a higher unit via OrgUnit.LeaderUserId
    public PositionLevel PositionLevel { get; set; }
    public bool IsPrimary { get; set; } = true;
    public DateTime FromUtc { get; set; }
    public DateTime? ToUtc { get; set; }
}
```

Indexes: `OrgUnit(ParentId)`, `OrgUnit(Path)` (prefix scans for subtree queries), `OrgUnit(Type, LeaderUserId)`, unique `OrgMembership(UserId, IsPrimary) WHERE IsPrimary = 1`, `OrgMembership(OrgUnitId)`.

Subtree resolution uses the materialized `Path`: a supervisor's subtree is every `OrgUnit` whose `Path` begins with the supervisor's led-unit `Path`. `IOrgScopeResolver` caches the current user's subtree per request.

### 5.2 Visibility columns (applied by Phases 07 and 13)

Competition, Dataset, and Project entities carry:

```csharp
public VisibilityScope VisibilityScope { get; set; } = VisibilityScope.Team;
public Guid VisibilityOrgUnitId { get; set; }   // the Team/Group/Directorate this is visible to; ignored for Company scope
```

`IVisibilityEvaluator.CanSee(user, scope, visibilityOrgUnitId)`:
- `Company` → true for any authenticated KOC user.
- otherwise → true when the user's home-unit `Path` is at or below the `Path` of `visibilityOrgUnitId`.

At creation the API defaults `VisibilityOrgUnitId` to the creator's own unit at the chosen level and rejects a unit the creator does not belong to (unless the caller is `PlatformAdmin`, who may target any unit).

## 6. API contracts

```http
GET  /api/v1/me                         # includes positionLevel, homeOrgUnit, ledOrgUnit
GET  /api/v1/me/permissions?resourceType=&resourceId=
GET  /api/v1/me/scope                    # my supervisory subtree (org units + member count)
GET  /api/v1/org/units?parentId=&type=   # browse the org tree (for visibility pickers)
GET  /api/v1/org/units/{id}/audience?scope=team|group|directorate|company  # audience preview count
POST /api/v1/admin/users/{id}/permissions  (PlatformAdmin)
DELETE /api/v1/admin/users/{id}/permissions/{permissionId}  (PlatformAdmin)
GET  /api/v1/admin/users/{id}/permissions  (PlatformAdmin)
POST /api/v1/admin/org/sync                (PlatformAdmin)   # reconcile org tree + positions
```

## 7. MudBlazor pages and components

- `Components/Account/Pages/SignIn.razor`
- `Components/Account/Pages/SignOut.razor`
- `Components/Account/Shared/AccountLayout.razor`
- `Components/Account/IdentityComponents.razor`
- `Components/Layout/MainLayout.razor` reads `IKocCurrentUser`
- `<AuthorizeView>` wrappers on top-level nav

## 8. Security and authorization

- Tenant validation rejects any token whose `tid` claim does not match the configured KOC tenant ID. Returns 403 with a clear error.
- Audience validation rejects any token whose `aud` claim does not include the configured audience.
- App role mapping is exhaustive; unknown app role values are logged and the user is treated as `Employee` (the baseline participant role, auto-granted to every authenticated KOC user).
- **Supervisory rollups are read-only.** A `TeamLeader`/`Manager`/`DCEO`/`CEO` may view aggregated participation, standings, and progress for their org subtree, but never edit, submit, or open another person's private submission internals beyond what visibility allows. Supervisory endpoints are gated by `IOrgScopeResolver` on the caller's led unit — a supervisor cannot pass an `orgUnitId` outside their subtree.
- **Visibility is enforced server-side** on every read of a competition, dataset, or project via `IVisibilityEvaluator`; list endpoints filter by subtree at query time (never client-side). Company scope is visible to all KOC users.
- Permissions expire automatically; expired rows are filtered at query time.
- The first-admin bootstrap is guarded: it runs only if `SELECT COUNT(*) FROM AspNetUserRoles WHERE RoleId = (SELECT Id FROM AspNetRoles WHERE Name = 'PlatformAdmin')` returns zero. After the first admin is granted, the bootstrap disables itself.

## 9. Tests

- Unit: tenant rejection, audience rejection, app role mapping, permission filtering
- Integration: end-to-end OIDC simulation with stub token handler
- Architecture: no references from Web to Infrastructure, no DbContext in Web

## 10. Verification commands

```bash
dotnet run --project src/Beep.KocAiCommunity.AppHost
```

Navigate to `https://localhost:5001/signin-oidc`. Confirm the KOC tenant login page appears. After sign-in, hit `/api/v1/me` to confirm the returned payload includes Entra-issued `oid` and `tid` plus the KOC app role claims.

```bash
dotnet test tests/Beep.KocAiCommunity.IntegrationTests --filter "FullyQualifiedName~Auth"
```

## 11. Acceptance gate

- Anonymous: redirected to sign-in
- Authenticated KOC user: signed in as `Employee` and sees the employee home
- A user who leads a unit sees the matching supervisory rollup (Team Leader→Team, Manager→Group, DCEO→Directorate, CEO→Company) and cannot query a subtree they do not lead
- Visibility: a Team-scoped resource is invisible to a peer in another Team; a Company-scoped resource is visible to all
- Creating a resource defaults its `VisibilityOrgUnitId` to the creator's own unit and rejects a foreign unit for non-admins
- Wrong tenant: 403 with clear error
- Wrong audience: 401
- First sign-in on a fresh database: that user becomes PlatformAdmin
- Permissions table is correctly populated from API calls
- Expired permissions are filtered at query time
- All tests pass

## 12. Risks and deferred work

- Entra tenant ID, client ID, and audience must be provisioned before any deployment; the plan documents the steps but does not provision them.
- The first-admin bootstrap requires careful audit logging; tests cover both bootstrap and disabled states.
- App role ↔ ASP.NET role mapping is centralized; future roles must be added to `KocAuthOptions.AppRoleMap` only.
