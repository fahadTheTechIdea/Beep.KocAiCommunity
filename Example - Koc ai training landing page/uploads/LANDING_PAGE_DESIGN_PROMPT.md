# Landing Page Design Prompt — KocAiCommunity

> Copy-pasteable prompt for another LLM or designer. Customize the bracketed values before using.

---

## Role

You are a **senior product designer and brand consultant** with deep experience designing B2B enterprise SaaS landing pages for mission-critical, regulated industries (oil & gas, energy, finance). You have shipped MudBlazor + .NET enterprise applications and understand KOC-grade brand restraint: confident, structured, conservative, and trustworthy. You balance executive-grade narrative with practitioner-grade proof.

## Project

**KocAiCommunity** is a dedicated, single-tenant AI collaboration and ML Studio platform for **Kuwait Oil Company (KOC)**. It combines:

- An internal **Community** surface (collaboration, discussions, datasets, projects) for KOC employees only.
- A **Studio** surface (visual ML workflow designer on Z.Blazor.Diagrams, ML.NET 5.0 trainers, AutoML, experiment tracking, model registry, inference).
- **Platform Admin** (settings, audit, role management, connector health).

KOC enterprise integrations: PPDM 39, OpenWells, EcoSys, SAP, AVEVA PI historian, ADLS Gen2.

The app is built on **.NET 10 (LTS), ASP.NET Core, MudBlazor 9.7** and is hosted in the **Azure Kuwait region**. Authentication is via **Microsoft Entra ID** (KOC tenant only). The audience is KOC employees only — there are no public users.

## Audience for the landing page

The landing page speaks to **two primary audiences in one page**:

1. **KOC executives** (CIO/CDO/digital transformation leads / division heads) — they sign off on adoption and budget. They want to see strategic value, ROI, security and compliance posture, governance, and risk reduction.
2. **KOC practitioners** (data engineers, ML engineers, domain experts, reservoir engineers, production engineers, HSE analysts) — they will use the platform daily. They want to see workflow designer capability, ML.NET runtime, connector coverage, dataset and experiment features, and how their day improves.

A third soft audience is **KOC information security and audit** — they need to see classification, audit, and tenant isolation visible at a glance.

## Brand direction

- **Style:** Corporate KOC brand. Confident, structured, conservative, trustworthy. Treat the page as if it were the KOC intranet: heavy use of structured grids, clear hierarchy, no marketing fluff.
- **Color palette:** Use KOC corporate colors. Anchor on a deep navy/petroleum blue (#0B2545 family), KOC teal as accent (#1F8A8C or similar), white surfaces, neutral grays for text, restrained accent for warnings/success/error. Avoid bright marketing colors. Avoid gradients on text. No emoji. No neon.
- **Typography:** Professional sans-serif (Inter, IBM Plex Sans, or KOC's standard font). Display weight 600–700 for hero; 400–500 for body. Tight letter-spacing on display. Generous line-height (1.5–1.7) for body.
- **Layout:** 12-column grid. 1280px container width. 96px section padding desktop, 64px tablet, 48px mobile. Asymmetric hero with product screenshot/canvas on the right.
- **Imagery:** Realistic O&G imagery (wellsites, control rooms, dashboards) rendered in muted color so the product screenshots stand out. No stock photos of generic "office workers". Real product screenshots and a workflow canvas render are required; do not use placeholder images.
- **Iconography:** MudBlazor icons (`Icons.Material.Filled.*`) only. Use line-style variants for dense lists, filled for primary CTAs. Never combine icon styles.
- **Tone:** Confident, factual, no hype. Active voice. No exclamation marks. No marketing clichés ("revolutionary", "cutting-edge", "synergy"). Use specific nouns: "PPDM 39 well dataset", "AutoML binary classification", "ADLS Gen2 artifact store".
- **Bilingual readiness:** English primary; structure supports a future Arabic RTL mirror without redesign.
- **Dark mode:** Not required for v1; design for light mode with neutral surface variants.

## Page goals

1. Establish KocAiCommunity as the **single, sanctioned AI workspace** for KOC.
2. Show the **two-in-one** value: Community collaboration + ML Studio in one app.
3. Show **KOC enterprise integration coverage** (PPDM, OpenWells, EcoSys, SAP, PI, ADLS).
4. Show **governance and trust**: Entra-only sign-in, KOC tenant isolation, classification, audit.
5. Drive two CTAs: **"Sign in with KOC Entra"** (primary) and **"Request access"** (secondary, links to admin if no PlatformAdmin yet exists).

## Information architecture (top to bottom)

The page is a single long-scroll page with eight sections. Each section has a clear primary message.

### 1. Top navigation (sticky)

- KOC wordmark on the left.
- Right-aligned anchors: Capabilities, Integrations, Governance, Workflows, Models.
- Primary CTA: **"Sign in with KOC Entra"** (MudBlazor `MudButton` `Color="Color.Primary"`, variant `Variant.Filled`).
- Secondary: language switcher (English / العربية).
- Sticky after the user scrolls past the hero. Translucent white background, subtle shadow on scroll.

### 2. Hero

- Headline (max 9 words): **"KOC's single workspace for AI collaboration and ML."**
- Sub-headline (1–2 sentences, ~30 words): "KocAiCommunity brings KOC teams, datasets, and ML.NET workflows into one governed environment — connected to PPDM, OpenWells, EcoSys, SAP, and AVEVA PI."
- Primary CTA: **"Sign in with KOC Entra"** (filled). Secondary CTA: **"Take the 3-minute tour"** (outlined, smooth-scroll to Capabilities).
- Trust line: "Single KOC tenant • Entra workforce sign-in • Microsoft Information Protection labels • Audit retained 7 years"
- Right column: a hero illustration of the **KocAiCommunity application shell** — app bar, navigation, the Studio canvas with sample O&G workflow, and a live metrics panel. Static render is fine for v1.

### 3. Value pillars (3 cards)

Three columns, each card uses `MudCard` with `MudIcon`, headline, 2-line description, and a "Learn more" link.

- **Collaborate without leakage.** Discuss, share, and review inside the KOC tenant only.
- **Build ML workflows visually.** Drag-and-drop ML.NET trainers on a typed canvas.
- **Govern every model.** Promote with two approvals, roll back in one click.

### 4. Capabilities (tabbed showcase)

MudBlazor `MudTabs` with five tabs. Each tab shows a 4–6 bullet list on the left and a product screenshot on the right.

- **Community.** Internal discussions, mentions, project activity, audit-friendly notifications.
- **Datasets.** Versioned datasets with KOC information-security classification, lineage, profile.
- **Workflows.** Typed ML.NET nodes, AutoML, parameter validation, immutable versions.
- **Experiments & Models.** Trial-level metrics, lineage, semantic versioning, rollback.
- **Competitions.** Internal KOC challenges with hidden evaluation data and reveal dates.

### 5. Integrations (logo grid + connector panel)

- Headline: **"Connected to your KOC enterprise systems."**
- Connector chips: PPDM 39, OpenWells, EcoSys, SAP (PM/MM), AVEVA PI, ADLS Gen2.
- Each chip is a `MudChip` with an icon and a "View" button that opens a drawer describing the connector (entity types supported, default classification, auth mode).
- Below the grid: a 3-step horizontal flow: `Source → Workflow → Inference` using MudBlazor stepper-style component or a custom connector SVG.

### 6. Workflow designer preview (interactive screenshot)

- Headline: **"From PPDM well data to a deployed model in one canvas."**
- Left: a large static image of the Z.Blazor.Diagrams canvas with a real O&G sample workflow (PI tags → missing-value handling → temporal split → FastTree regression → model registry). Each node annotated with a small label.
- Right: a "Why teams choose this" bullet list:
  - Typed ports and parameter validation
  - 200+ node workflows remain usable
  - AutoML with reproducible seeds
  - Compiler rejects invalid graphs before run

### 7. Governance and security (icon row)

Six governance tiles, 3×2 grid:

- **Single-tenant KOC deployment.** No multi-tenant exposure.
- **Microsoft Entra ID only.** Workforce sign-in, no local passwords.
- **KOC information-security classification.** Enforced on every download.
- **Encrypted at rest and in transit.** Data Protection in dev; Key Vault references in production.
- **Audit envelope.** Every admin action recorded with before/after diff.
- **KOC data residency.** Hosted in Azure Kuwait Central; backups in sovereign storage.

### 8. Call-to-action band

- Headline: **"Sign in with your KOC account."**
- Subline: "If you cannot sign in, your account may not yet be enabled for KocAiCommunity. Contact your division head or the platform admin."
- Primary CTA: **"Sign in with KOC Entra"**. Secondary: **"Contact platform admin"** (mailto or in-app help page).
- Footer line: "© [Year] Kuwait Oil Company. Internal use only."

### 9. Footer

- Three columns: Product, Resources, Legal.
- Product: Capabilities, Integrations, Workflows, Models.
- Resources: Documentation, Tutorials, FAQ, Contact.
- Legal: Information Security, Privacy (internal), Acceptable Use.
- Bottom row: KOC wordmark, "Internal use only", version stamp.

## Content voice rules

- Use KOC's preferred terminology: "subsurface", "upstream operations", "production", "facilities", "HSE". Avoid generic "data science".
- Numbers and metrics must be plausible and conservative. No fabricated KPIs.
- Quotes and testimonials: do not invent them. Use placeholder slots clearly marked `[KOC executive quote — to be supplied by KOC communications]`.
- Compliance language should be plain and verifiable: "hosted in Azure Kuwait Central" rather than "fully sovereign".

## Component inventory (MudBlazor only)

Use these MudBlazor components and only these:

- Layout: `MudLayout`, `MudAppBar`, `MudDrawer`, `MudMainContent`, `MudContainer`, `MudGrid`, `MudItem`, `MudStack`, `MudPaper`, `MudSpacer`.
- Navigation: `MudTabs`, `MudNavLink`, `MudBreadcrumbs`.
- Buttons: `MudButton`, `MudIconButton`, `MudToggleIconButton`, `MudButtonGroup`.
- Inputs: `MudTextField`, `MudSelect`, `MudSwitch`, `MudChip`, `MudChipSet`.
- Data display: `MudCard`, `MudCardHeader`, `MudCardContent`, `MudCardActions`, `MudList`, `MudListItem`, `MudSimpleTable`, `MudDataGrid`.
- Feedback: `MudTooltip`, `MudIcon`, `MudBadge`.
- Typography: `MudText` with `Typo.h1` … `Typo.body2`.

Verify every component, parameter, and event against the local reference at `Beep.KocAiCommunity/mudBlazor_Docs/` before writing markup. Do not invent components.

## Layout and responsive behavior

- Desktop ≥ 1280px: 12-column grid, 1280px max container, 96px section padding.
- Tablet 768–1279px: 8-column grid, 720px container, 64px section padding.
- Mobile < 768px: single column, 16px gutter, 48px section padding, hero illustration stacks below text, capability tabs collapse to `MudExpansionPanels`.
- All interactive targets ≥ 44×44 px.
- Sticky nav transitions from transparent to translucent on scroll.

## Accessibility requirements

- WCAG 2.2 AA compliance target.
- Color contrast ≥ 4.5:1 for body text, ≥ 3:1 for large text and UI components.
- Full keyboard navigation. Visible focus rings using MudBlazor defaults.
- Skip-to-content link as the first focusable element.
- All interactive components labeled; icons-only buttons have `aria-label`.
- All images have meaningful `alt` text; decorative images use `alt=""`.
- Heading order is correct (one `<h1>` on the hero only).
- Honor `prefers-reduced-motion` for any animations.
- All form fields have associated labels and error messages.

## Performance and engineering constraints

- Render path: SSR for first paint, Interactive Server for the rest.
- No external font loading on critical path; self-host fonts.
- No external icon library beyond MudBlazor.
- No marketing-tag scripts (no GTM, no Hotjar, no third-party analytics) on the public landing page.
- Inline telemetry limited to Entra app insights for KOC-internal observability.
- Total JS payload ≤ 50 KB on first interactive.
- LCP < 1.5s on internal KOC network; CLS < 0.05.

## Deliverables

Produce the following:

1. **Information architecture** — section list, anchor IDs, scroll order.
2. **Wireframe per section** — annotated ASCII or markdown layout showing the grid, content blocks, and component choices.
3. **Final markup skeleton** — MudBlazor `Landing.razor` file with all sections, component placeholders, parameter bindings, and class names. Layout file `LandingLayout.razor` if needed. Routing entry (`@page "/"`).
4. **Theme additions** — `KocBrandingConfig` updates and any new MudBlazor theme tokens needed for the landing page.
5. **Image plan** — list of required screenshots/illustrations with file paths and capture instructions.
6. **Copy deck** — headline, sub-headline, CTA labels, value pillar copy, governance tile copy, trust line.
7. **Accessibility annotations** — focus order, ARIA labels, alt text per image.
8. **Responsive notes** — breakpoints where each component changes layout.
9. **Acceptance test list** — verifiable checks for every section.

## Acceptance tests (must all pass before sign-off)

- Visual regression at desktop, tablet, mobile breakpoints.
- Keyboard-only navigation reaches every interactive element.
- Screen reader announces hero headline, primary CTA, section headings in order.
- Color contrast verifier reports zero AA failures.
- Lighthouse mobile: Performance ≥ 90, Accessibility ≥ 95, Best Practices ≥ 95, SEO ≥ 90.
- Page renders correctly with no JavaScript errors in console.
- `Sign in with KOC Entra` button routes to `/signin-oidc` and the OIDC round-trip completes.
- All screenshots and product renders use KOC data (synthetic but realistic) — no third-party branding.
- All MudBlazor APIs used match `mudBlazor_Docs/` entries.
- Page is testable with bUnit for component-level assertions.
- Localization strings are extracted into a resource file so an Arabic RTL mirror can be added without code changes.

## Out of scope

- Public sign-up (no external users).
- Marketing analytics or third-party tracking.
- Live chat widget.
- Cookie banner (internal use, no cookies required for the landing page).
- Pricing page (KOC internal product).
- Comparison tables versus external vendors.

## Reference files to consult before starting

- `Beep.KocAiCommunity/plans/koc-ai-community-platform/README.md`
- `Beep.KocAiCommunity/plans/koc-ai-community-platform/05_MUDBLAZOR_SHELL_AND_SETUP.md`
- `Beep.KocAiCommunity/plans/koc-ai-community-platform/references/TECHNOLOGY_MATRIX.md`
- `Beep.KocAiCommunity/plans/koc-ai-community-platform/references/FEATURE_PARITY_MATRIX.md`
- `Beep.KocAiCommunity/plans/koc-ai-community-platform/references/RISKS_AND_DEFERRED_SCOPE.md`
- `Beep.KocAiCommunity/mudBlazor_Docs/` — every component used must be verified here first.
