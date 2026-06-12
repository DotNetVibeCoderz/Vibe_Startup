# 🗄️ Database Schema — FastRide

> Complete database design, Entity Framework Core configuration, and migration guide.

---

## 📊 Entity Relationship Diagram

```
┌──────────────────┐       ┌──────────────────┐
│      User        │       │  DriverProfile   │
├──────────────────┤       ├──────────────────┤
│ Id (PK)          │──1:1──│ Id (PK)          │
│ FullName         │       │ UserId (FK)      │
│ Email (UNIQUE)   │       │ LicenseNumber    │
│ PhoneNumber      │       │ VehicleType      │
│ PasswordHash     │       │ VehiclePlate     │
│ Role (Enum)      │       │ Status (Enum)    │
│ IsVerified       │       │ Rating           │
│ CreatedAt        │       │ TotalTrips       │
│ UpdatedAt        │       │ TotalEarnings    │
└──────┬───────────┘       │ CurrentLatitude  │
       │                   │ CurrentLongitude │
       │                   └──────────────────┘
       │ 1:N (Rider)
       ▼
┌──────────────────┐       ┌──────────────────┐
│      Order       │       │    TripStop      │
├──────────────────┤       ├──────────────────┤
│ Id (PK)          │──1:N──│ Id (PK)          │
│ RiderId (FK)     │       │ OrderId (FK)     │
│ DriverId (FK)    │       │ SequenceNumber   │
│ PickupLatitude   │       │ Latitude         │
│ PickupLongitude  │       │ Longitude        │
│ PickupAddress    │       │ Address          │
│ DropoffLatitude  │       │ StopType (Enum)  │
│ DropoffLongitude │       └──────────────────┘
│ DropoffAddress   │
│ DistanceKm       │       ┌──────────────────┐
│ EstDurationMins  │       │     Payment      │
│ EstimatedFare    │       ├──────────────────┤
│ FinalFare        │──1:1──│ Id (PK)          │
│ VehicleCategory  │       │ OrderId (FK)     │
│ PaymentMethod    │       │ Amount           │
│ Status (Enum)    │       │ Method (Enum)    │
│ CreatedAt        │       │ Status (Enum)    │
│ AcceptedAt       │       │ CreatedAt        │
│ StartedAt        │       │ CompletedAt      │
│ CompletedAt      │       │ TransactionRef   │
│ CancelledAt      │       └──────────────────┘
│ RiderRating      │
│ DriverRating     │       ┌──────────────────┐
│ ReviewComment    │       │     Review       │
└──────────────────┘       ├──────────────────┤
                           │ Id (PK)          │
┌──────────────────┐       │ OrderId (FK)     │
│      Promo       │       │ ReviewerId (FK)  │
├──────────────────┤       │ TargetUserId     │
│ Id (PK)          │       │ Rating           │
│ Code (UNIQUE)    │       │ Comment          │
│ Description      │       │ CreatedAt        │
│ Type (Enum)      │       └──────────────────┘
│ Value            │
│ MaxDiscount      │       ┌──────────────────┐
│ ValidFrom        │       │  Notification    │
│ ValidUntil       │       ├──────────────────┤
│ IsActive         │       │ Id (PK)          │
│ UsageLimit       │       │ UserId           │
│ UsageCount       │       │ Title            │
└──────────────────┘       │ Message          │
                           │ Type (Enum)      │
┌──────────────────┐       │ IsRead           │
│   FareConfig     │       │ CreatedAt        │
├──────────────────┤       │ ReadAt           │
│ Id (PK)          │       └──────────────────┘
│ VehicleCategory  │
│ BaseFare         │
│ CostPerKm        │
│ CostPerMinute    │
│ MinimumFare      │
│ SurgeMultiplier  │
│ IsActive         │
└──────────────────┘
```

---

## 📋 Database Providers

FastRide supports multiple database providers through EF Core:

| Provider | Status | Connection String Example |
|----------|--------|--------------------------|
| **SQLite** | ✅ Default | `Data Source=FastRide.db` |
| **SQL Server** | ✅ Supported | `Server=.;Database=FastRide;...` |
| **MySQL** | ✅ Supported | `Server=localhost;Database=FastRide;...` |
| **PostgreSQL** | ✅ Supported | `Host=localhost;Database=FastRide;...` |

---

## 🔧 Configuration

### `appsettings.json`

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Data Source=FastRide.db"
  }
}
```

### Switching Provider

In `Program.cs`, change the `UseSqlite()` call:

```csharp
// SQLite (default)
options.UseSqlite(connectionString);

// SQL Server
options.UseSqlServer(connectionString);

// PostgreSQL
options.UseNpgsql(connectionString);

// MySQL
options.UseMySql(connectionString, ServerVersion.AutoDetect(connectionString));
```

---

## 🌱 Seed Data

The `SampleDataSeeder` class provides rich sample data for development:

```csharp
// Called automatically on first API run:
await SampleDataSeeder.SeedAsync(db);
```

### Seed Data Summary

| Entity | Count | Notes |
|--------|-------|-------|
| Users (Riders) | 50 | Indonesian names, realistic data |
| Users (Drivers) | 30 | With driver profiles |
| Users (Admin) | 1 | `admin@fastride.com` |
| Orders | 200 | Mixed statuses over 90-day history |
| Payments | ~110 | For completed orders |
| Reviews | ~140 | Indonesian language reviews |
| Promos | 8 | Various types and validity periods |
| Notifications | 40+ | Welcome and order updates |
| FareConfigs | 5 | One per vehicle category |

### Seeded Fare Configurations

| Category | Base Fare | Per Km | Per Min | Minimum |
|----------|-----------|--------|---------|---------|
| Economy | Rp 5.000 | Rp 3.000 | Rp 500 | Rp 10.000 |
| Comfort | Rp 7.000 | Rp 4.000 | Rp 700 | Rp 15.000 |
| Premium | Rp 10.000 | Rp 6.000 | Rp 1.000 | Rp 25.000 |
| Bike | Rp 3.000 | Rp 2.000 | Rp 300 | Rp 7.000 |
| Electric | Rp 5.000 | Rp 3.000 | Rp 500 | Rp 10.000 |

---

## 🏗️ Database Initialization

### Development (Auto)

On first API run, the database is automatically created and seeded:

1. `EnsureCreatedAsync()` — Creates database if not exists
2. `SeedAsync()` — Seeds data if no users exist

### Production (Migrations)

```bash
# Create initial migration
dotnet ef migrations add InitialCreate --project FastRide.Data

# Apply migration
dotnet ef database update --project FastRide.Data
```

---

## 📊 Query Performance

### Indexes

| Table | Index | Type |
|-------|-------|------|
| `Users` | `Email` | Unique |
| `Orders` | `Status` | Non-clustered |
| `Orders` | `CreatedAt` | Non-clustered |
| `Orders` | `RiderId` | Foreign Key |
| `Orders` | `DriverId` | Foreign Key |
| `Payments` | `Status` | Non-clustered |
| `Promos` | `Code` | Unique |

### Optimization Tips

1. Use `.AsNoTracking()` for read-only queries
2. Include navigation properties only when needed (`.Include()`)
3. Use pagination (`.Skip()`, `.Take()`) for large datasets
4. Consider adding composite indexes for frequent filter combinations
