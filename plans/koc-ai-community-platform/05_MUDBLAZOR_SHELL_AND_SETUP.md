# Phase 05 — MudBlazor Shell and Setup

**Status:** 🟡 PLANNING
**Dependencies:** Phase 01, Phase 02
**Goal:** Build the KOC-branded Blazor shell, providers, navigation, and first-run setup diagnostics.

## 1. Goal and dependencies

- MudBlazor providers (theme, popover, dialog, snackbar)
- KOC-branded theme tokens
- App bar, responsive drawer, navigation, breadcrumbs, command palette, notifications, account menu
- First-run setup wizard and diagnostics
- Verified MudBlazor APIs against the local `mudBlazor_Docs/`

## 2. Existing reference behavior

- BeepWeb shell: `Beep.Blazor.Server.App/Shared/MainLayout.razor:1-500`.
- Beep.OilandGas.Web shell: `Beep.OilandGas/Beep.OilandGas.Web/Shared/MainLayout.razor:1-303`.
- MudBlazor reference: `Beep.KocAiCommunity/mudBlazor_Docs/` (`README.md:1`).
- BeepWeb AGENTS.md MudBlazor gotchas: `BeepWeb/AGENTS.md:51-57`.

## 3. Architecture decisions

| Decision | Choice | Rationale |
|---|---|---|
| Layout primitive | `MudLayout` + `MudAppBar` + `MudDrawer` + `MudMainContent` | Per BeepWeb |
| Theme | `IKocThemeProvider` returning `MudTheme` | Mirrors `Beep.Razor.Components/Theme/BeepThemeProvider.cs:22-80` |
| Provider lifetimes | `IThemeProvider` scoped; theme JSON cached | Per Phase 00 service lifetime rules |
| Navigation | Sectioned groups: Home, Projects, Datasets, Workflows, Experiments, Models, Competitions, Discussions, Help, Admin | KOC-specific |
| Command palette | Ctrl+K overlay using MudBlazor Autocomplete | Standard |
| Setup wizard | Inline multi-step using `MudStepper` | Verified component |
| Diagnostics | Setup status tiles with green/red indicators | Standard |

## 4. Project-by-project deliverables

### 4.1 Ui.Shared

- `Components/Layout/MainLayout.razor` with providers
- `Components/Layout/AppBar.razor`
- `Components/Layout/NavMenu.razor`
- `Components/Layout/Breadcrumbs.razor`
- `Components/Layout/CommandPalette.razor`
- `Components/Layout/Notifications.razor`
- `Components/Layout/AccountMenu.razor`
- `Components/Setup/SetupWizard.razor`
- `Components/Setup/DiagnosticsTile.razor`
- `Components/Shared/PageHeader.razor`
- `Components/Shared/EmptyState.razor`
- `Components/Shared/LoadingState.razor`
- `Components/Shared/ErrorBoundary.razor`
- `Theme/KocThemeProvider.cs`
- `Theme/KocBrandingConfig.cs`
- `Services/KocCurrentUserService.cs` (wraps `IKocCurrentUser` for components)

### 4.2 Ui.Community, Ui.Studio, Ui.Admin

- Each project registers a `INavGroupProvider` so the shell can build the navigation dynamically

### 4.3 Web

- `App.razor` with `<Router>` and `<HeadOutlet>`
- `Components/Routes.razor`
- `Components/Layout/ReconnectModal.razor`
- `Program.cs` registers MudBlazor, theme, all UI projects, and the navigation registry

## 5. Entities and migrations

None in this stage.

## 6. API contracts

```http
GET  /api/v1/setup/diagnostics
```

Returns a snapshot of Entra, API, Database, Worker, Artifact Store health.

## 7. MudBlazor pages and components

- `Components/Pages/Home.razor`
- `Components/Pages/Setup.razor`
- `Components/Pages/Help/Index.razor`

## 8. Security and authorization

- Shell redirects unauthenticated users to `/signin-oidc`
- Employee minimum required for Home
- PlatformAdmin required for `/admin/*`

## 9. Tests

- Component: shell renders for anonymous, Employee, PlatformAdmin
- Component: command palette opens and closes
- Component: setup wizard transitions
- Component: navigation highlights active route
- Accessibility smoke: keyboard navigation, focus rings, ARIA labels

## 10. Verification commands

```bash
dotnet run --project src/Beep.KocAiCommunity.Web
```

## 11. Acceptance gate

- Shell renders correctly on desktop and mobile
- Theme provider returns a `MudTheme` with KOC palette tokens
- All MudBlazor APIs used match the local `mudBlazor_Docs/` references
- Provider count: theme, popover, dialog, snackbar
- Setup diagnostics reflect live health of API, DB, Worker, Storage
- Tests pass

## 12. Risks and deferred work

- MudBlazor 9.x specific gotchas must be verified against `mudBlazor_Docs/` before writing markup (per BeepWeb/AGENTS.md:51-57)
- Dark mode is supported but the default is the KOC light palette; document the override flow
- Command palette actions must respect authorization
