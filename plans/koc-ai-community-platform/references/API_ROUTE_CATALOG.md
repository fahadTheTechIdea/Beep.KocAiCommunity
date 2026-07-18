# API Route Catalog

Canonical API surface. Versioned under `/api/v1` for application endpoints and `/admin/api/v1` for platform admin endpoints.

Conventions:

- All endpoints return RFC 9457 Problem Details on error.
- All endpoints accept `Accept: application/json`.
- All POST endpoints honor `Idempotency-Key`.
- All mutating endpoints honor `If-Match` ETag where applicable.
- All endpoints require a valid Entra-issued bearer token.
- Authorization policies are documented per route in `AUTHORIZATION_MATRIX.md`.

## Health

| Method | Route | Purpose | Auth |
|---|---|---|---|
| GET | `/health` | Liveness | anonymous |
| GET | `/api/v1/setup/diagnostics` | Setup diagnostics snapshot | Employee |
| GET | `/admin/api/v1/dashboard/summary` | Admin dashboard summary | PlatformAdmin |

## Identity, org, and permissions (Phase 02)

| Method | Route | Purpose |
|---|---|---|
| GET | `/api/v1/me` | Current user (incl. positionLevel, homeOrgUnit, ledOrgUnit) |
| GET | `/api/v1/me/permissions?resourceType=&resourceId=` | Permissions for a resource |
| GET | `/api/v1/me/scope` | My supervisory subtree (org units + member count) |
| GET | `/api/v1/org/units?parentId=&type=` | Browse the KOC org tree (for visibility pickers) |
| GET | `/api/v1/org/units/{id}/audience?scope=team\|group\|directorate\|company` | Audience-count preview for a visibility choice |
| GET | `/api/v1/admin/users/{id}/permissions` | (PlatformAdmin) |
| PUT | `/api/v1/admin/users/{id}/permissions` | (PlatformAdmin) |
| DELETE | `/api/v1/admin/users/{id}/permissions/{permissionId}` | (PlatformAdmin) |
| POST | `/api/v1/admin/org/sync` | Reconcile org tree + positions (PlatformAdmin) |

## Artifacts (Phase 03)

| Method | Route | Purpose |
|---|---|---|
| POST | `/api/v1/artifacts/upload` | Upload artifact |
| GET | `/api/v1/artifacts/{id}` | Get artifact metadata |
| GET | `/api/v1/artifacts/{id}/download` | Download artifact (classification enforced) |
| DELETE | `/api/v1/artifacts/{id}` | Delete artifact |

## Profiles and discussions (Phase 06)

| Method | Route | Purpose |
|---|---|---|
| GET | `/api/v1/profiles/{userId}` | Get profile |
| PUT | `/api/v1/profiles/me` | Update my profile |
| GET | `/api/v1/discussions?scopeType=&scopeId=&page=` | List discussions |
| POST | `/api/v1/discussions` | Create discussion |
| GET | `/api/v1/discussions/{id}` | Get discussion |
| PUT | `/api/v1/discussions/{id}` | Update discussion |
| DELETE | `/api/v1/discussions/{id}` | Delete discussion |
| POST | `/api/v1/discussions/{id}/lock` | (discussion.moderate) |
| POST | `/api/v1/discussions/{id}/pin` | (discussion.moderate) |
| POST | `/api/v1/discussions/{id}/vote` | Vote |
| DELETE | `/api/v1/discussions/{id}/vote` | Withdraw vote |
| POST | `/api/v1/discussions/{id}/replies` | Add reply |
| GET | `/api/v1/discussions/{id}/replies` | List replies |
| POST | `/api/v1/discussions/{id}/replies/{replyId}/vote` | Reply vote |
| GET | `/api/v1/notifications?unreadOnly=` | List notifications |
| PUT | `/api/v1/notifications/{id}/read` | Mark read |
| GET | `/api/v1/activity?page=` | Activity feed |

## Datasets and projects (Phase 07)

| Method | Route | Purpose |
|---|---|---|
| GET | `/api/v1/datasets?classification=&domain=&scope=&page=` | List datasets (visibility-filtered by caller subtree) |
| POST | `/api/v1/datasets` | Create dataset (body: visibilityScope + visibilityOrgUnitId) |
| GET | `/api/v1/datasets/{id}` | Get dataset |
| PUT | `/api/v1/datasets/{id}` | Update dataset |
| DELETE | `/api/v1/datasets/{id}` | Delete dataset |
| POST | `/api/v1/datasets/{id}/versions` | Create version |
| GET | `/api/v1/datasets/{id}/versions` | List versions |
| POST | `/api/v1/datasets/{id}/versions/{versionId}/publish` | Publish version |
| POST | `/api/v1/datasets/{id}/versions/{versionId}/archive` | Archive version |
| GET | `/api/v1/datasets/{id}/versions/{versionId}/files` | List files |
| POST | `/api/v1/datasets/{id}/versions/{versionId}/files` | Add file |
| GET | `/api/v1/datasets/{id}/versions/{versionId}/files/{fileId}/download` | Download file |
| POST | `/api/v1/datasets/{id}/versions/{versionId}/profile` | Generate profile |
| GET | `/api/v1/datasets/{id}/versions/{versionId}/profile` | Get profile |
| POST | `/api/v1/datasets/{id}/imports` | Start import |
| GET | `/api/v1/datasets/{id}/imports/{jobId}` | Import status |
| GET | `/api/v1/projects?domain=&classification=&scope=&page=` | List projects (visibility-filtered) |
| POST | `/api/v1/projects` | Create project (body: visibilityScope + visibilityOrgUnitId) |
| GET | `/api/v1/projects/{id}` | Get project |
| PUT | `/api/v1/projects/{id}` | Update project |
| DELETE | `/api/v1/projects/{id}` | Delete project |
| POST | `/api/v1/projects/{id}/members` | Add member |
| PUT | `/api/v1/projects/{id}/members/{memberId}` | Update member role |
| DELETE | `/api/v1/projects/{id}/members/{memberId}` | Remove member |
| GET | `/api/v1/projects/{id}/activity` | Activity feed |
| POST | `/api/v1/projects/{id}/datasets/{datasetId}` | Link dataset |

## Connectors (Phase 07a)

| Method | Route | Purpose |
|---|---|---|
| GET | `/api/v1/connectors` | Catalog |
| GET | `/api/v1/connectors/{code}/instances` | List instances |
| POST | `/api/v1/connectors/{code}/instances` | Create instance |
| GET | `/api/v1/connectors/instances/{id}` | Get instance |
| PUT | `/api/v1/connectors/instances/{id}` | Update instance |
| DELETE | `/api/v1/connectors/instances/{id}` | Delete instance |
| POST | `/api/v1/connectors/instances/{id}/test` | Test connection |
| GET | `/api/v1/connectors/instances/{id}/schema` | Schema introspection |
| GET | `/api/v1/connectors/instances/{id}/resources?path=` | Browse resources |
| POST | `/api/v1/connectors/instances/{id}/import` | Import dataset |
| GET | `/api/v1/connectors/instances/{id}/health` | Health snapshot |
| POST | `/api/v1/connectors/instances/{id}/credentials` | Add credential |
| PUT | `/api/v1/connectors/instances/{id}/credentials/{key}` | Update credential |
| DELETE | `/api/v1/connectors/instances/{id}/credentials/{key}` | Delete credential |

## ML (Phase 08)

| Method | Route | Purpose |
|---|---|---|
| GET | `/api/v1/ml/nodes` | Catalog of ML nodes |
| GET | `/api/v1/ml/nodes/{id}` | Node details |
| POST | `/api/v1/ml/nodes/{id}/validate` | Validate parameters |

## Workflows (Phase 09)

| Method | Route | Purpose |
|---|---|---|
| GET | `/api/v1/workflows?projectId=&page=` | List workflows |
| POST | `/api/v1/workflows` | Create workflow |
| GET | `/api/v1/workflows/{id}` | Get workflow |
| PUT | `/api/v1/workflows/{id}` | Update workflow |
| DELETE | `/api/v1/workflows/{id}` | Delete workflow |
| GET | `/api/v1/workflows/{id}/versions` | List versions |
| POST | `/api/v1/workflows/{id}/versions` | Create draft version |
| GET | `/api/v1/workflows/{id}/versions/{versionNumber}` | Get version |
| POST | `/api/v1/workflows/{id}/versions/{versionNumber}/publish` | Publish version |
| POST | `/api/v1/workflows/{id}/versions/{versionNumber}/archive` | Archive version |
| POST | `/api/v1/workflows/{id}/versions/{versionNumber}/validate` | Validate version |
| POST | `/api/v1/workflows/{id}/versions/{versionNumber}/compile` | Compile version |
| POST | `/api/v1/workflows/{id}/import` | Import workflow definition |
| GET | `/api/v1/workflows/{id}/export` | Export workflow definition |
| GET | `/api/v1/workflow-templates?domain=` | List industry templates |
| POST | `/api/v1/workflow-templates/{code}/instantiate` | Instantiate a template |

## Runs (Phase 10)

| Method | Route | Purpose |
|---|---|---|
| POST | `/api/v1/runs` | Start a run |
| GET | `/api/v1/runs/{id}` | Get run |
| POST | `/api/v1/runs/{id}/cancel` | Cancel run |
| GET | `/api/v1/runs/{id}/logs` | Run logs |
| GET | `/api/v1/runs/{id}/attempts` | Run attempts |

## Experiments (Phase 11)

| Method | Route | Purpose |
|---|---|---|
| GET | `/api/v1/experiments?projectId=&page=` | List experiments |
| POST | `/api/v1/experiments` | Create experiment |
| GET | `/api/v1/experiments/{id}` | Get experiment |
| PUT | `/api/v1/experiments/{id}` | Update experiment |
| DELETE | `/api/v1/experiments/{id}` | Delete experiment |
| GET | `/api/v1/experiments/{id}/runs?page=` | List runs |
| POST | `/api/v1/experiments/{id}/runs` | Create run |
| GET | `/api/v1/runs/{id}` | Get run |
| PUT | `/api/v1/runs/{id}` | Update run (favorite, tags, best) |
| GET | `/api/v1/runs/{id}/metrics` | Metrics |
| POST | `/api/v1/runs/{id}/metrics` | Ingest metric |
| GET | `/api/v1/runs/{id}/parameters` | Parameters |
| POST | `/api/v1/runs/{id}/parameters` | Ingest parameter |
| GET | `/api/v1/runs/{id}/logs` | Logs |
| POST | `/api/v1/runs/{id}/logs` | Append log |
| GET | `/api/v1/runs/{id}/artifacts` | Artifacts |
| POST | `/api/v1/runs/{id}/artifacts` | Add artifact |
| POST | `/api/v1/experiments/{id}/compare` | Compare runs |
| GET | `/api/v1/experiments/{id}/best-run` | Best run |

## Models (Phase 12)

| Method | Route | Purpose |
|---|---|---|
| GET | `/api/v1/models?projectId=&classification=&page=` | List models |
| POST | `/api/v1/models` | Create model |
| GET | `/api/v1/models/{id}` | Get model |
| PUT | `/api/v1/models/{id}` | Update model |
| DELETE | `/api/v1/models/{id}` | Delete model |
| GET | `/api/v1/models/{id}/versions` | List versions |
| POST | `/api/v1/models/{id}/versions` | Create version |
| GET | `/api/v1/models/{id}/versions/{semVer}` | Get version |
| POST | `/api/v1/models/{id}/versions/{semVer}/promote` | Promote (with approvals) |
| POST | `/api/v1/models/{id}/versions/{semVer}/archive` | Archive |
| POST | `/api/v1/models/{id}/versions/{semVer}/rollback` | Rollback to previous version |
| GET | `/api/v1/models/{id}/versions/{semVer}/approvals` | Approvals |
| POST | `/api/v1/models/{id}/versions/{semVer}/approvals` | Add approval |
| POST | `/api/v1/models/{id}/versions/{semVer}/infer` | Single inference |
| POST | `/api/v1/models/{id}/versions/{semVer}/infer/batch` | Batch inference |
| GET | `/api/v1/models/{id}/versions/{semVer}/inference-logs` | Inference logs |

## Competitions (Phase 13)

| Method | Route | Purpose |
|---|---|---|
| GET | `/api/v1/competitions?status=&scope=&page=` | List competitions (visibility-filtered) |
| POST | `/api/v1/competitions` | Create competition (any Employee or CompetitionAdmin; body: visibilityScope + visibilityOrgUnitId) |
| GET | `/api/v1/competitions/{id}` | Get competition |
| PUT | `/api/v1/competitions/{id}` | Update competition (creator/CompetitionAdmin) |
| DELETE | `/api/v1/competitions/{id}` | Delete competition (creator/CompetitionAdmin) |
| POST | `/api/v1/competitions/{id}/activate` | Activate (creator/CompetitionAdmin) |
| POST | `/api/v1/competitions/{id}/conclude` | Conclude (creator/CompetitionAdmin) |
| POST | `/api/v1/competitions/{id}/reveal` | Reveal the concealed final leaderboard (creator/CompetitionAdmin) |
| GET | `/api/v1/competitions/{id}/leaderboard?board=live\|final` | Leaderboard (`final` 403s before RevealUtc) |
| POST | `/api/v1/competitions/{id}/submissions` | Submit |
| GET | `/api/v1/competitions/{id}/submissions?page=` | Submissions |
| GET | `/api/v1/competitions/{id}/submissions/{submissionId}` | Submission |
| POST | `/api/v1/competitions/{id}/submissions/{submissionId}/rescore` | Rescore (CompetitionAdmin) |

## Learning tracks (Phase 13a)

| Method | Route | Purpose |
|---|---|---|
| GET | `/api/v1/tracks?level=&domain=&page=` | List learning tracks (visibility-filtered) |
| GET | `/api/v1/tracks/{id}` | Get track |
| GET | `/api/v1/tracks/{id}/lessons` | List lessons |
| GET | `/api/v1/tracks/{id}/lessons/{lessonId}` | Get lesson |
| POST | `/api/v1/tracks/{id}/enroll` | Enroll (idempotent) |
| POST | `/api/v1/tracks/{id}/lessons/{lessonId}/complete` | Mark lesson complete |
| GET | `/api/v1/me/learning` | My enrollments + progress |
| POST | `/api/v1/tracks` | Create track (LearningAdmin) |
| PUT | `/api/v1/tracks/{id}` | Update track (LearningAdmin) |
| POST | `/api/v1/tracks/{id}/lessons` | Add lesson (LearningAdmin) |
| PUT | `/api/v1/tracks/{id}/lessons/{lessonId}` | Update lesson (LearningAdmin) |
| POST | `/api/v1/tracks/{id}/publish` | Publish track (LearningAdmin) |

## Supervision rollups (Phase 02 scope; Phases 13 & 13a data)

| Method | Route | Purpose |
|---|---|---|
| GET | `/api/v1/supervision/competitions?orgUnitId=` | Participation + standings rollup for the caller's subtree |
| GET | `/api/v1/supervision/learning?orgUnitId=` | Track-progress rollup for the caller's subtree |

All supervision routes are gated by `RequireSupervisor` and scoped to the caller's led subtree (`IOrgScopeResolver`); an out-of-subtree `orgUnitId` returns 403.

## Templates and help (Phase 14)

| Method | Route | Purpose |
|---|---|---|
| GET | `/api/v1/industry-templates?subdomain=` | List templates |
| GET | `/api/v1/industry-templates/{code}` | Get template |
| POST | `/api/v1/industry-templates/{code}/instantiate` | Instantiate |
| GET | `/api/v1/help/articles?category=` | List help articles |
| GET | `/api/v1/help/articles/{slug}` | Get article |
| GET | `/api/v1/help/tutorials/{slug}` | Get tutorial |

## Admin (Phase 14a)

| Method | Route | Purpose |
|---|---|---|
| GET | `/admin/api/v1/dashboard/summary` | Dashboard |
| GET | `/admin/api/v1/settings/categories` | Settings categories |
| GET | `/admin/api/v1/settings/categories/{code}/definitions` | Definitions |
| GET | `/admin/api/v1/settings/values?category={code}` | Values |
| PUT | `/admin/api/v1/settings/values/{definitionId}` | Update |
| POST | `/admin/api/v1/settings/values/{definitionId}/rollback/{versionId}` | Rollback |
| POST | `/admin/api/v1/settings/test/{definitionId}` | Test |
| GET | `/admin/api/v1/feature-flags` | List flags |
| PUT | `/admin/api/v1/feature-flags/{key}` | Update flag |
| GET | `/admin/api/v1/roles` | List roles |
| POST | `/admin/api/v1/roles` | Create role |
| PUT | `/admin/api/v1/roles/{id}` | Update role |
| DELETE | `/admin/api/v1/roles/{id}` | Delete role |
| GET | `/admin/api/v1/roles/{id}/permissions` | List role permissions |
| PUT | `/admin/api/v1/roles/{id}/permissions` | Update role permissions |
| GET | `/admin/api/v1/users/{id}/platform-roles` | List user roles |
| PUT | `/admin/api/v1/users/{id}/platform-roles` | Update user roles |
| GET | `/admin/api/v1/audit?actor=&action=&resource=&from=&to=&page=` | Audit log |
| GET | `/admin/api/v1/audit/{id}` | Audit detail |
| GET | `/admin/api/v1/audit/export?format=json|csv` | Export |
| GET | `/admin/api/v1/sessions` | Admin sessions |
| GET | `/admin/api/v1/sessions/{id}` | Session detail |
| POST | `/admin/api/v1/sessions/{id}/revoke` | Revoke |
| GET | `/admin/api/v1/health/components` | Health overview |
| GET | `/admin/api/v1/health/history?component=&hours=` | Component history |
| GET | `/admin/api/v1/maintenance/tasks` | Maintenance tasks |
| PUT | `/admin/api/v1/maintenance/tasks/{name}` | Update task |
| POST | `/admin/api/v1/maintenance/tasks/{name}/run` | Run now |
| GET | `/admin/api/v1/rate-limits` | Rate limits |
| PUT | `/admin/api/v1/rate-limits/{id}` | Update |
| GET | `/admin/api/v1/email-templates` | Templates |
| PUT | `/admin/api/v1/email-templates/{code}` | Update |
| POST | `/admin/api/v1/email-templates/{code}/preview` | Preview |
| POST | `/admin/api/v1/email-templates/{code}/send-test` | Send test |
| GET | `/admin/api/v1/notifications` | Notifications |
| POST | `/admin/api/v1/notifications/broadcast` | Broadcast |
| GET | `/admin/api/v1/diagnostics` | Live diagnostics |
| POST | `/admin/api/v1/diagnostics/redis-ping` | (optional) |
| POST | `/admin/api/v1/diagnostics/sql-ping` | SQL ping |
| POST | `/admin/api/v1/diagnostics/blob-ping` | Blob ping |
| POST | `/admin/api/v1/diagnostics/queue-ping` | Queue ping |
| POST | `/admin/api/v1/diagnostics/worker-ping` | Worker ping |
| GET | `/admin/api/v1/branding/current` | Branding |
| PUT | `/admin/api/v1/branding` | Update branding |
| GET | `/admin/api/v1/branding/presets` | (removed; KOC only) |
| POST | `/admin/api/v1/cache/invalidate` | Invalidate cache |

## Operations (Phase 15)

| Method | Route | Purpose |
|---|---|---|
| GET | `/api/v1/admin/operations/backup` | Backup status |
| POST | `/api/v1/admin/operations/restore` | Restore |
| POST | `/api/v1/admin/operations/disaster-recovery-test` | DR test |
| GET | `/api/v1/admin/operations/import/status` | Import status |
| POST | `/api/v1/admin/operations/import` | Start import |

## SignalR hubs

| Hub | Path | Purpose |
|---|---|---|
| RunProgressHub | `/hubs/runs` | Run progress events |
| LeaderboardHub | `/hubs/leaderboard` | Leaderboard updates |
| DiscussionHub | `/hubs/discussions` | Discussion activity |
| ProjectHub | `/hubs/projects` | Project collaboration events |
| AdminHub | `/hubs/admin` | Admin notifications |

## OpenAPI generation

OpenAPI 3.1 schema is generated for `/api/v1` and `/admin/api/v1`. Swagger UI is available in `Development` only.
