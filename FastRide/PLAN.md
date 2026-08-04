# 🚖 FastRide — Roadmap Pengembangan

> Dokumen ini adalah **rencana ke depan**. Untuk catatan apa yang sudah dikerjakan
> dan kapan, lihat [`Progress.md`](Progress.md).

**Status saat ini:** v2.0 — platform berjalan end-to-end (API, konsol admin, dua aplikasi
mobile, simulator) dengan autentikasi yang benar-benar ditegakkan.

---

## Prinsip yang dipegang

1. **API adalah satu-satunya sumber kebenaran.** Tidak ada klien yang menyentuh database
   langsung. Aturan bisnis hidup di API, bukan tersebar di UI.
2. **Kontrak dibagi, bukan diduplikasi.** Semua request/response ada di
   `FastRide.Shared/DTOs`. Klien tidak boleh mendeklarasikan salinannya sendiri.
3. **Setiap endpoint punya pemilik.** Tidak ada rute yang bisa diakses anonim kecuali
   `/api/health` dan grup `/api/auth`.
4. **Portabel di empat database.** Query harus bisa diterjemahkan di SQLite, SQL Server,
   PostgreSQL, dan MySQL — SQLite tidak mendukung `APPLY`, jadi proyeksi bertingkat dipecah.
5. **Bahasa antarmuka: Indonesia.** Pesan error ditulis untuk pengguna, bukan untuk mesin.

---

## Peta rilis

### ✅ v2.0 — Fondasi yang benar (selesai)

Detail lengkap ada di [`Progress.md`](Progress.md).

| Area | Hasil |
|------|-------|
| Keamanan | JWT ditegakkan di semua rute, cek kepemilikan, logout mematikan token, rate limiting |
| Order | Siklus penuh `Requested → Accepted → DriverArrived → Started → Completed` dengan validasi transisi |
| Pembayaran | Satu pembayaran per order (unique index) — perbaikan double-charge |
| Tarif | `MinimumFare` + `SurgeMultiplier` dipakai; harga di aplikasi sama dengan yang ditagih |
| Verifikasi | Unggah & tinjau dokumen driver (SIM/STNK/KTP) |
| Cache | `IMemoryCache` / Redis di balik `ICacheService` |
| UI | Design system "dispatch console" untuk admin + mobile |
| Simulator | Token per aktor, siklus penuh, metrik latensi |
| Pengujian | 318 test menjalankan API sungguhan |
| Pembayaran | QRIS/e-wallet/kartu/VA lewat Midtrans, Xendit, atau sandbox; callback terverifikasi |
| CI | Build, test, smoke end-to-end, MAUI, pindai kredensial |

---

### 🔜 v2.1 — Realtime & notifikasi (target berikutnya)

Prioritas tertinggi. Saat ini semua layar melakukan *polling*; ini boros dan membuat
pengalaman terasa lambat.

| Item | Kenapa | Perkiraan |
|------|--------|-----------|
| **SignalR hub** untuk posisi driver & status order | Menggantikan polling 6–15 detik di rider, driver, dan konsol admin | M |
| **Push notification** (FCM/APNs) | Order masuk harus membangunkan aplikasi driver, bukan menunggu app dibuka | L |
| **Background location** di aplikasi driver | GPS harus tetap terkirim saat layar mati; sekarang hanya saat simulator/manual | M |
| Ganti `EnsureCreated` dengan **EF Migrations** | Perubahan skema tidak boleh lagi berarti "hapus FastRide.db" | S |

---

### 🔜 v2.2 — Pematangan produk

| Item | Kenapa | Perkiraan |
|------|--------|-----------|
| **Peta sungguhan** (Leaflet/Mapbox) di tracking rider & konsol admin | Koordinat sekarang ditampilkan sebagai angka | M |
| **Geocoding alamat** | Alamat masih teks bebas; koordinat di aplikasi rider masih titik tetap Jakarta | M |
| **Dompet & top-up** | E-wallet sudah lewat gateway sungguhan, tapi belum ada saldo internal | L |
| **Refund** | Status `Refunded` ada dan callback-nya ditangani, tapi belum ada endpoint yang memicunya | M |
| **Payout ke driver** | `TotalEarnings` dihitung, pencairannya belum | L |
| **Rekonsiliasi terjadwal** | Status ditanyakan saat dibaca; job berkala akan menutup celah callback yang hilang | M |
| **Penetapan otomatis (auto-dispatch)** | Sekarang driver menarik order; sistem belum menawarkan ke driver terdekat lalu gagal-alih | M |
| **Surge otomatis** berbasis rasio permintaan/pasokan | `SurgeMultiplier` sudah dipakai tapi masih diatur manual admin | S |
| **Chat rider ↔ driver** | Belum ada kanal komunikasi dalam aplikasi | M |

---

### 🔜 v2.3 — Kualitas & operasi

| Item | Kenapa | Perkiraan |
|------|--------|-----------|
| ✅ ~~**Proyek pengujian** (unit + integrasi)~~ | Selesai — 318 test, lihat [`docs/TESTING.md`](docs/TESTING.md) | — |
| ✅ ~~**CI pipeline**~~ | Selesai — lihat [`docs/CI.md`](docs/CI.md) | — |
| **Test terhadap Postgres/SQL Server** | Suite berjalan di SQLite; provider lain hanya diverifikasi manual | M |
| **Test komponen Blazor** (bUnit) | Konsol admin masih diuji manual | M |
| **Health check terstruktur** (`/health/live`, `/health/ready`) | Untuk orkestrator | S |
| **Structured logging + OpenTelemetry** | Diagnosis produksi | M |
| **Docker Compose** (API + Postgres + Redis + MinIO) | Menyamakan lingkungan pengembangan | S |
| **Audit log** untuk tindakan admin | Penonaktifan akun dan perubahan tarif harus terlacak | S |

---

### 💡 Ide yang belum dijadwalkan

- gRPC untuk komunikasi internal (spesifikasi menyebut REST/gRPC; sekarang REST saja)
- Multi-stop lanjutan: penetapan harga per segmen, waktu tunggu di titik singgah
- Program loyalitas / poin
- Penjadwalan perjalanan (pesan untuk nanti)
- Mode korporat (penagihan ke perusahaan)
- Dukungan multi-kota dengan tabel tarif per wilayah

---

## Utang teknis yang diketahui

| Hal | Dampak | Rencana |
|-----|--------|---------|
| Suite hanya berjalan di SQLite | Terjemahan query provider lain tidak terjaga otomatis | v2.3 |
| UI (Blazor & MAUI) belum punya test | Perubahan tampilan hanya diverifikasi manual | v2.3 |
| Belum ada CI | Suite harus dijalankan manual sebelum commit | v2.3 |
| `EnsureCreatedAsync` tanpa migrasi | Ubah entity ⇒ harus hapus database | v2.1 |
| Reset password mengembalikan kode di Development | Belum ada pengirim email | Sambungkan SMTP sebelum produksi |
| Koordinat aplikasi rider masih titik tetap | Perlu izin lokasi + geocoding | v2.2 |
| `Microsoft.OpenApi` & `SQLitePCLRaw` punya advisory transitif | Menunggu paket Microsoft naik versi | Pantau |
| Komisi platform 20% masih konstanta | Seharusnya bisa dikonfigurasi per kota/kategori | v2.2 |

---

## Cara mengukur "selesai"

Sebuah item dianggap selesai jika:

1. Bisa dijalankan lewat `dotnet run` tanpa langkah manual tambahan;
2. Terlihat di konsol admin **atau** di aplikasi mobile (bukan hanya endpoint);
3. `dotnet test FastRide.Tests` hijau, dan perilaku barunya punya test yang menahannya;
4. Simulator masih berjalan 60 detik dengan 0 request gagal;
5. Dokumentasi terkait di `docs/` sudah menyebutkan perilaku barunya.
