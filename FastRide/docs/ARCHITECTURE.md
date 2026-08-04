# 🏛️ Arsitektur — FastRide

---

## Arah ketergantungan

```
FastRide.Shared  ◄── FastRide.Data  ◄── FastRide.Api
       ▲
       ├── FastRide.AdminWeb    (HTTP saja)
       ├── FastRide.RiderApp    (HTTP saja)
       ├── FastRide.DriverApp   (HTTP saja)
       └── FastRide.Simulator   (HTTP saja)
```

Aturannya satu kalimat: **hanya API yang menyentuh database.** Setiap klien berbicara lewat
HTTP dan memakai kontrak dari `FastRide.Shared`.

> AdminWeb dulu mereferensikan `FastRide.Data` (walau tidak memakainya). Referensi itu
> dihapus: proses kedua yang memegang `DbContext` yang sama akan melewati seluruh aturan
> yang ditegakkan API.

---

## Proyek

### `FastRide.Shared`

Satu-satunya sumber kebenaran untuk apa pun yang melintasi batas proses.

```
Models/        entity domain + enum
DTOs/          seluruh request & response
Common/        PagedResult, GeoUtils, Display, ApiError
Storage/       antarmuka IStorageProvider
```

`Display` juga tinggal di sini: pemetaan status ke warna sinyal dan label bahasa Indonesia
dipakai identik oleh konsol admin dan kedua aplikasi mobile, jadi sebuah status tidak
mungkin berarti satu hal di dashboard dan hal lain di ponsel.

> Sebelumnya setiap aplikasi mobile mendeklarasikan **salinan `VehicleCategory` dan
> `PaymentMethod` sendiri**. Karena enum bernilai 1-based dan klien mengirim angka mentah,
> menukar urutan di satu tempat akan diam-diam mengubah arti di tempat lain.

### `FastRide.Data`

`FastRideDbContext` (konfigurasi fluent, indeks, seed tabel tarif) dan `SampleDataSeeder`.

### `FastRide.Api`

```
Program.cs              perakitan host saja
Extensions/             AddFastRideDatabase / Cache / Storage / Auth / Cors / RateLimiting / Json
Security/               TokenService, CurrentUser, SecurityStampMiddleware, Policies
Services/               PricingService, OrderService, DispatchService, NotificationService,
                        CacheService, CsvExporter, Result
Endpoints/              satu berkas per area
Infrastructure/         penyedia storage (FileSystem, S3, Azure Blob)
```

> Sampai v1.x seluruh permukaan HTTP berupa lambda satu baris di dalam satu `Program.cs`
> sepanjang ±240 baris, dengan record request menumpuk di bagian bawah berkas. Pemecahan
> ini bukan soal estetika: aturan seperti "satu perjalanan aktif per rider" atau "satu
> pembayaran per order" tidak punya tempat untuk hidup di dalam lambda.

### `FastRide.AdminWeb`

Blazor Server, render mode `InteractiveServer` global. Halaman meng-*inject* `ApiClient` dan
memanggil API lewat HTTP; tidak ada yang menyentuh `DbContext`.

### `FastRide.RiderApp` / `FastRide.DriverApp`

MAUI Blazor Hybrid. Masing-masing punya `ApiClient` sendiri (endpoint-nya memang berbeda)
tetapi keduanya memakai DTO bersama. Sesi disimpan di `SecureStorage`.

### `FastRide.Simulator`

Konsol Spectre.Console yang menjalankan API sungguhan: setiap penumpang dan driver simulasi
punya klien HTTP dan tokennya sendiri.

---

## Alur permintaan

```
Klien
  │  Authorization: Bearer <jwt>
  ▼
UseRateLimiter        batas per klien
UseAuthentication     validasi tanda tangan JWT
SecurityStampMiddleware   token masih milik sesi yang hidup?
UseAuthorization      kebijakan peran
  ▼
Endpoint              cek kepemilikan (CanAccess)
  ▼
Service               aturan domain (PricingService, OrderService, DispatchService)
  ▼
DbContext (pooled)    query berproyeksi, AsNoTracking
```

---

## Keputusan penting

### Siklus order tinggal di satu tempat

`OrderService` memiliki tabel transisi yang sah:

```csharp
Requested     → Accepted, Cancelled, Expired
Accepted      → DriverArrived, Started, Cancelled
DriverArrived → Started, Cancelled
Started       → Completed, Cancelled
Completed / Cancelled / Expired → (akhir)
```

Transisi lain ditolak `409`. Aturannya sama entah pemanggilnya aplikasi driver, konsol
admin, atau simulator.

### Perebutan diselesaikan di database, bukan di memori

Dua tempat di mana dua pihak bisa berebut hal yang sama:

```csharp
// Menerima order — hanya satu driver yang bisa menang
await db.Orders
    .Where(o => o.Id == orderId && o.Status == OrderStatus.Requested && o.DriverId == null)
    .ExecuteUpdateAsync(...);

// Mengambil kuota promo — slot terakhir tidak bisa direbut dua kali
await db.Promos
    .Where(p => p.Id == promoId && p.UsageCount < p.UsageLimit && p.IsActive)
    .ExecuteUpdateAsync(...);
```

Pola baca-lalu-tulis tidak bisa memberi jaminan ini. Simulator memicu perebutan ini terus
menerus, jadi kesalahannya akan langsung terlihat.

### Pembayaran idempoten

Sebuah perjalanan bisa diselesaikan dari dua arah: driver menekan "selesai", atau
pembayaran diposkan. Keduanya bermuara pada satu baris pembayaran, dijaga unique index pada
`Payment.OrderId`. `POST /api/payments` mengembalikan pembayaran yang sudah ada alih-alih
membuat yang kedua, dan menangkap `DbUpdateException` bila kalah dalam perlombaan.

### Pencocokan dua tahap

Menghitung haversine untuk setiap driver berarti memindai seluruh tabel setiap kali aplikasi
melakukan polling. `DispatchService` menyaring dengan *bounding box* di SQL — yang bisa
memakai indeks — lalu menghitung jarak tepat hanya untuk kandidat yang tersisa.

### Cache di balik antarmuka

`ICacheService` punya dua implementasi: `IMemoryCache` (bawaan) dan Redis. Kode pemanggil
tidak tahu bedanya. Implementasi Redis memperlakukan setiap kegagalan sebagai *cache miss*
— gangguan cache tidak boleh menjatuhkan API.

Yang di-cache: ringkasan dashboard (10 detik), tabel tarif (10 menit, dibatalkan saat admin
menyimpan), security stamp (5 menit), kode reset kata sandi (15 menit).

### Enum sebagai string di kabel

`JsonStringEnumConverter` dipasang di API dan seluruh klien. Respons memakai
`"status": "Completed"`, bukan `5`. Angka masih diterima pada request demi kompatibilitas.
Ini menghapus seluruh kelas bug di mana penambahan nilai enum menggeser arti nilai lain.

### Portabilitas empat provider

Query harus bisa diterjemahkan di keempat provider. Dua batasan yang sudah menggigit dan
terdokumentasi di [`DATABASE.md`](DATABASE.md): SQLite tidak mendukung `APPLY`, dan `GroupBy`
tidak bisa memproyeksi langsung ke konstruktor record.

---

## Storage

`IStorageProvider` punya tiga implementasi, dipilih lewat `Storage:Provider`:

| Provider | Catatan |
|----------|---------|
| `FileSystem` | Bawaan. Disajikan sebagai berkas statis dari direktori yang benar-benar dipakai, bukan asumsi folder di `wwwroot` |
| `S3` / `minio` | AWS Signature V4 sungguhan, gaya path |
| `Azure` / `azureblob` | Tanda tangan Shared Key sungguhan |

> Kedua penyedia awan sebelumnya mengirim header `Authorization` berisi placeholder
> (`AWS4-HMAC-SHA256 Credential={key}/...`), jadi setiap unggahan pasti ditolak 403.

`ResolveFileName(url)` memetakan URL publik kembali ke kunci storage, dan mengembalikan
`null` untuk `data:` URI — yang membuat penghapusan avatar hasil generate tidak lagi mencoba
menghapus berkas yang tidak pernah ada.

---

## Performa

| Praktik | Di mana |
|---------|---------|
| `AddDbContextPool` | Semua permintaan |
| `AsNoTracking()` + proyeksi `Select` | Semua jalur baca |
| Satu `GroupBy` menggantikan hitungan berulang | Statistik dashboard, pendapatan driver |
| Satu endpoint `/dashboard/overview` | Menggantikan enam panggilan polling |
| Pra-filter bounding box | Pencocokan driver dan order |
| Kompresi respons | Semua permintaan |
| Indeks komposit sesuai pola query | Lihat [`DATABASE.md`](DATABASE.md) |

Contoh yang paling mencolok: `orders-by-hour` dulu menjalankan **24 query `Count()` sinkron
di dalam sebuah loop**. Sekarang satu `GroupBy`.

---

## Yang belum ada

| Item | Dampak |
|------|--------|
| Tidak ada realtime (SignalR) | Semua layar melakukan polling |
| Tidak ada migrasi EF | Ubah entity ⇒ hapus database |
| Tidak ada gRPC | Spesifikasi menyebut REST/gRPC; sekarang REST saja |

Urutan pengerjaannya ada di [`../PLAN.md`](../PLAN.md).
