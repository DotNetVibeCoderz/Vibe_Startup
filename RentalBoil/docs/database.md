# 🗄️ Database

## Entity Relationship Diagram

```
┌──────────────┐       ┌──────────────┐       ┌──────────────┐
│ AspNetUsers  │       │   Vehicles   │       │  Bookings    │
│ (Identity)   │       │              │       │              │
├──────────────┤       ├──────────────┤       ├──────────────┤
│ Id           │←──┐   │ Id           │←──┐   │ Id           │
│ FullName     │   │   │ Name         │   │   │ BookingNumber│ UNIQUE
│ Email        │   │   │ PlateNumber  │   │   │ VehicleId    │ FK
│ PhoneNumber  │   │   │ Type         │   │   │ CustomerId   │ FK
│ Role         │   │   │ Brand        │   │   │ Status        │ INDEX
│ KtpVerified  │   │   │ Model        │   │   │ StartDate     │ INDEX
│ SimVerified  │   │   │ Year         │   │   │ EndDate       │
│ LoyaltyPts   │   │   │ Color        │   │   │ DurationDays  │
│ MembershipTier│  │   │ Transmission │   │   │ BasePrice     │
│ ProfilePhoto │   │   │ FuelType     │   │   │ InsuranceCost │
│ IsSuspended  │   │   │ Capacity     │   │   │ Discount      │
│ RegisteredAt │   │   │ PricePerHour │   │   │ CouponCode    │
└──────────────┘   │   │ PricePerDay  │   │   │ TotalPrice    │
                    │   │ DynamicPrice │   │   │ PaymentStatus │
                    │   │ Location     │   │   │ PaymentMethod │
                    │   │ Latitude     │   │   │ PaidAt        │
                    │   │ Longitude    │   │   │ PickupAddress │
                    │   │ AvgRating    │   │   │ CreatedAt     │ INDEX
                    │   │ ReviewCount  │   │   │ UpdatedAt     │
                    │   │ RentalCount  │   │   └──────────────┘
                    │   │ IsAvailable  │   │          │
                    │   │ IsVerified   │   │          │ 1:1
                    │   │ OwnerId ─────┼───┘          ↓
                    │   │ LockStatus   │       ┌──────────────┐
                    │   │ EngineStatus │       │  Payments    │
                    │   │ MotionStatus │       ├──────────────┤
                    │   │ CurrentSpeed │       │ Id           │
                    │   │ CurrentHead  │       │ BookingId    │ FK
                    │   │ CreatedAt    │       │ Method       │
                    │   │ UpdatedAt    │       │ Amount       │
                    │   └──────────────┘       │ Status       │
                    │          │               │ ExternalTxnId│
                    │          │ 1:N           │ ExpiresAt    │
                    │          ↓               │ CreatedAt    │
                    │   ┌──────────────┐       └──────────────┘
                    │   │VehiclePhotos │
                    │   ├──────────────┤
                    │   │ Id           │
                    │   │ VehicleId    │ FK
                    │   │ Url          │
                    │   │ IsPrimary    │
                    │   │ SortOrder    │
                    │   └──────────────┘
                    │
                    │   ┌──────────────────┐
                    │   │VehicleAvailability│
                    │   ├──────────────────┤
                    │   │ Id               │
                    │   │ VehicleId        │ FK
                    │   │ StartDate        │
                    │   │ EndDate          │
                    │   │ Reason           │
                    │   │ IsBooked         │
                    │   └──────────────────┘
                    │
                    └──→ (OwnerId FK to Users)

┌──────────────┐       ┌──────────────┐       ┌──────────────┐
│   Reviews    │       │ Notifications│       │ ChatMessages │
├──────────────┤       ├──────────────┤       ├──────────────┤
│ Id           │       │ Id           │       │ Id           │
│ VehicleId    │ FK    │ UserId       │ FK    │ SenderId     │ FK
│ BookingId    │ FK    │ Title        │       │ ReceiverId   │ FK
│ UserId       │ FK    │ Message      │       │ BookingId    │ FK
│ Rating       │ 1-5   │ Type         │       │ Message      │
│ Comment      │       │ Link         │       │ IsRead       │
│ IsVerified   │       │ IsRead       │       │ SentAt       │
│ CreatedAt    │       │ CreatedAt    │       │ ReadAt       │
└──────────────┘       │ ReadAt       │       └──────────────┘
                        └──────────────┘

┌──────────────┐       ┌──────────────┐       ┌──────────────┐
│ Promotions   │       │ LoyaltyTxns  │       │    FAQs      │
├──────────────┤       ├──────────────┤       ├──────────────┤
│ Id           │       │ Id           │       │ Id           │
│ Code         │UNIQUE │ UserId       │ FK    │ Question     │
│ Description  │       │ Points       │       │ Answer       │
│ DiscountType │       │ Type         │       │ Category     │
│ DiscountValue│       │ Description  │       │ SortOrder    │
│ MinTransaction│      │ BookingId    │       │ Language     │
│ MaxDiscount  │       │ CreatedAt    │       │ IsActive     │
│ StartDate    │       └──────────────┘       └──────────────┘
│ EndDate      │
│ UsageLimit   │       ┌──────────────┐
│ UsageCount   │       │SystemSettings│
│ IsActive     │       ├──────────────┤
│ RequiredTier │       │ Id           │
│ CreatedAt    │       │ Key          │ UNIQUE
└──────────────┘       │ Value        │
                        │ Group        │
┌──────────────┐       │ Description  │
│ChatSessions  │       │ UpdatedAt    │
├──────────────┤       └──────────────┘
│ Id           │
│ UserId       │ FK    ┌──────────────┐
│ Title        │       │ChatHistories │
│ Model        │       ├──────────────┤
│ CreatedAt    │       │ Id           │
│ UpdatedAt    │       │ SessionId    │ FK
│ IsActive     │       │ Role         │
└──────────────┘       │ Content      │
                        │ ImageUrls    │
                        │ DocumentUrls │
                        │ TokenCount   │
                        │ CreatedAt    │
                        └──────────────┘
```

---

## Index Strategy

| Table | Index | Reason |
|-------|-------|--------|
| Vehicles | Type, PricePerDay, IsAvailable, Capacity | Search filters |
| Vehicles | OwnerId | Partner dashboard |
| Vehicles | IsVerified | Admin verification |
| Bookings | BookingNumber (UNIQUE) | Lookup by booking number |
| Bookings | CustomerId, VehicleId, Status, PaymentStatus | Dashboard queries |
| Bookings | CreatedAt, StartDate | Date range filters |
| Reviews | VehicleId, UserId, Rating | Vehicle detail, user history |
| Notifications | UserId, IsRead, CreatedAt | Unread count, time sort |
| Promotions | Code (UNIQUE), IsActive | Coupon validation |

---

## Seed Data

`DbInitializer.SeedAsync()` membuat:

| Entity | Count | Detail |
|--------|-------|--------|
| Users | 6 | 1 Admin, 2 Partner, 3 Customer |
| Vehicles | 8 | 6 Mobil, 2 Motor |
| Vehicle Photos | 16 | 2 per kendaraan |
| Bookings | 3 | Completed, Active, Pending |
| Reviews | 8 | Rating 4-5 |
| Promotions | 4 | WELCOME50, WEEKEND25, dll |
| FAQs | 8 | ID & EN |
| System Settings | 9 | Konfigurasi sistem |

---

## Multi-Database Support

```csharp
// appsettings.json
"Database": { "Provider": "SQLite" }   // SQLite | SqlServer | MySQL | PostgreSQL

// Program.cs
builder.Services.AddDbContext<AppDbContext>(options =>
{
    switch (dbProvider)
    {
        case "SqlServer": options.UseSqlServer(connectionString); break;
        case "MySQL": options.UseMySql(...); break;
        case "PostgreSQL": options.UseNpgsql(...); break;
        default: options.UseSqlite(connectionString); break;
    }
});
```

---

## Case-Insensitive Search

Semua query pencarian menggunakan `.ToLower()`:

```csharp
// Vehicle search
var s = search.ToLowerInvariant();
query = query.Where(v => v.Name.ToLower().Contains(s) || v.Brand.ToLower().Contains(s));

// Coupon validation
var codeLower = code.ToLowerInvariant();
var coupon = await db.Promotions.FirstOrDefaultAsync(p => p.Code.ToLower() == codeLower);

// FAQ search in Kernel Functions
var kw = keyword.ToLowerInvariant();
var faqs = await db.Faqs.Where(f => f.Question.ToLower().Contains(kw)).ToListAsync();
```
