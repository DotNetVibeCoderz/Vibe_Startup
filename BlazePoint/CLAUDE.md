# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project

**BlazePoint** — a SharePoint-style collaboration portal, fully implemented as a single-project **Blazor Server** app on **.NET 10** at `src/BlazePoint`. `Requirement.md` is the original spec (Indonesian); `docs/` has architecture/API/config docs; READMEs exist in English (`README.md`) and Indonesian (`README.id.md`). UI text is Indonesian.

## Commands

```
dotnet build src/BlazePoint/BlazePoint.csproj      # build
dotnet run --project src/BlazePoint               # run → http://localhost:5112
```

There is no test project. First run auto-creates + seeds the SQLite DB and builds the search index. **To re-seed sample data, delete `src/BlazePoint/App_Data/`** (the seeder returns early if any Site row exists). Demo login: `admin@blazepoint.local` / `Blaze123!` (also editor@/viewer@/budi@/sari@/rudi@/maya@/andi@).

## Architecture (read docs/architecture.md for detail)

- **Whole UI is interactive**: `App.razor` renders `<Routes @rendermode="InteractiveServer" />`. Consequently components never see `HttpContext` — current user comes from `AuthenticationStateProvider`, wrapped by `Components/Shared/BpComponentBase` (exposes `UserId`, `IsAdmin`, `IsEditor`; pages override `OnInitializedCoreAsync`, **not** `OnInitializedAsync`).
- **Auth form-posts live at `/auth/*`** (minimal APIs in `Api/AccountEndpoints.cs`), NOT `/account/*` — those are the Blazor page routes; mapping a POST on the same path causes `AmbiguousMatchException`.
- **All services are singletons** using `IDbContextFactory<ApplicationDbContext>` (never hold a DbContext). `NotificationService.Changed` / `NavigationService.Changed` events push live updates to all circuits — unsubscribe in `Dispose`.
- **Provider factories in `Program.cs`**, all driven by `appsettings.json`: database (Sqlite/SqlServer/PostgreSql/MySql via `EnsureCreated`, no migrations), `IFileStorage` (FileSystem/AzureBlob/S3/MinIO — MinIO reuses `S3Storage` with `ForcePathStyle`), embeddings (`IEmbeddingGenerator` from Microsoft.Extensions.AI: Local hash fallback/OpenAI/Ollama), vector index (`IVectorIndex?`: null = in-DB cosine, or Qdrant/Chroma REST).
- **Search**: content services call `SearchService.IndexAsync` on save; `ReindexAllAsync` rebuilds (Admin → Settings).
- **CMS**: a page's content is `List<WebPartModel>` JSON (draft in `ContentJson`, live in `PublishedJson`; publish bumps version + snapshot row). WebParts are Razor components registered in `Components/WebParts/WebPartRegistry.cs` and rendered via `DynamicComponent` — to add one, create the component with a `[Parameter] Dictionary<string,string> Settings` and add a descriptor to the registry (palette + property panel are generated). Masterpages (`News`/`Intranet`/`Web`) are switch cases in `PageView.razor` + CSS classes `mp-*`.
- **Workflow engine** (`WorkflowService`): definitions are JSON graphs (`WfGraph`); `Approval` nodes create `ApprovalTask` assigned to a user id or `"role:X"` and pause; `Condition` routes via edge labels `yes`/`no`.
- **Clippy** (`Services/Clippy/`): Semantic Kernel, provider chosen by `Clippy:Provider` — Anthropic uses the OpenAI connector pointed at Anthropic's OpenAI-compatible endpoint. Plugins: `UtilityPlugin`, `WebPlugin` (Tavily/scrape/file), `BlazePointDataPlugin` (DB queries). Streaming via `IAsyncEnumerable`; **no `yield` inside catch** — collect the error then yield after.

## Razor gotchas hit in this codebase

- Never name a loop variable `page` in .razor files — `@page.X` is parsed as the `@page` directive. Use `pg`.
- SVG `<text>` is a reserved Razor tag — emit it via `(MarkupString)` (see `WorkflowDesigner.razor`).
- Attributes containing C# string literals use single-quoted attribute syntax: `@onclick='() => Go("/")'`.
- `HotChocolate.Path` collides with `System.IO.Path` — fully qualify `System.IO.Path` in service/endpoint files.
- When editing files with PowerShell, use `[System.IO.File]::ReadAllText/WriteAllText` with UTF8 (no BOM) — `Get-Content`/`Set-Content` corrupts the emoji/Indonesian text in this repo.
