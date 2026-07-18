# Phase 14 — O&G Templates, Domain Admin, and Help

**Status:** 🟡 PLANNING
**Dependencies:** Phase 09, Phase 12, Phase 13
**Goal:** Replace the multi-industry template catalog with a single O&G taxonomy, add per-domain admin pages, and provide the help system.

## 1. Goal and dependencies

- O&G workflow templates grouped by upstream / midstream / downstream / HSE
- Per-domain admin pages for ML Studio, Datasets, Projects, Competitions, Discussions
- Branding and theme settings (KOC-only)
- Tutorials, FAQ, API documentation, contextual node help, sample workflows
- Data-retention and artifact-cleanup policies

## 2. Existing reference behavior

- Beep.AI.MLStudio: `app/industry_profiles/`, `app/industry_modules/`, `workflow_templates/<industry>/_index.json`.
- Beep.AI.Community: `app/services/branding_service.py`, `app/services/theme_service.py`.

## 3. Architecture decisions

| Decision | Choice | Rationale |
|---|---|---|
| Taxonomy | Single O&G domain with 4 subdomains | KOC only |
| Templates | Code-first registration; JSON catalog for user templates | Standard |
| Admin pages | Per-domain admin pages under each module | Distributed responsibility |
| Branding | KOC theme; no preset marketplace | KOC only |
| Help | In-app tutorial system; markdown content | Standard |

## 4. Project-by-project deliverables

### 4.1 Application

- `IIndustryTemplateRegistry`
- `IAdminHelpService`

### 4.2 Domain

- `IndustryTemplateDefinition`, `IndustryTemplateVersion`

### 4.3 Infrastructure

- EF Core configurations
- Template seed data for O&G (4 subdomains)

### 4.4 UI

- `Pages/Studio/Admin/Templates.razor`
- `Pages/Datasets/Admin/Index.razor`
- `Pages/Projects/Admin/Index.razor`
- `Pages/Competitions/Admin/Index.razor`
- `Pages/Discussions/Admin/Index.razor`
- `Components/Help/Tutorial.razor`
- `Components/Help/ContextualHelp.razor`
- `Components/Studio/Admin/NodeHelp.razor`

## 5. Entities and migrations

```csharp
public class IndustryTemplateDefinition : AuditableEntity
{
    public string Code { get; set; } = default!;  // e.g. "og-upstream-production-forecast"
    public string DisplayName { get; set; } = default!;
    public string Subdomain { get; set; } = default!; // upstream, midstream, downstream, hse
    public string Description { get; set; } = default!;
    public string OwnerUserId { get; set; } = default!;
    public string Visibility { get; set; } = "koc";  // always koc for this app
    public string? Tags { get; set; }
    public int LatestVersionNumber { get; set; }
}

public class IndustryTemplateVersion : AuditableEntity
{
    public Guid TemplateDefinitionId { get; set; }
    public int VersionNumber { get; set; }
    public string Status { get; set; } = "draft";
    public int SchemaVersion { get; set; } = 1;
    public string DefinitionJson { get; set; } = default!;
    public string SnapshotHash { get; set; } = default!;
}
```

## 6. API contracts

```http
GET    /api/v1/industry-templates?subdomain=
GET    /api/v1/industry-templates/{code}
POST   /api/v1/industry-templates/{code}/instantiate

GET    /api/v1/help/articles?category=
GET    /api/v1/help/articles/{slug}
GET    /api/v1/help/tutorials/{slug}
```

## 7. MudBlazor pages and components

- All admin pages use MudBlazor; data grids for tabular content; forms with `MudForm` validation

## 8. Security and authorization

- PlatformAdmin required to manage ML templates
- `dataset.admin` permission required for dataset admin actions
- CompetitionAdmin required for competition admin
- `discussion.moderate` permission required for discussion moderation actions

## 9. Tests

- Unit: template instantiation, versioning
- Integration: domain admin actions
- Component: admin forms, help viewer

## 10. Verification commands

```bash
dotnet test tests/Beep.KocAiCommunity.IntegrationTests --filter "FullyQualifiedName~Templates|FullyQualifiedName~Help"
```

## 11. Acceptance gate

- A KOC employee can complete an end-to-end guided scenario without database intervention
- Domain admin actions are audited
- Help articles are searchable
- Tests pass

## 12. Risks and deferred work

- Industry templates ship with seeded O&G content; expansion is iterative
- Help content authoring flow is admin-only; future user-authored content is deferred
