# 📋 FastRide — Catatan Perkembangan

> Rekaman apa yang sudah dikerjakan. Rencana ke depan ada di [`PLAN.md`](PLAN.md).

---

## Ringkasan kondisi

| Komponen | Status | Catatan |
|----------|--------|---------|
| `FastRide.Shared` | ✅ Stabil | Satu-satunya sumber kontrak DTO, enum, dan helper tampilan |
| `FastRide.Data` | ✅ Stabil | EF Core 10, 4 provider, seeder ±420 order berpola realistis |
| `FastRide.Api` | ✅ Stabil | 50+ endpoint, JWT ditegakkan, cache, rate limit |
| `FastRide.AdminWeb` | ✅ Stabil | 10 halaman, login admin, Chart.js aktif |
| `FastRide.RiderApp` | ✅ Berjalan | 5 layar; koordinat masih titik tetap Jakarta |
| `FastRide.DriverApp` | ✅ Berjalan | 4 layar; belum ada GPS latar belakang |
| `FastRide.Simulator` | ✅ Stabil | Siklus penuh + metrik latensi |
| `FastRide.Tests` | ✅ Stabil | 318 test, ±72 detik, 0 gagal |
| CI (GitHub Actions) | ✅ Aktif | Build, test, smoke end-to-end, MAUI, pindai kredensial |

**Verifikasi terakhir:**

- `dotnet test FastRide.Tests` → **318/318 lulus** (Debug dan Release).
- Simulator 40 detik, 6 penumpang + 3 driver → 20 order, 16 selesai, **12 pembayaran lunas**,
  407 request, **0 gagal**, **0 exception di log API**, latensi p50 3 ms / p95 12 ms.
- QRIS teruji end-to-end lewat simulator, bukan hanya lewat test.

---

## v2.0 — Perombakan menyeluruh

### 🔒 Keamanan — dari "dekoratif" menjadi nyata

Sebelumnya JWT diterbitkan tapi **tidak ada satu pun endpoint yang memeriksanya**. Setiap
rute bisa dipanggil anonim; `.AllowAnonymous()` pada rute auth hanya hiasan.

| Perbaikan | Detail |
|-----------|--------|
| Otorisasi ditegakkan | Semua grup endpoint memakai `RequireAuthorization()`; hanya `/api/health` dan `/api/auth/*` yang anonim |
| Kebijakan peran | `AdminOnly`, `DriverOnly`, `RiderOnly` |
| Cek kepemilikan | `CurrentUser.CanAccess()` — rider tidak bisa membaca trip rider lain hanya dengan mengganti id di URL |
| Logout benar-benar mematikan sesi | `User.SecurityStamp` dinaikkan; `SecurityStampMiddleware` menolak token lama (dengan cache, tanpa query per request) |
| Ganti/reset kata sandi | Menaikkan stamp ⇒ semua sesi lama berakhir |
| Rate limiting | 30 percobaan/menit untuk `/api/auth`, 600/menit global — terverifikasi mengembalikan 429 |
| Enumerasi email ditutup | Login dan lupa-sandi memberi jawaban sama untuk email terdaftar maupun tidak |
| Path traversal ditutup | `FileSystemStorageProvider` menolak nama berkas yang keluar dari folder unggahan |
| Rahasia JWT wajib | Aplikasi menolak start kalau `Jwt:Secret` kurang dari 32 karakter |

### 🐞 Bug yang diperbaiki

| Bug | Akibat sebelumnya | Perbaikan |
|-----|-------------------|-----------|
| **Double payment** | `POST /api/payments` dan `complete-order` sama-sama membuat pembayaran ⇒ satu trip bisa ditagih dua kali | Unique index pada `Payment.OrderId` + jalur penyelesaian idempoten |
| **Endpoint hilang** | `GET /api/orders/{id}` dan `/api/mobile/rider/{id}/trips` dipanggil klien tapi tidak ada ⇒ layar "My Trips" selalu gagal diam-diam | Kedua endpoint dibuat |
| **Field fantom** | `DashboardStats.ActiveRiders`, `TotalTripsToday`, `HourlyData.Revenue` tidak pernah dikirim API ⇒ selalu 0 | Dikirim sungguhan oleh `/api/dashboard/stats` |
| **`MinimumFare` & `SurgeMultiplier` diabaikan** | Kolom ada di database tapi tidak dipakai; tarif bisa di bawah minimum | `FareConfig.Quote()` menerapkan surge lalu minimum sebagai lantai |
| **Harga aplikasi ≠ harga tagihan** | Aplikasi rider menampilkan angka hardcoded (`25000`, `40000`, …) | `POST /api/orders/quote` memakai tabel tarif yang sama dengan penagihan |
| **Hapus foto profil** | `new Uri(u.PhotoUrl)` mengasumsikan berkas terunggah, padahal avatar adalah `data:` URI | `IStorageProvider.ResolveFileName()` mengembalikan null untuk data URI |
| **Bentrok port** | `launchSettings.json` memakai 52545/52543 sementara semua klien menunjuk 5001/5002 ⇒ tidak ada klien yang bisa konek dengan `dotnet run` | Profil diluruskan ke 5001/5000 dan 5002/5003 |
| **S3 & Azure signing palsu** | Header `Authorization` berisi placeholder `AWS4-HMAC-SHA256 Credential=.../...` ⇒ semua unggahan 403 | AWS Signature V4 dan Azure Shared Key diimplementasikan sungguhan |
| **Akun demo tidak ada** | README menjanjikan `budi.santoso@email.com`, tapi seeder memakai nama acak | Rider #0 dan driver #0 dibuat eksplisit |
| **Enum lintas proyek** | Aplikasi mobile mendeklarasikan salinan `VehicleCategory`/`PaymentMethod` sendiri | Semua memakai `FastRide.Shared` |
| **`Simulation:DurationSeconds` diabaikan** | Ada di konfigurasi, tidak pernah dibaca | Dipakai, plus argumen `--duration` |
| **Simulator memakai token rider untuk endpoint driver** | Lolos hanya karena tidak ada otorisasi | Setiap aktor punya klien dan tokennya sendiri |
| **`Console.KeyAvailable` crash saat stdin di-redirect** | Simulator mati di CI/pipe | Dijaga dengan `Console.IsInputRedirected` |
| **CSS aplikasi mobile tidak ada** | `index.html` menautkan `css/app.css` yang tidak pernah dibuat | Design system mobile dibuat untuk kedua aplikasi |

### ⚡ Optimasi

| Sebelum | Sesudah | Dampak |
|---------|---------|--------|
| `orders-by-hour` menjalankan **24 query `Count()` sinkron di dalam loop** | Satu `GroupBy` | Endpoint paling lambat menjadi satu perjalanan ke database |
| Dashboard memanggil 6 endpoint terpisah tiap 10 detik | `/api/dashboard/overview` (satu panggilan, cache 10 detik) | Beban database turun drastis saat dashboard terbuka |
| `AddDbContext` | `AddDbContextPool` | Context dipakai ulang, bukan dialokasikan per request |
| Query membaca entity penuh lalu memetakan | Proyeksi `Select` + `AsNoTracking()` di semua jalur baca | Lebih sedikit kolom, tanpa change tracking |
| Pencarian driver terdekat menghitung jarak untuk semua driver | Pra-filter bounding box di SQL, haversine tepat di memori | Tidak lagi memindai seluruh tabel |
| `db.Orders.Count()` + `Sum()` terpisah | Satu `GroupBy` beragregat | Setengah jumlah query pada layar driver & rider |
| Tabel tarif dibaca tiap penetapan harga | Cache 10 menit, di-invalidasi saat admin menyimpan | Menghilangkan query panas |
| Tanpa kompresi | `UseResponseCompression` | Payload mobile lebih kecil |
| Indeks seadanya | Indeks komposit `(Status, CreatedAt)`, `(DriverId, Status, CompletedAt)`, `(RiderId, CreatedAt)`, `(UserId, IsRead, CreatedAt)` | Sesuai pola query nyata |

**Portabilitas:** proyeksi bertingkat (koleksi + subquery berkorelasi dalam satu `Select`)
diterjemahkan menjadi SQL `APPLY` yang **tidak didukung SQLite**. Ditemukan saat pengujian
dan dipecah menjadi query datar di `OrderService.GetDetailAsync`, `ReviewsForUser`, dan
`PendingVerification`. Agregat `GroupBy` yang diproyeksikan langsung ke konstruktor record
juga tidak bisa diterjemahkan EF — diubah lewat tipe anonim.

### 🧩 Modul baru sesuai `Spek.md`

| Modul spesifikasi | Status sebelumnya | Sekarang |
|-------------------|-------------------|----------|
| Reset password | ❌ tidak ada | ✅ `forgot-password` + `reset-password` (kode di cache, 15 menit) |
| Logout | ❌ tidak ada | ✅ mematikan token lewat security stamp |
| Cache (Memory/Redis) | ❌ tidak ada | ✅ `ICacheService`, dipilih lewat `Cache:Provider` |
| Multi-stop trip | ⚠️ entity ada, tanpa endpoint | ✅ `stops[]` saat booking, ikut dihitung tarifnya |
| GPS tracking driver | ❌ tidak ada | ✅ `PUT /location`, `GET /orders/{id}/tracking` dengan ETA |
| Batal order | ❌ tidak ada | ✅ oleh rider/driver/admin + pengembalian kuota promo |
| Verifikasi dokumen driver | ❌ tidak ada | ✅ unggah, tinjau admin, blokir online sebelum disetujui |
| Manajemen tarif | ❌ tidak ada | ✅ CRUD `FareConfig` dari konsol admin |
| Manajemen promo | ⚠️ hanya baca | ✅ CRUD penuh, dinonaktifkan (bukan dihapus) bila sudah terpakai |
| Manajemen pengguna | ❌ tidak ada | ✅ daftar, cari, aktif/nonaktif (langsung memutus sesi) |
| Laporan keuangan | ❌ tidak ada | ✅ kotor/diskon/bersih/komisi + seri harian |
| Ekspor CSV | ❌ tidak ada | ✅ order dan laporan keuangan (dengan BOM agar Excel benar) |
| Notifikasi dibaca | ⚠️ hanya daftar | ✅ jumlah belum dibaca, tandai satu / semua |
| Ulasan dua arah | ⚠️ satu arah | ✅ rider↔driver, satu ulasan per orang per order |

### 🎨 UI/UX

Arah desain: **"dispatch console"** — diambil dari dunia rambu transit dan semantik lampu
lalu lintas, bukan template dashboard generik.

- **Warna berfungsi, bukan hiasan.** Amber = menunggu, jade = jalan/selesai, vermilion =
  berhenti/batal, biru = dalam perjalanan. Satu bahasa warna dipakai di konsol admin dan
  kedua aplikasi mobile, jadi arti status dipelajari sekali.
- **Tipografi:** Barlow Condensed (judul, dari huruf rambu jalan), IBM Plex Sans (teks),
  IBM Plex Mono dengan angka tabular (uang, kode, waktu) supaya kolom angka lurus.
- **Elemen signature:** *Fleet Pulse* — satu pip per driver di atas setiap halaman admin,
  menjawab "berapa banyak armada yang benar-benar bekerja sekarang" jauh lebih baik
  daripada kartu statistik.
- **Ritme hari** digambar sebagai pita jalan dengan penanda jam sekarang.
- Tema terang/gelap, `prefers-reduced-motion` dihormati, fokus keyboard terlihat, tabel
  lebar menggulir di dalam wadahnya sendiri.
- **Chart.js akhirnya dipakai.** Sebelumnya dimuat di setiap halaman tapi tidak pernah
  dipanggil; grafik dibuat dari tinggi `div`.
- Konsol admin sekarang **punya layar login** — sebelumnya tidak ada sama sekali.
- Aplikasi mobile menyimpan sesi di `SecureStorage`, jadi menutup aplikasi tidak lagi
  berarti login ulang.

### 📦 Ketergantungan

| Paket | Perubahan | Alasan |
|-------|-----------|--------|
| `Npgsql.EntityFrameworkCore.PostgreSQL` | 9.0.4 → 10.0.3 | Menghilangkan peringatan NU1608 |
| `Pomelo.EntityFrameworkCore.MySql` → `MySql.EntityFrameworkCore` | 9.0.0 → 10.0.9 | Pomelo belum punya build EF Core 10 |
| `Microsoft.AspNetCore.OpenApi`, `JwtBearer`, EF Core | 10.0.8 → 10.0.9 | Selaras |
| `Microsoft.Extensions.Caching.StackExchangeRedis` | baru | Dukungan cache Redis |
| `FastRide.AdminWeb` → `FastRide.Data` | dihapus | Konsol hanya bicara lewat HTTP |

Dua advisory transitif masih ada (`Microsoft.OpenApi` 2.0.0 lewat `AspNetCore.OpenApi`,
`SQLitePCLRaw` 2.1.11 lewat EF Sqlite). Menaikkannya berarti lompat versi mayor di luar
kendali paket Microsoft; dibiarkan dan dipantau.

---

## v2.1 — Suite pengujian

Utang teknis terbesar dari v2.0 dilunasi: **248 test** (96 unit + 152 integrasi), berjalan
±65 detik tanpa perlu menyalakan API atau menyiapkan database.

Bagian integrasi menjalankan **aplikasi yang sebenarnya** lewat
`WebApplicationFactory<Program>` — autentikasi, otorisasi, batas laju, cache, dan EF Core
yang sama seperti produksi. Tidak ada yang di-*mock*. Rincian ada di
[`docs/TESTING.md`](docs/TESTING.md).

Setiap bug yang diperbaiki di v2.0 kini punya test yang menahannya: double-payment, rute
tanpa otorisasi, penggantian id di URL, logout yang tidak mematikan token, tarif minimum
yang diabaikan, promo yang habis saat sekadar dilihat harganya, dan seterusnya.

### Dua bug yang ditemukan justru saat menulis test

| Temuan | Akibat | Perbaikan |
|--------|--------|-----------|
| **`ConfigureAppConfiguration` datang terlambat** pada minimal hosting | Seluruh suite diam-diam berjalan di `FastRide.db` milik pengembang, bukan database sekali pakai — 132 test gagal karena bentrok email | Pindah ke `builder.UseSetting(...)`, plus `AssertIsIsolated()` yang gagal keras kalau isolasi tidak berlaku |
| **`UseHttpsRedirection` mencopot header `Authorization`** | Test client mengikuti redirect http→https dan kehilangan token; setiap permintaan terautentikasi tiba sebagai anonim | Sakelar `ApiSettings:UseHttpsRedirection` (bawaan `true`) — juga berguna di belakang reverse proxy yang sudah menangani TLS |

Yang kedua bukan sekadar masalah test: perilaku yang sama muncul pada *reverse proxy* mana
pun yang menerminasi TLS lalu meneruskan HTTP ke aplikasi.

### Satu test yang ternyata salah, bukan kodenya

`AvailableOrders_ShowsAnOpenBookingWithItsPickupDistance` gagal karena saya mengira daftar
tawaran diurutkan menurut jarak saja. Ternyata API mengurutkan **yang menunggu paling lama
lebih dulu** — perilaku yang benar untuk keadilan antrean. Testnya yang diperbaiki: kelas ini
sekarang memesan dari Kelapa Gading, ~9 km dari titik Sudirman yang dipakai kelas lain,
sehingga ordernya tidak tertimbun order lama milik test lain.

---

## v2.2 — Sistem pembayaran sungguhan + CI

### Temuan: yang lama adalah pencatat, bukan sistem pembayaran

| Bukti | Dampak |
|-------|--------|
| Setiap `Payment` dibuat langsung `Status = Completed` | Uang tidak pernah berpindah |
| `TransactionReference` dibuat lokal | Bukan referensi gateway |
| `Pending` & `Failed` tidak pernah di-assign | Kode mati |
| QRIS tidak ada | Metode paling umum di Indonesia absen |
| Nol integrasi PSP | Pencarian `midtrans/xendit/webhook` hanya cocok dengan kata "dokumen" |

### Yang dibangun

- **Payment intent**: satu baris per order (unique index tetap), tapi kini mesin status
  `Pending → AwaitingPayment → Completed / Failed / Expired`. Percobaan gagal mengatur ulang
  baris yang sama, jadi jaminan anti-double-charge bertahan **dan** retry mungkin.
- **QRIS** dengan payload EMVCo asli — tag TLV benar, CRC-16/CCITT-FALSE lolos verifikasi,
  bisa dipindai. Di-render jadi SVG di API supaya aplikasi mobile tidak butuh encoder QR.
- **Empat provider** di balik `IPaymentProvider`: `manual` (tunai), `simulated` (demo/test),
  `midtrans`, `xendit`.
- **Konfigurasi ganda**: `appsettings.json` disemai ke database saat start pertama, lalu
  konsol admin menang. Ganti gateway tanpa deploy ulang.
- **Callback terverifikasi**: tanda tangan diperiksa sebelum satu field pun dipercaya,
  idempoten, menolak nominal yang tidak cocok, dan callback terlambat tidak bisa membatalkan
  pelunasan.
- **CI pipeline**: build + 318 test, smoke end-to-end dengan API sungguhan, build MAUI, dan
  pemindai yang menolak commit berisi kunci gateway produksi.

### Cacat desain yang tertangkap lebih dulu

Unique index `Payment.OrderId` dari v2.0 akan **mengunci order selamanya** begitu pembayaran
benar-benar bisa gagal — satu kegagalan, order tidak akan pernah bisa dibayar. Model intent
menyelesaikannya tanpa filtered index (MySQL tidak mendukungnya).

### Dua bug yang ditemukan oleh test yang baru ditulis

| Bug | Akibat | Perbaikan |
|-----|--------|-----------|
| `ChargeAsync` memperlakukan `Pending` sebagai "sudah in-flight" | Pembayaran yang dibuat saat driver menyelesaikan trip tidak pernah bisa dikirim ke provider — rider terkunci memegang order yang tidak bisa dibayar | Penjaga diperketat ke `AwaitingPayment` **dan** punya `ProviderReference` |
| Simulator memakai `(PaymentMethod)Next(1, 5)` | QRIS (nilai 5) tidak pernah teruji sama sekali | Diganti pemilih berbobot yang menyebut metodenya secara eksplisit |

### Satu test lama yang salah, bukan kodenya

`PayingAStartedTrip_ClosesItOut` mengasumsikan "posting pembayaran = lunas" — persis asumsi
yang membuat gateway sungguhan mustahil dipasang. Dipecah jadi dua test yang menyatakan
perilaku sebenarnya: membuka charge **tidak** menutup trip; melunasinya baru menutup.

---

## Cara memverifikasi ulang

```bash
# 1. Suite pengujian — pertahanan pertama, tidak perlu apa pun berjalan
dotnet test FastRide.Tests   # 318 test

# 2. API (hapus FastRide.db dulu kalau skema berubah)
dotnet run --project FastRide.Api

# 3. Konsol admin → https://localhost:5002 (admin@fastride.com / Password123)
dotnet run --project FastRide.AdminWeb

# 4. Simulator 60 detik
dotnet run --project FastRide.Simulator -- --duration 60 --riders 8 --drivers 4
```

Sehat jika: **248/248 test lulus**, simulator melaporkan 0 request gagal (selain `409`
rebutan order yang wajar), dan konsol admin menampilkan order baru bermunculan pada panel
"Ritme hari ini".
