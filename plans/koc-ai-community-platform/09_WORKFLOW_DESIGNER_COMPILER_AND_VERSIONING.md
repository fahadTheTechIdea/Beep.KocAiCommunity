# Phase 09 — Workflow Designer, Compiler, and Versioning

**Status:** 🟡 PLANNING
**Dependencies:** Phase 08
**Goal:** Visual workflow designer on Z.Blazor.Diagrams, typed `WorkflowDefinition` JSON, immutable versioning, and a workflow compiler with cycle detection and type checks.

## 1. Goal and dependencies

- Z.Blazor.Diagrams 3.0.4.1 proof of concept on a real workflow
- Custom MudBlazor workflow nodes with typed ports
- Palette, drag/drop, connect validation, pan/zoom, minimap, selection, copy/paste, undo/redo, auto-layout, keyboard commands
- Property inspector, schema mapping, node help, validation panel, run controls
- Application-owned `WorkflowDefinition` JSON with schema version
- Immutable versions, drafts, publishing, import/export, templates, snapshot hashes
- Compiler: cycle detection, topological ordering, type compatibility, required-input validation, execution-level generation

## 2. Existing reference behavior

- Beep.AI.MLStudio jsPlumb editor: `Beep.AI.MLStudio/Beep.AI.MLStudio/static/js/workflow/workflow-builder.js:15-100`.
- Workflow contracts: `Beep.AI.MLStudio/Beep.AI.MLStudio/app/services/workflow/contracts.py:1-299`.
- Executor (Kahn): `Beep.AI.MLStudio/Beep.AI.MLStudio/app/services/workflow/executor.py:25-410`.
- Verifier: `Beep.AI.MLStudio/Beep.AI.MLStudio/app/services/workflow/verifier.py:59-262`.

## 3. Architecture decisions

| Decision | Choice | Rationale |
|---|---|---|
| Editor | Z.Blazor.Diagrams 3.0.4.1 | Native Blazor, MIT licensed, custom nodes |
| Why not Elsa Studio | Flowchart-only, UI validation missing | Decision recorded in Phase 00 |
| Why not jsPlumb | Last release 2021; MLStudio editor is fragile | Decision recorded in Phase 00 |
| Workflow JSON | Application-owned versioned `WorkflowDefinition` | Avoid coupling to editor library |
| Versioning | Immutable versions, draft → published | Provenance |
| Compiler | Static type checking + topological order | Predictable execution |

## 4. Project-by-project deliverables

### 4.1 Workflow

- `WorkflowDefinition` (JSON-serializable)
- `WorkflowNode`, `WorkflowEdge`, `WorkflowPort`, `WorkflowParameter`
- `WorkflowVersion`
- `WorkflowCompiler` (cycle detection, type checks, topological levels)
- `IWorkflowSerializer`, `IWorkflowValidator`

### 4.2 Application/Workflow

- `IWorkflowService`, `IWorkflowVersionService`, `IWorkflowTemplateService`
- DTO ↔ entity mapping

### 4.3 Domain

- `Workflow`, `WorkflowVersion`, `WorkflowTemplate`, `WorkflowRun`

### 4.4 Infrastructure

- EF Core configurations
- JSON serialization with schema version

### 4.5 API

- Workflow CRUD, versioning, templates, import/export, validation

### 4.6 UI

- `Pages/Studio/Workflows/Index.razor`
- `Pages/Studio/Workflows/Editor.razor`
- `Pages/Studio/Workflows/Versions.razor`
- `Pages/Studio/Workflows/Run.razor`
- `Components/Studio/WorkflowCanvas.razor`
- `Components/Studio/NodePalette.razor`
- `Components/Studio/PropertyInspector.razor`
- `Components/Studio/ValidationPanel.razor`
- `Components/Studio/RunControls.razor`

## 5. Entities and migrations

```csharp
public class Workflow : AuditableEntity
{
    public string Name { get; set; } = default!;
    public string Description { get; set; } = default!;
    public string OwnerUserId { get; set; } = default!;
    public Guid? ProjectId { get; set; }
    public Guid? DatasetId { get; set; }
    public KocDataClassification Classification { get; set; }
    public int LatestVersionNumber { get; set; }
}

public class WorkflowVersion : AuditableEntity
{
    public Guid WorkflowId { get; set; }
    public int VersionNumber { get; set; }
    public string Status { get; set; } = "draft";
    public int SchemaVersion { get; set; } = 1;
    public string DefinitionJson { get; set; } = default!;
    public string SnapshotHash { get; set; } = default!;
    public string? Notes { get; set; }
}

public class WorkflowTemplate : AuditableEntity
{
    public string Code { get; set; } = default!;
    public string DisplayName { get; set; } = default!;
    public string Description { get; set; } = default!;
    public string Domain { get; set; } = default!;
    public string DefinitionJson { get; set; } = default!;
    public int SchemaVersion { get; set; } = 1;
    public string SnapshotHash { get; set; } = default!;
}
```

## 6. API contracts

```http
GET    /api/v1/workflows?projectId=&page=
POST   /api/v1/workflows
GET    /api/v1/workflows/{id}
PUT    /api/v1/workflows/{id}
DELETE /api/v1/workflows/{id}
GET    /api/v1/workflows/{id}/versions
POST   /api/v1/workflows/{id}/versions
GET    /api/v1/workflows/{id}/versions/{versionNumber}
POST   /api/v1/workflows/{id}/versions/{versionNumber}/publish
POST   /api/v1/workflows/{id}/versions/{versionNumber}/archive
POST   /api/v1/workflows/{id}/versions/{versionNumber}/validate
POST   /api/v1/workflows/{id}/versions/{versionNumber}/compile
POST   /api/v1/workflows/{id}/import
GET    /api/v1/workflows/{id}/export

GET    /api/v1/workflow-templates?domain=
POST   /api/v1/workflow-templates/{code}/instantiate
```

## 7. MudBlazor pages and components

- Workflow canvas uses `Z.Blazor.Diagrams` with custom MudBlazor-rendered node templates
- Property inspector uses `MudForm`, `MudTextField`, `MudSelect`, `MudNumericField`, `MudSwitch`, `MudFileUpload`
- Validation panel uses `MudList` + `MudChip`

## 8. Security and authorization

- Employee minimum
- Project members can read workflows
- Project owners and PlatformAdmin can edit and publish
- Classification enforced on download/export

## 9. Tests

- Unit: compiler cycle detection, type compatibility, execution level generation
- Unit: serializer schema version migration
- Integration: workflow lifecycle, version immutability
- Component: canvas drag/drop, palette search, property inspector

## 10. Verification commands

```bash
dotnet test tests/Beep.KocAiCommunity.UnitTests --filter "FullyQualifiedName~Workflow"
```

## 11. Acceptance gate

- 200-node workflow remains usable in the browser
- Round-trip serialization without data loss
- Cycle detection rejects invalid workflows
- Type compatibility is enforced
- Version immutability enforced
- Tests pass

## 12. Risks and deferred work

- Auto-layout algorithm needs careful selection (dagre or ELK); benchmark first
- Z.Blazor.Diagrams customization requires deep DOM/JS interop testing
- Interactive debugging (breakpoints, step-through) is explicitly deferred
