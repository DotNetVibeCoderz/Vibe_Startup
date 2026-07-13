# 🎮 PCHub - Rental PC & Game Center Management

A comprehensive management application for PC rental / Game Center businesses.

## 🌟 Features

### Admin Web (Blazor Server)
- 📊 **Dashboard Analytics** - Real-time stats, revenue charts, popular games
- 🖥️ **PC Management** - CRUD, status tracking, monitoring
- 👥 **User Management** - Registration, membership tiers, loyalty points
- 💰 **Billing System** - Automatic time-based billing, multiple payment methods
- 📅 **Reservation System** - Online PC booking with duration & game selection
- 👑 **Membership Plans** - Tiered subscription with discounts & bonuses
- 🎉 **Promos & Discounts** - Promo codes, time-limited offers
- 🏆 **Tournaments** - Event management, prize pools, participant tracking
- 🤖 **Koh Dedi AI Chatbot** - Virtual assistant for customer inquiries
- 📄 **Reports** - Financial reports, export to CSV/Excel
- 🎨 **Modern UI** - Neo-brutalism soft design, dark/light theme
- 📘 **Swagger API** - Full API documentation

### Tech Stack
- **.NET 9** Blazor Server
- **Entity Framework Core** (SQLite/SQL Server/PostgreSQL/MySQL)
- **BCrypt.Net** for security
- **ClosedXML + CsvHelper** for data export

## 🚀 Quick Start

```bash
cd PCHubAdmin
dotnet run
```

Open https://localhost:5001

**Demo Login:** `admin` / `Admin123!`

**API Docs:** https://localhost:5001/swagger

## 📁 Project Structure
```
PCHub/
├── src/PCHub.Shared/    # Shared library
├── src/PCHub.Admin/     # Blazor Server Admin ✅
├── src/PCHub.Client/    # WPF Client (Coming Soon)
└── docs/                # Documentation
```

## 🔑 Default Users
| Username | Password | Role |
|----------|----------|------|
| admin | Admin123! | Admin |
| operator1 | Operator123! | Operator |
| budi | Member123! | Member |

## 📦 Sample Data
- 17 Users (1 admin, 1 operator, 15 members)
- 15 Gaming PCs with various specs
- 12 Popular games
- 5 Membership tiers
- 4 Active promos
- 30 Billing history records
- 10 Sample reservations

## 🏗️ Built by
Gravicode Studios - https://studios.gravicode.com

---
Made with ❤️ by Jacky the Code Bender
