# 19b — WinForms BlazorWebView shell (Stage 3)

Stand up the desktop app hosting the shared Studio Blazor UI. This stage proves the
**hybrid host renders** (MudBlazor + `Z.Blazor.Diagrams` inside `BlazorWebView`) and
works against a **running API** as a thin client — before local execution (Stage 4)
replaces the backend. See `19_WINFORMS_DESKTOP_STUDIO.md`.

## Goal

A runnable `Beep.KocAiCommunity.WinForms` app whose main window is a `BlazorWebView`
that renders `WorkflowDesigner` from `Ui.Studio`, talking to the API via the shared
HTTP `IKocApiClient`.

## Work

- New project `src/Beep.KocAiCommunity.WinForms`:
  - `net10.0-windows`, `<UseWindowsForms>true</UseWindowsForms>`,
    `<OutputType>WinExe</OutputType>`.
  - PackageRef `Microsoft.AspNetCore.Components.WebView.WindowsForms` (add its version
    to `Directory.Packages.props`) + `MudBlazor`.
  - ProjectRefs: `Ui.Studio`, `Ui.Shared`, `Client`, `Contracts`.
  - Add to `Beep.KocAiCommunity.slnx`. It is Windows-only — record that Linux CI
    excludes this project (solution filter or `-p:...` guard); shared libs stay green
    cross-platform.
- Host wiring (WinForms + Blazor):
  - `Program.cs`: `ApplicationConfiguration.Initialize()`; build a
    `Microsoft.Extensions.Hosting`/`ServiceCollection` with `AddWindowsFormsBlazorWebView()`,
    `AddMudServices()`, and `AddKocHttpClient(apiBaseUrl)` (Stage 1 helper); run
    `MainForm`.
  - `MainForm.cs`: a `BlazorWebView` docked fill; `HostPage = "wwwroot/index.html"`;
    `RootComponents.Add<Shell>("#app")`.
  - `wwwroot/index.html`: minimal Blazor host page loading MudBlazor CSS/JS and the
    `Ui.Studio`/`Ui.Shared` `_content/...` static assets (RCL static web assets are
    served by BlazorWebView automatically).
  - `Shell.razor`: a MudBlazor layout + a `Router` with
    `AdditionalAssemblies = [ typeof(WorkflowDesigner).Assembly ]` so the RCL routes
    (`/workflow`, `/workflows`) resolve; default route → the designer.
- Provide a minimal `DevIdentity` persona and `RealtimeOptions` so the reused
  components' injected services resolve.

## Smoke test (the point of this stage)

Launch the app with the API running: the designer canvas renders, the node palette
populates (from `GET /ml/nodes`), a node's property inspector works, and dragging
`dataset → split → train → evaluate` + Run against a server dataset produces
node-by-node results. This confirms MudBlazor + Z.Blazor.Diagrams behave in WebView2.

## Key files

`src/Beep.KocAiCommunity.WinForms/{Program.cs, MainForm.cs, Shell.razor,
wwwroot/index.html, *.csproj}`, `Directory.Packages.props`, `Beep.KocAiCommunity.slnx`.

## DoD / verification

- `dotnet build src/Beep.KocAiCommunity.WinForms` succeeds on Windows;
  `dotnet build Beep.KocAiCommunity.slnx -warnaserror` clean; format clean; existing
  test suite unaffected (no new automated tests this stage — it is a host).
- Manual: the smoke test above passes. Commit.

## Risks

- WebView2 runtime must be present (standard on Win10/11; the installer/first-run can
  prompt if missing).
- Static assets: if MudBlazor/Blazor.Diagrams CSS/JS don't load, fix the `index.html`
  `_content/...` references before proceeding.
