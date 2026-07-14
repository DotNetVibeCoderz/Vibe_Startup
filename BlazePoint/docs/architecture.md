# BlazePoint — Architecture

## Overview

BlazePoint is a single-project **Blazor Server** application (`src/BlazePoint`) on **.NET 10**. All interactivity runs over a SignalR circuit (`<Routes @rendermode="InteractiveServer" />` in `App.razor`); REST/GraphQL endpoints and auth form-posts are plain HTTP.

```
┌─────────────────────────────────────────────────────────────┐
│                        Browser                              │
│   Blazor Server circuit (SignalR)  ·  HTTP (REST/GraphQL)   │
└──────────────┬──────────────────────────────┬───────────────┘
               │                              │
┌──────────────▼──────────────┐  ┌────────────▼───────────────┐
│  Components/ (Razor UI)     │  │  Api/                      │
│  Layout · Pages · WebParts  │  │  ApiEndpoints (REST)       │
│  Shared components          │  │  AccountEndpoints (/auth)  │
└──────────────┬──────────────┘  │  GraphQLQuery (/graphql)   │
               │                 └────────────┬───────────────┘
┌──────────────▼──────────────────────────────▼───────────────┐
│  Services/  (all singletons, DbContext-factory based)       │
│  DocumentService · ListService · PageService · SearchService│
│  WorkflowService · CalendarService · ShareLinkService       │
│  DiscussionService · FormService · NavigationService        │
│  SiteService · DashboardService · Notification/Audit/User   │
│  Clippy/ (Semantic Kernel)  ·  Storage/ (IFileStorage)      │
└──────────────┬───────────────────────┬──────────────────────┘
               │                       │
┌──────────────▼───────────┐  ┌────────▼─────────────────────┐
│ Data/ApplicationDbContext│  │ External providers           │
│ EF Core 10 + Identity    │  │ Storage: FS/Azure/S3/MinIO   │
│ SQLite/SQLSrv/PG/MySQL   │  │ Vector: Local/Qdrant/Chroma  │
└──────────────────────────┘  │ LLM: OpenAI/Anthropic/       │
                              │      Gemini/Ollama           │
                              └──────────────────────────────┘
```

## Key design decisions

### DbContext factory + singleton services
Blazor Server circuits are long-lived, so services never hold a `DbContext`. Every operation creates a short-lived context via `IDbContextFactory<ApplicationDbContext>`. This makes all services safe to register as singletons — one instance shared across circuits, which also lets `NotificationService.Changed` and `NavigationService.Changed` events push live UI updates to all connected users.

### Provider factories (config-driven)
`Program.cs` reads `appsettings.json` once at startup and wires:
- **Database** — the `AddDbContextFactory` switch selects UseSqlite / UseSqlServer / UseNpgsql / UseMySql. Schema is created with `EnsureCreated` (no migrations) so any provider works on first run.
- **Storage** — `IFileStorage` (FileSystemStorage, AzureBlobStorage, or S3Storage; MinIO reuses S3Storage with `ForcePathStyle` + custom `ServiceURL`).
- **Embeddings** — `IEmbeddingGenerator<string, Embedding<float>>` (Microsoft.Extensions.AI abstraction) with three implementations: `LocalHashEmbeddingGenerator` (deterministic 384-dim, zero dependency), OpenAI REST, Ollama REST.
- **Vector index** — `IVectorIndex?`: `null` means Local mode (cosine similarity over embeddings stored as blobs in the `SearchIndex` table); Qdrant/Chroma implementations talk REST.

### Search pipeline
Content-producing services (documents, pages, list items, discussions) call `SearchService.IndexAsync(entityType, id, title, content, link)` on save. The entry stores plain text (full-text via `LIKE`) and an embedding (semantic). `ReindexAllAsync` rebuilds everything (exposed in Admin → Settings and run automatically on first boot).

### Versioning model
- **Documents**: every upload with the same name/folder bumps `Document.Version` and appends a `DocumentVersion` row pointing to a new storage key — old blobs are kept, enabling rollback (which itself creates a new version).
- **CMS pages**: `ContentJson` is the draft; `PublishAsync` copies it to `PublishedJson`, bumps `Version`, and appends `CmsPageVersion`. Rollback re-publishes an old version's JSON.

### Workflow engine
A definition is a JSON graph (`WfGraph`: nodes + edges) edited visually in the designer. `WorkflowService.AdvanceAsync` walks the graph: `Approval` nodes create `ApprovalTask` rows (assigned to a user or `role:X`) and pause; `Condition` nodes route by comparing a context key against edge labels `yes`/`no`; `Notify` nodes push notifications; `End` completes the instance. Rejecting a task rejects the instance unless a `no` edge exists.

### Clippy (Semantic Kernel)
`ClippyService.BuildKernel()` creates a kernel per request based on `Clippy:Provider`:
- **OpenAI** — native SK connector (custom endpoint supported).
- **Anthropic** — SK OpenAI connector pointed at Anthropic's OpenAI-compatible endpoint (`https://api.anthropic.com/v1`).
- **Gemini** — `Microsoft.SemanticKernel.Connectors.Google`.
- **Ollama** — `Microsoft.SemanticKernel.Connectors.Ollama` (local, no key).

Three plugins are always registered: `UtilityPlugin` (date/time, math via `DataTable.Compute`, unit conversion), `WebPlugin` (Tavily search, page scraping, file-from-URL), and `BlazePointDataPlugin` (portal statistics, document/list search, sites, events, discussions, page content). `FunctionChoiceBehavior.Auto()` lets the model call them. Responses stream token-by-token to the UI; history (last 30 messages) is rebuilt per request with image attachments loaded as bytes so any provider can consume them.

### CMS webparts
A page's content is a `List<WebPartModel>` (type key, title, column 0–2, order, settings dictionary) serialized to JSON. `WebPartRegistry` maps type keys to Razor components rendered via `DynamicComponent`. Masterpages are style variants (`News`, `Intranet`, `Web`) applied by `PageView`. See [custom-webparts.md](custom-webparts.md).

### Auth
ASP.NET Core Identity with cookie auth. Because the whole UI is interactive (no HttpContext during rendering), login/register/reset are **plain HTML form posts** to minimal-API endpoints under `/auth/*` (not `/account/*`, which are the Blazor page routes — a POST to the same path would be ambiguous). Roles: Admin, Editor, Viewer; new registrations get Viewer.

## Project layout

```
src/BlazePoint/
├── Api/                 # REST, auth form endpoints, GraphQL query
├── Components/
│   ├── Layout/          # MainLayout (fb-style shell), EmptyLayout
│   ├── Pages/           # one .razor per route (+ Account/, Admin/)
│   ├── Shared/          # BpComponentBase, UserBadge, FormFieldInput…
│   └── WebParts/        # registry + webpart components
├── Data/                # entities, DbContext, seeder
├── Services/            # business logic (see diagram)
│   ├── Clippy/          # Semantic Kernel service + plugins
│   ├── Search/          # embeddings, vector indexes, SearchService
│   └── Storage/         # IFileStorage implementations
├── wwwroot/             # app.css (theme system), js/blazepoint.js
└── appsettings.json     # all provider configuration
```
