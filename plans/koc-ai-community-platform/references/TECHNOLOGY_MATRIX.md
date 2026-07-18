# Technology Matrix

Pinned package versions, refresh cadence, and rationale. Refresh cadence means how often this matrix should be revisited; not how often packages should be upgraded.

## Runtime

| Component | Version | Refresh cadence | Rationale |
|---|---|---|---|
| .NET SDK | 10.0.302 | Per LTS cadence | Verified locally 2026-07-17 |
| .NET Runtime | 10.0.10 | Per LTS cadence | LTS through November 2028 |
| Aspire | 13.4.6 | Quarterly review | Built-in observability and orchestration |

## UI

| Package | Version | Refresh cadence | Rationale |
|---|---|---|---|
| MudBlazor | 9.7.0 | Quarterly review | Latest stable; matches BeepWeb |
| Blazor.Diagrams (Z.Blazor.Diagrams) | 3.0.4.1 | Quarterly review | Native Blazor workflow editor |
| FluentValidation | 11.x | Annual | API validation |
| Blazored.LocalStorage | 4.5.0 | As needed | Theme persistence |

## Authentication

| Package | Version | Refresh cadence | Rationale |
|---|---|---|---|
| Microsoft.Identity.Web | 4.13.2 | Quarterly review | Microsoft-blessed Entra integration |
| Microsoft.AspNetCore.Authentication.OpenIdConnect | 10.0.10 | With .NET | Standard |
| Microsoft.AspNetCore.Authentication.JwtBearer | 10.0.10 | With .NET | Standard |

## Data

| Package | Version | Refresh cadence | Rationale |
|---|---|---|---|
| Microsoft.EntityFrameworkCore | 10.0.10 | With .NET | Standard |
| Microsoft.EntityFrameworkCore.SqlServer | 10.0.10 | With .NET | Production |
| Microsoft.EntityFrameworkCore.Sqlite | 10.0.10 | With .NET | Dev/test |
| Microsoft.EntityFrameworkCore.Design | 10.0.10 | With .NET | Migrations |
| Microsoft.AspNetCore.Identity.EntityFrameworkCore | 10.0.10 | With .NET | Identity tables |

## ML

| Package | Version | Refresh cadence | Rationale |
|---|---|---|---|
| Microsoft.ML | 5.0.0 | Annual | Latest stable |
| Microsoft.ML.AutoML | 0.23.0 | Annual | Pairs with Microsoft.ML 5.0 |
| Microsoft.ML.LightGbm | 5.0.0 | Annual | Boosted trees |
| Microsoft.ML.FastTree | 5.0.0 | Annual | Fast trees |
| Microsoft.ML.TimeSeries | 5.0.0 | Annual | Forecasting |
| Microsoft.ML.OnnxConverter | 5.0.0 | Annual | ONNX export |
| Microsoft.ML.Parquet | 5.0.0 | Annual | Parquet IO |
| Microsoft.Extensions.ML | 5.0.0 | Annual | PredictionEnginePool |

## Real-time and background

| Package | Version | Refresh cadence | Rationale |
|---|---|---|---|
| Microsoft.AspNetCore.SignalR | 10.0.10 | With .NET | Standard |
| Microsoft.AspNetCore.SignalR.Client | 10.0.10 | With .NET | Standard |
| Microsoft.AspNetCore.RateLimiting | 10.0.10 | With .NET | Standard |
| Microsoft.Extensions.Hosting | 10.0.10 | With .NET | Worker SDK |

## Cloud (optional)

| Package | Version | Refresh cadence | Rationale |
|---|---|---|---|
| Azure.Storage.Blobs | 12.x | Annual | Azure Blob artifacts |
| Azure.Identity | 1.x | Annual | Managed Identity |
| Azure.Security.KeyVault.Secrets | 4.x | Annual | Key Vault |

## Testing

| Package | Version | Refresh cadence | Rationale |
|---|---|---|---|
| xUnit | 2.9.3 | Annual | Standard |
| Microsoft.NET.Test.Sdk | 17.12.0 | Annual | Standard |
| FluentAssertions | 6.x | Annual | Standard |
| bUnit | 1.x | Annual | MudBlazor component tests |
| Microsoft.AspNetCore.Mvc.Testing | 10.0.10 | With .NET | End-to-end tests |
| coverlet.collector | 6.x | Annual | Coverage |
| Playwright | 1.x | Annual | End-to-end UI tests |
| Microsoft.Playwright.NUnit | 1.x | Annual | Playwright runner |

## Tools

| Package | Version | Refresh cadence | Rationale |
|---|---|---|---|
| FluentMigrator | (avoid) | n/a | Use EF migrations only |

## Reference apps inventory (used for design)

| Reference | Path | Stack |
|---|---|---|
| Beep.AI.Community | `C:\Users\f_ald\source\repos\The-Tech-Idea\Beep.AI.Community` | Python 3.11 / Flask 3 / SQLAlchemy 2 / SQLite |
| Beep.AI.MLStudio | `C:\Users\f_ald\source\repos\The-Tech-Idea\Beep.AI.MLStudio` | Python 3.11 / Flask 3 / SQLAlchemy 2 / SQLite / sklearn / jsPlumb |
| Beep.AI.Server | `C:\Users\f_ald\source\repos\The-Tech-Idea\Beep.AI.Server` | Python 3.11 / Flask 3 |
| Beep.ML.NET | `C:\Users\f_ald\source\repos\The-Tech-Idea\Beep.ML.NET` | .NET 6/7 / ML.NET 3.0 / WinForms |
| Beep.AI.Shared | `C:\Users\f_ald\source\repos\The-Tech-Idea\Beep.AI.Shared` | .NET 6-9 / ML.NET contracts |
| BeepWeb | `C:\Users\f_ald\source\repos\The-Tech-Idea\BeepWeb` | .NET 10 / MudBlazor 9.7 |
| Beep.OilandGas.Web | `C:\Users\f_ald\source\repos\The-Tech-Idea\Beep.OilandGas\Beep.OilandGas.Web` | .NET 10 / MudBlazor 9.5 / OIDC |
| Beep.StreamingEvents.Web.Web | `C:\Users\f_ald\source\repos\The-Tech-Idea\Beep.StreamingEvents\Beep.StreamingEvents.Web\Beep.StreamingEvents.Web.Web` | .NET 10 / MudBlazor 9.4 / Fluxor |
| Beep.ApiServer | `C:\Users\f_ald\source\repos\The-Tech-Idea\Beep.ApiServer` | .NET 8 / EF Identity / JWT |

## Local SDK verification (2026-07-17)

```
.NET SDK 10.0.302
Runtime 10.0.10
Workloads: maui-windows, android, ios, wasm-tools
```

Aspire workload is not installed locally; Aspire templates ship as NuGet packages, so absence is not a blocker.
