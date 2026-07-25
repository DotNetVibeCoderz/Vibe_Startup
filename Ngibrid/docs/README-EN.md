# 🚚 Ngibrid Logistics Management Platform

> A modern logistics platform in the spirit of Tiki, JNE, and Paxel — built with .NET 10, Blazor Server, and Semantic Kernel.

![.NET](https://img.shields.io/badge/.NET-10.0-blueviolet)
![Blazor](https://img.shields.io/badge/Blazor-Server-purple)
![License](https://img.shields.io/badge/license-MIT-green)

🇮🇩 **Versi Bahasa Indonesia:** [../README.md](../README.md)

> The UI, seed data, and the other documents in this folder are written in Indonesian, matching the
> product's target market. This file is the English entry point.

---

## 📖 Table of Contents

1. [Features](#-features)
2. [Prerequisites](#-prerequisites)
3. [Install & Run](#-install--run)
4. [Demo Accounts](#-demo-accounts)
5. [Page Map](#-page-map)
6. [Configuration](#️-configuration)
7. [City Master Data](#-city-master-data)
8. [Database](#-database)
9. [Storage](#-storage)
10. [REST API](#-rest-api)
11. [AI Chat Bot (Mas Supri)](#-ai-chat-bot-mas-supri)
12. [Background Simulators](#-background-simulators)
13. [Sample Data](#-sample-data)
14. [Tech Stack](#-tech-stack)
15. [Further Documentation](#-further-documentation)

---

## 🚀 Features

### 📦 Core Logistics
- **Order management** — order creation, daily tracking numbers, status history, printable shipping
  labels with **QR code + Code128 barcode**. Sender and recipient addresses are picked from
  cascading **province → city** dropdowns backed by the city master data (see below).
- **Shipment tracking** — live GPS over SignalR, Leaflet map, automatic status transitions,
  multi-channel notifications.
- **Delivery scheduling** — courier assignment, **route optimization** (nearest-neighbour + 2-opt),
  ETA and distance-saving estimates.
- **Pickup requests** — home/office pickup with courier assignment.

### 💳 Payment & Finance
- **Multi-payment** — e-wallet, bank transfer, COD, credit card. A **Bayar** (pay) button on the
  Orders page opens a bill (method → channel) and shows the payment instructions; settlement is
  verified by staff on the Payments page (`/payment`, Admin/Manager only).
- **Invoicing** — printable HTML invoices, e-receipts, financial summaries.
- **Insurance** — premium derived from declared value, claim submission and review.

### 🏭 Warehouse & Inventory
- Multi-warehouse with capacity, storage locations (rack/zone), **RFID and barcode**.
- Stock in/out movements with batch numbers and expiry dates.
- **Packaging optimization** — box size recommendation, volumetric weight (L×W×H / 6000).
- **IoT sensors** — temperature and humidity monitoring with cold-chain alerts.

### 👥 Customer, Courier & Support
- **Customer portal** — tracking, transaction history, a profile page with **avatar upload**
  (JPG/PNG/GIF/WebP, max 5 MB, stored through the active `Storage` provider), and a
  **loyalty program** (Bronze → Platinum, points redeemable for discounts).
- **Courier app** — daily task list, optimized route, field status updates, customer communication.
- **Customer support** — chat bot, SignalR live chat, complaint tickets with priority and SLA.

### 📊 Analytics & Dashboard
- **Business analytics** — shipment volume, revenue, SLA compliance, courier performance.
- **Operational dashboard** — real-time snapshot of active orders, online couriers, warehouses, alerts.
- **Trend analysis** — demand forecasting (Holt exponential smoothing with weekly seasonality),
  **peak-season detection**, cost-optimization insights.
- Every chart is drawn with **D3.js** (line/area, donut, bar, forecast band, sparkline) and follows
  the light/dark theme.

### 🔌 Integrations
- **Marketplaces** — Tokopedia and Shopee order sync, status push back to the marketplace.
- **ERP / CRM** — generic connectors with a per-integration sync log.
- **Third-party logistics & cross-border** — partner rate comparison and package handover.
- **IoT** — smart lockers (compartments, PIN, automatic expiry), temperature sensors, RFID.

### 🔒 Security & Compliance
- Login, register, logout, profile, **password reset** (token required), password change.
- **Role-based access** — Admin, Manager, WarehouseStaff, Courier, Customer, enforced per endpoint
  and per page.
- **Audit trail** — action, entity, old/new values, IP address, user agent.
- **Regulatory compliance** — tax records (VAT/withholding), customs declarations with a
  *de minimis* rule, export/import documents.

### 🤖 AI
- **Mas Supri chat bot** — multi-model (OpenAI, Anthropic, Gemini, Ollama) with
  **function calling on all four providers**.
- **Route optimization** — 2-opt heuristic for the fastest/cheapest route.
- **Dynamic pricing** — distance zones, peak hours, weekends, and current demand.
- **Green logistics** — per-shipment carbon emission, eco-delivery option, carbon offset pricing.

---

## 📋 Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
- A database (pick one): **SQLite** (default, nothing to install), SQL Server, MySQL 8.0+, PostgreSQL 15+
- Optional for the chat bot: an OpenAI / Anthropic / Gemini API key, or a local [Ollama](https://ollama.com)
- Optional for web search: a [Tavily](https://tavily.com) API key

The app runs fully **without any API key** — AI features simply reply with a message pointing the
user to the Settings page.

---

## 🔧 Install & Run

```bash
git clone https://github.com/your-org/ngibrid.git
cd ngibrid

dotnet restore
dotnet build          # 0 errors, 0 warnings
dotnet run            # or: dotnet watch run (hot reload)
```

Addresses (see `Properties/launchSettings.json`):

| Endpoint | URL |
|---|---|
| Application | http://localhost:5182 · https://localhost:7061 |
| Swagger UI | http://localhost:5182/api/docs *(Development only)* |
| Health check | http://localhost:5182/api/v1/health |

Release build:

```bash
dotnet publish -c Release
```

> **Schema note.** The schema is created with `EnsureCreatedAsync()`, not migrations.
> If you change an entity, delete `Data/ngibrid.db` (or the target database) so the new schema is built.

---

## 👤 Demo Accounts

The seeder populates the database on first run.

| Email | Password | Role |
|---|---|---|
| admin@ngibrid.com | `Admin123!` | Admin |
| manager@ngibrid.com | `Manager123!` | Manager |
| staff@ngibrid.com | `Staff123!` | Warehouse staff |
| courier1@ngibrid.com … courier3@ | `Courier123!` | Courier |
| customer1@ngibrid.com … customer5@ | `Customer123!` | Customer |

The login page has shortcut buttons that fill in the demo credentials.

---

## 🗺 Page Map

| Route | Page | Access |
|---|---|---|
| `/` · `/dashboard` | Operational dashboard with D3 charts | everyone |
| `/orders` | Order list & creation, QR/barcode label printing | signed in |
| `/tracking` · `/tracking/{no}` | Shipment tracking + **live map** (marker moves during GPS simulation) | public |
| `/pickup` | Pickup requests | signed in |
| `/payment` | Payments, invoices, insurance claims | signed in |
| `/warehouse` | Warehouses, inventory, IoT sensors, **network map** | staff |
| `/courier` | Courier tasks, **fleet map** + optimised route map | courier |
| `/analytics` | Demand forecast, trends, cost insights, emissions | management |
| `/integrations` | Marketplaces, ERP/CRM, 3PL partners | admin/manager |
| `/compliance` | Tax, customs, export/import documents | admin/manager |
| `/locker` | Smart lockers & compartments + **location map** | staff |
| `/notifications` | Notification centre | signed in |
| `/support` | Complaint tickets & live chat | signed in |
| `/chat` | Mas Supri chat bot | signed in |
| `/profile` | Profile, loyalty, change password | signed in |
| `/settings` | Every application setting | admin/manager |
| `/login` · `/register` · `/forgot-password` · `/reset-password` | Authentication | public |
| `/access-denied` | "Insufficient permissions" page | public |

---

## ⚙️ Configuration

Everything lives in `appsettings.json` **and can be changed from inside the app** on the
**Settings** page (`/settings`). Changes are written back to the file and the configuration is
reloaded, so they take effect without a restart — except for switching database provider.

```json
{
  "Database":  { "Provider": "SQLite", "ConnectionStrings": { "…": "…" } },
  "Storage":   { "Provider": "FileSystem", "BasePath": "wwwroot/uploads", "MaxFileSizeMb": 25 },
  "ChatBot":   { "DefaultModel": "OpenAI", "Temperature": 0.7, "Models": { "…": {} } },
  "AI":        { "DynamicPricing": { "BaseFare": 9000, "RatePerKm": 22 } },
  "GPS":       { "Simulator": { "Enabled": true, "UpdateIntervalMs": 5000 } },
  "IoT":       { "Simulator": { "Enabled": true }, "LockerSimulator": { "Enabled": true } },
  "Loyalty":   { "PointsPerRupiah": 0.0001, "RupiahPerPoint": 100 },
  "Compliance":{ "Customs": { "DutyRate": 0.075, "ImportVatRate": 0.11 } }
}
```

Every key and its default is listed in **[CONFIGURATION.md](CONFIGURATION.md)**.

### Tariff model

First kilogram = `BaseFare + distance × RatePerKm`; each additional kilogram costs 60% of that,
then multiplied by the service tier (ECO/REG/EXP/SAMEDAY), peak-hour, weekend, and demand factors.
Distance is a Haversine calculation between the two cities' master-data coordinates, times a 1.3
road factor overland (≤600 km) or 1.12 for legs that are effectively flown or shipped.

---

## 🗺 City Master Data

The `Cities` table holds **all 514 Indonesian kabupaten/kota across 38 provinces** — `Id`,
`Country`, `Province`, `Name`, `Type` (KOTA/KABUPATEN), `SeatName`, `Latitude`, `Longitude`.
Coordinates are the **administrative seat** of each area rather than its geometric centroid,
because that is where a parcel is actually consigned.

Every distance, tariff, emission figure, map pin, and route plan resolves against this table, so any
route in Indonesia is computed precisely — not just a handful of big cities.

- The seed list lives in `Data/IndonesiaCities.cs` and is inserted only when the `Cities` table is
  empty; rows added or corrected in the database win over it.
- `Name` is the bare name and `Type` separates kota from kabupaten. **26 names are carried by both**
  (Bandung, Bogor, Cirebon, Tasikmalaya, Solok, Sorong, …) and the two can be far apart — which is
  why orders store the province as well and pass it into every price calculation.
- Lookup is forgiving: `Kab. Bandung`, `KABUPATEN BANDUNG`, and the seat name `Soreang` all resolve.
  A bare `Bandung` is read as **Kota** Bandung.

```bash
curl "http://localhost:5182/api/v1/provinces"
curl "http://localhost:5182/api/v1/cities?province=Papua%20Tengah"
curl "http://localhost:5182/api/v1/cities/distance?from=Kota%20Bandung&to=Kota%20Surabaya"
```

---

## 🗄 Database

Defaults to **SQLite** (`Data/ngibrid.db`, created automatically). Switch it in `/settings` or
`appsettings.json`:

```json
"Database": {
  "Provider": "SQLServer",
  "ConnectionStrings": {
    "SQLite":    "Data Source=Data/ngibrid.db",
    "SQLServer": "Server=.;Database=NgibridDb;Trusted_Connection=true;TrustServerCertificate=true;",
    "MySQL":     "Server=localhost;Database=NgibridDb;User=root;Password=root;",
    "Postgre":   "Host=localhost;Database=NgibridDb;Username=postgres;Password=postgres;"
  }
}
```

Changing the database provider requires an **application restart**.

---

## 📁 Storage

`Storage:Provider` supports `FileSystem`, `AzureBlob`, `S3`, and `MinIO`, used for chat attachments,
proof-of-delivery photos, and compliance documents. Extension and size validation run before any
bytes are read.

---

## 📡 REST API

Minimal API under `/api/v1`, Swagger UI at `/api/docs`. Authentication uses the Identity cookie
issued by `/api/auth/login`.

```bash
# sign in and keep the cookie
curl -c cookie.txt -X POST http://localhost:5182/api/auth/login \
  -H "Content-Type: application/json" \
  -d '{"email":"admin@ngibrid.com","password":"Admin123!"}'

# track a shipment (public)
curl "http://localhost:5182/api/v1/orders/track/NGB2607250001ABCD"

# compare tariffs across services
# provinces & cities (public)
curl "http://localhost:5182/api/v1/provinces"
curl "http://localhost:5182/api/v1/cities?province=Bali"

# compare every service tier (province is optional but makes it precise)
curl "http://localhost:5182/api/v1/pricing/compare?origin=Kota%20Bandung&originProvince=Jawa%20Barat&dest=Kota%20Surabaya&destProvince=Jawa%20Timur&weight=3"

# 14-day demand forecast
curl -b cookie.txt "http://localhost:5182/api/v1/analytics/forecast?days=14"
```

Full endpoint reference with access levels: **[API.md](API.md)** (Indonesian).

---

## 🤖 AI Chat Bot (Mas Supri)

A Semantic Kernel–based assistant on `/chat`.

- **Four models**, selectable per session: OpenAI, Anthropic, Gemini, Ollama.
  Semantic Kernel has no official connector for Anthropic or Gemini, so Ngibrid translates
  `KernelFunction` metadata into each API's tool schema — meaning **all four models expose the same
  set of functions**.
- **Multi-session** — create, select, reset, and delete sessions; titles are generated from the first
  message.
- **Attachments** — images are sent as image content (the model genuinely sees them); documents are
  uploaded and their link is read back through the `read_file_from_url` function.
- **Kernel functions** — track order, list my orders, quote shipping, warehouse info, courier
  availability, order statistics, demand forecast, date/time, math evaluation, **web search
  (Tavily)**, URL scraping, read file from URL, volume & emission calculation, 3PL partner options,
  FAQ, ticket creation, loyalty balance, smart locker lookup, and notifications.
- **Rich markdown** — tables, code blocks, images, and media links (YouTube/mp4/mp3) become embedded
  players. Raw HTML is disabled for safety.

Details: **[CHATBOT.md](CHATBOT.md)** (Indonesian).

---

## 🛰 Background Simulators

Three `BackgroundService` instances run on their own threads and can each be switched off in config:

| Simulator | What it does | Default interval |
|---|---|---|
| `GpsSimulatorService` | Moves couriers along their route, broadcasts positions over SignalR | 5 s |
| `IotSimulatorService` | Warehouse temperature/humidity sensors, cold-chain alerts to staff | 10 s |
| `SmartLockerSimulatorService` | Locker heartbeats, battery drain, compartment expiry | 30 s |

---

## 📊 Sample Data

`DataSeeder` creates: 10 users across 5 roles, 4 warehouses, 3 couriers with vehicles, a service
catalogue, **~375 orders spread across the last 120 days** (weekday shape plus two campaign spikes,
so the volume, SLA, monthly-trend and forecast charts have a real series) with full status history,
payments, invoices, insurance
claims, inventory plus stock movements, marketplace/ERP/CRM integrations, 3PL partners, smart
lockers, support tickets, pickup requests, loyalty transactions, tax records, and customs
declarations.

> The seeder stops entirely once any user row exists — delete `Data/ngibrid.db` to reseed.

---

## 📚 Tech Stack

| Area | Technology |
|---|---|
| Framework | .NET 10, ASP.NET Core |
| UI | Blazor Server (Interactive Server), CSS custom properties, light/dark theme |
| Charts & maps | D3.js v7, Leaflet — both vendored locally, no CDN |
| Data | EF Core 9 (SQLite / SQL Server / MySQL / PostgreSQL) |
| Real-time | SignalR (tracking, chat, notification, courier hubs) |
| AI | Semantic Kernel plus HTTP connectors for Anthropic & Gemini |
| Storage | FileSystem, Azure Blob, AWS S3, MinIO |
| Auth | ASP.NET Core Identity (`long` keys), cookies, policies |
| API | Minimal API + Swagger |
| Misc | Markdig, QRCoder, a hand-written Code128 encoder |

---

## 📖 Further Documentation

| Document | Contents |
|---|---|
| [ARCHITECTURE.md](ARCHITECTURE.md) | Layers, folder structure, design decisions, data flows |
| [API.md](API.md) | Complete REST endpoint reference with access levels |
| [CHATBOT.md](CHATBOT.md) | Models, kernel functions, attachments, chat bot security |
| [CONFIGURATION.md](CONFIGURATION.md) | Every `appsettings.json` key and production notes |
| [../README.md](../README.md) | Bahasa Indonesia version |

---

## 📄 License

MIT License

---

**Built with ❤️ by the Ngibrid Team | Gravicode Studios**
