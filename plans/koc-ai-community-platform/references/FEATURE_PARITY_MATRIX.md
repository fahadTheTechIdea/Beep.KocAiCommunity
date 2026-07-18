# Feature Parity Matrix

This matrix maps every feature in the Python reference apps (`Beep.AI.Community`, `Beep.AI.MLStudio`, `Beep.AI.Server`) to its landing zone in `Beep.KocAiCommunity`. Many features are narrowed by the KOC focus (single tenant, employees only, O&G only).

Legend:

- ✅ **Ported** — feature ships in `Beep.KocAiCommunity`
- 🔧 **Adapted** — feature is narrower in scope (KOC-only, O&G-only, or restricted)
- 🟡 **Deferred** — feature is documented but ships after MVP
- ❌ **Out of scope** — explicitly removed for the KOC focus

## Community (Beep.AI.Community)

| Feature | Status | Notes / Plan section |
|---|---|---|
| User profile | 🔧 | Phase 06 — KOC employees only; department/job title from Entra |
| Discussions, replies, votes | 🔧 | Phase 06 — internal-only; mentions resolve to KOC users only |
| Activity feed | 🔧 | Phase 06 — scoped to projects the user belongs to |
| Notifications | 🔧 | Phase 06 — in-app + email; KOC-only |
| Moderation tools | 🔧 | Phase 06 — `discussion.moderate` permission |
| External public profiles | ❌ | Out of scope (KOC employees only) |
| External followers | ❌ | Out of scope (KOC employees only) |
| Cross-tenant branding | ❌ | Out of scope (single KOC tenant) |
| Industry selector | ❌ | Out of scope (O&G only) |
| Datasets (general) | 🔧 | Phase 07 — KOC classification enforced |
| Dataset marketplace (external publishers) | ❌ | Out of scope (KOC internal catalog only) |
| Projects | 🔧 | Phase 07 — O&G subdomain tagging |
| Competitions | 🔧 | Phase 13 — internal, Kaggle-style challenges; a core surface; org-scoped visibility; any Employee or CompetitionAdmin can create |
| Live vs concealed-final leaderboards | ✅ | Phase 13 — Kaggle-style split within the competition's visibility scope |
| External / public-web leaderboards | ❌ | Out of scope (nothing exposed outside KOC) |
| Sample data seeding | 🟡 | Phase 14 — KOC sample workflows only |
| Industries visibility | 🔧 | Phase 14 — single O&G taxonomy |
| Branding (per-tenant themes) | 🔧 | Phase 14a — KOC theme only |
| Setup wizard | ✅ | Phase 01/05 — adapted for KOC |
| Setup re-entrancy guard | ✅ | Phase 01/05 |
| First-run detection | ✅ | Phase 01/05 |
| Two-mode auth (local vs OIDC) | 🔧 | Phase 02 — Entra-only (no local password) |
| API key auth | 🟡 | Deferred — internal-only first |
| Admin settings (SMTP, MFA, SSO, CAPTCHA) | 🔧 | Phase 14a — KOC Entra app roles; MFA flag only |
| Admin security settings | ✅ | Phase 14a |
| Admin user/role management | ✅ | Phase 14a |
| Admin branding editor | 🔧 | Phase 14a — KOC theme only |
| Rate limiting | ✅ | Phase 04 |
| WebSocket live updates | 🔧 | Phase 04 — SignalR instead of SocketIO |
| Flask-SocketIO | ❌ | Replaced by SignalR |
| Subprocess execution of user scripts | ❌ | Out of scope (security) |
| Path traversal guard | ✅ | Phase 03 |
| Network guardrails (SSRF) | ✅ | Phase 07 / Phase 07a |

## KOC-specific additions (new — no Python analog)

These features are new to `Beep.KocAiCommunity` and exist to serve the training mission and the KOC org structure. They are not ported from the Python reference apps.

| Feature | Status | Notes / Plan section |
|---|---|---|
| Learning tracks (guided upskilling) | ✅ | Phase 13a — 3 seeded tracks (Getting started → Solve a real problem → Make it dependable), lessons, enrollment, progress |
| Learn ↔ compete tie-in | ✅ | Phase 13/13a — tracks recommend competitions and vice versa |
| KOC org hierarchy (Team/Group/Directorate/Company) | ✅ | Phase 02 — `OrgUnit` tree, materialized path |
| Position levels (Employee→TeamLeader→Manager→DCEO→CEO) | ✅ | Phase 02 — from the org directory |
| Supervisory rollup dashboards | ✅ | Phase 02 scope + Phase 13/13a data — read-only, scoped to the caller's subtree |
| Org-scoped visibility on create (Team/Group/Directorate/Company) | ✅ | Phase 02 model; applied to datasets/projects (07) and competitions (13) |

## ML Studio (Beep.AI.MLStudio)

| Feature | Status | Notes / Plan section |
|---|---|---|
| ML projects, templates, snapshots | ✅ | Phase 07 |
| Datasets, imports, profile | ✅ | Phase 07 / Phase 07a |
| Workflow designer | 🔧 | Phase 09 — Z.Blazor.Diagrams replaces jsPlumb |
| Workflow templates (industry) | 🔧 | Phase 14 — single O&G taxonomy replaces 17-industry catalog |
| Workflow versions | ✅ | Phase 09 |
| Workflow scheduling (cron) | ✅ | Phase 10 |
| File watch triggers | ✅ | Phase 10 |
| Run lifecycle | ✅ | Phase 10 |
| Health summary state machine | ✅ | Phase 10 |
| Run events with std markers | ✅ | Phase 10 — adapted to IMonitor |
| Experiment tracking | ✅ | Phase 11 — native EF tracker |
| AutoML | ✅ | Phase 11 |
| ML.NET abstractions (IMLTrain etc.) | ✅ | Phase 08 — reused from Beep.AI.Shared |
| ML.NET.AutoML | ✅ | Phase 08 |
| MLflow integration | 🟡 | Phase 11 — optional `IExperimentSink` adapter |
| Model registry | ✅ | Phase 12 |
| Model promotion | ✅ | Phase 12 |
| Inference | ✅ | Phase 12 — Microsoft.Extensions.ML |
| ML Server integration | 🟡 | Deferred — Microsoft.Extensions.ML covers inline |
| Dataset marketplace (Kaggle/HF) | ❌ | Out of scope (KOC internal catalog only) |
| Jupyter kernel execution | ❌ | Out of scope (asset storage/versioning only) |
| Industry profiles | 🔧 | Phase 14 — single O&G profile |
| Industry modules | 🔧 | Phase 14 — single O&G module set |
| Theme provider | 🔧 | Phase 05 — KOC theme |
| User auth (local vs IdentityServer) | 🔧 | Phase 02 — Entra-only |
| API tokens | 🔧 | Phase 02 — Entra tokens only |
| Plugin management | ✅ | Phase 09 (institutional plugin pattern) |
| Sub-workflow expansion | 🟡 | Deferred |
| Interactive debugging | ❌ | Explicitly not ported |

## Server (Beep.AI.Server)

| Feature | Status | Notes / Plan section |
|---|---|---|
| Service Design Pattern (Overview/Workspace/Feature) | ✅ | Phase 04/05 |
| Phased service gating (env not ready → missing packages → ready) | ✅ | Phase 05 |
| One page = one business concern | ✅ | Phase 05+ |
| Advanced toggle (sessionStorage) | ✅ | Phase 05 |
| Rows default, cards opt-in | ✅ | Phase 05 |
| Code-first agent framework | ❌ | Out of scope (no agents in MVP) |
| LangGraph / DeepAgents | ❌ | Out of scope |
| Tooling plugin pattern (`plugin.py` + `plugin.yaml`) | 🔧 | Phase 09 — institutional version |
| `@admin_required` decorator | ✅ | Phase 14a — `RequirePlatformAdmin` policy |
| Audit envelope middleware | ✅ | Phase 14a |
| Setting services via typed registry | ✅ | Phase 14a |
| Wizard / first-run bootstrap | ✅ | Phase 05 |
| Multi-tenant routing | ❌ | Out of scope |
| Magic/voice/chatbot services | ❌ | Out of scope |
| RAG / MCP / tool management | 🟡 | Deferred — not core to KOC ML studio |
| Wizard templates | ✅ | Phase 14 |
