# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

WashUp is a laundry management system: a single-project Blazor Server app (.NET 10 preview) with EF Core, ASP.NET Identity, Semantic Kernel AI chat, and simulated IoT/GPS features. The product targets Indonesian users — UI text, role names, order statuses, and seed data are in Bahasa Indonesia. Keep new user-facing strings in Indonesian to match.

## Commands

```bash
dotnet build          # build (project targets net10.0 preview SDK)
dotnet run            # run at http://localhost:5262 (https: 7086); Swagger UI at /swagger in Development
```

There is no test project and no linter configured.

Database is created with `EnsureCreatedAsync()` + `DataSeeder` at startup — **there are no EF migrations**. After changing any model or `AppDbContext`, delete `WashUp.db` and restart to recreate/reseed. The seeder skips itself if any user exists.

Demo logins (all seeded): `owner@washup.id`, `admin@washup.id`, `kurir1@washup.id` with password `WashUp@2024`; `pelanggan1@email.com` with `Pelanggan@123`.

## Architecture

Everything lives in one project (`WashUp.csproj`); `Program.cs` is the composition root and also contains all Minimal API endpoints (`/api/*`) inline — there are no controller classes.

**Render mode**: the app runs global InteractiveServer, decided per-request in `Components/App.razor` via `HttpContext.AcceptsInteractiveRouting()`. Auth pages (`Components/Pages/Auth/*`) opt out with `[ExcludeFromInteractiveRouting]` and use static-SSR forms (`FormName` + `[SupplyParameterFromForm]`) because Identity must set cookies on a plain HTTP request; logout is the `GET /auth/logout` minimal-API endpoint, never `SignOutAsync` from a circuit. A custom `AppClaimsPrincipalFactory` adds the `FullName` claim used by the layout.

- **Components/** — Blazor UI. One feature per folder under `Components/Pages/<Feature>/Index.razor` (Orders, Finance, IoT, Chat, etc.), plus `Components/Pages/Admin/Users.razor` for role management. Routing in `Components/Routes.razor` uses `AuthorizeRouteView`; unauthenticated users are redirected to `/auth/login`. `_Imports.razor` already imports EF Core, Identity, Models, Data, and Services, so pages inject `AppDbContext` and services directly (no repository layer). Charts use Blazor-ApexCharts (`AddApexCharts()` registered); note `FormatYAxisLabel` is WASM-only — use a JS-string `YAxisLabels.Formatter` instead. Modals/empty states/timeline are plain CSS classes from `washup-theme.css` (`modal-backdrop`, `modal-panel`, `empty-state`, `timeline`).
- **Models/** — entities grouped by domain into multi-class files (e.g. `Finance.cs` holds Invoice, FinancialTransaction, TaxRecord). Add related entities to the existing domain file rather than one file per class.
- **Data/** — `AppDbContext` (extends `IdentityDbContext<ApplicationUser>`, all relationships/indexes configured in `OnModelCreating`) and `DataSeeder`.
- **Services/** — plain classes registered in `Program.cs` DI (no interfaces). Scoped: Storage, Notification, Invoice, ChatBot. Singleton: `IoTSimulatorService` and `GpsSimulatorService`, which are started fire-and-forget at the end of `Program.cs` and loop on background threads, creating a fresh DI scope per iteration to write sensor/GPS rows to the DB every few seconds.
- **Hubs/** — currently empty; `AddSignalR()` is registered but no hubs exist yet. Real-time UI updates today come from Blazor Server circuits plus the simulators polling the DB.

### Provider-switch pattern

Three subsystems select an implementation from `appsettings.json` at runtime via a string switch (not DI):

- **Database**: `DatabaseProvider` = `SQLite` (default) / `PostgreSQL` / `SqlServer` / `MySQL` (Pomelo; server version pinned via `MySqlServerVersion` config, not AutoDetect), switched in `Program.cs`; each has its own connection string.
- **Storage**: `FileStorage:Provider` = `FileSystem` / `AzureBlob` / `S3` / `MinIO`, switched inside `StorageService`.
- **AI chat**: `AI:Provider` = `OpenAI` / `Anthropic` / `Gemini` / `Ollama`, switched inside `ChatBotService.Initialize()`. All providers go through the Semantic Kernel **OpenAI connector** pointed at provider-specific endpoints — there is no native Anthropic/Gemini connector. Chatbot persona ("Mbok Inem"), system prompt, and sampling settings come from the `ChatBot` config section; kernel functions (Tavily search, URL scrape, DB query, math) are defined in `ChatBotService`.

API keys in `appsettings.json` are empty by default; chat and search features are no-ops until keys are supplied.

### Domain conventions

- Order status is a plain string with Indonesian values: `Diterima → Dicuci → Disetrika → Selesai → Dikirim` (no enum — compare literals exactly, e.g. `Program.cs` dashboard endpoint). Transitions are logged to `OrderStatusLog`.
- Roles are seeded via `HasData` in `AppDbContext`: `Pemilik` (owner), `Admin`, `Kurir` (courier), `Pelanggan` (customer). Use these exact names in `[Authorize(Roles = ...)]` and `AuthorizeView`.
- `FinancialTransaction.TransactionType` is `"Income"` / `"Expense"` (English strings).
- IoT device types: `MesinCuci`, `Listrik`, `Air`, `SensorSuhu` — the simulator's switch in `IoTSimulatorService` must be extended for any new type.

### API auth

`/api/*` endpoints requiring auth use the `Api` policy, which accepts the Identity cookie **or** a JWT bearer from `POST /api/auth/token` (config in `appsettings.json` `Jwt` section). CSV exports live at `/api/reports/export/{orders|finance}`.

### Docs

`PLAN.md` is the feature checklist (all items complete as of July 2026 — see its "Revisi Penyelesaian" section); `README.md` / `README-ID.md` and `docs/DOCUMENTATION.md` describe features and config. Update `PLAN.md` status when completing items listed there.
