# Architecture

## Solution layout

```
AppBender.sln
├─ src/AppBender.Core          # platform engine (no UI)
│  ├─ Common/                  # JsonUtil, TemplateEngine, MathEvaluator
│  ├─ Data/                    # ApplicationDbContext (EF Core + Identity), ApplicationUser, claims factory
│  ├─ Models/                  # EntityDefinition, DataRecord, FormDefinition, WorkflowDefinition, ...
│  ├─ Services/                # DataHub, Forms, Workflows, Apps, Audit, Usage, Versioning,
│  │                           # Storage, Email, Scripting, GraphQL executor, Import/Export
│  ├─ Workflows/               # WorkflowEngine, 35+ IWorkflowAction implementations, trigger services
│  ├─ Connectors/              # IConnector providers + ConnectorRuntime + DataSyncService
│  └─ AI/                      # Semantic Kernel LLM client, plugins, chat, RAG, AI Studio, ML builder
├─ src/AppBender.Web           # Blazor Server UI + API
│  ├─ Api/                     # REST Data API, GraphQL, webhooks, files, package export/import
│  ├─ Middleware/              # API key auth, tenant resolution, usage tracking
│  ├─ Components/              # layout, shared components (FormRenderer, charts, Modal), pages
│  └─ Services/SeedData.cs     # first-run sample data
└─ tests/AppBender.Tests       # unit tests (template engine, math, workflow engine, chunking, ...)
```

## Key design decisions

### Schema-less Data Hub on a fixed relational schema
Entities are *metadata* (`EntityDefinition` with `FieldsJson`), while rows live in a single
`DataRecords` table (`DataJson`). This gives Dataverse-like flexibility (create entities at runtime,
version schemas, AI-generated schemas) without runtime `ALTER TABLE`. Validation, type coercion,
formula fields (`qty * unit_price`), auto-numbers, and choice constraints are enforced by
`DataHubService` on every write. Query filtering/sorting/paging happens in `DataHubService`
after a tenant+entity indexed fetch.

### Everything is JSON-definable
Forms (`LayoutJson` — a `FormComponent` tree), workflows (`StepsJson` — a `WorkflowStep` tree),
and custom connectors (`SpecJson`) are plain JSON documents. That is what makes
export/import, versioning + rollback (`VersionSnapshots`), and AI generation
(LLM emits the same JSON) uniform across the platform.

### Workflow engine
`WorkflowEngine` walks the step tree; control-flow steps (`condition`, `switch`, `foreach`,
`do_until`, `scope`, `delay`, `terminate`) are interpreted by the engine, everything else is an
`IWorkflowAction` resolved from DI by its `Type` key — adding an action = one class + one DI line.
All step config values support `{{templates}}` resolved against the run context
(`trigger`, `vars`, `steps.<name>.output`, `utcNow`, `guid`, ...).

`WorkflowRunner` executes each run in its own DI scope (correct tenant + fresh DbContexts) and
persists a `WorkflowRun` with per-step logs. Triggers:

- **Schedule** — `ScheduleTriggerService` (BackgroundService + Cronos, 20s tick)
- **Webhook** — `POST /api/webhooks/{key}` (the `respond` action sets the HTTP response)
- **Entity events / Form submit** — in-process `EventBus` → `EventTriggerService`

### Multi-tenancy & security
Scoped `ITenantContext` carries `TenantId`/user/roles; it is populated by
`TenantResolutionMiddleware` (HTTP + API) and by `MainLayout` (Blazor circuits) from the
user's claims (`org`, `displayname` added by `AppClaimsPrincipalFactory`). Every service filters
by `TenantId`. Machine access uses `X-Api-Key` (mapped to a synthetic Developer principal by
`ApiKeyMiddleware`). Roles: `Admin`, `Developer`, `EndUser` guard pages and API controllers.

### AI layer
One `ILlmClient` (Semantic Kernel) reaches all four providers through their OpenAI-compatible
chat-completions endpoints, so tool calling works uniformly. Kernel plugins (math, datetime,
web search/scrape, dataset query) are attached per request. Embeddings/images/audio use the
provider's raw HTTP endpoints. RAG stores chunk embeddings in SQLite (cosine search in memory,
keyword fallback when no embedding model is configured).

### Blazor Server
Interactive Server render mode for all app pages; DbContexts are created per operation via
`IDbContextFactory` to stay circuit-safe. Charts are hand-rolled SVG components (no JS chart
library), keeping the app light. Theme (light/dark) uses Bootstrap 5.3 `data-bs-theme` +
localStorage.

## Request flow (Data API example)

```
HTTP GET /api/data/customers
  → ApiKeyMiddleware        (X-Api-Key → principal, if no cookie)
  → TenantResolutionMiddleware (claims → ITenantContext)
  → UsageTrackingMiddleware (metrics)
  → DataApiController       ([Authorize])
  → DataHubService.QueryAsync (tenant-filtered fetch + in-memory filter/sort/page)
```
