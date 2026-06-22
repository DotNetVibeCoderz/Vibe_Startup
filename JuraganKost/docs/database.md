# 🗄️ Database & Models

## ERD (Entity Relationship Diagram)

```
AspNetUsers (Identity)
    │
    ├── Kost (1:N) — PemilikId → AspNetUsers.Id
    │   ├── Kamar (1:N)
    │   │   ├── Penghuni (1:N)
    │   │   │   ├── Kontrak (1:N)
    │   │   │   ├── Pembayaran (1:N)
    │   │   │   ├── Komplain (1:N)
    │   │   │   └── Review (1:N)
    │   │   ├── Tagihan (1:N)
    │   │   ├── InventarisItem (1:N)
    │   │   └── IoTSensorData (1:N)
    │   ├── Staff (1:N)
    │   ├── MarketplaceListing (1:1)
    │   └── Review (1:N)
    │
    ├── Notifikasi (1:N)
    ├── ChatThread (1:N) — UserId = string (no FK)
    │   └── ChatMessageDb (1:N)
    └── Penghuni (1:1) — UserId → AspNetUsers.Id
```

## Tabel Database (16 Tabel)

### Identity Tables (Auto-generated)
| Tabel | Deskripsi |
|---|---|
| `AspNetUsers` | User accounts (extended dengan `NamaLengkap`, `RoleExt`, dll) |
| `AspNetRoles` | Roles: SuperAdmin, Pemilik, Admin, Penghuni, Staff |
| `AspNetUserRoles` | User-Role mapping |

### Domain Tables

| Tabel | Kolom Utama | Relasi |
|---|---|---|
| **Kost** | Id, Nama, Alamat, Kota, Provinsi, Deskripsi, Status, Jenis, PemilikId | FK → AspNetUsers |
| **Kamar** | Id, NomorKamar, KostId, HargaSewa, Deposit, Status, Jenis, Luas, Fasilitas | FK → Kost |
| **Penghuni** | Id, NamaLengkap, NIK, NoHP, KamarId, UserId | FK → Kamar, FK → AspNetUsers |
| **Kontrak** | Id, NomorKontrak, PenghuniId, KamarId, TanggalMulai, TanggalSelesai, HargaSewa | FK → Penghuni, FK → Kamar |
| **Tagihan** | Id, NomorTagihan, PenghuniId, KamarId, Jenis, Jumlah, Status, JatuhTempo | FK → Penghuni, FK → Kamar |
| **Pembayaran** | Id, NomorPembayaran, PenghuniId, TagihanId, Jumlah, Metode, Status | FK → Penghuni, FK → Tagihan |
| **Komplain** | Id, NomorKomplain, PenghuniId, KamarId, Kategori, Judul, Status, Respon | FK → Penghuni, FK → Kamar |
| **InventarisItem** | Id, Nama, Kode, KostId, KamarId, Kategori, Jumlah, Status | FK → Kost, FK → Kamar |
| **Staff** | Id, Nama, KostId, Posisi, Gaji, Status | FK → Kost |
| **Review** | Id, KostId, PenghuniId, Rating (1-5), Komentar, Emoji | FK → Kost, FK → Penghuni |
| **Notifikasi** | Id, UserId, Judul, Pesan, Tipe, IsDibaca | FK → AspNetUsers |
| **IoTSensorData** | Id, DeviceId, KamarId, Jenis, Nilai, Satuan, Timestamp | FK → Kamar |
| **MarketplaceListing** | Id, KostId, IsPublic, HighlightFitur | FK → Kost (unique) |

### Chat Tables

| Tabel | Kolom Utama | Relasi |
|---|---|---|
| **ChatThreads** | Id, SessionId (unique), UserId (string, no FK), Provider, Title | → ChatMessages |
| **ChatMessages** | Id, ChatThreadId, Role, Content (max 8000), Timestamp | FK → ChatThreads |

## Enums

```csharp
StatusKamar     = Kosong, Terisi, Booking, Perbaikan
JenisKamar      = Standar, Premium, VIP, Suite
StatusPenghuni  = Aktif, Keluar, Blacklist
StatusKontrak   = Aktif, Selesai, Dibatalkan, Perpanjangan
StatusTagihan   = BelumDibayar, Dibayar, Terlambat, Dibatalkan
StatusPembayaran= Pending, Diverifikasi, Ditolak, Refund
StatusKomplain  = Menunggu, Diproses, Selesai, Ditolak
MetodePembayaran= Transfer, EWallet, QRIS, VirtualAccount, Tunai
UserRoleExt     = SuperAdmin, Pemilik, Admin, Penghuni, Staff
```

## Database Provider

Didukung 4 provider database (dikonfigurasi di `appsettings.json`):

```json
{
  "DatabaseProvider": "SQLite",  // SQLite | SqlServer | PostgreSQL | MySql
  "ConnectionStrings": {
    "Default": "Data Source=JuraganKost.db",
    "SqlServer": "Server=.;Database=JuraganKost;...",
    "PostgreSQL": "Host=localhost;Database=JuraganKost;...",
    "MySql": "Server=localhost;Database=JuraganKost;..."
  }
}
```

## Migration Strategy

Development: `EnsureCreated()` auto-create tables dari model
Production: Gunakan EF Core Migrations (`dotnet ef migrations add`)

## Seed Data

`SeedService.cs` membuat data sample:
- 13 users (1 SuperAdmin, 1 Pemilik, 1 Admin, 9 Penghuni, 1 test user)
- 2 Kost (Melati Indah, Mawar Premium)
- 12 Kamar (6 Standar, 4 Premium, 2 VIP)
- 9 Penghuni + Kontrak + Tagihan
- 3 Staff, 5 Inventaris, 2 Komplain, 3 Review, 2 Marketplace

---

## ⚠️ Catatan Penting

1. **`Tagihan.Total`** adalah computed property (`Jumlah + Denda - Diskon`). Jangan gunakan di LINQ query EF — gunakan ekspresi langsung (`x.Jumlah + (x.Denda ?? 0) - (x.Diskon ?? 0)`).

2. **SQLite + ORDER BY decimal**: Decimal dipetakan ke TEXT di SQLite. Cast ke double: `.OrderBy(x => (double)x.HargaSewa)`.

3. **`ChatThread.UserId`** tidak punya FK constraint — plain string untuk fleksibilitas.
