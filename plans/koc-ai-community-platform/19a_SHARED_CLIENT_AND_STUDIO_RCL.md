# 19a — Shared Client library + Studio UI extraction (Stages 1–2)

Foundation for the desktop app: make the API client and the Studio Blazor UI into
**shared libraries** both Web and WinForms consume. Behaviour-preserving — the Web
app must look and work exactly as before. See `19_WINFORMS_DESKTOP_STUDIO.md` for
the overall architecture.

## Stage 1 — `Beep.KocAiCommunity.Client` (extract the HTTP client)

**Goal:** move the framework-agnostic client out of Web into a reusable library.

**Work**
- New project `src/Beep.KocAiCommunity.Client` (`net10.0`, plain class library),
  referencing **Contracts** only. Add to `Beep.KocAiCommunity.slnx`.
- Move from `src/Beep.KocAiCommunity.Web/Services/`:
  `KocApiClient.cs` (+ `IKocApiClient`), `DevIdentity.cs` (incl. `DevIdentityHandler`,
  `Persona`), `RealtimeOptions.cs`. Keep the namespace stable or add usings — these
  types have **no Blazor/MudBlazor dependency** (`KocApiClient` uses only
  `System.Net.Http.Json` + Contracts DTOs), so they move cleanly.
- Add a DI helper `ServiceCollectionExtensions.AddKocHttpClient(this IServiceCollection,
  string apiBaseUrl)` that registers `DevIdentity`, `DevIdentityHandler`, the typed
  `HttpClient<IKocApiClient,KocApiClient>` with base address + handler, and
  `RealtimeOptions` — i.e. exactly what `Web/Program.cs` does today.
- Web references the new project; `Web/Program.cs` calls `AddKocHttpClient(apiBaseUrl)`
  instead of the inline registration. Delete the moved files from Web.

**Key files:** `src/Beep.KocAiCommunity.Client/*`, `Web/Program.cs`, `Web csproj`.

**DoD / verification:** solution builds `-warnaserror`; `dotnet format` clean; full
test suite green (component tests still render Web pages); the Web app is unchanged
at runtime. Commit.

## Stage 2 — Move the Studio Blazor UI into `Ui.Studio`

**Goal:** the designer + registry + node visuals live in the shared RCL so the
desktop can host the same components.

**Work**
- Into `src/Beep.KocAiCommunity.Ui.Studio` (existing RCL; add package refs for
  `MudBlazor` + `Z.Blazor.Diagrams`, project ref to **Client**) move from Web:
  - `Components/Pages/WorkflowDesigner.razor`, `Components/Pages/Workflows.razor`
  - `Components/Dialogs/CreateWorkflowDialog.razor`, `RunWorkflowDialog.razor`
  - `Diagrams/NodeVisuals.cs`, `Diagrams/MlNode.cs`
  - the competition submit/browse component reused on the desktop (`Compete.razor`
    may move to `Ui.Community` or `Ui.Studio` — pick the one the desktop references).
- Fix namespaces/`_Imports.razor` in `Ui.Studio` (MudBlazor, Blazor.Diagrams,
  Contracts, Client, Ui.Shared). The routes (`@page "/workflow"`, `/workflows`) stay;
  RCL pages are discovered by the Web router via `AdditionalAssemblies` — confirm the
  Web `Router` already includes the Ui.Studio assembly (it references it).
- Web keeps only app wiring; its Studio pages now come from the RCL.

**Key files:** `Ui.Studio/*`, `Ui.Studio csproj`, Web `Routes`/`_Imports`, Web csproj.

**DoD / verification:** build `-warnaserror`; format clean; the bUnit **component
tests** that touch Studio still pass (update namespaces if needed); manually confirm
`/workflow` and `/workflows` render in the Web app from the RCL. Commit.

## Notes

- Keep each stage a separate commit so a regression in the Web app is easy to bisect.
- No functional change in these two stages — they are pure extraction/relocation. The
  desktop app is not added until Stage 3 (`19b`).
