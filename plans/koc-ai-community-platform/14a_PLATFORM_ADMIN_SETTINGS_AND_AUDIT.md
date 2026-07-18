# Phase 14a — Platform Admin, Settings, and Audit

**Status:** 🟡 PLANNING
**Dependencies:** Phase 02, Phase 03, Phase 04
**Goal:** Build the platform admin surface under a single `PlatformAdmin` role, with a typed settings service and a complete audit trail.

## 1. Goal and dependencies

- `/admin/*` surface for platform operators
- Typed settings service (code-first registry; DB stores state, not behaviour)
- Audit envelope writes before/after diffs for every admin action
- KOC connector health overview and info-sec classification editor
- First-admin bootstrap on a fresh database

## 2. Existing reference behavior

- Beep.AI.MLStudio: `app/services/admin/security_settings.py:38` (typed settings pattern).
- Beep.AI.Community: `app/routes/web/admin.py:18` (admin routes).
- Beep.AI.Server: `app/utils/permissions.py:185` (`@admin_required`).

## 3. Architecture decisions

| Decision | Choice | Rationale |
|---|---|---|
| Authz | Single Entra app role `PlatformAdmin` | KOC scope |
| Settings storage | Typed registry with EF-backed `SettingDefinition` / `SettingValue` | Per Beep.AI.Server AGENTS.md (no JSON as source of truth) |
| Secrets | Encrypted column via Data Protection (dev) / Key Vault references (production) | Standard |
| Audit | `AdminAuditLog` writes in same DB transaction as change | No dual-write inconsistency |
| Sessions | `AdminSession` tracks admin sign-ins | Per Beep.AI.Server |
| Health | `SystemHealthSnapshot` written by hosted service every minute | Standard |

## 4. Project-by-project deliverables

### 4.1 Application

- `ISettingsService`, `ISettingsContributor`, `SettingsCategoryAttribute`
- `IAuditService`, `IPlatformRoleService`, `IFeatureFlagService`
- `IAdminDiagnosticsService`

### 4.2 Domain

- `SettingsCategory`, `SettingDefinition`, `SettingValue`, `SettingOverride`, `SettingAudit`
- `FeatureFlag`
- `PlatformRole`, `PlatformRolePermission`, `UserPlatformRole`
- `AdminAuditLog`, `AdminSession`
- `SystemHealthSnapshot`
- `MaintenanceTask`, `MaintenanceTaskRun`
- `RateLimitPolicy`
- `EmailTemplate`
- `Notification`

### 4.3 Infrastructure

- EF Core configurations
- `SettingsService` (typed registry + DB lookup)
- `AuditService` (writes AdminAuditLog in same transaction)
- `PlatformRoleService`
- `FeatureFlagService`
- `EmailSender` (SMTP / SendGrid)
- `BackgroundHealthMonitor` hosted service

### 4.4 API

- `/admin/api/v1/*` endpoints (catalog in Phase 04)

### 4.5 UI

- Admin pages (catalog in `14a_PLATFORM_ADMIN_SETTINGS_AND_AUDIT.md` parent plan)

## 5. Entities and migrations

Already documented in the plan overview and Phase 04. Schema `platform`.

## 6. API contracts

Already documented in Phase 04 and the plan overview.

## 7. MudBlazor pages and components

```text
/admin                       Dashboard (KPIs, recent audit, active sessions, health)
/admin/settings              Settings categories index
/admin/settings/general      General platform settings
/admin/settings/auth         Entra ID, scopes, audience, app roles mapping
/admin/settings/email        SMTP + email sender config, templates, send-test
/admin/settings/storage      Artifact store: local/Azure Blob provider + container + SAS
/admin/settings/security     Password policy, session lifetime, MFA requirement flag, lockout
/admin/settings/rate-limits  Rate-limit policies editor
/admin/settings/branding     Theme JSON, logos, palette tokens
/admin/settings/integrations External service endpoints
/admin/users                 User directory
/admin/users/{id}            User detail
/admin/users/{id}/roles      Role assignment page
/admin/roles                 Platform roles list
/admin/roles/{id}            Role detail + permission grid
/admin/audit                 Audit log with filter drawer, export, detail drawer
/admin/sessions              Active admin sessions
/admin/feature-flags         Feature flag list and edit
/admin/health                Health overview with per-component drill-down
/admin/maintenance           Scheduled maintenance tasks
/admin/notifications         Compose broadcast notification
/admin/diagnostics           Live diagnostics
/admin/help                  Contextual help and admin playbook
```

## 8. Security and authorization

- `RequirePlatformAdmin` policy registered globally
- First-admin bootstrap runs only when zero PlatformAdmin members exist
- All admin write endpoints emit `AdminAuditLog` in same DB transaction
- Secrets encrypted at rest; never logged in plaintext
- Rate limits on admin endpoints
- Session tracking with IP and user-agent

## 9. Tests

- Unit: settings registry, audit envelope, encryption, feature flag evaluation
- Integration: every admin endpoint returns 403 to non-admin tokens; 100% coverage target
- Integration: first-admin bootstrap and disabled state
- Component: settings forms, role assignment, audit detail

## 10. Verification commands

```bash
dotnet test tests/Beep.KocAiCommunity.IntegrationTests --filter "FullyQualifiedName~Admin"
```

## 11. Acceptance gate

- All admin endpoints return 403 to non-admin tokens
- Setting changes are persisted with audit and version
- Encrypted secrets are unreadable in audit JSON
- Dashboard reflects real health, audit, and counts
- First-admin bootstrap works on a fresh database
- Tests pass

## 12. Risks and deferred work

- Key Vault integration in production is a deployment concern; dev uses Data Protection
- Feature flag targeting rules are initially boolean + rollout percentage; complex targeting is a follow-up
- Admin impersonation is out of scope for MVP
