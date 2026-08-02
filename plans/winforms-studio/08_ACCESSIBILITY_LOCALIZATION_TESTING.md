# 08 — Accessibility, localization, and testing

> **Depends on:** the surfaces built by 01–07. **Gates:** calling the desktop finished.

## Context

Three things have never been assessed on the desktop:

1. **Accessibility.** Not once. The canvas in particular is a drag-and-drop surface with no known
   keyboard path, which means anyone who cannot use a mouse cannot build a pipeline.
2. **Arabic on the desktop specifically.** The chrome was localized and the switcher added on
   2026-08-02, but the app has not been *run* in Arabic. The shared node property panel is localized;
   whether the canvas mirrors sensibly is unknown.
3. **Automated coverage of the desktop shell.** `LocalDatasetStore` has tests. The pages have none —
   `Datasets.razor`, `Settings.razor`, `Competitions.razor` and the layout are unverified.

There is also no performance budget. Nobody knows the cold-start time, and BlazorWebView apps are
notorious for a slow first paint.

## Scope

**In**

- Keyboard reachability across the app, including the canvas
- Screen-reader labelling and a contrast audit in both themes
- Arabic RTL verified by running it
- Component tests for the desktop pages
- A measured cold-start figure and a budget

**Out**

- WCAG certification. This is an internal tool; the goal is that it can be used, not a compliance badge.
- Localization beyond English and Arabic.
- Load testing. Single-user desktop.

## Design

### Keyboard

The canvas is the hard part and the reason this is a phase rather than a checklist item.

| Surface | Requirement |
|---|---|
| Nav, dialogs, forms | Tab order follows visual order; focus always visible |
| Node palette | `Ctrl+K` focuses search; arrows move; `Enter` adds (Phase 03) |
| **Canvas** | Tab cycles nodes; arrows nudge a selected node; `Delete` removes; `Enter` opens properties |
| **Connections** | A keyboard path to connect two nodes — the genuinely hard one |
| Property panel | Reachable from a selected node without the mouse |
| Run / Stop | Keyboard-accessible, with a shortcut |

Connecting nodes by keyboard has no obvious idiom. The workable pattern: select source, press `C`,
choose the target from a filtered list of valid targets, `Enter`. The validation from Phase 03 already
knows which targets are legal, so this reuses it rather than inventing rules.

> If keyboard connection proves too costly, **say so** and record it as a known limitation rather than
> claiming keyboard support that stops at the interesting part.

### Screen readers

MudBlazor emits reasonable ARIA for its own components. Ours needs:

- `aria-label` on every icon-only button — several exist in the desktop chrome
- The canvas as an `application` region with a text description of the graph, so a screen reader user
  can hear the pipeline's shape
- Live regions for run progress and the connection state, so changes are announced
- Announce validation errors when they appear, not only visually

Test with Narrator, which is on every Windows machine and is what a KOC user would have.

### Contrast

Both themes, against WCAG AA (4.5:1 body, 3:1 large). The petrol/brass palette has not been checked.
Most likely offenders: caption text at `opacity:.7`, disabled states, and the node status colours on
the canvas.

### Arabic on the desktop

The gap is that it has never been run. Specifically:

- Does the app bar mirror correctly?
- Does the **canvas** mirror — and *should* it? A pipeline reads left-to-right as a data flow. Mirroring
  it to right-to-left may be correct for an Arabic reader or may be confusing. **This needs a native
  Arabic speaker's judgement, not a developer's guess.**
- Do the node property panel's fields lay out sensibly?
- Do numbers stay Western? (They should — `FormattingCulture` is pinned — but verify on the desktop path,
  which sets culture differently from the web.)

The canvas-direction question is the interesting one and should be answered by asking, not deciding.

### Component tests

`bunit` already tests Web pages. The desktop pages are ordinary Blazor components and can be tested the
same way — the barrier is that they resolve desktop services, which the existing `ComponentTests` project
does not reference.

Add `Desktop.Local` and `WinForms` references, or a small `DesktopComponentTests` project if that pulls
WinForms into a test host awkwardly. Cover: the datasets page empty state and import path, settings
persistence, the competitions offline state, and the layout's language switch.

### Performance

| Metric | Budget | Why |
|---|---|---|
| Cold start to interactive | < 5 s | BlazorWebView first paint is the known risk |
| Warm start | < 2 s | |
| Dataset list, 100 datasets | < 500 ms | |
| Canvas with 40 nodes | 60 fps pan/zoom | The designer is the app |
| Memory at rest | < 400 MB | Leaves room for training |
| Memory during training | Phase 04's cap + 400 MB | Must not exhaust the machine |

Measure before optimising. The Phase 00 research points at `ShouldRender` and `@key` as the levers if
the canvas is the problem.

## Files

| File | Change |
|---|---|
| `Ui.Studio/Pages/WorkflowDesigner.razor` | Keyboard navigation, ARIA, live regions |
| `Ui.Shared/wwwroot/css/koc-blueprint.css` | Contrast fixes |
| `WinForms/Components/*.razor` | `aria-label` on icon-only controls; focus order |
| `tests/…/DesktopComponentTests/` | New — or desktop references added to ComponentTests |
| `docs/DESKTOP_ACCESSIBILITY.md` | New — what is supported and what is not |

## Acceptance criteria

- [ ] Every function reachable by keyboard, or the exception is documented
- [ ] A pipeline can be built end to end without a mouse — or the limitation is recorded plainly
- [ ] Focus is visible everywhere
- [ ] Narrator can navigate the app and hear the pipeline's shape
- [ ] Both themes meet AA for body and large text
- [ ] The app has been **run** in Arabic and the findings recorded
- [ ] The canvas-direction question has been put to a native speaker and answered
- [ ] Numbers render Western in Arabic on the desktop
- [ ] Desktop pages have component tests
- [ ] Cold start measured and inside budget, or the gap recorded

## Tests

| Test | Level |
|---|---|
| Datasets empty state renders the import prompt | Component |
| Import adds to the list and clears the empty state | Component |
| Settings persist across a remount | Component |
| Competitions offline shows the cached banner | Component |
| Language switch writes settings and notifies | Component |
| Every icon-only button has an accessible name | Component — assert across all desktop pages |
| Canvas keyboard: add, select, move, connect, delete | Component |

That icon-button test is worth writing as a sweep rather than per-page: it catches the next one somebody
adds without a label, which is the actual failure mode.

## Risks

| Risk | Mitigation |
|---|---|
| Keyboard connection on the canvas proves impractical | Time-box it. Document the limitation rather than shipping a half path |
| Contrast fixes change the KOC brand palette | Adjust text weight and opacity before hue; escalate if the brand colour itself fails |
| RTL canvas mirroring is wrong for pipelines | Ask a native speaker before implementing either way |
| Desktop component tests drag WinForms into the test host | Separate project if so; the pages themselves are plain Blazor |
| Performance budget missed late | Measure at the start of the phase, not the end |
