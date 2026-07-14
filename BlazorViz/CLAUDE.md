# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project

BlazorViz — a Power BI-like data analysis & visualization web app. Single Blazor Server project on
**.NET 10** at `src/BlazorViz` (solution `BlazorViz.sln`). `requirements.md` is the original spec;
`docs/` holds architecture/user/config/API docs (EN + ID READMEs).

## Commands

```powershell
dotnet build                                 # build (from repo root or src/BlazorViz)
dotnet run --project src/BlazorViz           # run; DB migrates + seeds automatically
dotnet ef migrations add <Name>              # from src/BlazorViz (dotnet-ef installed globally)
```

There are no tests yet. Smoke-test by running the app and hitting `/Account/Login`, `/swagger`,
and `/api/v1/datasets` with the `X-Api-Key` header (a demo key is seeded; read it from
`src/BlazorViz/Data/app.db`, table `ApiKeys`).

Seeded logins: `admin@blazorviz.local` / `Admin123!` (also analyst@/Analyst123!, viewer@/Viewer123!).

## Architecture (read docs/architecture.md for detail)

- **`Models/TableData.cs`** is the universal data transport (typed columns + `object?[]` rows) used by
  connectors, ETL, charts, export, ML and the API. Not `System.Data.DataTable`.
- **Dataset execution pipeline** (`Services/DatasetService.cs`): source (one of 9 `IDataConnector`s in
  `Services/Connectors/` or an uploaded file) → `EtlService` steps (`EtlStep` list, join resolves other
  datasets) → optional script (`Services/Scripting/`: Roslyn C# / Jint JS / external Python) →
  `IMemoryCache` with TTL = `Dataset.RefreshIntervalSeconds`.
- **Dashboards**: `Dashboard.LayoutJson` ⇄ `DashboardLayout { Tabs[Panels], Filters, RefreshIntervalSeconds }`.
  `ChartBuilder` converts a `PanelDef` + `TableData` into ECharts option JSON; `PanelChart.razor` renders it
  via `wwwroot/js/blazorviz.js` (`window.bv.*` interop: ECharts, Leaflet maps, Gridstack, custom
  Chart.js/D3 visuals, theme, downloads). Designer = Gridstack drag/drop; viewer/share = CSS grid.
  Saves append `DashboardVersion` rows (rollback supported); public share via `/share/{token}` + `EmptyLayout`.
- **AI** (`Services/Ai/`): `AiKernelFactory` builds a Semantic Kernel per request from the `Ai` config
  section (OpenAI & Anthropic via OpenAI connector, Gemini via Google connector, Ollama native). Plugins:
  math/datetime/web/data-query + user `.csx` files from `plugins/` (`PluginService`). `ChatService` streams,
  persists `ChatSession/ChatMessageEntity`, injects RAG context, records token estimates in `UsageMetrics`.
- **RAG** (`Services/Rag/`): embedder (Local hashing default / OpenAI / Ollama) + `IVectorIndex`
  (InMemory JSON-persisted default; Qdrant/Chroma/Azure AI Search via REST), selected in `Ai:Rag` config.
- **Singleton services use `IDbContextFactory<ApplicationDbContext>`** (registered singleton); Identity
  uses the scoped `ApplicationDbContext` with `optionsLifetime: Singleton` — keep this pairing intact in
  `Program.cs` or startup DI validation fails.
- **API**: `Api/ApiEndpoints.cs` maps `/api/v1` with an endpoint filter checking `X-Api-Key` against the
  `ApiKeys` table; Swagger UI at `/swagger` (Swashbuckle 10 / Microsoft.OpenApi 2.x — types live in
  `Microsoft.OpenApi`, not `.Models`).
- **Telemetry**: `AuditService` (audit trail), `UsageService` (query/chat/token/api counters),
  `PerfMonitor` + `PerfMiddleware` (ring buffer for /admin/performance).

## Conventions & gotchas

- UI pages are per-page `@rendermode InteractiveServer`, `[Authorize]` by default; designer pages require
  Admin/Analyst role, admin pages Admin. Share page is `[AllowAnonymous]`.
- Theme: neo-brutalism soft, CSS variables in `wwwroot/app.css`, dark/light via `data-theme` attribute on
  `<html>` (set pre-paint in `App.razor`, toggled via `bv.setTheme`).
- Chart/JS libraries (ECharts, Chart.js, D3, Leaflet, Gridstack, fonts) load from CDN in `App.razor`.
- `Microsoft.CodeAnalysis.CSharp.Scripting` is pinned to **5.0.0** to match EF design-time tooling — do not
  bump it independently (dotnet-ef crashes with mixed Roslyn versions).
- SKEXP/NU1608 warnings are suppressed via `<NoWarn>` in the csproj (SK alpha connectors).
- Seeding (`Data/SeedData.cs`) generates deterministic sample CSVs under `App_Data/samples/` and creates
  sample datasets/dashboards only when the Datasets table is empty.
