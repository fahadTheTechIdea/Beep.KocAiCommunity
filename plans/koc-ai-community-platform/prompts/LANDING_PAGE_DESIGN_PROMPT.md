# Home / Launch Page Design Prompt — KocAiCommunity

> Copy-pasteable prompt for another LLM or designer. Customize the bracketed values before using.
>
> **This is an internal Kuwait Oil Company application, not a commercial product.** The page is the internal
> home/launch page KOC employees see: sign-in, orientation, and quick access. It is **not** marketing or sales —
> no external audience, no pitch, no pricing, no vendor comparisons. Write it like a well-designed internal
> corporate tool home page: clear, factual, functional.
>
> **A worked example already exists — build from it.** The reference mockup at
> `Beep.KocAiCommunity/Example - Koc ai training landing page/KocAiCommunity Landing.dc.html` expresses the
> intended visual system, section order, and — critically — the **role switcher** that re-scopes the whole page
> per KOC position level. Reproduce that design language in MudBlazor. Do not invent a different look.

---

## Reference files (consult before starting)

- **Example mockup (the source of truth for look & structure):** `Beep.KocAiCommunity/Example - Koc ai training landing page/KocAiCommunity Landing.dc.html`
- **KOC logo:** `Beep.KocAiCommunity/Assets/KOC_Logo.png` — the blue KOC eagle wordmark; use it in the nav (top-left) and footer. Preserve clear space; never recolor or stretch it.
- **O&G icon library:** `Beep.KocAiCommunity/Assets/icons/` — 236 domain PNG icons (wells, pumps, pipelines, tanks, gauges, refineries, plus named ones like `PAD.png`, `rigmovement.png`, `stuckwells.png`, `drillingactivities.png`). Use these for domain flavor (connector chips, worked-example nodes, track/competition cards). For generic UI affordances use MudBlazor Material icons; do not mix icon styles within one component.
- `Beep.KocAiCommunity/plans/koc-ai-community-platform/README.md`
- `Beep.KocAiCommunity/plans/koc-ai-community-platform/02_ENTRA_ID_SECURITY_AND_RBAC.md` — org hierarchy, position roles, visibility
- `Beep.KocAiCommunity/plans/koc-ai-community-platform/13_COMPETITIONS_AND_LEADERBOARDS.md` and `13a_LEARNING_TRACKS_AND_UPSKILLING.md`
- `Beep.KocAiCommunity/plans/koc-ai-community-platform/references/TECHNOLOGY_MATRIX.md`
- `Beep.KocAiCommunity/mudBlazor_Docs/` — every MudBlazor component used must be verified here first.

## Role

You are a **senior enterprise UX/UI designer for internal corporate applications** (intranet-grade line-of-business
tools) in regulated industries (oil & gas, energy). You design for people who already work at KOC and need to get
into the tool and understand what it does — not for prospects who must be sold. You understand KOC-grade restraint:
confident, structured, conservative, trustworthy.

## Project

**KocAiCommunity** is a dedicated, single-tenant, **internal** platform whose primary purpose is to **train and
familiarize KOC employees with AI and machine learning**. Employees **learn** through guided tracks and then
**compete** in internal, Kaggle-style competitions on real KOC data. Management **supervises** adoption through
org-scoped rollup dashboards. It combines:

- A **Learn & Compete** core — learning tracks (Phase 13a) + internal competitions with leaderboards (Phase 13).
- A **Studio** surface — visual ML workflow designer, ML.NET trainers, AutoML, experiments, model registry.
- A **Community** surface — discussions, datasets, projects, all inside KOC.
- **Platform/Competition/Learning admin** — organize competitions, author tracks, manage settings and audit.

Built on **.NET 10 (LTS), ASP.NET Core, MudBlazor 9.7**, hosted in the **Azure Kuwait region**, authenticated via
**Microsoft Entra ID** (KOC tenant only). KOC enterprise integrations: PPDM 39, OpenWells, EcoSys, SAP, AVEVA PI,
ADLS Gen2. Audience: KOC employees only. Nothing is sold, licensed, or exposed outside KOC.

## The KOC org hierarchy (drives the whole page)

KOC is modelled as **Team ⊂ Group ⊂ Directorate ⊂ Company (KOC)**. Every person has a position level in the
reporting line: **Employee → Team Leader → Manager → DCEO → CEO** (Team Leader leads a Team, Manager a Group, DCEO a
Directorate, CEO the Company). Employees are the primary players — they learn and compete, and "have the most fun."
Managers supervise: each level sees a **read-only rollup** of the org subtree beneath it.

### Role switcher (core interaction — reproduce from the example)

A sticky **"View as"** switcher under the nav lets the viewer preview the page as each position level. The switcher
re-scopes: the hero eyebrow/headline/sub-headline, the value pillars, the "at your level" priorities, and the
participation dashboard. In production the signed-in user's actual position selects the default view; the switcher
is primarily an orientation/demo affordance and must be keyboard-operable (`role="tablist"`).

| View | Hero framing | Participation dashboard shows | Emphasis |
|---|---|---|---|
| **Employee** | "Your AI & ML workspace." | **Personal**: competitions entered, your rank, submissions; "my competitions" cards with rank/score/action | Fun, hands-on: learn a track, join a challenge, climb the leaderboard |
| **Team Leader** | "Your team's AI & ML workspace." | **Team**: member table — who's active, competitions entered, best rank | Upskill the team; run team challenges |
| **Manager** | "Your department's AI & ML workspace." | **Group**: teams table — people active, competitions, best standing | Capability at scale; adoption you can see; governed |
| **DCEO** | "Your directorate's AI & ML workspace." | **Directorate**: groups table — people active, competitions, models in use | Capability without dependency; risk under control |
| **CEO** | "KOC's AI & ML workspace." | **Company**: directorates table — people active, competitions, models in use | A sovereign AI capability; a skilled workforce; accountable |

All supervisory numbers are **read-only oversight**. A manager sees how their people are doing; they never submit or
edit on someone's behalf. (See `AUTHORIZATION_MATRIX.md`.)

## Brand direction — the "blueprint" system (from the example)

The example establishes an engineering **blueprint** aesthetic. Reproduce it via CSS custom properties / MudBlazor
theme tokens; do not hardcode hex in markup.

- **Design tokens** (define once, use everywhere): `--color-bg`, `--color-text`, `--color-surface`, `--color-divider`, `--color-accent`, `--color-accent-300/500/700/900`, `--font-heading`, `--shadow-lg`. Map these into the MudBlazor theme (`PaletteLight`, typography) so components and custom blocks share one palette.
- **Accent = KOC blue**, taken from `KOC_Logo.png` (petroleum/corporate blue, ~`#1466A5`–`#1B75BB` family; sample the logo for the exact value and derive the 300/500/700/900 ramp). White/near-white surfaces, neutral grays for text, restrained success/warning/error. No bright marketing colors, no gradients on text, no neon, no emoji.
- **Blueprint cards:** bordered panels with small **corner ticks** at all four corners (the example's `.blueprint` + `.corner tl/tr/bl/br`), thin dividers, and light **grid-paper backgrounds** on canvas/figure areas. This is the signature look — use it for stat tiles, competition cards, tables, and the worked-example figure.
- **Typography:** a display **heading font** (`--font-heading`) used **UPPERCASE** with slight letter-spacing for section headers and stat numbers; a clean sans for body (Inter / IBM Plex Sans / KOC standard). Body line-height 1.5–1.7. Self-host fonts.
- **Tone:** confident, factual, no hype, active voice, no exclamation marks, no marketing clichés and no sales language ("best-in-class", "why teams choose"). Prefer specific nouns: "PPDM 39 well dataset", "AutoML binary classification", "ESP failure risk".
- **Bilingual readiness:** English primary; structure supports a future Arabic RTL mirror without redesign (EN / العربية toggle in the nav, as in the example).
- **Dark mode:** not required for v1; design for light with neutral surface variants.

## Information architecture (top to bottom — matches the example)

1. **Sticky top nav** — KOC logo + "KocAiCommunity" wordmark (left); anchors Learn / What's inside / Your data / See it work / Trust; EN·العربية toggle; primary action **"Sign in with KOC"**.
2. **"View as" role switcher** (sticky, below nav) — Employee / Team Leader / Manager / DCEO / CEO.
3. **Hero** — role-scoped eyebrow + headline + sub-headline; primary **Sign in with KOC** + secondary (Browse learning tracks / View governance); a trust line; and a **worked-example blueprint figure** on the right (e.g. "Predicting pump failures": WELL DATA → TIDY UP → TEACH → PREDICT with a "94% caught / 6 wks earlier" results rail).
4. **Participation dashboard (role-scoped)** — 3 stat tiles + either the Employee's personal competition cards or the supervisor's scoped participation table (Team/Group/Directorate/Company per the table above).
5. **Value pillars (role-scoped)** — 3 blueprint cards; content changes per role (Employee: learn on your work / compete with colleagues / share across KOC; managers: upskill the team / adoption you can see / governed by default; etc.).
6. **"At your level" priorities** — role-scoped checklist + 3 stat tiles.
7. **Learn & compete** — 3 track cards (Getting started → Solve a real problem → Make it dependable, with level, lesson count, hours) + a **live challenge** card (e.g. "Spot the pump before it fails", teams entered / attempts) + a **leaderboard** table (rank, team, accuracy, trend).
8. **Capabilities (tabs)** — Community · Datasets · Workflows · Experiments & models · Competitions; each tab: a factual bullet list + a screenshot slot.
9. **Integrations** — connector chips (PPDM 39, OpenWells, AVEVA PI, EcoSys, SAP, ADLS Gen2) each opening a detail drawer (what it holds, default classification, sign-in mode) + a 3-step flow: **Bring your data → Build a model → Put it to use**.
10. **Workflow designer preview** — a blueprint canvas of a real O&G workflow (PI tags → fill gaps → split by time → learn patterns → model registry) + a factual "what this canvas gives you" list.
11. **Governance & trust** — 6 tiles (KOC only; your KOC login; data stays labelled; protected end to end; full record kept; hosted in Kuwait) + an "at a glance" facts plate (who it's for, learning tracks, connected systems, where it runs, activity retained 7 yrs).
12. **Access / CTA band** — "Sign in with your KOC account"; guidance if sign-in fails (contact division head / platform team); accent-900 background.
13. **Footer** — logo + one-line internal description; columns Platform / Help / Policies; "Internal use only" + version stamp.

## Creating a competition, dataset, or project — show the visibility choice

Where the page (or a linked create flow) previews creation, surface the **"who can see this"** control: a
Team / Group / Directorate / Company segmented selector defaulting to the creator's own units, with a live
**audience-count preview**. This is the org-scoped visibility model (`02_ENTRA_ID_SECURITY_AND_RBAC.md`) and applies
to competitions, datasets, and projects. Note that a **CompetitionAdmin** can create competitions *and* the datasets
and projects that back them, at any visibility scope they are permitted.

## Content voice rules

- Describe capabilities factually; do not sell, promise ROI, or use adoption/marketing language.
- Use KOC terminology: "subsurface", "upstream operations", "production", "facilities", "HSE"; avoid generic "data science".
- Numbers must be plausible and conservative; no fabricated KPIs.
- No testimonials or case studies (internal application). Use clearly-marked placeholders only if a quote slot is unavoidable.
- Compliance language plain and verifiable: "hosted in Azure Kuwait Central", not "fully sovereign".

## Component inventory (MudBlazor only)

- Layout: `MudLayout`, `MudAppBar`, `MudDrawer`, `MudMainContent`, `MudContainer`, `MudGrid`, `MudItem`, `MudStack`, `MudPaper`, `MudSpacer`.
- Navigation: `MudTabs`, `MudNavLink`, `MudBreadcrumbs`, `MudToggleGroup`/`MudButtonGroup` (role switcher, "view as").
- Buttons: `MudButton`, `MudIconButton`, `MudToggleIconButton`.
- Inputs: `MudTextField`, `MudSelect`, `MudSwitch`, `MudChip`, `MudChipSet`.
- Data display: `MudCard`, `MudList`, `MudSimpleTable`, `MudDataGrid` (leaderboard, participation tables), `MudProgressLinear` (track progress).
- Feedback/overlay: `MudTooltip`, `MudIcon`, `MudBadge`, `MudDrawer` (connector detail).
- Typography: `MudText` (`Typo.h1`…`Typo.body2`).

The blueprint corner-tick cards and grid-paper figures are custom CSS wrappers around `MudPaper`. Verify every
MudBlazor component, parameter, and event against `mudBlazor_Docs/` before writing markup. Do not invent components.

## Layout and responsive behavior

- Desktop ≥ 1280px: 12-column grid, ~1200px container, generous section padding (the example uses `clamp()`); asymmetric hero.
- Tablet 768–1279px: reduced columns; participation tables scroll inside `overflow-x:auto`.
- Mobile < 768px: single column; hero figure stacks below text; role-switcher tabs wrap; nav anchors collapse; flow arrows rotate to vertical.
- All interactive targets ≥ 44×44 px; sticky nav + role switcher remain reachable.

## Accessibility requirements

- WCAG 2.2 AA. Body contrast ≥ 4.5:1; large text / UI ≥ 3:1. Verify the KOC-blue accent on white meets AA for its use.
- Full keyboard navigation incl. the role switcher and capability tabs (`role="tablist"`, `aria-selected`, arrow keys).
- Skip-to-content link first; visible focus rings; one `<h1>` (hero) with correct heading order.
- Icons-only buttons have `aria-label`; images have meaningful `alt` (KOC logo alt "Kuwait Oil Company"); decorative blueprint ticks are `aria-hidden`.
- Honor `prefers-reduced-motion`; all form fields labeled with error messages.

## Performance and engineering constraints

- SSR for first paint, Interactive Server thereafter.
- Self-host fonts; no external font/icon/CDN on the critical path; no marketing-tag or third-party analytics scripts.
- Reference `Assets/` images by app-relative path (copied to `wwwroot`); optimize PNGs; provide width/height to avoid CLS.
- Total JS ≤ 50 KB first interactive; LCP < 1.5s on the internal KOC network; CLS < 0.05.

## Deliverables

1. **Information architecture** — section list, anchor IDs, scroll order.
2. **Theme tokens** — the `--color-accent` ramp sampled from `KOC_Logo.png`, `--font-heading`, surfaces/dividers, mapped into the MudBlazor `PaletteLight`/typography; a reusable **blueprint card** CSS partial (corner ticks + grid backgrounds).
3. **Role-scoped content model** — the per-role hero/pillars/priorities/dashboard content (a data structure like the example's `roleConfig`/`dashConfig`), so one page renders all five views.
4. **Wireframe per section** — annotated layout showing grid, blocks, and component choices.
5. **Markup skeleton** — MudBlazor `Home.razor` (+ `HomeLayout.razor` if needed, `@page "/"`) with the role switcher wired to the content model; the shared `VisibilityScopePicker` referenced where creation is shown.
6. **Image/asset plan** — which `Assets/icons/*` map to which connector chips, track cards, and worked-example nodes; logo placement; screenshot slots.
7. **Copy deck** — per-role headlines/sub-headlines, pillar copy, governance tiles, trust/fact lines, action labels.
8. **Accessibility annotations** — focus order, ARIA for role switcher & tabs, alt text per image.
9. **Responsive notes** — breakpoints where each component changes.
10. **Acceptance test list** — verifiable checks per section, including per-role rendering.

## Acceptance tests (must all pass before sign-off)

- Role switcher: selecting each of Employee/Team Leader/Manager/DCEO/CEO re-scopes hero, pillars, priorities, and the participation dashboard to the matching content; keyboard-operable.
- Employee view shows personal competition cards (rank/score); managerial views show the correctly-scoped participation table (Team/Group/Directorate/Company).
- Visual regression at desktop/tablet/mobile; no horizontal body scroll (wide tables scroll in their own container).
- Keyboard-only navigation reaches every interactive element; screen reader announces the hero `<h1>`, primary action, and section headings in order.
- Contrast verifier reports zero AA failures (including KOC-blue accent usage).
- `Sign in with KOC` routes to `/signin-oidc` and completes the OIDC round-trip.
- KOC logo renders from `Assets/KOC_Logo.png`; domain icons render from `Assets/icons/`; no third-party branding.
- Lighthouse mobile: Performance ≥ 90, Accessibility ≥ 95, Best Practices ≥ 95. (SEO is **not** a target — authenticated internal page, not indexed.)
- All MudBlazor APIs match `mudBlazor_Docs/`; page is bUnit-testable; localization strings extracted for a future Arabic RTL mirror.

## Out of scope

This is an internal application; all commercial-product / marketing surfaces are out of scope:

- Public sign-up (no external users); SEO / indexing.
- Marketing analytics or third-party tracking; live chat / lead-capture widgets; cookie banner.
- Pricing, plans, licensing, or any purchase flow (nothing is sold).
- Comparison tables versus external vendors; testimonials, case studies, or persuasive "adoption" narrative.
