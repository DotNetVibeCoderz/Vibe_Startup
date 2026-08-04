# 🇬🇧 FastRide — English Summary

A ride-hailing platform on .NET 10: API, operations console, rider app, driver app, and a
traffic simulator.

> The full documentation set is written in Indonesian, matching the product's domain
> language. This page is a summary for English readers; see [`../README.md`](../README.md)
> and the `docs/` folder for the complete text.

---

## Projects

| Project | What it is |
|---------|------------|
| `FastRide.Api` | Minimal API — all business rules, and the only thing that touches the database |
| `FastRide.AdminWeb` | Operations console (Blazor Server) |
| `FastRide.RiderApp` | Rider app (MAUI Blazor Hybrid) |
| `FastRide.DriverApp` | Driver app (MAUI Blazor Hybrid) |
| `FastRide.Simulator` | Rider/driver load simulator (Spectre.Console) |
| `FastRide.Shared` | Models, DTOs, and shared helpers |
| `FastRide.Data` | EF Core DbContext + sample data |

---

## Quick start

```bash
dotnet restore

dotnet run --project FastRide.Api          # https://localhost:5001
dotnet run --project FastRide.AdminWeb     # https://localhost:5002
dotnet run --project FastRide.Simulator -- --duration 60
```

Sample data is seeded on first run.

| Role | Email | Password |
|------|-------|----------|
| Admin | `admin@fastride.com` | `Password123` |
| Rider | `budi.santoso@email.com` | `Password123` |
| Driver | `andi.santoso@drive.com` | `Password123` |

---

## Features

- **Booking** — fare quote before booking, 5 vehicle categories, multi-stop trips, promo
  codes, 4 payment methods
- **Trip lifecycle** — `Requested → Accepted → DriverArrived → Started → Completed`, with
  validated transitions
- **Live tracking** — driver position, distance, and ETA
- **Driver** — online status, nearby order offers, daily earnings, document verification
- **Pricing** — base + distance + time, surge multiplier, minimum fare, cancellation fee
- **Payments** — cash, QRIS, e-wallet, card, virtual account, through Midtrans, Xendit or a
  sandbox provider; the gateway is switched from the admin console without a redeploy
- **Admin console** — live overview, orders, drivers, riders, payments, financial reports,
  fare table, promos, verification queue, user management, CSV export
- **Security** — JWT enforced on every route, ownership checks, logout invalidates tokens,
  rate limiting

---

## Configuration

| Setting | Options | Default |
|---------|---------|---------|
| `Database:Provider` | `SQLite`, `SqlServer`, `PostgreSQL`, `MySQL` | `SQLite` |
| `Storage:Provider` | `FileSystem`, `S3`/`minio`, `Azure` | `FileSystem` |
| `Cache:Provider` | `Memory`, `Redis` | `Memory` |

Override with environment variables (`Database__Provider=PostgreSQL`).

| Service | HTTPS | HTTP |
|---------|-------|------|
| API | 5001 | 5000 |
| Admin console | 5002 | 5003 |

---

## Development notes

- **Tests:** `dotnet test FastRide.Tests` — 248 tests, ~65s, no running API or database
  needed. See [`TESTING.md`](TESTING.md).
- **No EF migrations** — the schema is created with `EnsureCreated`, so changing an entity
  means deleting `FastRide.Api/FastRide.db`. The API stops at startup with a clear message
  if it finds a stale schema.
- **Don't `dotnet build FastRide.sln` without the MAUI workload** — the solution includes
  both mobile apps. Build projects individually for backend work.

---

## Documentation index

| Document | Contents |
|----------|----------|
| [`API.md`](API.md) | Full endpoint reference |
| [`AUTH.md`](AUTH.md) | Authentication, authorization, token revocation |
| [`DATABASE.md`](DATABASE.md) | Schema, indexes, sample data, portability traps |
| [`ARCHITECTURE.md`](ARCHITECTURE.md) | Structure and design decisions |
| [`DASHBOARD.md`](DASHBOARD.md) | Operations console and its design direction |
| [`SIMULATOR.md`](SIMULATOR.md) | Simulator usage |
| [`PAYMENTS.md`](PAYMENTS.md) | QRIS, gateways, callbacks, and configuration |
| [`TESTING.md`](TESTING.md) | Test suite and how to extend it |
| [`CI.md`](CI.md) | CI pipeline |
| [`DEPLOYMENT.md`](DEPLOYMENT.md) | Running outside a developer machine |
| [`../PLAN.md`](../PLAN.md) | Roadmap |
| [`../Progress.md`](../Progress.md) | What has been done, and why |
