# Authorization Matrix

Entra app roles, resource permissions, and policy mapping for `Beep.KocAiCommunity`.

## Entra App Roles (KOC tenant)

Two kinds of role: **position levels** that mirror the KOC reporting line (exactly one per user, sourced from the org directory) and additive **function roles** granted on top.

### Position levels (reporting line)

| Role | Leads org unit | Description | Allowed actions |
|---|---|---|---|
| `Employee` | — (member of a Team) | Default KOC participant | Learn (enroll in tracks, complete lessons); create + submit to competitions; create datasets/projects; run own workflows; everything within their visibility scope |
| `TeamLeader` | a Team | Team supervisor | Everything an Employee can, **plus** read-only rollup of their Team's participation, standings, and progress |
| `Manager` | a Group | Group/department supervisor | Employee actions **plus** read-only rollup across the Teams in their Group |
| `DCEO` | a Directorate | Directorate supervisor | Employee actions **plus** read-only rollup across the Groups in their Directorate |
| `CEO` | the Company | Company-wide supervisor | Employee actions **plus** company-wide read-only rollup across Directorates |

Supervisory rollups are **read-only**: a supervisor can see how their people are doing but cannot submit, edit, or open private submission internals on their behalf, and cannot query an org unit outside their own subtree.

### Function roles (additive, granted by an admin)

| Role | Description | Allowed actions |
|---|---|---|
| `PlatformAdmin` | Platform operators | All admin endpoints; manage roles, settings, org sync, audit; may target any org unit / visibility scope |
| `CompetitionAdmin` | Competition organizer | Create/edit/activate/reveal competitions; manage scoring; rescore; **and create + manage the datasets and projects that back competitions** (dataset.write/admin, project.write/admin on resources they own, at any visibility scope they are permitted) |
| `LearningAdmin` | Learning content author | Author and publish learning tracks and lessons |
| `Auditor` | Read-only auditor | Read audit, sessions, health; cannot mutate |

## Resource permissions (EF-backed)

Permissions are keyed by `<resource>.<action>` strings. They are granted per-user to a specific resource instance.

| Permission key | Description |
|---|---|
| `project.read` | Read project metadata and members |
| `project.write` | Edit project metadata |
| `project.admin` | Manage members; delete project |
| `dataset.read` | Read dataset metadata, schema, profile |
| `dataset.download` | Download dataset files |
| `dataset.write` | Edit dataset metadata; create versions |
| `dataset.admin` | Manage dataset permissions; delete dataset |
| `workflow.read` | Read workflow and versions |
| `workflow.execute` | Run a workflow |
| `workflow.write` | Edit workflow; create versions |
| `workflow.publish` | Publish workflow versions |
| `workflow.admin` | Delete workflow; manage permissions |
| `experiment.read` | Read experiments and runs |
| `experiment.write` | Create experiments and runs; ingest metrics |
| `experiment.admin` | Delete experiments; mark best run |
| `model.read` | Read model metadata and versions |
| `model.execute` | Invoke inference |
| `model.write` | Edit model metadata; create versions |
| `model.promote` | Promote a model version (requires 2 approvals) |
| `model.admin` | Delete model; manage approvals |
| `competition.read` | Read competition and leaderboard (subject to visibility) |
| `competition.submit` | Submit to a competition |
| `competition.admin` | Create/edit/activate competitions; rescore |
| `track.read` | Read a learning track and lessons |
| `track.enroll` | Enroll in a track; record lesson progress |
| `track.admin` | Author/publish learning tracks and lessons |
| `orgscope.read` | Read supervisory rollups for the caller's own subtree |

## Visibility (org-scoped)

Competitions, datasets, and projects carry a `VisibilityScope` (`Team`/`Group`/`Directorate`/`Company`) plus a `VisibilityOrgUnitId`. Read access requires **both** the relevant `*.read` permission (or public visibility) **and** that the caller's home org unit is within the visibility subtree. `IVisibilityEvaluator` performs the subtree check; list endpoints filter by it at query time. `Company` scope is visible to all KOC users. At creation the caller may only choose a unit they belong to (Team/Group/Directorate) or `Company`; `PlatformAdmin` may target any unit.

## Default memberships

| Role | Auto-grant on first sign-in? |
|---|---|
| `Employee` | Yes — baseline participant for every authenticated KOC user |
| `TeamLeader` / `Manager` / `DCEO` / `CEO` | Sourced from the org directory (a user who leads an `OrgUnit` gets the matching level); not hand-granted |
| `CompetitionAdmin` | No — admin grants |
| `LearningAdmin` | No — admin grants |
| `Auditor` | No — admin grants |
| `PlatformAdmin` | First sign-in on a fresh database only |

## Policy registration

```csharp
// Program.cs (Api)
builder.Services.AddAuthorization(o =>
{
    // Baseline participant (every KOC user is at least Employee)
    o.AddPolicy("RequireEmployee", p => p.RequireRole("Employee", "TeamLeader", "Manager", "DCEO", "CEO"));

    // Function roles
    o.AddPolicy("RequirePlatformAdmin", p => p.RequireRole("PlatformAdmin"));
    o.AddPolicy("RequireCompetitionAdmin", p => p.RequireRole("CompetitionAdmin", "PlatformAdmin"));
    o.AddPolicy("RequireLearningAdmin", p => p.RequireRole("LearningAdmin", "PlatformAdmin"));
    o.AddPolicy("RequireAuditor", p => p.RequireRole("Auditor", "PlatformAdmin"));

    // Supervisory rollups — any position that leads a unit; the handler scopes to the caller's subtree
    o.AddPolicy("RequireSupervisor", p => p.RequireRole("TeamLeader", "Manager", "DCEO", "CEO", "PlatformAdmin"));

    // Resource permission + visibility (evaluated together per resource id from the route)
    o.AddPolicy("Permission:project.read", p => p.AddRequirements(new PermissionRequirement("project.read")));
    // ... one policy per permission key
});
```

Permission policies are evaluated by `PermissionAuthorizationHandler` (reads `UserEntityPermission` scoped to the route resource id) **and** `VisibilityAuthorizationHandler` (calls `IVisibilityEvaluator` for the resource's `VisibilityScope`/`VisibilityOrgUnitId`). Both must pass. Supervisory endpoints are additionally gated by `OrgScopeAuthorizationHandler`, which rejects any `orgUnitId` outside the caller's led subtree.

## Route-to-policy mapping

| Route pattern | Policy |
|---|---|
| `/health` | anonymous |
| `/api/v1/setup/diagnostics` | RequireEmployee |
| `/api/v1/me`, `/api/v1/me/permissions`, `/api/v1/me/scope` | RequireEmployee |
| `/api/v1/org/units`, `/api/v1/org/units/{id}/audience` | RequireEmployee (for visibility pickers) |
| `/api/v1/profiles/me` | RequireEmployee |
| `/api/v1/discussions` GET | RequireEmployee |
| `/api/v1/discussions` POST | RequireEmployee |
| `/api/v1/discussions/{id}/lock`, `/pin` | Permission: discussion.moderate |
| `/api/v1/datasets` GET | RequireEmployee (+ visibility filter) |
| `/api/v1/datasets` POST | RequireEmployee (creator sets visibility scope) |
| `/api/v1/datasets/{id}` GET | Permission: dataset.read + visibility |
| `/api/v1/datasets/{id}/versions/{versionId}/files/{fileId}/download` | Permission: dataset.download + visibility |
| `/api/v1/projects/{id}/activity` | Permission: project.read + visibility |
| `/api/v1/connectors/instances/{id}` mutations | RequirePlatformAdmin |
| `/api/v1/workflows/{id}/run` | Permission: workflow.execute |
| `/api/v1/workflows/{id}/versions/{versionNumber}/publish` | Permission: workflow.publish |
| `/api/v1/runs/{id}/cancel` | Permission: workflow.execute OR Permission: workflow.admin |
| `/api/v1/models/{id}/versions/{semVer}/infer` | Permission: model.execute |
| `/api/v1/models/{id}/versions/{semVer}/promote` | Permission: model.promote |
| `/api/v1/tracks` GET | RequireEmployee |
| `/api/v1/tracks/{id}/enroll`, `/lessons/{lessonId}/complete` | Permission: track.enroll |
| `/api/v1/tracks` POST/PUT, `/publish` | RequireLearningAdmin |
| `/api/v1/competitions` GET, `/{id}` GET | RequireEmployee + visibility |
| `/api/v1/competitions` POST (create) | RequireEmployee (creator sets visibility) OR RequireCompetitionAdmin |
| `/api/v1/competitions/{id}/submissions` | Permission: competition.submit + visibility |
| `/api/v1/competitions/{id}` PUT/DELETE, `/activate`, `/reveal` | RequireCompetitionAdmin (or resource competition.admin) |
| `/api/v1/competitions/{id}/submissions/{submissionId}/rescore` | RequireCompetitionAdmin |
| `/api/v1/supervision/**` (rollups) | RequireSupervisor (scoped to caller's subtree) |
| `/api/v1/admin/operations/import`, `/api/v1/admin/org/sync` | RequirePlatformAdmin |
| All `/admin/api/v1/*` | RequirePlatformAdmin |

## First-admin bootstrap

```csharp
// UserPlatformRoleService.GrantFirstAdminIfNone(userId)
public async Task<bool> GrantFirstAdminIfNoneAsync(string userId, CancellationToken ct)
{
    var hasAdmin = await _db.UserPlatformRoles
        .AnyAsync(r => r.RoleId == PlatformAdminRoleId && r.ExpiresUtc == null, ct);
    if (hasAdmin) return false;
    await GrantAsync(userId, PlatformAdminRoleId, grantedBy: "system", expiresUtc: null, ct);
    await _audit.WriteAsync(new AdminAuditLog { Action = "first-admin-grant", Resource = "user", ResourceId = userId, OccurredUtc = DateTime.UtcNow }, ct);
    return true;
}
```

Called by `OnTokenValidated` after role claim mapping.

## Audit envelope

Every admin write action writes an `AdminAuditLog` row in the same DB transaction as the change. Read actions optionally write an access log if the resource is sensitive.

`AdminAuditLog` columns:

- `Id`
- `ActorUserId`
- `ActorRole`
- `Action` (e.g. `setting.update`, `role.create`)
- `Resource` (e.g. `setting`, `role`, `user`)
- `ResourceId`
- `BeforeJson`
- `AfterJson`
- `IpAddress`
- `UserAgent`
- `RequestId`
- `OccurredUtc`

Secrets in `BeforeJson`/`AfterJson` are redacted before write.

## Tenant validation

All bearer tokens are validated to ensure:

- `tid` claim matches the configured KOC tenant ID.
- `aud` claim includes the configured audience.
- `iss` matches the Entra authority URL.

Failures return 403 (tenant mismatch) or 401 (audience/issuer mismatch) with appropriate `WWW-Authenticate` header.
