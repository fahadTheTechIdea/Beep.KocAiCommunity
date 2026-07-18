# Test and Acceptance Matrix

Test categories and per-stage acceptance gates.

## Test categories

| Category | Project | What it covers | Frameworks |
|---|---|---|---|
| Unit | Beep.KocAiCommunity.UnitTests | Pure logic, no IO | xUnit, FluentAssertions |
| Integration | Beep.KocAiCommunity.IntegrationTests | API, Worker, EF, connectors against both providers | xUnit, FluentAssertions, Testcontainers |
| Component | Beep.KocAiCommunity.ComponentTests | MudBlazor components, render and interaction | xUnit, bUnit |
| Architecture | Beep.KocAiCommunity.ArchitectureTests | Dependency direction, project graph rules | xUnit, NetArchTest |
| End-to-end | Beep.KocAiCommunity.EndToEndTests | AppHost startup, full scenarios | xUnit, Microsoft.AspNetCore.Mvc.Testing |
| Security | Beep.KocAiCommunity.SecurityTests | OWASP-aligned scenarios | xUnit, custom |
| Performance | Beep.KocAiCommunity.PerformanceTests | Critical-path benchmarks | xUnit, BenchmarkDotNet |
| Migration | Beep.KocAiCommunity.MigrationTests | Upgrade paths and rollback | xUnit, EF migrations |

## Per-stage acceptance gates

### Stage 01

- All projects compile with warnings-as-errors
- All test projects run with zero tests
- Architecture tests pass
- Aspire launches all services
- `/health` returns 200 in each service
- Format verification passes

### Stage 02

- Tenant rejection tests pass
- Audience rejection tests pass
- App role mapping tests pass
- Permission filtering tests pass
- First-admin bootstrap and disabled-state tests pass

### Stage 03

- Both providers' migrations apply to fresh databases
- Migration rollback works
- Save changes interceptor populates audit columns
- Artifact upload, download, and delete work end-to-end
- Classification is enforced on downloads
- Content sniffing rejects mismatched MIME types

### Stage 04

- OpenAPI schema is generated and accurate
- Pagination, filtering, sorting, ETags work
- Idempotency keys are honored
- Rate limits engage
- Outbox events are persisted and dispatched
- SignalR clients receive events in order
- Problem Details returned for all error paths

### Stage 05

- Shell renders correctly on desktop and mobile
- Theme provider returns a `MudTheme` with KOC palette tokens
- All MudBlazor APIs used match the local `mudBlazor_Docs/` references
- Provider count: theme, popover, dialog, snackbar
- Setup diagnostics reflect live health

### Stage 06

- KOC user can post, reply, vote, attach
- Mention autocomplete only suggests KOC users
- Notifications appear in real time
- Activity feed scoped correctly
- Moderator actions are audited

### Stage 07

- Dataset version immutability enforced
- Classification enforced on download
- Project membership changes reflected in authorization checks immediately
- Profile generation reproducible with fixed seed
- Import URL SSRF guard works

### Stage 07a

- Each connector passes integration tests against a sandbox or a mocked equivalent
- Credentials are encrypted at rest
- Schema introspection works
- Import creates a new dataset version with provenance metadata
- Health probe runs on schedule and writes `ConnectorHealthSnapshot`

### Stage 08

- Each ML task trains, evaluates, saves, reloads, predicts deterministically
- AutoML trials persist results via the `IMonitor` adapter
- Featurization guards prevent fitting on training-test union

### Stage 09

- 200-node workflow remains usable in the browser
- Round-trip serialization without data loss
- Cycle detection rejects invalid workflows
- Type compatibility is enforced
- Version immutability enforced

### Stage 10

- Runs survive worker restart
- Cancellation works
- Duplicate claims are prevented
- Progress is delivered via SignalR
- Resource limits enforced

### Stage 11

- Multiple AutoML trials persist live metrics
- Comparison results are reproducible
- Run lineage includes workflow, dataset, environment, and dependency snapshots
- `IMonitor` does not block ML.NET training
- `IExperimentSink` can be swapped to MLflow REST adapter

### Stage 12

- Model can move from experiment to registry to inference and safely roll back
- Promotion requires two approvals
- Inference logs capture latency and outcome
- Classification is enforced on inference

### Stage 13

- Hidden evaluation data is never accessible via the API
- Scoring is reproducible
- Quotas work
- Leaderboard ties are deterministic
- Reveal date hides public score until reached

### Stage 14

- A KOC employee can complete an end-to-end guided scenario without database intervention
- Domain admin actions are audited
- Help articles are searchable

### Stage 14a

- All admin endpoints return 403 to non-admin tokens
- Setting changes are persisted with audit and version
- Encrypted secrets are unreadable in audit JSON
- Dashboard reflects real health, audit, and counts
- First-admin bootstrap works on a fresh database

### Stage 15

- All tests pass on both providers
- All CI gates pass
- Security tests show no high-severity issues
- Performance benchmarks within budget
- Staging deployment passes smoke, security, migration, backup/restore, rollback exercises
- Disaster recovery test passes

## Test execution commands

```bash
# Per-stage
dotnet test tests/Beep.KocAiCommunity.UnitTests --filter "FullyQualifiedName~Stage1"
dotnet test tests/Beep.KocAiCommunity.IntegrationTests --filter "FullyQualifiedName~Stage1"
dotnet test tests/Beep.KocAiCommunity.ComponentTests --filter "FullyQualifiedName~Stage1"

# Provider split
dotnet test tests/Beep.KocAiCommunity.IntegrationTests --filter "FullyQualifiedName~Sqlite"
dotnet test tests/Beep.KocAiCommunity.IntegrationTests --filter "FullyQualifiedName~SqlServer"

# End-to-end
dotnet test tests/Beep.KocAiCommunity.EndToEndTests
```

## CI gates

```yaml
# .github/workflows/build.yml
name: build
on: [push, pull_request]
jobs:
  test:
    runs-on: ubuntu-latest
    services:
      sqlserver:
        image: mcr.microsoft.com/azure-sql-edge:latest
        env:
          ACCEPT_EULA: "Y"
          MSSQL_SA_PASSWORD: "StrongPassword!"
        ports: ["1433:1433"]
    steps:
      - uses: actions/checkout@v4
      - uses: actions/setup-dotnet@v4
        with:
          dotnet-version: 10.0.x
      - run: dotnet restore
      - run: dotnet format --verify-no-changes
      - run: dotnet build --no-restore -warnaserror
      - run: dotnet test --no-build --logger "trx;LogFileName=results.trx"
      - uses: actions/upload-artifact@v4
        with:
          name: test-results
          path: "**/results.trx"
```

## Performance budgets

| Scenario | Budget |
|---|---|
| Workflow canvas render (200 nodes) | < 200 ms p95 |
| Dataset preview (10k rows) | < 500 ms p95 |
| SignalR event delivery (within circuit) | < 200 ms p95 |
| Queue throughput | > 50 jobs/minute sustained |
| Inference latency (cached model) | < 50 ms p95 |

## Security tests

| Threat | Test |
|---|---|
| Authorization bypass | Token without role claims rejected |
| Token tampering | Invalid signature rejected |
| Tenant confusion | Wrong tenant token rejected |
| Path traversal | Upload paths constrained |
| SSRF | Connector URL blocked on private IPs |
| Archive bomb | Upload size limit enforced |
| Model trust | Unsigned model rejected for inference |
| Secret exposure | Secrets redacted from audit JSON |
| SQL injection | EF parameterized queries only |
| XSS | Blazor renders encoded by default |
| CSRF | Antiforgery enabled for all POSTs |
