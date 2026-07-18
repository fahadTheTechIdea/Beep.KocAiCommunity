# Solution Dependency Map

Project-by-project dependency graph with rationale. Arrows point from dependent to dependency.

## Apps

```
Beep.KocAiCommunity.AppHost
  ├── ServiceDefaults
  ├── Web
  ├── API
  └── Worker

Beep.KocAiCommunity.Web
  ├── Ui.Shared
  ├── Ui.Community
  ├── Ui.Studio
  ├── Ui.Admin
  └── Application (DTOs only)

Beep.KocAiCommunity.Api
  ├── Application
  ├── Infrastructure
  ├── Domain
  ├── Contracts
  └── Connectors.Abstractions

Beep.KocAiCommunity.Worker
  ├── Application
  ├── Infrastructure
  ├── Domain
  ├── ML
  └── Workflow
```

## Libraries

```
Contracts
  (no project references; pure DTOs)

Domain
  (no project references; pure entities and enums)

Application
  ├── Domain

Infrastructure
  ├── Application
  ├── Domain

ML
  ├── Application
  ├── Domain
  └── Workflow

Workflow
  ├── Application
  └── Domain

Connectors.Abstractions
  ├── Application
  └── Domain

Connectors.PPDM        → Connectors.Abstractions
Connectors.OpenWells   → Connectors.Abstractions
Connectors.EcoSys      → Connectors.Abstractions
Connectors.Sap         → Connectors.Abstractions
Connectors.Pi          → Connectors.Abstractions
Connectors.AdlsGen2    → Connectors.Abstractions

Ui.Shared      → (MudBlazor only)
Ui.Community   → Ui.Shared, Application (DTOs only)
Ui.Studio      → Ui.Shared, Application (DTOs only)
Ui.Admin       → Ui.Shared, Application (DTOs only)
```

## Tests

```
UnitTests          → all libs
IntegrationTests   → Api, Worker, Infrastructure
ComponentTests     → Web, Ui.*
ArchitectureTests  → all projects (validates the rules below)
EndToEndTests      → AppHost
SecurityTests      → Api, Worker, Web
PerformanceTests   → Api, Worker
MigrationTests     → Infrastructure
```

## Rationale

- **Web does not reference Infrastructure, EF Core, or ML.NET.** Web is a thin UI shell that consumes typed API clients. This is enforced by `ArchitectureTests`.
- **UI projects reference Application only for DTOs.** UI never imports entities, EF Core, or ML.NET types. Enforced by `ArchitectureTests`.
- **Domain has no project references.** Pure C# types and enums.
- **Contracts has no project references.** Pure DTOs.
- **Workflow does not depend on ML.** Workflow produces a typed execution plan that the Worker dispatches to ML.
- **Connectors are adapter pattern.** Each connector is its own project so additions are isolated.
- **Worker does not depend on Web or UI.** Worker is headless.

## Architecture rules enforced by `ArchitectureTests`

1. Web does not reference Infrastructure.
2. Web does not reference EF Core.
3. UI projects do not reference Infrastructure or EF Core.
4. Domain has zero project references.
5. Contracts has zero project references.
6. Connectors.* projects do not reference ML or Workflow.
7. Application does not reference Infrastructure.
8. Worker does not reference Web or any UI project.
9. Api does not reference Web or any UI project.
10. Cycles are forbidden.

## Anti-patterns blocked by the dependency map

| Anti-pattern | Where prevented |
|---|---|
| Web reaching into EF Core | Web → Infrastructure not allowed |
| UI importing entities | UI → Application (DTOs only) |
| Domain depending on infrastructure | Domain has no project refs |
| Worker serving UI | Worker does not reference Web/UI |
| Cross-UI library imports | Each UI depends on Ui.Shared, not on each other |
