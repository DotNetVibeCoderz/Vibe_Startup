# 🏗️ Arsitektur Sistem RentalBoil

## Overview

RentalBoil mengadopsi arsitektur **Blazor Server** dengan **Clean Architecture** pattern. Aplikasi berjalan di server .NET, UI dirender ke browser melalui SignalR WebSocket.

```
┌─────────────────────────────────────────────────────────┐
│                    BROWSER (Client)                      │
│  ┌──────────────────────────────────────────────────┐  │
│  │  SignalR WebSocket  ←→  Blazor Server Components │  │
│  └──────────────────────────────────────────────────┘  │
│  Leaflet.js · Bootstrap 5.3 · CSS Claymorphism        │
└─────────────────────────────────────────────────────────┘
                           ↕
┌─────────────────────────────────────────────────────────┐
│                   ASP.NET CORE (.NET 10)                 │
│  ┌──────────┐  ┌──────────┐  ┌──────────────────────┐  │
│  │  Blazor  │  │ Minimal  │  │  SignalR Hubs         │  │
│  │  Server  │  │   API    │  │  (Notif/Chat/GPS)    │  │
│  └──────────┘  └──────────┘  └──────────────────────┘  │
│  ┌──────────────────────────────────────────────────┐  │
│  │              SERVICE LAYER                        │  │
│  │  Vehicle · Booking · Payment · Notification      │  │
│  │  Chat · Review · Promotion · Loyalty              │  │
│  │  GPS Simulator · BotService · Export             │  │
│  └──────────────────────────────────────────────────┘  │
│  ┌──────────────────────────────────────────────────┐  │
│  │              DATA LAYER                           │  │
│  │  EF Core DbContext (SQLite/SQLServer/MySQL/PG)   │  │
│  └──────────────────────────────────────────────────┘  │
└─────────────────────────────────────────────────────────┘
```

---

## 📂 Struktur Project

```
RentalBoil/
├── Api/                          # REST API Endpoints
│   ├── ApiEndpoints.cs           # 30+ API endpoints
│   └── ApiKeyMiddleware.cs       # X-Api-Key auth
│
├── Components/
│   ├── App.razor                 # Root HTML + JS globals
│   ├── Routes.razor              # Blazor Router
│   ├── _Imports.razor            # Global usings
│   ├── Layout/
│   │   └── MainLayout.razor      # Sidebar + Topbar + Footer
│   ├── Pages/
│   │   ├── Home.razor            # Landing page
│   │   ├── Login.razor           # Login
│   │   ├── Register.razor        # Registration
│   │   ├── Profile.razor         # User profile + upload
│   │   ├── Vehicles.razor        # Vehicle search
│   │   ├── VehicleDetail.razor   # Vehicle detail + booking
│   │   ├── VehicleMap.razor      # Single vehicle map
│   │   ├── Faq.razor             # FAQ page
│   │   ├── About.razor           # About page
│   │   ├── Reports.razor         # Reports (Admin/Partner)
│   │   ├── Notifications.razor   # Notification center
│   │   ├── Logout.razor          # Logout handler
│   │   ├── Customer/
│   │   │   ├── Bookings.razor    # Booking management
│   │   │   ├── Payment.razor     # Payment page
│   │   │   └── Review.razor      # Review page
│   │   ├── Partner/
│   │   │   ├── Dashboard.razor   # Partner dashboard
│   │   │   └── Vehicles.razor    # Partner vehicle CRUD
│   │   ├── Admin/
│   │   │   ├── Dashboard.razor   # Admin dashboard
│   │   │   └── Map.razor         # Admin fleet map
│   │   ├── Chat/
│   │   │   └── ChatBot.razor     # AI Chat Bot UI
│   │   └── Gps/
│   │       └── GpsTracking.razor # GPS live tracking
│   └── Shared/
│       └── RedirectToLogin.razor
│
├── Data/
│   ├── AppDbContext.cs           # EF Core DbContext
│   └── DbInitializer.cs         # Seed data
│
├── Hubs/
│   └── AppHubs.cs               # SignalR Hubs
│
├── Models/
│   ├── Enums.cs                 # All enums
│   ├── ApplicationUser.cs       # User entity
│   ├── Vehicle.cs               # Vehicle + Photo + Availability
│   ├── Booking.cs               # Booking + Payment
│   └── OtherModels.cs           # Chat, Notif, Promo, etc.
│
├── Services/
│   ├── VehicleService.cs        # Vehicle CRUD + search
│   ├── BookingService.cs        # Booking management
│   ├── CoreServices.cs          # Notif, Chat, Review, etc.
│   ├── UtilityServices.cs       # Export, Storage, Theme
│   ├── SimulatorAndBot.cs       # GPS Simulator
│   ├── BotService.cs            # AI Chat (4 providers)
│   └── BotKernelFunctions.cs    # 17 kernel functions
│
├── wwwroot/
│   └── app.css                  # Claymorphism CSS
│
├── docs/                        # Documentation
├── Program.cs                   # Entry point
├── appsettings.json             # Configuration
└── RentalBoil.csproj            # Project config
```

---

## 🔄 Data Flow

### Booking Flow

```
Customer                  Server                   Partner
   │                        │                        │
   │  1. Search Vehicle     │                        │
   │───────────────────────→│                        │
   │                        │                        │
   │  2. Select & Book      │                        │
   │───────────────────────→│                        │
   │                        │  3. Notify Partner     │
   │                        │───────────────────────→│
   │                        │                        │
   │  4. Pay (via Payment)  │  5. Partner Confirms   │
   │───────────────────────→│←───────────────────────│
   │                        │                        │
   │  6. Pickup Vehicle     │  7. GPS Tracking Start │
   │───────────────────────→│                        │
   │                        │                        │
   │  8. Active Rental      │  GPS Simulator Loop    │
   │  (GPS tracking)        │  (every 3 sec)         │
   │                        │                        │
   │  9. Return Vehicle     │  10. GPS Stop          │
   │───────────────────────→│                        │
   │                        │                        │
   │  11. Review & Rating   │                        │
   │───────────────────────→│                        │
```

### AI Chat Flow

```
User                BotService              Kernel Functions
 │                      │                        │
 │  1. "Cari Avanza"    │                        │
 │─────────────────────→│                        │
 │                      │  2. Auto-detect        │
 │                      │  search_vehicles_db    │
 │                      │───────────────────────→│
 │                      │                        │
 │                      │  3. Query AppDbContext │
 │                      │←───────────────────────│
 │                      │                        │
 │  4. Hasil pencarian  │                        │
 │←─────────────────────│                        │
```

---

## 🛡️ Security Architecture

```
┌──────────────────────────────────────┐
│         AUTHENTICATION               │
│  ┌─────────────┐  ┌───────────────┐  │
│  │ ASP.NET     │  │  API Key      │  │
│  │ Identity    │  │  Middleware   │  │
│  │ (Cookie)    │  │  (X-Api-Key)  │  │
│  └─────────────┘  └───────────────┘  │
│         ↕                ↕            │
│  ┌─────────────────────────────────┐  │
│  │  Role-Based Authorization       │  │
│  │  Admin | Partner | Customer     │  │
│  └─────────────────────────────────┘  │
└──────────────────────────────────────┘
```

### API Authentication
- Semua endpoint `/api/*` memerlukan header `X-Api-Key`
- Bisa juga via query string `?api_key=...`
- Swagger endpoint dikecualikan
- Constant-time string comparison (anti timing attack)

### Cookie Authentication
- ASP.NET Core Identity dengan cookie
- Role-based authorization: `[Authorize(Roles = "Admin")]`
- Session 7 hari, sliding expiration

---

## 📊 Database Architecture

```
┌──────────┐     ┌──────────┐     ┌──────────┐
│  Users   │────→│ Bookings │←────│ Vehicles │
│ (Identity)│    │          │    │          │
└──────────┘     └──────────┘     └──────────┘
      │               │                 │
      ↓               ↓                 ↓
┌──────────┐     ┌──────────┐     ┌──────────┐
│Notifications│  │ Payments │     │ Reviews  │
└──────────┘     └──────────┘     └──────────┘
      │               │
      ↓               ↓
┌──────────┐     ┌──────────┐
│   Chat   │     │Promotions│
│ Messages │     │          │
└──────────┘     └──────────┘
```

> Detail lengkap di [Database](database.md)
