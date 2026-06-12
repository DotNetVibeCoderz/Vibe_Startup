# 🚖 FastRide — Ride-Hailing Platform Solution

> **Production-grade multi-project .NET ride-hailing platform** with Rider App, Driver App, Admin Dashboard, and Parallel Order Simulator.

*(This is the English version. For Bahasa Indonesia, see [README-ID.md](README-ID.md))*

---

## 🎯 Overview

FastRide is a modern, full-featured ride-hailing platform built with the .NET ecosystem.

| Feature | Description |
|---------|-------------|
| 🚗 **Multi-Vehicle** | Economy, Comfort, Premium, Bike, Electric |
| 💰 **Dynamic Fare** | Base + distance pricing with surge |
| 🛑 **Multi-Stop** | Waypoints & multiple stops |
| 💳 **Payments** | Cash, E-Wallet, Credit Card, Transfer |
| ⭐ **Ratings** | Two-way rider ↔ driver |
| 🎫 **Promos** | Percentage or fixed discount codes |
| 📊 **Analytics** | Realtime charts & export |
| 🎮 **Simulator** | Parallel load testing |

---

## 🏛️ Architecture

```
FastRide/
├── FastRide.Shared/        # Shared Models, DTOs, Enums
├── FastRide.Data/          # EF Core DbContext + Seeder
├── FastRide.Api/           # Minimal API (.NET 10)
├── FastRide.AdminWeb/      # Blazor Server Dashboard
├── FastRide.RiderApp/      # MAUI Blazor (iOS/Android/Win)
├── FastRide.DriverApp/     # MAUI Blazor (iOS/Android/Win)
├── FastRide.Simulator/     # Spectre.Console Simulation
└── docs/                   # Full documentation
```

---

## 🚀 Quick Start

```bash
dotnet restore
dotnet run --project FastRide.Api      # API (auto-seeds data!)
dotnet run --project FastRide.AdminWeb  # Dashboard
dotnet run --project FastRide.Simulator # 30s parallel sim
```

---

## 📊 Sample Data

| Entity | Count |
|--------|-------|
| Riders | 50 |
| Drivers | 30 |
| Orders | 200+ |
| Payments | 110+ |
| Reviews | 140+ |
| Promos | 8 |

---

## 📘 Docs

| Doc | Topic |
|-----|-------|
| [API.md](API.md) | REST endpoints |
| [AUTH.md](AUTH.md) | Auth & authorization |
| [DATABASE.md](DATABASE.md) | DB schema & seeding |
| [SIMULATOR.md](SIMULATOR.md) | Simulator guide |
| [DASHBOARD.md](DASHBOARD.md) | Admin dashboard |
| [ARCHITECTURE.md](ARCHITECTURE.md) | System architecture |
| [DEPLOYMENT.md](DEPLOYMENT.md) | Deployment guide |

---

## 👥 Demo Accounts

| Role | Email | Password |
|------|-------|----------|
| Admin | `admin@fastride.com` | `Password123` |
| Rider | `budi.santoso@email.com` | `Password123` |
| Driver | `andi.santoso@drive.com` | `Password123` |

---

Built with ❤️ by **Jacky the Code Bender** at [Gravicode Studios](https://studios.gravicode.com)

> 💡 *Buy me a coffee!* → https://studios.gravicode.com/products/budax
