# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## What AppBender Is

A self-hosted low-code platform (.NET 10 + Blazor Server) combining Power Apps (visual form
designer), Power Automate (workflow engine), and Dataverse (Data Hub) concepts, with an AI
co-pilot ("App Guru") built on Semantic Kernel. The original product spec is `requirements.md`
(Indonesian); full feature docs are in `docs/`.

## Commands

```powershell
dotnet build                                # build solution
dotnet test                                 # run all unit tests
dotnet test --filter "FullyQualifiedName~WorkflowEngineTests"   # one test class
dotnet run --project src/AppBender.Web      # run the app (creates + seeds appbender.db on first run)
```

Sample logins after seeding: `admin@appbender.io/Admin123!`, `dev@appbender.io/Dev123!`,
`user@appbender.io/User123!`. Data API key for curl testing: `X-Api-Key: dev-api-key-change-me`.
Swagger at `/swagger`.

No EF migrations — schema is `EnsureCreated()` at startup. To reset local state, delete
`appbender.db*` in the repo root (it is recreated and reseeded on next run).

## Architecture (the parts that span multiple files)

- **Two projects**: `AppBender.Core` (all engine/services, no UI) and `AppBender.Web`
  (Blazor UI, API controllers, middleware, seeding). Web references Core.
- **Dynamic data model**: entities are metadata rows (`EntityDefinition.FieldsJson`); records all
  live in one `DataRecords` table as JSON (`DataJson`). `DataHubService` enforces validation,
  formula fields, autonumbers, and raises `EventBus` events on every write. There are no
  per-entity tables — never try to ALTER TABLE for user entities.
- **Everything user-designed is JSON**: form layouts (`FormComponent` tree), workflow steps
  (`WorkflowStep` tree), custom connector specs. Export/import, version snapshots, and AI
  generation all reuse these same JSON shapes.
- **Workflow engine**: control-flow step types (condition/switch/foreach/do_until/scope/delay/
  terminate) are interpreted inside `WorkflowEngine`; every other step type is an
  `IWorkflowAction` found by its `Type` key via DI (registered in
  `WorkflowActionRegistration.AddWorkflowActions`). Config values are `{{template}}` strings
  resolved by `Common/TemplateEngine` against `trigger`/`vars`/`steps.*`. Runs execute in an
  isolated DI scope via `WorkflowRunner` (singleton), which also persists `WorkflowRun` logs.
  Triggers: `ScheduleTriggerService` (Cronos), `WebhookController`, `EventTriggerService`
  (EventBus subscriptions).
- **Multi-tenancy**: scoped `ITenantContext` filtered manually in every service. It is populated
  in TWO places — `TenantResolutionMiddleware` (HTTP/API requests) and `MainLayout` (Blazor
  circuits) — because circuit DI scopes don't run middleware. Background workflow runs set it
  explicitly from the workflow's `TenantId`.
- **DbContext discipline**: Blazor Server circuits share scope, so all services create contexts
  per-operation via `IDbContextFactory<ApplicationDbContext>`. Identity's scoped `DbContext` is
  bridged from the factory in `Program.cs`.
- **AI layer**: one `SemanticKernelLlmClient` reaches OpenAI/Anthropic/Gemini/Ollama through
  their OpenAI-compatible endpoints (configured in `AiOptions` / appsettings `AI` section);
  kernel function plugins live in `Core/AI/KernelPlugins.cs`. Embeddings/image/audio use raw
  HTTP. RAG chunks + embeddings are stored in SQLite with in-memory cosine search and a keyword
  fallback.
- **API auth**: `ApiKeyMiddleware` turns `X-Api-Key` into a synthetic Developer principal before
  `UseAuthorization`; middleware order in `Program.cs` matters
  (auth → apikey → authorization → tenant → usage).

## Razor gotchas already encountered here

- Components with multiple RenderFragments (e.g. `Modal` with `Footer`) require explicit
  `<ChildContent>` wrapping.
- Attributes containing C# lambdas with string interpolation need single-quote delimiters:
  `@onclick='() => Nav.NavigateTo($"/x/{id}")'`.
- SVG `<text>` elements clash with Razor's `<text>` tag — emit them via `MarkupString`
  (see `BarChart.razor`).
- Markup inside methods only compiles in local functions declared in the template area
  (`@{ void Render(...) { ... } }`), not in `@code` blocks (see `FormRenderer.razor`).
- Config values that may be empty strings: use `string.IsNullOrWhiteSpace`, not `??`
  (appsettings templates ship empty strings, which bypassed `??` and crashed startup once).

## Conventions

- Package versions: EF/Identity pinned to 10.0.10 in BOTH csprojs (mismatch causes NU1605);
  `Microsoft.CodeAnalysis.CSharp.Scripting` pinned to 5.0.0 to match EF Design's Workspaces dep.
- Semantic Kernel experimental warnings are suppressed via `NoWarn` (SKEXP0001/0010/0110) in
  `AppBender.Core.csproj`.
- User-facing seed content and sample prompts are bilingual (Indonesian-first); docs are English
  with `README.id.md` as the Indonesian entry point.
