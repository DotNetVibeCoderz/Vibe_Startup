# 🚖 FastRide — Ride-Hailing Platform Solution

> **Production-grade multi-project .NET ride-hailing platform** with Rider App, Driver App, Admin Dashboard, and Parallel Order Simulator.

[![.NET](https://img.shields.io/badge/.NET-10.0-512BD4?style=flat&logo=dotnet)](https://dotnet.microsoft.com)
[![C#](https://img.shields.io/badge/C%23-12.0-239120?style=flat&logo=csharp)](https://learn.microsoft.com/en-us/dotnet/csharp/)
[![License](https://img.shields.io/badge/license-MIT-green.svg)](LICENSE)
[![Status](https://img.shields.io/badge/status-ready%20for%20development-brightgreen.svg)]()

---

## 📖 README Bahasa

- [🇮🇩 **Bahasa Indonesia**](docs/README-ID.md) — Dokumentasi lengkap dalam Bahasa Indonesia
- [🇬🇧 **English**](docs/README-EN.md) — Full documentation in English

---

## 🎯 Overview

FastRide is a modern, full-featured ride-hailing platform built with the .NET ecosystem. It includes everything you need to run a ride-hailing service: rider mobile app, driver mobile app, admin web dashboard, and a powerful simulator for load testing and demos.

### Key Features

| Feature | Description |
|---------|-------------|
| 🚗 **Multi-Vehicle Categories** | Economy, Comfort, Premium, Bike, Electric |
| 💰 **Dynamic Fare Calculation** | Base fare + distance-based pricing with surge multiplier |
| 🛑 **Multi-Stop Trips** | Support for waypoints and multiple stops |
| 💳 **Multiple Payment Methods** | Cash, E-Wallet, Credit Card, Bank Transfer |
| ⭐ **Rating & Review System** | Two-way rating (rider ↔ driver) |
| 🎫 **Promo & Discount Engine** | Percentage or fixed-amount promos with usage limits |
| 📊 **Real-time Analytics** | Dashboard with charts, filters, and export capabilities |
| 🔐 **Auth Ready** | JWT-based authentication scaffold |
| 🎮 **Parallel Simulator** | Spectre.Console-based live simulation for load testing |

---

## 🏛️ Architecture

```
FastRide/
├── FastRide.Shared/        # 📦 Shared Models, DTOs, Enums
├── FastRide.Data/          # 🗄️ EF Core DbContext + Sample Data Seeder
├── FastRide.Api/           # 🚀 Minimal API (.NET 10 REST/GRPC)
├── FastRide.AdminWeb/      # 🖥️ Blazor Server Admin Dashboard
├── FastRide.RiderApp/      # 📱 MAUI Blazor Hybrid (iOS/Android/Windows)
├── FastRide.DriverApp/     # 📱 MAUI Blazor Hybrid (iOS/Android/Windows)
├── FastRide.Simulator/     # 🎮 Console App (Spectre.Console live simulation)
├── FastRide.sln            # 📋 Solution file
└── docs/                   # 📘 Full documentation
```

### Layer Architecture

```
┌──────────────────────────────────────────────────────┐
│                PRESENTATION LAYER                     │
│  Rider App (MAUI)  │  Driver App (MAUI)  │  Admin    │
├──────────────────────────────────────────────────────┤
│                APPLICATION LAYER                      │
│     Minimal API (REST/GRPC)    │   Auth Service      │
│     Order Service              │   Payment Service   │
│     Notification Service       │                     │
├──────────────────────────────────────────────────────┤
│                INFRASTRUCTURE LAYER                    │
│  EF Core (SQLite/SQL Server)   │   File/Blob Storage │
│  Redis/Memory Cache            │   Identity + JWT    │
├──────────────────────────────────────────────────────┤
│                SIMULATION LAYER                       │
│  Console App — Parallel Rider & Driver Simulation    │
└──────────────────────────────────────────────────────┘
```

---

## 🚀 Quick Start

### Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- [MAUI Workload](https://learn.microsoft.com/en-us/dotnet/maui/get-started/installation) (for mobile apps)

### 1. Clone & Restore

```bash
git clone <repository-url>
cd FastRide
dotnet restore
```

### 2. Run the API

```bash
dotnet run --project FastRide.Api
# API runs on https://localhost:5001
# Sample data auto-seeded on first run!
```

### 3. Run Admin Dashboard

```bash
dotnet run --project FastRide.AdminWeb
# Dashboard runs on https://localhost:5002
```

### 4. Run the Simulator

```bash
dotnet run --project FastRide.Simulator
# Watch live 10 riders + 5 drivers simulation for 30 seconds!
```

### 5. Run Mobile Apps (requires MAUI workload)

```bash
dotnet workload install maui
dotnet run --project FastRide.RiderApp
dotnet run --project FastRide.DriverApp
```

---

## 📊 Sample Data

The API automatically seeds the database with rich sample data on first run:

| Entity | Count | Details |
|--------|-------|---------|
| 👤 Riders | **50** | Diverse names, emails, registration dates |
| 🚗 Drivers | **30** | With profiles, vehicles, ratings, earnings |
| 👑 Admin | **1** | `admin@fastride.com` |
| 📋 Orders | **200+** | Mixed statuses, realistic locations & fares |
| 💰 Payments | **110+** | For completed orders |
| ⭐ Reviews | **140+** | Realistic Indonesian reviews |
| 🎫 Promos | **8** | Welcome, weekend, payday, seasonal |
| 🔔 Notifications | **40+** | Welcome + order update notifications |

---

## 🛠️ Tech Stack

| Layer | Technology |
|-------|-----------|
| **API** | .NET 10 Minimal API |
| **Web Admin** | Blazor Server (.NET 10) |
| **Mobile** | MAUI Blazor Hybrid |
| **Database** | EF Core (SQLite default, SQL Server/MySQL/PostgreSQL ready) |
| **Auth** | Identity + JWT scaffold |
| **Simulator** | Spectre.Console 0.49 |
| **Charts** | Chart.js 4.4 |
| **CSS** | Bootstrap 5.3 + Custom Dark Theme |

---

## 📘 Documentation

Full documentation available in the [`docs/`](docs/) folder:

| Document | Description |
|----------|-------------|
| [`API.md`](docs/API.md) | Complete API endpoint reference (REST/GRPC) |
| [`AUTH.md`](docs/AUTH.md) | Authentication & authorization guide |
| [`DATABASE.md`](docs/DATABASE.md) | Database schema, migrations, seeding |
| [`SIMULATOR.md`](docs/SIMULATOR.md) | Simulator usage & configuration |
| [`DASHBOARD.md`](docs/DASHBOARD.md) | Admin dashboard features guide |
| [`ARCHITECTURE.md`](docs/ARCHITECTURE.md) | Deep dive into system architecture |
| [`DEPLOYMENT.md`](docs/DEPLOYMENT.md) | Deployment guide (Docker, Azure, self-host) |
| [`README-ID.md`](docs/README-ID.md) | 🇮🇩 Full README in Bahasa Indonesia |
| [`README-EN.md`](docs/README-EN.md) | 🇬🇧 Full README in English |

---

## 🔧 Configuration

### Database

Edit `FastRide.Api/appsettings.json`:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Data Source=FastRide.db"
  }
}
```

For SQL Server:
```json
"DefaultConnection": "Server=.;Database=FastRide;Trusted_Connection=true;TrustServerCertificate=true"
```

---

## 🎮 Simulator Demo

```
  _____                 _     ____    _       _
 |  ___|   __ _   ___  | |_  |  _ \  (_)   __| |   ___
 | |_     / _` | / __| | __| | |_) | | |  / _` |  / _ \
 |  _|   | (_| | \__ \ | |_  |  _ <  | | | (_| | |  __/
 |_|      \__,_| |___/  \__| |_| \_\ |_|  \__,_|  \___|

▶ Starting simulation...
   Riders: 10 | Drivers: 5 | Duration: 30s

┌──────────────────────────────────────────────────────────┐
│ 📊 Simulation Stats                                     │
│ ⏱️ Time: 12s / 30s                                     │
│ 📊 Total Orders: 47 | 🆕 Requested: 12 | ✅ Completed: 21│
└──────────────────────────────────────────────────────────┘
```

---

## 👥 Demo Accounts

| Role | Email | Password |
|------|-------|----------|
| Admin | `admin@fastride.com` | `Password123` |
| Rider | `budi.santoso@email.com` | `Password123` |
| Driver | `andi.santoso@drive.com` | `Password123` |

---

## 🤝 Contributing

Contributions are welcome! Please see our [Contributing Guide](CONTRIBUTING.md).

---

## 📄 License

MIT License — see [LICENSE](LICENSE) for details.

---

## 👨‍💻 Credits

Built with ❤️ by **Jacky the Code Bender** at [Gravicode Studios](https://studios.gravicode.com)

> 💡 *Kalau berkenan, traktir pulsa ya!* → https://studios.gravicode.com/products/budax
