# Architecture

BlazorViz is a single Blazor Server project (`src/BlazorViz`) organized by feature. All interactive UI runs
server-side over SignalR; heavy work happens in singleton services that create short-lived `DbContext`
instances through `IDbContextFactory`.

## Solution layout

```
src/BlazorViz
├── Api/                 Minimal APIs (/api/v1) + API-key endpoint filter
├── Components/
│   ├── Account/         ASP.NET Core Identity UI (from template)
│   ├── Layout/          MainLayout (sidebar shell), EmptyLayout (share/embed), NavMenu
│   ├── Pages/           One .razor per screen (Datasets, Dashboards, DataWizard, Admin/…)
│   └── Shared/          PanelChart, DashboardRenderer, FilterBar, DataTableView, ThemeToggle
├── Data/                EF Core: ApplicationDbContext, Entities, Migrations, SeedData
├── Models/              TableData (columnar transport), DashboardLayout/PanelDef/FilterDef, EtlStep
├── Services/
│   ├── Connectors/      IDataConnector ×9 (5 ADO.NET + Excel/CSV/REST/GraphQL) + factory
│   ├── Scripting/       C# (Roslyn) / JS (Jint) / Python (process) runners + templates
│   ├── Ai/              AiOptions, AiKernelFactory, ChatService, Data Wizard plugins, PluginService
│   ├── Rag/             Embedders, vector indexes (InMemory/Qdrant/Chroma/Azure AI Search), RagService
│   ├── Storage/         IFileStorage: FileSystem / AzureBlob / S3-MinIO
│   ├── DatasetService   source → ETL → script pipeline + caching
│   ├── EtlService       11 table operations incl. join & pivot
│   ├── ChartBuilder     PanelDef + TableData → ECharts option JSON
│   ├── DashboardService CRUD + versioning + share tokens
│   ├── MlService        ML.NET forecast / regression / clustering
│   └── TelemetryServices AuditService, UsageService, PerfMonitor(+middleware)
├── plugins/             *.csx user plugins → Data Wizard kernel functions
└── wwwroot/js/blazorviz.js  ECharts/Leaflet/Gridstack/custom-visual/theme interop
```

## Data flow

```
Connection (config JSON)        Uploaded file
        │                            │
        └────────► DatasetService ◄──┘
                     │  1. IDataConnector.QueryAsync → TableData
                     │  2. EtlService.ApplyAsync (EtlStep list, joins resolve other datasets)
                     │  3. IScriptRunner.RunAsync (optional C#/JS/Python)
                     │  4. IMemoryCache (TTL = RefreshIntervalSeconds, else 10 min)
                     ▼
   ┌──────────┬───────────────┬─────────────┬──────────────┐
   PanelChart  Minimal API     Data Wizard    MlService
   (ECharts /  (/api/v1/…)     (DataQuery     (forecast /
   Leaflet /                    plugin)        regression /
   table/KPI)                                  cluster)
```

`TableData` is the universal transport: a list of typed columns plus `object?[]` rows. It is deliberately
not `System.Data.DataTable` — smaller, serializable, and cheap to clone/filter.

## Dashboards

A dashboard's `LayoutJson` deserializes to `DashboardLayout { Tabs[ Panels[] ], Filters[], RefreshIntervalSeconds }`.
Panels sit on a 12-column grid (`X/Y/W/H`). The designer uses Gridstack for drag/resize and writes positions
back through a `[JSInvokable]` callback; the viewer renders the same layout as CSS grid (read-only).
Every save appends a `DashboardVersion` row (last 30 kept) — rollback copies an old layout into a new version.
Public sharing issues a `ShareToken`; `/share/{token}` uses `EmptyLayout` and allows anonymous access.

## AI

`AiKernelFactory` builds a Semantic Kernel per request from the `Ai` config section. OpenAI and Anthropic
use the OpenAI connector (Anthropic via its OpenAI-compatible endpoint); Gemini uses the Google connector;
Ollama its own. Every kernel gets the math/datetime/web/data plugins plus any user `.csx` plugins.
`ChatService` streams replies, persists turns, estimates tokens (~4 chars/token) into `UsageMetrics`,
and prepends RAG context when indexed documents match the question.

RAG embedding defaults to a **local hashing embedder** (offline, deterministic); switch to OpenAI/Ollama
embeddings in config. Vector search defaults to the JSON-persisted in-memory index; Qdrant/Chroma/Azure AI
Search are REST-based implementations selected by `Ai:Rag:VectorStore`.

## Cross-cutting

- **Auth**: Identity + roles (Admin/Analyst/Viewer). Designer pages require Admin/Analyst; admin pages Admin.
- **Audit**: `AuditService.LogAsync(category, action, details, user)` from every mutating flow.
- **Usage**: `UsageService.Record(kind, value)` — kinds: query, chat, token_in/out, api.
- **Perf**: `PerfMiddleware` samples every non-static request into a ring buffer read by /admin/performance.
- **API auth**: `X-Api-Key` header checked against the `ApiKeys` table by an endpoint filter on the `/api/v1` group.
