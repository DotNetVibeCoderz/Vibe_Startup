# 🗄️ Database — FastRide

EF Core 10 dengan empat provider. Pilih lewat `Database:Provider` di
`FastRide.Api/appsettings.json`.

| Provider | Nilai konfigurasi | Paket |
|----------|-------------------|-------|
| SQLite (bawaan) | `SQLite` | `Microsoft.EntityFrameworkCore.Sqlite` |
| SQL Server | `SqlServer` / `mssql` | `Microsoft.EntityFrameworkCore.SqlServer` |
| PostgreSQL | `PostgreSQL` / `postgres` / `npgsql` | `Npgsql.EntityFrameworkCore.PostgreSQL` |
| MySQL | `MySQL` | `MySql.EntityFrameworkCore` |

```json
"Database": {
  "Provider": "SQLite",
  "AutoSeed": true,
  "ConnectionStrings": {
    "SQLite": "Data Source=FastRide.db",
    "SqlServer": "Server=.;Database=FastRide;Trusted_Connection=true;TrustServerCertificate=true",
    "PostgreSQL": "Host=localhost;Database=FastRide;Username=postgres;Password=postgres",
    "MySQL": "Server=localhost;Database=FastRide;User Id=root;Password=root"
  }
}
```

> MySQL memakai provider resmi Oracle karena Pomelo belum merilis build untuk EF Core 10.
> Metode ekstensinya `UseMySQL` (huruf besar), bukan `UseMySql`.

---

## ⚠️ Belum ada migrasi

Skema dibuat dengan `Database.EnsureCreatedAsync()`. Konsekuensinya:

- **Mengubah entity berarti menghapus database.** Untuk SQLite: hapus
  `FastRide.Api/FastRide.db`, lalu jalankan API lagi — data contoh akan dibuat ulang.
- `dotnet ef migrations add` bukan bagian dari alur kerja saat ini.

Supaya kegagalannya tidak membingungkan, API melakukan **uji kolom** saat start. Kalau ada
database lama dengan skema usang, aplikasi berhenti dengan pesan yang menyebutkan persis
apa yang harus dilakukan, bukan gagal belakangan dengan `SQLite Error 1: no such column`.

Berpindah ke EF Migrations ada di [`../PLAN.md`](../PLAN.md) v2.1.

Set `Database:AutoSeed` ke `false` bila tidak ingin data contoh dibuat.

---

## Entity

### `User`

| Kolom | Catatan |
|-------|---------|
| `Id` | GUID |
| `Email` | Unik |
| `PasswordHash` | BCrypt work factor 12 |
| `Role` | `Rider` = 1, `Driver` = 2, `Admin` = 3 |
| `IsVerified` | Email/telepon terverifikasi |
| `IsActive` | Akun nonaktif tidak bisa masuk |
| `SecurityStamp` | Naik saat logout / ganti sandi — inilah yang membatalkan token lama |
| `PhotoUrl` | URL storage, atau `data:` URI untuk avatar inisial |
| `LastLoginAt` | |

### `DriverProfile`

Satu per driver, kunci asing unik ke `User`.

| Kolom | Catatan |
|-------|---------|
| `VehicleCategory` | Menentukan tarif yang berlaku dan order yang cocok |
| `Status` | `Offline` = 0, `Online` = 1, `OnTrip` = 2, `Break` = 3 |
| `Rating`, `RatingCount` | Dihitung ulang dari tabel `Reviews` |
| `CurrentLatitude`, `CurrentLongitude`, `Heading` | Posisi terakhir |
| `LocationUpdatedAt` | Lebih tua dari 10 menit ⇒ tidak ikut pencocokan |
| `IsDocumentVerified`, `VerifiedAt` | Wajib `true` untuk online |

### `DriverDocument`

SIM/STNK/KTP dan sebagainya. Unik per `(DriverProfileId, Type)` — unggah ulang mengganti
baris yang ada, bukan menumpuk.

### `Order`

| Kolom | Catatan |
|-------|---------|
| `Code` | Kode pendek unik, mis. `FR-9LTPXJ` |
| `DistanceKm`, `EstimatedDurationMinutes` | Haversine + estimasi lalu lintas |
| `EstimatedFare`, `DiscountAmount`, `FinalFare` | Diskon dicatat terpisah agar laporan bisa memisahkannya |
| `SurgeMultiplier` | Dibekukan saat pemesanan — tarif tidak boleh berubah surut |
| `PromoCode` | Kode yang benar-benar terpakai |
| `Status` | Lihat siklus di [`API.md`](API.md) |
| `AcceptedAt` … `CancelledAt` | Jejak waktu setiap tahap |
| `CancelledBy` | `Rider`, `Driver`, atau `System` |
| `DriverLatitude/Longitude` | Posisi driver untuk perjalanan ini |

### `TripStop`

Titik singgah multi-stop, berurut lewat `SequenceNumber`, dengan `ReachedAt`.

### `Payment`

| Kolom | Catatan |
|-------|---------|
| `OrderId` | **Unique index** — satu pembayaran per order |
| `Amount`, `DiscountAmount` | |
| `TransactionReference` | `TRX-20260804-A1B2C3D4` |

Unique index inilah yang menutup bug double-charge: dulu `POST /api/payments` dan
`complete-order` sama-sama membuat baris pembayaran untuk order yang sama.

### `Promo`

`MinOrderAmount` dan `VehicleCategory` opsional mempersempit keberlakuan. Kuota diambil
lewat `UPDATE` bersyarat sehingga aman terhadap perebutan.

### `Review`

Unik per `(OrderId, ReviewerId)` — satu ulasan per orang per perjalanan.

### `FareConfig`

Satu baris per kategori kendaraan, di-*seed* lewat `HasData`. `Quote()` menerapkan surge
pada bagian terukur lalu memakai `MinimumFare` sebagai lantai — urutan yang sama dengan
rincian tarif yang ditampilkan ke penumpang.

### `Notification`

Punya `OrderId` opsional agar aplikasi bisa membuka perjalanan terkait langsung.

---

## Indeks

Dipilih mengikuti pola query nyata, bukan ditebak:

| Tabel | Indeks | Dipakai oleh |
|-------|--------|--------------|
| `Orders` | `(Status, CreatedAt)` | Papan order terbuka |
| `Orders` | `(DriverId, Status, CompletedAt)` | Pendapatan & riwayat driver |
| `Orders` | `(RiderId, CreatedAt)` | Riwayat penumpang |
| `Orders` | `Code` unik | Pencarian kode |
| `DriverProfiles` | `Status`, `(CurrentLatitude, CurrentLongitude)` | Pencocokan driver terdekat |
| `Payments` | `OrderId` unik, `(Status, CreatedAt)` | Pembayaran & laporan |
| `Notifications` | `(UserId, IsRead, CreatedAt)` | Kotak masuk & jumlah belum dibaca |
| `Reviews` | `TargetUserId`, `(OrderId, ReviewerId)` unik | Rating driver |
| `DriverDocuments` | `(DriverProfileId, Type)` unik | Antrean verifikasi |

---

## ⚠️ Portabilitas query

Dua jebakan yang sudah ditemukan lewat pengujian nyata, keduanya penting bila Anda menambah
query baru:

**1. SQLite tidak mendukung `APPLY`.** Proyeksi yang menggabungkan koleksi *dan* subquery
berkorelasi dalam satu `Select` diterjemahkan menjadi `CROSS APPLY` dan gagal saat runtime:

```csharp
// ❌ gagal di SQLite
.Select(o => new {
    Stops = o.Stops.Select(...).ToList(),
    Payment = db.Payments.Where(p => p.OrderId == o.Id).Select(...).FirstOrDefault()
})

// ✅ query datar terpisah
var order = await db.Orders.Where(...).Select(...).FirstOrDefaultAsync(ct);
var stops = await db.TripStops.Where(s => s.OrderId == id).Select(...).ToListAsync(ct);
```

**2. `GroupBy` tidak bisa langsung memproyeksi ke konstruktor record.** Pakai tipe anonim
dulu, lalu petakan di memori:

```csharp
// ❌ tidak bisa diterjemahkan
.GroupBy(p => p.Method).Select(g => new PaymentMethodBreakdownItem(g.Key, g.Count(), g.Sum(p => p.Amount)))

// ✅
.GroupBy(p => p.Method).Select(g => new { Method = g.Key, Count = g.Count(), Amount = g.Sum(p => p.Amount) })
```

---

## Data contoh

`SampleDataSeeder` berjalan saat start bila tabel `Users` kosong.

| Isi | Jumlah |
|-----|--------|
| Penumpang | 50 |
| Driver | 30 (dengan dokumen; sebagian sengaja belum terverifikasi) |
| Admin | 1 |
| Order | ±420, tersebar 90 hari |
| Pembayaran | Satu per order selesai |
| Ulasan | ±70% dari order selesai |
| Promo | 8 |
| Notifikasi | ±65 |

Beberapa keputusan yang membuat data ini berguna:

- **Kurva permintaan nyata.** Order mengikuti pola jam sibuk pagi dan sore Jakarta, bukan
  sebaran seragam — grafik dashboard jadi masuk akal.
- **Hari ini padat.** ±30 order dibuat untuk hari berjalan sehingga panel "hari ini" tidak
  kosong saat pertama kali dibuka.
- **Rating konsisten.** `DriverProfile.Rating` dihitung dari ulasan yang benar-benar ditulis.
- **Akun demo pasti ada.** Rider #0 dan driver #0 dibuat eksplisit sebagai
  `budi.santoso@email.com` dan `andi.santoso@drive.com` — sebelumnya nama diacak sehingga
  kredensial di README tidak pernah benar.

Semua akun contoh memakai kata sandi `Password123`.

---

## Mengganti provider

```bash
# PostgreSQL
docker run -d --name fastride-pg -e POSTGRES_PASSWORD=postgres -p 5432:5432 postgres:16

# lalu ubah appsettings.json
"Database": { "Provider": "PostgreSQL" }
```

Bisa juga lewat variabel lingkungan tanpa mengubah berkas:

```bash
export Database__Provider=PostgreSQL
dotnet run --project FastRide.Api
```

Skema dan data contoh dibuat otomatis pada database baru.
