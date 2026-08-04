# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Overview

FastRide is a .NET 10 ride-hailing platform: a Minimal API backend, a Blazor Server admin
console, two MAUI Blazor Hybrid mobile apps (rider/driver), and a Spectre.Console load
simulator. Domain and UI text is Indonesian (Rupiah fares, Jakarta coordinates around
-6.2/106.8).

Docs live in `docs/` plus `PLAN.md` (roadmap), `Progress.md` (what has been done and why),
and `Spek.md` (original spec, Indonesian). They were rewritten alongside the v2.0 code and
currently match it — but verify against source before relying on any specific claim.

## Build & Run

```bash
dotnet restore
dotnet build FastRide.Api          # build individual projects, not the .sln
dotnet run --project FastRide.Api          # https://localhost:5001
dotnet run --project FastRide.AdminWeb     # https://localhost:5002
dotnet run --project FastRide.Simulator -- --duration 60
```

- **Do not `dotnet build FastRide.sln` unless the MAUI workload is installed** — the solution
  includes RiderApp/DriverApp targeting `net10.0-android;net10.0-ios;net10.0-maccatalyst`
  (+windows). The workload *is* installed on this machine; build a single framework when
  checking mobile: `-f net10.0-windows10.0.19041.0`.
- **Ports are now consistent** across `appsettings.json`, `launchSettings.json`, and every
  client: API 5001/5000, AdminWeb 5002/5003. They used to disagree (52545 vs 5001), which
  broke every client under `dotnet run`. If you change one, change all of them, and keep
  AdminWeb's address in the API's `ApiSettings:CorsOrigins`.
- **Tests:** `dotnet test FastRide.Tests` — 248 tests, ~65s, no running API or database
  needed. The integration half hosts the real app via `WebApplicationFactory<Program>`.
  No lint/format config — match surrounding style.
- Simulator: `S` stops, `P` pauses, `--duration N` auto-stops. It needs the admin account to
  approve its drivers' documents.

## Architecture

Dependency direction: `Shared` ← `Data` ← `Api`. Every client references only `Shared` and
talks HTTP. AdminWeb's reference to `FastRide.Data` was deliberately removed.

- **FastRide.Shared** — entities, enums, `DTOs/DTOs.cs` (single source of truth for every
  request/response), `Common/` (`PagedResult`, `GeoUtils`, `Display`, `ApiError`), and
  `Storage/IStorageProvider.cs`. `Display` holds the status→signal-colour mapping and
  Indonesian labels shared by all three UIs.
- **FastRide.Data** — `FastRideDbContext` (fluent config, indexes, `HasData` fare table) and
  `SampleDataSeeder` (~420 orders on a realistic demand curve; guarded by `if (await
  db.Users.AnyAsync()) return;`).
- **FastRide.Api** — split into `Endpoints/` (one file per area), `Services/`
  (`PricingService`, `OrderService`, `DispatchService`, `NotificationService`,
  `CacheService`, `CsvExporter`, `Result`), `Security/` (`TokenService`, `CurrentUser`,
  `SecurityStampMiddleware`, `Policies`), `Extensions/`, `Infrastructure/`. `Program.cs` is
  host assembly only.
- **FastRide.AdminWeb** — Blazor Server, global `InteractiveServer`. `AdminSession` holds
  the JWT per circuit in `ProtectedSessionStorage`; `ApiClient` binds to Shared DTOs.
- **FastRide.RiderApp / FastRide.DriverApp** — each has its own `ApiClient` (different
  endpoints) but both use Shared DTOs and enums. Sessions persist in MAUI `SecureStorage`.
  `ApiEndpoint.BaseUrl` is platform-aware (Android needs 10.0.2.2, not localhost).

### Invariants worth knowing before editing

- **Order transitions** are validated by a table in `OrderService`. Anything not listed
  returns 409.
- **Races are resolved in the database**, not in memory: accepting an order and consuming a
  promo slot both use conditional `ExecuteUpdateAsync`. Don't replace these with
  read-then-write.
- **One payment per order**, enforced by a unique index on `Payment.OrderId`. The row is a
  payment *intent*, not a receipt: a failed charge is retried by resetting that same row, so
  the anti-double-charge guarantee survives while retries stay possible. `POST /api/payments`
  is idempotent — a live charge returns the same QR rather than issuing a second one.
- **Payments go through `IPaymentProvider`** (`manual`, `simulated`, `midtrans`, `xendit`),
  chosen per method by `PaymentProviderRegistry`. Configuration comes from
  `Payments:Providers` in appsettings, seeded into the database on first start, where admin
  edits then win. See `docs/PAYMENTS.md`.
- **Webhooks are anonymous but verified.** `POST /api/payments/webhook/{provider}` checks the
  provider's own signature before trusting any field, is idempotent, refuses an amount that
  disagrees with the charge, and never un-pays a settled trip.
- `POST /api/payments/sandbox/{orderId}/settle|fail` stands in for the payer against the
  simulated provider. It drives the real signed callback path and is only mapped outside
  Production.
- **A rider may have only one trip in flight**; a second booking returns 409.
- **Drivers must have three approved documents** (SIM/STNK/KTP) before going online or
  accepting. Enforced in `SetStatus` and `OrderService.AcceptAsync`.
- **Driver GPS older than 10 minutes** is excluded from matching (`DispatchService.LocationFreshness`).

### Configuration switches

- `Database:Provider` — `SQLite` (default) | `SqlServer` | `PostgreSQL` | `MySQL`.
  MySQL uses Oracle's `MySql.EntityFrameworkCore` (`UseMySQL`, capital SQL) because Pomelo
  has no EF Core 10 build.
- `Storage:Provider` — `FileSystem` (default) | `S3`/`minio` | `Azure`. S3 and Azure now
  sign requests properly (SigV4 / Shared Key); they previously sent placeholder headers.
- `Cache:Provider` — `Memory` (default) | `Redis`, behind `ICacheService`.
- `Database:AutoSeed` — set false to skip sample data.

Schema is created with `EnsureCreatedAsync()` — **there are no EF migrations**. Changing an
entity means deleting `FastRide.Api/FastRide.db`. Program.cs probes the schema at startup
and fails with an explicit instruction rather than a confusing "no such column" later.

## Conventions & hazards

- **Enums cross the wire as strings** both ways (`JsonStringEnumConverter` on API and every
  client); numeric input is still accepted. Enums are 1-based except `DriverStatus.Offline = 0`
  and `EWalletChannel.Unspecified = 0`. `PaymentMethod` gained `Qris = 5` and
  `VirtualAccount = 6`; anything indexing the enum by range (the simulator once used
  `Next(1, 5)`) silently misses them.
- **Never commit live gateway keys.** CI fails on `Mid-server-`, `Mid-client-`, and
  `xnd_production_`. Sandbox placeholders in `appsettings.json` are fine.
- **Authorization is real.** Every group calls `RequireAuthorization()`; only `/api/health`,
  `/api/auth/*`, and `GET /api/reviews/user/{id}` are anonymous. Routes with `{userId}` also
  check ownership via `ClaimsPrincipal.CanAccess()`.
- **Logout actually works** — it bumps `User.SecurityStamp`, and `SecurityStampMiddleware`
  rejects tokens carrying an older stamp. Same for password change/reset and account
  suspension.
- **Two EF translation traps** (both hit in practice, both documented in `docs/DATABASE.md`):
  SQLite cannot do `APPLY`, so never combine a collection projection and a correlated
  subquery in one `Select`; and `GroupBy` cannot project straight into a record
  constructor — use an anonymous type, then map.
- Passwords use BCrypt (work factor 12) in both API and seeder; all seeded accounts are
  `Password123` (`admin@fastride.com`, `budi.santoso@email.com`, `andi.santoso@drive.com`).
  Those two demo users are now created explicitly rather than by chance.
- On register, `PhotoUrl` is an inline `data:image/svg+xml;base64,...` initials avatar.
  `IStorageProvider.ResolveFileName()` returns null for data URIs, so deleting a generated
  avatar no longer tries to delete a file that never existed.
- **Design system**: "dispatch console" — Barlow Condensed / IBM Plex Sans / IBM Plex Mono,
  with four signal colours (amber=waiting, jade=go/done, vermilion=stopped, blue=in transit)
  used identically across AdminWeb and both mobile apps. See `docs/DASHBOARD.md`.
- `.claude/skills/frontend-design/` is a local skill for UI work. `Design.md` at the repo
  root is an **older copy** of that same skill file, not project design documentation.

## Verifying a change

```bash
dotnet test FastRide.Tests               # first line of defence, ~65s

dotnet run --project FastRide.Api        # delete FastRide.db first if the schema changed
dotnet run --project FastRide.Simulator -- --url http://localhost:5000 --duration 30
```

The simulator exercises the same flows end to end against a live host — registration,
document verification, GPS, quoting, booking, the full trip lifecycle, cancellation, payment,
and reviews. **0 failed requests** is the bar.

### Writing integration tests — two traps that have already bitten

Both are documented at length in `docs/TESTING.md`; the short version:

- **Use `builder.UseSetting(...)`, not `ConfigureAppConfiguration`.** Minimal hosting reads
  configuration before service registration, so `ConfigureAppConfiguration` values arrive too
  late and every test silently runs against the developer's own `FastRide.db`.
  `FastRideApiFactory.AssertIsIsolated()` exists to make that failure loud.
- **`UseHttpsRedirection` strips the Authorization header** under the test client, which
  follows redirects. Hence the `ApiSettings:UseHttpsRedirection` switch (default `true`),
  which is also what you want behind a TLS-terminating proxy.
