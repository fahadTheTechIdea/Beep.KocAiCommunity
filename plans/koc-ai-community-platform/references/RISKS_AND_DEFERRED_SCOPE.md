# Risks and Deferred Scope

Items that are explicitly out of scope for the KOC-focused MVP and items that carry risk in scope.

## Out of scope (KOC focus decisions)

These items are deliberately removed from the platform because this is an internal KOC application — KOC is the sole owner and operator, not a customer of a commercial product:

- **Multi-tenant support.** Single KOC tenant only. No tenant switcher, no per-tenant branding, no per-tenant rate-limit partitions.
- **External community users.** KOC employees only. No public profile pages, no external followers, no public activity feed.
- **Industry selector.** O&G only. No industry switching UX, no industry profile marketplace.
- **External dataset marketplace.** KOC internal catalog only. No Kaggle or Hugging Face integrations in MVP.
- **Public competition leaderboards.** Internal KOC competitions only. No external submissions.
- **Jupyter notebook execution.** Notebook assets may be stored and versioned but never executed.
- **Arbitrary user-uploaded script execution in Worker.** Trusted scorer plugins only.
- **On-prem deployment.** Documented as future option.
- **Cross-region failover outside Kuwait sovereign boundary.** Documented as future option.
- **Multi-tenant branding presets.** KOC theme only; no preset marketplace.

## Anti-patterns explicitly avoided

These are behaviors we observed in the Python references and chose not to copy:

- **JSON as source of truth for behaviour.** Per Beep.AI.Server AGENTS.md. The typed settings service stores state, not behaviour.
- **Massive service files (1000-2000 lines).** Per BeepWeb AGENTS.md. Enforce the 300-500 line cap.
- **Inline `<script>` / `<style>` in templates.** All JS in `wwwroot/js`, CSS in `wwwroot/css`.
- **Scoring service executes user-uploaded Python in subprocess.** Per `Beep.AI.Community/app/services/scoring_service.py:80-138`. Out of scope.
- **Two competing theme systems.** Per Beep.AI.Community. One `IThemeProvider` and one `BrandingConfig`.
- **Hardcoded paths in services.** All paths from `IOptions<KocOptions>`.
- **Bare `except:` clauses.** Specific exception types or `Exception` with logging.
- **`socketio.run(allow_unsafe_werkzeug=True)`.** SignalR over the configured Kestrel transport.
- **jsPlumb workflow editor.** Replaced by Z.Blazor.Diagrams.
- **Mass `DbContext.ConfigEditor.DataConnections.Add(...)` calls.** All datasource work goes through service abstractions.

## Risks and mitigations

### Domain

| Risk | Severity | Mitigation |
|---|---|---|
| ML.NET 5.0 API changes since 3.0 | Medium | Dedicated research in Phase 08; integration tests cover each task end-to-end |
| Z.Blazor.Diagrams customization depth | Medium | Phase 09 begins with a proof of concept on a real workflow |
| KOC enterprise connectivity (PPDM/SAP/PI) | High | Mock adapters are the staging default; real connectivity is the longest lead time |
| MLflow optional sink without .NET client | Low | `IExperimentSink` abstraction; ship EF sink first; add MLflow adapter later |
| SignalR scaling across instances | Medium | Redis backplane is a future option; document in deployment guide |

### Operational

| Risk | Severity | Mitigation |
|---|---|---|
| SQLite trigger-based concurrency fragile | Medium | Document the rule; centralize in the SaveChanges interceptor |
| Outbox ordering must be guaranteed by ID | Medium | Document; tests assert ordering |
| Idempotency key store must be persistent | Medium | Stored in EF, not memory |
| EF migrations diverge across providers | Medium | Two migration assemblies; provider-agnostic entity design |
| Long-running ML jobs blocking interactive requests | Medium | Separate Worker process; queue-based dispatch |

### Compliance

| Risk | Severity | Mitigation |
|---|---|---|
| KOC info-sec data classification rules | High | Per-resource classification field; enforced at download time; admin editor for escalation policy |
| KOC data residency | High | Kuwait region for production; backup storage in sovereign boundary |
| Audit retention period | Medium | Configurable per Phase 14a; default 7 years for production data |
| Encryption at rest | High | Data Protection in dev; Key Vault references in production |
| Encryption in transit | High | HTTPS only; HSTS enabled |
| Identity tenant validation | High | Token validation rejects non-KOC tenants with 403 |

### Migration

| Risk | Severity | Mitigation |
|---|---|---|
| Python SQLite schema differs from EF model | High | Migration tooling reads Python metadata, emits JSON for import; manual review |
| User passwords in Python sources | Medium | Migration ignores passwords; users must re-authenticate via Entra |
| Stored experiment models (Python pickle) | Medium | Re-train in .NET is the only supported migration path |
| Stored workflow definitions (Python jsPlumb) | Low | New application owns the JSON format; manual recreation required |
| User-uploaded scripts (Python scoring) | High | Out of scope; equivalent feature is not provided in the new app |

## Deferred items (planned but not in MVP)

These items are documented for follow-up planning:

- **External competition mode.** Future.
- **External community users.** Future.
- **Notebook execution sandbox.** Future.
- **On-prem deployment.** Future.
- **Cross-region failover outside Kuwait sovereign boundary.** Future.
- **MLflow REST adapter.** Future; abstraction in place.
- **Voice/audio bot services.** Future; out of scope for MVP.
- **Magic/chatbot services.** Future; out of scope for MVP.
- **RAG/MCP integration.** Future; deferred.
- **Deep learning scenarios (TorchSharp).** Future.
- **Interactive workflow debugging (breakpoints, step-through).** Future.
- **Advanced workflow auto-layout.** Future; benchmark dagre vs. ELK.
- **User-authored help content.** Future.
- **Admin impersonation.** Future.
- **Multi-tenant failover and tenancy abstraction.** Future.

## Definition of done (global)

```bash
dotnet restore
dotnet format --verify-no-changes
dotnet build --no-restore -warnaserror
dotnet test --no-build
```

All four must pass on every push and pull request, on both providers, with zero high-severity security issues, within performance budgets, and across the per-stage acceptance gates.
