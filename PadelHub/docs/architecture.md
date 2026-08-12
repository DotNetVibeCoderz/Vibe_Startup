# PadelHub - Arsitektur Aplikasi

## Overview

PadelHub dibangun dengan arsitektur **Blazor Server** menggunakan pola **N-Tier** yang terstruktur.

```
┌─────────────────────────────────────────────┐
│              Browser (Client)               │
│          SignalR WebSocket Connection        │
├─────────────────────────────────────────────┤
│           Blazor Server (.NET 10)            │
│  ┌─────────┐ ┌──────────┐ ┌──────────────┐ │
│  │ Razor   │ │ MudBlazor│ │  Components  │ │
│  │ Pages   │ │   UI     │ │   (Shared)   │ │
│  └────┬────┘ └──────────┘ └──────────────┘ │
│       │                                      │
│  ┌────┴───────────────────────────────────┐ │
│  │         Services Layer                 │ │
│  │  ┌──────────┐ ┌────────┐ ┌─────────┐  │ │
│  │  │ Business │ │Export  │ │ Payment │  │ │
│  │  │ Logic    │ │Service │ │Service  │  │ │
│  │  └──────────┘ └────────┘ └─────────┘  │ │
│  └────┬───────────────────────────────────┘ │
│       │                                      │
│  ┌────┴───────────────────────────────────┐ │
│  │         Data Access Layer              │ │
│  │    AppDbContext (EF Core)              │ │
│  │    SQLite / SQLServer / MySQL / PG     │ │
│  └────────────────────────────────────────┘ │
│                                              │
│  ┌────────────────────────────────────────┐ │
│  │         Minimal API Layer              │ │
│  │    /api/players, /api/clubs, etc.      │ │
│  │    /api/payments/webhook/{provider}    │ │
│  │    Swagger Documentation               │ │
│  └────────────────────────────────────────┘ │
└─────────────────────────────────────────────┘
                     │
                     ▼
        Payment gateway (Xendit, Midtrans)
```

## Database Schema

### Core Tables
- **AspNetUsers** (Identity) + ApplicationUser
- **Clubs** - Club information
- **Courts** - Court/padel field data
- **Facilities** - Club facilities
- **OperatingHours** - Club operating hours

### Business Tables
- **Reservations** - Court bookings
- **Payments** - Payment transactions
- **MembershipPackages** - Membership plans
- **UserMemberships** - User membership subscriptions
- **LoyaltyPoints** - Loyalty point tracking

### Player & Coach
- **PlayerProfiles** - Player data
- **PlayerStats** - Match statistics
- **PlayerAchievements** - Player awards
- **Coaches** - Coach profiles
- **TrainingSessions** - Training bookings
- **CourseMaterials** - Training materials

### Tournament
- **Tournaments** - Tournament data
- **TournamentRegistrations** - Registrations
- **Matches** - Match data
- **MatchPlayers** - Player/match relationship

### Social
- **TimelinePosts** - Social posts
- **TimelineComments** - Post comments
- **TimelineLikes** - Post likes/reactions
- **ChatMessages** - Chat messages
- **ChatGroups** - Chat groups
- **ForumTopics/Posts** - Forum discussions
- **SocialEvents** - Community events

### System
- **AuditLogs** - Activity tracking
- **Badges/UserBadges** - Gamification
- **SensorData** - IoT data
- **IoTSimulators** - IoT simulation config
- **SystemConfigs** - System settings

## Design Patterns

- **Repository Pattern**: EF Core DbContext sebagai repository
- **Dependency Injection**: Semua services di-inject via DI container
- **Service Layer**: Business logic terpisah dari UI
- **Fluent API**: Konfigurasi model via Fluent API di DbContext
- **Cascading Parameters**: Untuk dialog dan state management
- **Strategy Pattern**: `IPaymentGateway` — satu kontrak, banyak provider

---

## Lapisan Pembayaran

```
Halaman Checkout / Reservasi
          │
          ▼
  PaymentCheckoutService     ← satu-satunya tempat status Payment berubah
          │
          ▼
  PaymentGatewayRegistry     ← cari provider aktif berdasarkan konfigurasi
          │
   ┌──────┴───────┬──────────────┐
   ▼              ▼              ▼
ManualTransfer  Xendit        Midtrans
 (verifikasi   (Invoice API)  (Snap API)
  operator)
```

Webhook masuk lewat `POST /api/payments/webhook/{xendit|midtrans}` di `Program.cs`.
Alurnya selalu: **verifikasi dulu, baru ubah status.**

| Tahap | Xendit | Midtrans |
|-------|--------|----------|
| Keaslian | header `x-callback-token` | `signature_key` = SHA-512(`order_id` + `status_code` + `gross_amount` + ServerKey) |
| Lunas | `PAID`, `SETTLED` | `settlement`, atau `capture` dengan `fraud_status: accept` |
| Kedaluwarsa | `EXPIRED` | `expire` |
| Batal/gagal | — | `cancel`, `deny`, `failure` |

Pengaman tambahan di `PaymentCheckoutService.ApplyCallbackAsync`: bila nominal pada
notifikasi berbeda dari nominal tagihan, status **tidak** diubah dan kejadiannya dicatat
sebagai error. Setiap percobaan bayar memakai `ExternalId` baru
(`PDH-{paymentId}-{yyMMddHHmmss}`) karena Midtrans menolak `order_id` yang dipakai ulang.

Menambah provider baru: buat implementasi `IPaymentGateway`, daftarkan di `Program.cs`,
tambahkan seksinya di `appsettings.json`. Halaman checkout, tab Provider di Keuangan,
dan endpoint webhook mengikuti otomatis.

---

## Design System — "Court Glass & Floodlight"

Bahasa visual diambil dari lapangan padel: dinding kaca, permukaan teal, garis servis
putih, bola optic lime di bawah lampu sorot.

| Berkas | Isi |
|--------|-----|
| `Theme/PadelHubTheme.cs` | `MudTheme` tunggal: palet terang & gelap, tipografi, radius, bayangan |
| `wwwroot/css/app.css` | Token `--pd-*` dan poles komponen MudBlazor, berlaku ke semua halaman |
| `Components/Shared/` | `PageHeader`, `StatPlate`, `CourtGraphic`, `EmptyState` |

- Tipografi: **Archivo** (judul), **Instrument Sans** (teks), **IBM Plex Mono** (angka & label).
- Token kustom diturunkan dari variabel `--mud-palette-*` lewat `color-mix`, sehingga
  mode gelap ikut otomatis tanpa penulisan palet kedua di CSS.
- Gerak dibatasi 140–220 ms dan dimatikan penuh saat `prefers-reduced-motion: reduce`.
- Pilihan tema disimpan di `localStorage`; `wwwroot/js/theme-boot.js` memasangnya
  sebelum halaman digambar agar mode gelap tidak berkedip putih.

> Catatan: seluruh JavaScript wajib berada di berkas eksternal `wwwroot/js/`.
> Skrip inline di dalam `<body>` bisa ter-render sebagai teks biasa ketika
> enhanced navigation Blazor mem-patch DOM setelah login.
