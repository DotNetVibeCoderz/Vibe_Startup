# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Commands

```bash
dotnet build                 # expect 0 errors, 0 warnings — keep it that way
dotnet run                   # http://localhost:5182 · https://localhost:7061 (Properties/launchSettings.json)
dotnet watch run             # hot reload
dotnet publish -c Release
```

There is no solution file, no test project, and no test framework — `dotnet build` plus hitting endpoints on a running instance is the only verification loop. Swagger UI is at `/api/docs` (Development only). Seeded demo login: `admin@ngibrid.com` / `Admin123!` (also `manager@`, `staff@`, `courier1..3@`, `customer1..5@`; see `Services/DataSeeder.cs`).

## What this is

Single-project ASP.NET Core (.NET 10) app: Blazor Server UI + Minimal API + SignalR + EF Core, for an Indonesian logistics/courier platform. UI copy, seed data, and `docs/` are in Indonesian — match that when adding user-facing strings. `docs/README-EN.md` is the English README; everything else in `docs/` is Indonesian.

## Architecture notes that aren't obvious from the file tree

**Services don't map 1:1 to files.** Several classes live per file, so grep for the class name rather than guessing a path:
- `TrackingService.cs` → `TrackingService`, `DynamicPricingService`, `PriceResult`
- `PaymentInvoiceService.cs` → `PaymentService`, `InvoiceService`, `InsuranceService`, `GreenLogisticsService`
- `CourierPickupService.cs` → `CourierService`, `PickupService`, `SupportTicketService`
- `NotificationAnalyticsService.cs` → `NotificationService`, `AnalyticsService`, `RevenueSummary`, `CourierPerformance`, `ActivityEntry`, `OperationalSnapshot`
- `IntegrationService.cs` → `MarketplaceService`, `PartnerLogisticsService`, `SmartLockerService`
- `ComplianceService.cs` → `ComplianceService`, `LoyaltyService`
- `CityService.cs` → `CityService`, `CityCoordinates` (the latter used to live in `RouteOptimizationService.cs`)
- `ChatPlugins.cs` → all six Semantic Kernel plugins
- `AiProviders.cs` → `AnthropicChatClient`, `GeminiChatClient`, `KernelToolInvoker`, `AiTurn`/`AiImage`
- `SimulatorServices.cs` → `GpsSimulatorService`, `IotSimulatorService`, `SmartLockerSimulatorService`
- `Hubs/AppHubs.cs` → all four hubs; `Models/*Models.cs` group ~4–6 entities each

Every service is hand-registered in `Program.cs` — a new service class is invisible until added there.

**City master data is the source of truth for every coordinate.** `Models/MasterDataModels.cs` holds the `City` entity (Country/Province/Name/Type/SeatName/Lat/Lng); `Data/IndonesiaCities.cs` is the compiled-in seed of all 514 kabupaten/kota across 38 provinces, inserted by `CityService.InitializeAsync()` **when the `Cities` table is empty** — which is checked independently of `DataSeeder`'s "no users yet" guard, and runs *before* it in `Program.cs` because the sample orders are built from real cities. `CityCoordinates` is the static name→coordinate index everything resolves through (tracking, pricing, routing, green logistics, the GPS simulator); `CityService.Load` swaps in the live DB rows at startup, and the compiled table is the pre-DB fallback. Two traps: (1) the index keys must carry the **type** — "Kota Bandung" and "Kabupaten Bandung" both normalise to `bandung`, and their seats are 15 km apart, so a province+name key alone silently collapses them; (2) `Prefixes` is declared **above** the index fields on purpose — static field initialisers run in declaration order and `Build()` reads it, so moving it down throws in the type initialiser and (because `Program.cs` catches DB-init failures) leaves the app running with no seeded users. Orders carry `SenderProvince`/`RecipientProvince`; pass them to `CalculatePriceAsync` and `CityCoordinates.Resolve` or the kota/kabupaten ambiguity comes back.

**Database is multi-provider and schema-created, not migrated.** `Data/DbProviderFactory.ConfigureProvider` switches on `Database:Provider` (`SQLite` default, `SQLServer`, `MySQL`, `Postgre`) with one connection string per provider under `Database:ConnectionStrings`. Startup calls `EnsureCreatedAsync()` — despite `MigrationsAssembly` being configured, there are no migrations. **Changing an entity requires deleting `Data/ngibrid.db`** (or the target DB) to pick up the new schema. `DataSeeder.SeedAsync()` no-ops entirely if any user row exists, so a partial seed stays partial.

**Entities derive from `BaseEntity`;** `NgibridDbContext.SaveChanges*` overrides stamp `UpdatedAt` on every modify and `CreatedAt` **only when it is still `default`** — so backdated inserts (seed history, imported orders) survive. It used to overwrite unconditionally, which flattened the whole seeded history onto install day and made every time series a single point; don't reinstate that. Identity uses `long` keys with `ApplicationUser`/`ApplicationRole`/`ApplicationUserRole` and renamed tables (`Users`, `Roles`, `UserRoles`, …).

**Auth happens over HTTP endpoints, never from a component.** `Api/AuthEndpoints.cs` owns login/register/logout/forgot-password/reset-password/change-password. An interactive Blazor Server component runs on an established SignalR circuit where response headers are long gone, so `SignInManager` cannot write the auth cookie there. The auth pages `fetch` those endpoints via `ngibrid.postAuth` and then do a full reload. Don't "simplify" this back into the component.

**Background services are singletons that must scope their own `DbContext`.** `GpsSimulatorService`, `IotSimulatorService`, and `SmartLockerSimulatorService` are each registered twice — as a singleton (so pages can call `StartSimulation`) and as a hosted service — and use `IServiceScopeFactory` inside the loop. Follow that pattern for any new `BackgroundService`. They honor `GPS:Simulator:Enabled`, `IoT:Simulator:Enabled`, and `IoT:LockerSimulator:Enabled` and exit immediately when false.

**Chat bot ("Mas Supri") routes per provider but shares one function set.** `ChatBotService` builds a single `Kernel` with six plugins (`Logistics`, `DateTime`, `Math`, `Internet`, `Pricing`, `Support`, all in `ChatPlugins.cs`). OpenAI and Ollama go through the real SK connector with `FunctionChoiceBehavior.Auto()`; Anthropic and Gemini go through `AnthropicChatClient`/`GeminiChatClient` in `AiProviders.cs`, which translate `KernelFunction` metadata into each API's tool schema and run their own tool-call loop (max 5 rounds). **All four providers therefore get function calling** — if you add a plugin, it works everywhere for free. Plugins take `IServiceScopeFactory`, never a `DbContext`. `InternetPlugin` uses the named `Tavily` HttpClient configured in `Program.cs` and guards `scrape_url`/`read_file_from_url` against SSRF.

**Config is writable at runtime.** `AppConfigService` writes `appsettings.json` back to disk (writer lock + write-temp-then-`File.Replace`, preserving JSON value types) and triggers a config reload; `/settings` is the UI for it. Prefer reading values through `IConfiguration` at call time rather than caching them in a field, or runtime changes won't take effect.

**Static CSS/JS are cache-busted from `App.razor`.** `AssetVersion` (the built assembly's last-write time) is appended as `?v=` to every hand-written stylesheet and script. Without it the browser keeps serving its cached copy and an edit to `ngibrid.css`/`chat.css` looks like it did nothing — which is worth remembering when a style fix "doesn't work": check the served file with curl before assuming the CSS is wrong. Add the same `?v=@AssetVersion` to any new local asset.

**Front end is hand-rolled, no CSS/JS framework.** `wwwroot/css/ngibrid.css` defines the design system as CSS variables (`--bg-*`, `--text-*`, `--accent`, `--radius-*`, `--sidebar-width`) with a `[data-theme="dark"]` override block. **The palette is declared on the document root only** — never re-declare those variables on a wrapper element. A wrapper that disagreed with `<html data-theme>` re-scoped the palette mid-tree, so inherited text kept the root's colour while backgrounds switched: white cards with near-white headings. `ThemeToggle` syncs the server-side `ThemeService` from `ngibrid.getStoredTheme()` on first render for the same reason. `components.css` holds the newer component styles. JS interop goes through `window.ngibrid` in `wwwroot/js/ngibrid.js` (Leaflet maps, `postAuth`, `printHtml`, theme handling), which delegates all charting to `wwwroot/js/ngibrid-charts.js` (**D3 v7**: line/area, donut, bar, forecast band, sparkline; theme-aware via CSS vars, redrawn by a `ResizeObserver` registry). D3 and Leaflet are vendored locally — do not add CDN links. `Components/App.razor` sets `@rendermode="InteractiveServer"` globally, so every page runs in a SignalR circuit.

**A D3 chart inside a tab must be redrawn every time the tab is re-entered.** Switching tabs removes the previous tab's markup, and the chart's `<svg>` goes with it — a one-shot `chartPending` flag set on load leaves the panel blank for anyone who visits another tab and comes back (`PaymentPage.razor` did exactly this). Re-arm the flag in the tab-change handler, not just after data loads. Note `ngibridCharts.barChart` filters out `value <= 0` and draws `emptyState` for an empty series, so "no bars" is ambiguous between no data and no draw — check `#id svg` count when debugging.

**Leaflet maps must be drawn from `OnAfterRenderAsync`, never from `OnInitializedAsync` or an event handler.** Two ways this bites: JS interop during the prerender pass throws `InvalidOperationException: JavaScript interop calls cannot be issued at this time`, and a handler that sets state then immediately calls JS runs *before* Blazor emits the target `<div>`, so `L.map('…')` finds nothing and the map silently never appears. The pattern everywhere is a `mapPending` flag set by the loader and consumed in `OnAfterRenderAsync` inside try/catch. Maps live on `/tracking` (route + live GPS marker), `/courier` (fleet + optimised route), `/warehouse`, and `/locker`; the generic helper is `ngibrid.renderPointsMap`, which keys maps by container id and builds popups from plain fields (never server-supplied HTML).

**Routes must be unique across components.** The Blazor router throws on ambiguous templates, and this is easy to reintroduce: `DashboardPage.razor` owns both `/` and `/dashboard`, so nothing else may claim `/`. Route matching is case-insensitive — don't add `@page "/Login"` alongside `@page "/login"`.

## Known rough edges

- `Middleware/` and `Helpers/` exist but are empty.
- `appsettings.json` carries a checked-in placeholder JWT secret and empty API-key slots for OpenAI/Anthropic/Gemini/Tavily/Midtrans/Xendit/SMTP/Twilio/Firebase. Notification channels (SMTP/Twilio/FCM) skip silently when unconfigured; in-app notifications still persist.
- Marketplace sync falls back to a deterministic sample-order generator when no marketplace credentials are set, so the import flow is testable but the data is synthetic.
- Payment gateways (Midtrans/Xendit) are recorded but not actually called — confirmation is manual.

## UI design work

`.claude/skills/frontend-design/SKILL.md` is installed; use it when building or reshaping UI.
