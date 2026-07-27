# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project

**Joka** — an Online Travel Agent (OTA) mockup/demo app: flights, trains, hotels, car rental, activities, travel packages, plus an AI assistant ("Mas Bolang"). Single Blazor Server project targeting **.NET 10**. Not a git repository; there is no test project and no solution file.

EF Core is deliberately still on **9.0.0** while the framework is 10: Pomelo, the MySQL provider, has no 10.x release and caps `Microsoft.EntityFrameworkCore.Relational` at `[9.0.0, 9.0.999]`, so upgrading EF would mean dropping a database provider that `requirements.md` requires. `net9.0` assemblies run fine on `net10.0`. The reason is also recorded as a comment in `Joka.csproj` — don't "fix" the version mismatch without checking whether Pomelo has shipped an EF 10 build.

All user-facing UI copy and most inline code comments are in **Bahasa Indonesia**. Match that when editing pages. Code identifiers are English.

**`Progress.md` is the authoritative status board** — check it before assuming a feature works, and update it when you finish something. `PLAN.md` is the forward-looking roadmap (what's next and why that order); it does not track completion.

Four roles exist: `Admin`, `Operator`, `Merchant`, `User` (constants in `Models/Backoffice/Backoffice.cs`). Back-office consoles live at `/admin`, `/operator`, `/merchant`, each guarded by an authorization policy registered in `Program.cs`. Every seeded account uses the password `Joka123!`.

`Routes.razor` **must** use `AuthorizeRouteView` — with plain `RouteView` the `[Authorize]` attributes on those pages are silently ignored and anyone can open them.

Pages that call `HttpContext.SignInAsync` (currently `Login.razor`) **must** carry `[ExcludeFromInteractiveRouting]`. `App.razor` picks the render mode per request from `HttpContext.AcceptsInteractiveRouting()`; under `InteractiveServer` the cascading `HttpContext` is null and sign-in throws.

Localization: `SharedResource` lives in the **root `Joka` namespace**, not `Joka.Resources`, and `AddLocalization()` is called **without** `ResourcesPath`. MSBuild names the embedded resource from the neighbouring class's namespace (`Joka.SharedResource`); setting `ResourcesPath` makes the localizer look for `Joka.Resources.SharedResource`, miss, and silently echo the keys instead of the text. The neutral `.resx` holds Indonesian, so an untranslated key renders Indonesian rather than a raw key.

Data Protection keys are persisted to `Data/keys` and the application name is pinned. Don't remove that — without it every restart invalidates in-flight antiforgery tokens and auth cookies, which surfaces as *"A valid antiforgery token was not provided"* on login.

## Commands

```bash
dotnet build                  # 0 errors expected; ~15 warnings are pre-existing (ImageSharp CVEs, CS0649)
dotnet run                    # http://localhost:5275 (https://localhost:7204) — NOT :5000 as README claims
dotnet watch                  # hot reload during UI work
```

Swagger UI at `/swagger` (Development environment only).

`SKEXP0010` (Semantic Kernel experimental) and `CS4014` (unawaited task) are suppressed project-wide in `Joka.csproj` — don't re-introduce them as noise-suppression elsewhere.

## Database

- Provider chosen by `Database:Provider` in `appsettings.json`: `SQLite` (default), `SQLServer`, `MySQL`, `Postgre`. The switch lives in `Program.cs:24-41`.
- **SQLite maps every `decimal` to `double`** via a value converter applied in `OnModelCreating`. SQLite cannot `ORDER BY` a decimal, so without this every "sort by price" query throws `NotSupportedException` at runtime. Keep it when adding money fields; it is what allows sorting to stay in SQL.
- **There are no EF migrations.** `Program.cs:119-124` calls `Database.EnsureCreated()` then `SeedData.InitializeAsync`. Consequence: **any model change requires deleting `Data/joka.db`** (SQLite) or dropping the DB — `EnsureCreated` will not alter an existing schema and the app will fail at runtime with missing-column errors instead.
- `SeedData.cs` is idempotent-by-emptiness (it seeds only when tables are empty), and produces the demo airports/airlines/flights/hotels/etc. Seeded users have **no `PasswordHash`**, so they cannot log in via the password path — register a new account instead.
- Cross-cutting EF config lives in `AppDbContext.OnModelCreating`: soft-delete global query filters (`!e.IsDeleted`) on the main catalog entities, plus unique indexes on booking/transaction/voucher codes and `User.Email`. New catalog entities deriving from `BaseEntity` should get a matching query filter if they are soft-deletable.

## Architecture

Three entry surfaces share one `AppDbContext`:

1. **Blazor pages** (`Components/Pages/*.razor`) — interactive server rendering. Pages **inject `AppDbContext` directly**; there is no repository layer. Only `AuthService` wraps data access, and only for auth/bookings/wishlist/notifications.
2. **Minimal API** (all of it inline in `Program.cs`, hanging off `apiGroup` from line 312) — read-only search endpoints under `/api`, plus auth, Google OAuth and the payment webhooks. New endpoints go in this file, in the existing commented section blocks.
3. **Services** (`Services/`) — no repository layer, just feature services: `AuthService`, `CheckoutService`, `AnalyticsService`, `ReviewService`, `SupportService`, `TransportService`, `FraudDetectionService`, `MerchantCatalogService`, `NotificationService`, `CurrencyService`, `SettingsService`, `HealthProbeService`, `MarkdownService`, plus `Chat/`, `Payments/`, `Storage/` and `Trains/`.

Models are grouped by domain under `Models/<Domain>/<Domain>.cs` — one file holds several related entities (e.g. `Models/Flights/Flight.cs` contains `Airport`, `Airline`, `Flight`, `FlightBooking`; `Models/Buses/Bus.cs` contains `BusTerminal`, `BusOperator`, `BusService`, `BusSchedule`, `BusBooking`). Don't create one-file-per-class. Buses and shuttles share one set of tables and are told apart by `BusService.ServiceType`. All entities derive from `Models/Common/BaseEntity` (Guid `Id`, `CreatedAt`, `UpdatedAt`, `IsDeleted`, `CreatedBy`).

`Components/_Imports.razor` already `@using`s every model/service namespace — new pages rarely need their own using directives.

## Authentication — known-fragile area

- Cookie auth is the default scheme. Google is registered **only when `AppSettings:Auth:GoogleClientId` and `GoogleClientSecret` are both non-empty** — registering it with blank credentials makes `OAuthOptions.Validate()` throw on *every* request, because the authentication middleware initialises remote handlers per request. The challenge scheme falls back to cookies when Google is off, and `Login.razor` hides the Google button.
- `App.razor` renders `<Routes @rendermode="InteractiveServer" />`, making the **entire** component tree interactive, so `[CascadingParameter] HttpContext` is **null on every page except the static-SSR ones**. This has already caused two rounds of bugs (login itself, then Wishlist/Bookings/Notifications/Profile all claiming the user was logged out). **Read auth state through `AuthenticationStateProvider` or `<AuthorizeView>`, never through `HttpContext`** — `AddCascadingAuthenticationState()` is registered for exactly this.
- Sign-in happens in non-interactive places only: `Login.razor` (marked `[ExcludeFromInteractiveRouting]`) and the `POST /login-google`, `GET /google-callback`, `POST /logout` endpoints in `Program.cs`.
- Password hashing is PBKDF2 with a per-user random salt. Legacy SHA256-with-static-salt hashes are still accepted on login and silently upgraded on the first successful sign-in, so don't delete that path while old rows may exist.
- `RequestPasswordResetAsync` deliberately returns the reset token in the response message for demo purposes.

## ChatBot (Mas Bolang)

- `ChatBotService` builds a Semantic Kernel per call to `InitializeAsync(provider)` and registers two plugins: `ChatKernelFunctions` (`utilitas` — Tavily, scrape, file read, date/time, math, currency) and `JokaDataFunctions` (`joka` — queries the app's own inventory and looks up bookings by code).
- Both plugins are registered with **`AddFromObject`, not `AddFromType<T>()`**. The kernel has its own service container that knows nothing about `IHttpClientFactory` or `AppDbContext`, so type-based registration cannot construct them.
- **Auto function calling is configured per connector, not once.** `BuildSettings()` returns a different execution-settings class depending on the provider: `OpenAIPromptExecutionSettings.ToolCallBehavior` for OpenAI/Ollama, `GeminiPromptExecutionSettings.ToolCallBehavior` (a `GeminiToolCallBehavior`, a different type entirely), and plain `PromptExecutionSettings.FunctionChoiceBehavior` for the `IChatClient`-backed Anthropic path. Set the wrong one and the functions are registered but never called — the model just answers from memory, which is exactly the failure mode that made it invent prices before.
- `JokaDataFunctions` returns Markdown tables on purpose — the chat page renders them directly. It also keeps a `CityAliases` map because the seed data is inconsistent ("Denpasar" for the airport, "Bali" for hotels); searches match the typed term *and* its alias rather than substituting one for the other.
- Each provider now uses its native connector. Anthropic has no first-party SK connector, so it goes `Anthropic.SDK` → `IChatClient` → `ChatClientBuilder.UseFunctionInvocation()` → `AsChatCompletionService(sp)`. `UseFunctionInvocation` lives in the **`Microsoft.Extensions.AI`** package, not `.Abstractions`, and — being an extension method — needs the `using`; fully qualifying the receiver is not enough. It is called statically as `FunctionInvokingChatClientBuilderExtensions.UseFunctionInvocation(pipeline, null, null)` because importing that namespace collides with `Joka.Models.Chat.ChatResponse`.
- Ollama is still reached through `AddOpenAIChatCompletion`, and its endpoint gets `/v1` appended — Ollama's OpenAI-compatible surface lives there, not at the root.
- A missing API key now throws `InvalidOperationException` from `InitializeAsync` instead of quietly sending `"sk-placeholder"`. `SendMessageAsync` catches it, so the user still sees the Indonesian "layanan AI sedang tidak tersedia" message rather than a stack trace.
- The service holds `ChatHistory` in memory (scoped → per Blazor circuit). Durable history is the page's job: `Chat.razor` separately persists `ChatSession`/`ChatMessage` rows.

## Configuration

Everything is `appsettings.json`-driven; there is no options-pattern binding for most sections — code reads `IConfiguration` by string path (`_config["ChatBot:Providers:OpenAI:ApiKey"]`). When adding a feature, add its section to `appsettings.json` and read it the same way.

Secrets (`GoogleClientId/Secret`, AI keys, payment gateway keys, Tavily) ship as empty strings in `appsettings.json`. Use user-secrets or environment variables locally rather than filling them in.

## Storage

All four `IStorageService` providers are implemented (FileSystem, Azure Blob, S3, MinIO) and chosen by `Storage:Provider`. `StorageServiceFactory.Create()` falls back to FileSystem whenever the selected provider's credentials are blank — an unconfigured cloud provider must not take the app down.

For **user-supplied** uploads (the avatar on `Profile.razor`), derive the stored extension from a content-type whitelist, never from `Path.GetExtension(file.Name)`. FileSystem storage writes into `wwwroot`, which is served as static files, so a name like `cat.png.html` would come back as same-origin HTML — stored XSS. The merchant uploads predate this and still trust the uploaded name; they are behind an authenticated merchant role, but tighten them if you touch that code.

`UpdateProfileAsync` treats `AvatarUrl = null` as "leave it alone" and `AvatarUrl = ""` as "clear it". That distinction is what lets the ordinary Save button omit the field without wiping the photo while "Remove photo" still works.

## Payments

- `IPaymentGateway` has three implementations: `StubGateway` (instant settle, demo), `MidtransGateway` (Snap) and `XenditGateway` (Invoice). `PaymentGatewayFactory.Create()` reads `Payment:DefaultGateway` and **falls back to the stub whenever credentials are missing or the name is unknown**.
- **A transaction is only ever marked paid in two places**: `StubGateway`, or a webhook whose signature was verified (`POST /api/payments/midtrans-notification`, `POST /api/payments/xendit-callback` in `Program.cs`). The browser never gets to tell the server a payment succeeded — a customer can edit whatever the browser posts. Keep it that way.
- Both webhooks verify first, then return **200 for booking codes they don't recognise**, so the gateway stops retrying a notification we will never be able to match.
- Voucher quota and loyalty points are consumed in `SettleAsync`, not at checkout: an abandoned payment must not eat someone else's voucher. Gateways re-send notifications, so `SettleAsync` returns early when the transaction is already `Completed` — don't add work before that guard.

## Trains and transport

- `ITrainScheduleProvider` has `KaiTrainScheduleProvider` (enabled only when `Integrations:KAI:ApiKey` is set) and `LocalTrainScheduleProvider`. **KAI has no public API** — that class is an integration seam, deliberately. It never throws (an outage returns an empty list), remote departures come back with `ScheduleId == null` so `IsBookable` is false, and `Trains.razor` says out loud when it fell back rather than quietly showing local data as if it were live.
- `TransportService.FareFor` is `static` and is the **only** place a ride fare is computed. The search page, the stored `TransportBooking`, `GET /api/transport` and the chatbot's `cari_transportasi_lokal` all call it, so they cannot disagree. Per-km fares are clamped to `MinimumFare` and rounded to the nearest Rp500.

## Support (live agent)

`SupportBroadcaster` is a **singleton** that raises `MessagePosted`; pages subscribe and re-render through `InvokeAsync`. It pushes over Blazor's own circuit — there is no second SignalR client connection, and adding one would be the wrong fix. Unsubscribe in `Dispose` or the circuit leaks.

## UI / design system

- **Design tokens and all shared component classes live in `wwwroot/app.css`** — a plain global stylesheet. They must stay there. Blazor scopes every selector in a `.razor.css` file to its own component (`.card` becomes `.card[b-xxxxx]`), so putting `:root`, `body`, or shared classes in one silently disables them everywhere else. `Components/Layout/MainLayout.razor.css` is now shell-only (sidebar, top bar, page frame) and correctly scoped.
- Style: "Neo Brutalism Soft" — 2px `var(--border-strong)` borders, hard offset shadows, `#FF5C35` orange / `#FFB800` yellow.
- Three type roles: `--font-display` (Bricolage Grotesque, sparingly), `--font-body` (Plus Jakarta Sans), `--font-mono` (JetBrains Mono — for booking codes, times, airport codes, prices).
- The signature component is `.stub`: a perforated ticket card (`.stub-main` + `.stub-end`, punched holes drawn by `.stub-end::before/::after`), paired with `.board` departure-board typography. Use it for anything that represents a real ticket.
- Dark mode is `:root[data-theme="dark"]` on `<html>`, set before first paint by the bootstrap script in `App.razor` and toggled through `window.joka.toggleTheme()` (`wwwroot/js/joka.js`), persisted in `localStorage`. **Always use `var(--...)` tokens** — hardcoded colours break the toggle.
- Token *names* are load-bearing: ~20 pages reference `--accent-primary`, `--border-strong`, `--radius-md` etc. in inline `style` attributes. Retune their values, never rename them.
- Charts go through `<Chart>` (`Components/Shared/Chart.razor`) over `wwwroot/js/charts.js`; D3 is loaded from CDN in `App.razor` and `charts.js` is listed **after** it because it needs `d3` in scope. Colours are read from the CSS custom properties at draw time and every chart redraws on the `joka:theme` event that `setTheme` dispatches — so never hardcode a series colour, and don't add a chart that caches its palette.
- `Chart.razor` guards re-renders with a signature string built from the data; without it every parent render redraws the SVG and the entry animation restarts.

## Razor gotchas worth knowing

- An **inline `<style>` block inside a `.razor` file is global**, not scoped — only a companion `.razor.css` file gets the `[b-xxxxx]` attribute rewrite. `::deep` in an inline block is emitted verbatim and is invalid CSS.
- Razor's HTML parser reads a literal `<style>` **inside a CSS comment** as a nested opening tag and fails with `RZ9980: Unclosed tag`. Reword the comment.
- `@inject SomeService Foo` inside `Foo.razor` is `CS0542: member names cannot be the same as their enclosing type` — the generated class is named after the file. Name the injected property something else (`Support.razor` injects `SupportService` as `Tickets`, `Transport.razor` injects `TransportService` as `Rides`).
- Nested double quotes inside an `@onclick` lambda need a single-quoted attribute: `@onclick='() => _open = false'`.

## Not wired up

Present in config/packages but with no implementation behind them — don't assume they work: the KAI API itself (the provider is real, the endpoint is a guess — see above), ImageSharp, `AppSettings:Auth` Google OAuth unless you supply credentials.

Wired up and easy to assume otherwise: SignalR (`MapHub` at `/hubs/notifications` for external clients; in-app pushes use the Blazor circuit), payment gateways, localization (`Resources/SharedResource.resx` + `.en.resx`, 355 keys — the **language selector is still hidden** behind `AppSettings:ShowLanguageSwitcher` pending a translation review, but the machinery works), currency conversion, QRCoder (`ETicket.razor` renders a real PNG data URI via `PngByteQRCode`).

Culture is pinned at startup from `AppSettings:DefaultLanguage` (`id-ID` by default). Without it, `N0` prices render as `Rp1,200,000` instead of `Rp1.200.000`.

**Because of that pin, parse every config value with `CultureInfo.InvariantCulture`.** `appsettings.json` writes `0.7`, but under `id-ID` a culture-sensitive `double.TryParse("0.7")` reads it as **7** — which silently sent `temperature: 7` to OpenAI and made every chat request fail with HTTP 400. The same applies to any numeric string that comes from markup or config rather than from a user typing in their own locale.
