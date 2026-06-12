# 🏛️ Architecture — FastRide

> Deep dive into the FastRide system architecture, design patterns, and technical decisions.

---

## 🎯 Architectural Goals

1. **Separation of Concerns** — Clear boundaries between layers
2. **Testability** — Each component testable in isolation
3. **Scalability** — Horizontal scaling ready
4. **Maintainability** — Clean code, consistent patterns
5. **Performance** — Efficient algorithms, minimal overhead

---

## 🏗️ Solution Structure

```
FastRide.sln
├── FastRide.Shared/           # 📦 Shared Kernel
│   ├── Models/                #   Domain entities
│   │   ├── User.cs
│   │   ├── Order.cs
│   │   ├── Payment.cs
│   │   ├── Common.cs
│   │   └── Enums.cs
│   └── DTOs/
│       └── DTOs.cs            #   Data Transfer Objects
│
├── FastRide.Data/             # 🗄️ Data Access Layer
│   ├── FastRideDbContext.cs   #   EF Core context
│   └── SampleDataSeeder.cs   #   Development seed data
│
├── FastRide.Api/              # 🚀 Application Layer (Minimal API)
│   ├── Program.cs             #   Endpoints + DI configuration
│   └── appsettings.json       #   Configuration
│
├── FastRide.AdminWeb/         # 🖥️ Presentation (Blazor Server)
│   ├── Components/
│   │   ├── App.razor
│   │   ├── Routes.razor
│   │   ├── Layout/
│   │   │   └── MainLayout.razor
│   │   └── Pages/
│   │       ├── Dashboard.razor
│   │       ├── Orders.razor
│   │       ├── Drivers.razor
│   │       ├── Riders.razor
│   │       ├── Payments.razor
│   │       ├── Promos.razor
│   │       └── Analytics.razor
│   └── wwwroot/
│       └── css/app.css
│
├── FastRide.RiderApp/         # 📱 Mobile (MAUI Blazor)
│   ├── MauiProgram.cs
│   ├── Pages/
│   │   ├── Home.razor
│   │   ├── BookRide.razor
│   │   ├── MyTrips.razor
│   │   └── Profile.razor
│   └── wwwroot/
│
├── FastRide.DriverApp/        # 📱 Mobile (MAUI Blazor)
│   ├── MauiProgram.cs
│   ├── Pages/
│   │   ├── Home.razor
│   │   ├── Earnings.razor
│   │   └── DriverProfile.razor
│   └── wwwroot/
│
├── FastRide.Simulator/        # 🎮 Simulation (Console)
│   └── Program.cs
│
└── docs/                      # 📘 Documentation
    ├── API.md
    ├── AUTH.md
    ├── DATABASE.md
    ├── SIMULATOR.md
    ├── DASHBOARD.md
    ├── ARCHITECTURE.md
    └── DEPLOYMENT.md
```

---

## 🔗 Project Dependencies

```
FastRide.Shared ◄─── FastRide.Data
     ▲                    ▲
     │                    │
     ├────────────────────┤
     │                    │
FastRide.Api        FastRide.AdminWeb
     │
     ├──── FastRide.RiderApp
     ├──── FastRide.DriverApp
     └──── FastRide.Simulator
```

---

## 🧱 Layer Details

### 1. Shared Layer (`FastRide.Shared`)

**Purpose:** Shared kernel containing domain models, DTOs, and enums used by all projects.

**Key Decisions:**
- **C# `record` types for DTOs** — Immutable, value-based equality
- **No external dependencies** — Pure .NET class library
- **Domain enums** — Type-safe status and category definitions

**Models:**
| Model | Type | Purpose |
|-------|------|---------|
| `User` | Entity | Core user with Rider/Driver role |
| `DriverProfile` | Entity | Extended driver information |
| `Order` | Aggregate Root | Complete ride order lifecycle |
| `TripStop` | Value Object | Intermediate stop in multi-stop trip |
| `Payment` | Entity | Payment transaction record |
| `Promo` | Entity | Discount code configuration |
| `Notification` | Entity | User notification record |
| `Review` | Entity | Post-trip rating and review |
| `FareConfig` | Entity | Pricing configuration per vehicle |

**Enums (10):**
`UserRole`, `DriverStatus`, `OrderStatus`, `VehicleCategory`, `PaymentMethod`, `PaymentStatus`, `PromoType`, `NotificationType`, `TripStopType`

---

### 2. Data Layer (`FastRide.Data`)

**Purpose:** Database access via Entity Framework Core.

**Key Decisions:**
- **EF Core Fluent API** — Full control over schema
- **SQLite default** — Zero-config development database
- **Multi-provider ready** — Switch to SQL Server/MySQL/PostgreSQL via connection string
- **Seed data** — Rich development data for demos

**DbContext Configuration:**
- 9 DbSets for all entities
- Composite indexes on frequently queried columns
- Relationship configurations with cascade behaviors
- Decimal precision (18,2) for monetary values

---

### 3. API Layer (`FastRide.Api`)

**Purpose:** REST/GRPC API using .NET Minimal API.

**Key Decisions:**
- **Minimal API** — Less ceremony, better performance
- **Endpoint grouping** — Organized by domain (Auth, Orders, Drivers, etc.)
- **CORS-enabled** — Allow Blazor and MAUI clients
- **Auto-migration** — Database created and seeded on startup
- **OpenAPI** — Swagger documentation in development

**Endpoints:**
| Group | Count | Status |
|-------|-------|--------|
| Health | 1 | ✅ Complete |
| Auth | 2 | 🟡 Scaffold |
| Riders | 1 | ✅ Complete |
| Drivers | 1 | ✅ Complete |
| Orders | 2 | 🟡 Partial |
| Payments | 1 | 🟡 Scaffold |
| Dashboard | 1 | ✅ Complete |

---

### 4. Admin Web (`FastRide.AdminWeb`)

**Purpose:** Blazor Server admin dashboard.

**Key Decisions:**
- **Blazor Server** — Real-time UI without JavaScript SPA complexity
- **Bootstrap 5.3** — Responsive, familiar component library
- **Custom dark theme** — Brand-consistent styling
- **Chart.js** — Client-side charts via CDN

**Design Patterns:**
- `MainLayout` — Shared layout with sidebar navigation
- `@page` routing — Blazor-native page navigation
- Component composition — Reusable card, table patterns

---

### 5. Mobile Apps (`FastRide.RiderApp`, `FastRide.DriverApp`)

**Purpose:** MAUI Blazor Hybrid apps for iOS, Android, and Windows.

**Key Decisions:**
- **MAUI Blazor Hybrid** — Shared Blazor components across platforms
- **Single Project** — One project per app for all platforms
- **Bootstrap CDN** — Consistent styling with admin dashboard
- **Mobile-optimized UI** — Touch-friendly, responsive design

**Target Frameworks:**
- `net10.0-android`
- `net10.0-ios`
- `net10.0-maccatalyst`
- `net10.0-windows10.0.19041.0`

---

### 6. Simulator (`FastRide.Simulator`)

**Purpose:** Console-based parallel simulation for load testing.

**Key Decisions:**
- **Spectre.Console** — Rich console UI with live tables
- **Task-based parallelism** — `Task.Run` for concurrent simulation
- **Thread-safe collections** — `lock` for shared order list
- **Configurable parameters** — Rider count, driver count, duration

---

## 🔄 Data Flow

### Order Lifecycle

```
Rider creates order
       │
       ▼
  [Requested] ──► Driver searches for orders
       │                │
       │                ▼
       │           [Accepted] ──► Driver heads to pickup
       │                │
       │                ▼
       │           [DriverArrived] ──► Rider enters vehicle
       │                │
       │                ▼
       │           [Started] ──► Trip in progress
       │                │
       │                ▼
       │           [Completed] ──► Payment processed
       │                │
       ▼                ▼
  [Cancelled]      [Review submitted]
  [Expired]
```

---

## 🎨 Design Patterns Used

| Pattern | Usage |
|---------|-------|
| **Repository** (via EF Core) | Data access abstraction |
| **DTO** | API request/response contracts |
| **Dependency Injection** | Built-in .NET DI container |
| **Options Pattern** | Configuration binding |
| **Minimal API** | Lightweight HTTP endpoints |
| **Aggregate Root** | Order as consistency boundary |
| **Seeder Pattern** | Development data initialization |

---

## 📊 Performance Considerations

1. **AsNoTracking()** — Read-only queries for dashboard
2. **Pagination** — Limit result sets (Take 50 for orders)
3. **Indexed queries** — Status and date columns indexed
4. **Connection pooling** — EF Core default pooling
5. **Lazy loading disabled** — Explicit Include() for relationships
6. **CORS optimization** — Restrict origins in production

---

## 🔮 Future Architecture

- [ ] **Message Queue** — RabbitMQ/Kafka for order events
- [ ] **CQRS** — Separate read/write models for analytics
- [ ] **Event Sourcing** — Full audit trail of order state changes
- [ ] **Microservices** — Split by bounded context
- [ ] **API Gateway** — YARP or Ocelot for routing
- [ ] **gRPC** — High-performance service-to-service communication
- [ ] **Docker Compose** — Containerized development environment
