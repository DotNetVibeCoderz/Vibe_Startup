# 🚚 Ngibrid Logistics Management Platform

> Platform manajemen logistik modern seperti Tiki, JNE, dan Paxel — dibangun dengan .NET 10, Blazor Server, dan Semantic Kernel.

![.NET](https://img.shields.io/badge/.NET-10.0-blueviolet)
![Blazor](https://img.shields.io/badge/Blazor-Server-purple)
![License](https://img.shields.io/badge/license-MIT-green)

🇬🇧 **English version:** [docs/README-EN.md](docs/README-EN.md)

---

## 📖 Daftar Isi

1. [Fitur Utama](#-fitur-utama)
2. [Prasyarat](#-prasyarat)
3. [Instalasi & Menjalankan](#-instalasi--menjalankan)
4. [Akun Demo](#-akun-demo)
5. [Peta Halaman](#-peta-halaman)
6. [Konfigurasi](#️-konfigurasi)
7. [Database](#-database)
8. [Storage](#-storage)
9. [REST API](#-rest-api)
10. [Chat Bot AI (Mas Supri)](#-chat-bot-ai-mas-supri)
11. [Simulator Latar Belakang](#-simulator-latar-belakang)
12. [Master Data Kota](#-master-data-kota)
13. [Sample Data](#-sample-data)
14. [Tech Stack](#-tech-stack)
15. [Dokumentasi Lengkap](#-dokumentasi-lengkap)

---

## 🚀 Fitur Utama

### 📦 Core Logistics
- **Order Management** — pembuatan pesanan, nomor resi harian, riwayat status, label pengiriman dengan **QR code + barcode Code128** siap cetak. Alamat pengirim/penerima dipilih lewat **combobox propinsi → kota** yang bersumber dari master data kota (lihat di bawah).
- **Shipment Tracking** — posisi GPS real-time lewat SignalR, peta Leaflet, update status otomatis, notifikasi multi-kanal.
- **Delivery Scheduling** — penjadwalan kurir, **optimasi rute** (nearest-neighbour + 2-opt), estimasi ETA dan penghematan jarak.
- **Pickup Request** — permintaan penjemputan dari rumah/kantor beserta penugasan kurir.

### 💳 Payment & Finance
- **Multi-payment** — e-wallet, transfer bank, COD, kartu kredit. Tombol **Bayar** di halaman
  Pesanan membuka tagihan (pilih metode → channel) dan menampilkan instruksi pembayaran;
  pelunasan diverifikasi petugas di halaman Pembayaran (`/payment`, khusus Admin/Manager).
- **Invoice & Billing** — invoice HTML siap cetak, e-receipt, rekap keuangan.
- **Asuransi** — premi otomatis dari nilai barang, pengajuan dan review klaim.

### 🏭 Warehouse & Inventory
- Multi-gudang dengan kapasitas, lokasi penyimpanan (rak/zona), **RFID & barcode**.
- Pencatatan stok masuk/keluar (movement) beserta batch dan tanggal kedaluwarsa.
- **Optimasi kemasan** — rekomendasi ukuran box, berat volumetrik (P×L×T/6000).
- **Sensor IoT** — monitoring suhu & kelembaban, alarm rantai dingin.

### 👥 Customer, Courier & Support
- **Portal pelanggan** — tracking, riwayat transaksi, profil dengan **unggah foto profil**
  (JPG/PNG/GIF/WebP, maks 5 MB, tersimpan lewat provider `Storage` yang aktif), dan
  **loyalty program** (Bronze → Platinum, tukar poin jadi diskon).
- **Aplikasi kurir** — daftar tugas harian, rute teroptimasi, update status di lapangan, komunikasi dengan pelanggan.
- **Customer support** — chatbot, live chat SignalR, tiket komplain dengan prioritas & SLA.

### 📊 Analytics & Dashboard
- **Business analytics** — volume pengiriman, revenue, kepatuhan SLA, performa kurir.
- **Dashboard operasional** — snapshot real-time (order aktif, kurir online, gudang, alert).
- **Analisis tren** — prediksi permintaan (Holt exponential smoothing + musiman mingguan), deteksi **peak season**, insight optimasi biaya.
- Seluruh grafik digambar dengan **D3.js** (line/area, donut, bar, forecast band, sparkline) dan mengikuti tema terang/gelap.

### 🔌 Integrasi
- **Marketplace** — sinkronisasi pesanan Tokopedia & Shopee, push status balik ke marketplace.
- **ERP / CRM** — konektor generik dengan log sinkronisasi per integrasi.
- **3PL & lintas negara** — perbandingan tarif mitra logistik dan serah-terima paket.
- **IoT** — smart locker (kompartemen, PIN, kedaluwarsa otomatis), sensor suhu, RFID.

### 🔒 Security & Compliance
- Login, Register, Logout, Profil, **Reset Password** (wajib token), ganti password.
- **Role-based access** — Admin, Manager, WarehouseStaff, Courier, Customer dengan policy per endpoint dan per halaman.
- **Audit trail** — aksi, entitas, nilai lama/baru, IP, dan user agent.
- **Kepatuhan regulasi** — pencatatan pajak (PPN/PPh), deklarasi bea cukai dengan aturan *de minimis*, dokumen ekspor-impor.

### 🤖 AI
- **Mas Supri Chat Bot** — multi-model (OpenAI, Anthropic, Gemini, Ollama) dengan **function calling di keempat provider**.
- **Route Optimization** — heuristik 2-opt untuk rute tercepat/termurah.
- **Dynamic Pricing** — tarif berbasis zona jarak, jam sibuk, akhir pekan, dan tingkat permintaan.
- **Green Logistics** — perhitungan emisi karbon per pengiriman, opsi eco-delivery, carbon offset.

---

## 📋 Prasyarat

- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
- Database (pilih salah satu): **SQLite** (default, tanpa instalasi), SQL Server, MySQL 8.0+, PostgreSQL 15+
- Opsional untuk Chat Bot: API key OpenAI / Anthropic / Gemini, atau [Ollama](https://ollama.com) lokal
- Opsional untuk pencarian internet: API key [Tavily](https://tavily.com)

Aplikasi tetap berjalan penuh **tanpa satu pun API key** — fitur AI akan memberi pesan yang mengarahkan ke halaman Pengaturan.

---

## 🔧 Instalasi & Menjalankan

```bash
git clone https://github.com/your-org/ngibrid.git
cd ngibrid

dotnet restore
dotnet build          # 0 error, 0 warning
dotnet run            # atau: dotnet watch run (hot reload)
```

Alamat aplikasi (lihat `Properties/launchSettings.json`):

| Endpoint | URL |
|---|---|
| Aplikasi | http://localhost:5182 · https://localhost:7061 |
| Swagger UI | http://localhost:5182/api/docs *(hanya Development)* |
| Health check | http://localhost:5182/api/v1/health |

Publikasi rilis:

```bash
dotnet publish -c Release
```

> **Catatan skema database.** Skema dibuat dengan `EnsureCreatedAsync()`, bukan migration.
> Bila Anda mengubah entitas, hapus `Data/ngibrid.db` (atau database target) agar skema baru terbentuk.

---

## 👤 Akun Demo

Seeder mengisi database saat pertama kali dijalankan.

| Email | Password | Peran |
|---|---|---|
| admin@ngibrid.com | `Admin123!` | Admin |
| manager@ngibrid.com | `Manager123!` | Manager |
| staff@ngibrid.com | `Staff123!` | Warehouse Staff |
| courier1@ngibrid.com … courier3@ | `Courier123!` | Kurir |
| customer1@ngibrid.com … customer5@ | `Customer123!` | Pelanggan |

Halaman login menyediakan tombol pintasan untuk mengisi kredensial demo.

---

## 🗺 Peta Halaman

| Rute | Halaman | Akses |
|---|---|---|
| `/` · `/dashboard` | Dashboard operasional + grafik D3 | semua |
| `/orders` | Daftar & pembuatan pesanan, cetak label QR/barcode | login |
| `/tracking` · `/tracking/{resi}` | Lacak kiriman + **peta live** (marker bergerak saat simulasi GPS) | publik |
| `/pickup` | Permintaan penjemputan | login |
| `/payment` | Pembayaran, invoice, klaim asuransi | login |
| `/warehouse` | Gudang, inventaris, sensor IoT, **peta jaringan gudang** | staff |
| `/courier` | Tugas kurir, **peta armada** + peta rute teroptimasi | kurir |
| `/analytics` | Prediksi permintaan, tren, insight biaya, emisi | manajemen |
| `/integrations` | Marketplace, ERP/CRM, mitra 3PL | admin/manager |
| `/compliance` | Pajak, bea cukai, dokumen ekspor-impor | admin/manager |
| `/locker` | Smart locker & kompartemen + **peta lokasi locker** | staff |
| `/notifications` | Pusat notifikasi | login |
| `/support` | Tiket komplain & live chat | login |
| `/chat` | Chat Bot Mas Supri | login |
| `/profile` | Profil, loyalty, ganti password | login |
| `/settings` | Seluruh konfigurasi aplikasi | admin/manager |
| `/login` · `/register` · `/forgot-password` · `/reset-password` | Autentikasi | publik |
| `/access-denied` | Halaman "hak akses kurang" | publik |

---

## ⚙️ Konfigurasi

Semua parameter berada di `appsettings.json` **dan dapat diubah langsung dari aplikasi** melalui
halaman **Pengaturan** (`/settings`). Perubahan ditulis kembali ke file lalu konfigurasi di-*reload*,
sehingga berlaku tanpa restart (kecuali penggantian provider database).

```json
{
  "Database":  { "Provider": "SQLite", "ConnectionStrings": { "…": "…" } },
  "Storage":   { "Provider": "FileSystem", "BasePath": "wwwroot/uploads", "MaxFileSizeMb": 25 },
  "ChatBot":   { "DefaultModel": "OpenAI", "Temperature": 0.7, "Models": { "…": {} } },
  "AI":        { "DynamicPricing": { "BaseFare": 9000, "RatePerKm": 22 } },
  "GPS":       { "Simulator": { "Enabled": true, "UpdateIntervalMs": 5000 } },
  "IoT":       { "Simulator": { "Enabled": true }, "LockerSimulator": { "Enabled": true } },
  "Loyalty":   { "PointsPerRupiah": 0.0001, "RupiahPerPoint": 100 },
  "Compliance":{ "Customs": { "DutyRate": 0.075, "ImportVatRate": 0.11 } }
}
```

Daftar lengkap setiap key beserta nilai defaultnya ada di **[docs/CONFIGURATION.md](docs/CONFIGURATION.md)**.

### Model tarif

Kg pertama = `BaseFare + jarak × RatePerKm`; kg berikutnya 60% dari itu, lalu dikali pengali layanan
(ECO/REG/EXP/SAMEDAY), jam sibuk, akhir pekan, dan permintaan. Jarak dihitung Haversine antar
koordinat kota dari master data, dikali faktor jalan 1,3 untuk jarak darat (≤600 km) atau 1,12 untuk
leg yang praktis ditempuh udara/laut.

---

## 🗺 Master Data Kota

Tabel `Cities` berisi **seluruh 514 kota/kabupaten di 38 propinsi Indonesia** — kolomnya `Id`,
`Country`, `Province`, `Name`, `Type` (KOTA/KABUPATEN), `SeatName`, `Latitude`, `Longitude`.
Koordinatnya adalah **ibu kota (pusat pemerintahan)** daerah tersebut, bukan titik tengah geometris,
karena paket dikirim ke kotanya.

Semua perhitungan jarak, tarif, emisi, peta, dan optimasi rute memakai tabel ini, jadi rute mana pun
di Indonesia terhitung presisi — bukan cuma segelintir kota besar.

- Data awal ada di `Data/IndonesiaCities.cs` dan diisi otomatis saat tabel `Cities` masih kosong.
  Baris yang ditambah/dikoreksi lewat database menang atas daftar bawaan itu.
- `Name` menyimpan nama telanjang, `Type` membedakan kota dari kabupaten. **26 nama dipakai
  keduanya** (Bandung, Bogor, Cirebon, Tasikmalaya, Solok, Sorong, …) dan letak keduanya bisa
  berjauhan — makanya propinsi ikut disimpan di pesanan dan ikut dikirim saat menghitung ongkir.
- Pencarian nama toleran: `Kab. Bandung`, `KABUPATEN BANDUNG`, dan nama ibu kota `Soreang`
  sama-sama ketemu. Nama telanjang `Bandung` dianggap **Kota** Bandung.

```bash
curl "http://localhost:5182/api/v1/provinces"
curl "http://localhost:5182/api/v1/cities?province=Papua%20Tengah"
curl "http://localhost:5182/api/v1/cities/distance?from=Kota%20Bandung&to=Kota%20Surabaya"
```

---

## 🗄 Database

Default **SQLite** (`Data/ngibrid.db`, dibuat otomatis). Ganti lewat `/settings` atau `appsettings.json`:

```json
"Database": {
  "Provider": "SQLServer",
  "ConnectionStrings": {
    "SQLite":    "Data Source=Data/ngibrid.db",
    "SQLServer": "Server=.;Database=NgibridDb;Trusted_Connection=true;TrustServerCertificate=true;",
    "MySQL":     "Server=localhost;Database=NgibridDb;User=root;Password=root;",
    "Postgre":   "Host=localhost;Database=NgibridDb;Username=postgres;Password=postgres;"
  }
}
```

Ganti provider database memerlukan **restart aplikasi**.

---

## 📁 Storage

`Storage:Provider` mendukung `FileSystem`, `AzureBlob`, `S3`, dan `MinIO` — dipakai untuk lampiran
chat, foto bukti pengiriman, dan dokumen kepatuhan. Validasi ekstensi serta ukuran berjalan sebelum
berkas dibaca.

---

## 📡 REST API

Minimal API di bawah `/api/v1` dengan Swagger UI di `/api/docs`. Autentikasi memakai cookie Identity
dari `/api/auth/login`.

```bash
# login lalu simpan cookie
curl -c cookie.txt -X POST http://localhost:5182/api/auth/login \
  -H "Content-Type: application/json" \
  -d '{"email":"admin@ngibrid.com","password":"Admin123!"}'

# lacak resi (publik)
curl "http://localhost:5182/api/v1/orders/track/NGB2607250001ABCD"

# daftar propinsi & kota (publik)
curl "http://localhost:5182/api/v1/provinces"
curl "http://localhost:5182/api/v1/cities?province=Bali"

# bandingkan tarif semua layanan (propinsi opsional, tapi bikin presisi)
curl "http://localhost:5182/api/v1/pricing/compare?origin=Kota%20Bandung&originProvince=Jawa%20Barat&dest=Kota%20Surabaya&destProvince=Jawa%20Timur&weight=3"

# prediksi permintaan 14 hari
curl -b cookie.txt "http://localhost:5182/api/v1/analytics/forecast?days=14"
```

Referensi lengkap seluruh endpoint beserta level aksesnya: **[docs/API.md](docs/API.md)**.

---

## 🤖 Chat Bot AI (Mas Supri)

Asisten virtual berbasis **Semantic Kernel** di halaman `/chat`.

- **Empat model**, dipilih per sesi: OpenAI, Anthropic, Gemini, Ollama.
  Semantic Kernel belum punya konektor resmi untuk Anthropic dan Gemini, sehingga Ngibrid
  menerjemahkan metadata `KernelFunction` menjadi skema tool masing-masing API —
  hasilnya **keempat model memakai kumpulan fungsi yang sama**.
- **Multi-session** — buat, pilih, reset, dan hapus sesi; judul dibuat otomatis dari pesan pertama.
- **Lampiran** — gambar dikirim sebagai *image content* (model benar-benar melihatnya), dokumen
  diunggah lalu tautannya dibaca lewat fungsi `read_file_from_url`.
- **Kernel functions** — lacak resi, daftar pesanan, cek ongkir, info gudang, ketersediaan kurir,
  statistik order, prediksi permintaan, tanggal/waktu, kalkulasi matematika, **pencarian internet
  (Tavily)**, *scraping* URL, baca berkas dari URL, hitung volume & emisi, opsi mitra 3PL, FAQ,
  buat tiket, poin loyalty, cari smart locker, dan notifikasi.
- **Markdown lengkap** — tabel, blok kode, gambar, serta media (YouTube/mp4/mp3) menjadi player
  tertanam. HTML mentah dinonaktifkan demi keamanan.

Detail: **[docs/CHATBOT.md](docs/CHATBOT.md)**.

---

## 🛰 Simulator Latar Belakang

Tiga `BackgroundService` berjalan di thread terpisah dan dapat dimatikan lewat konfigurasi:

| Simulator | Fungsi | Interval default |
|---|---|---|
| `GpsSimulatorService` | Menggerakkan kurir sepanjang rute, menyiarkan posisi lewat SignalR | 5 detik |
| `IotSimulatorService` | Sensor suhu & kelembaban gudang, alarm rantai dingin ke staf | 10 detik |
| `SmartLockerSimulatorService` | Heartbeat locker, penurunan baterai, kedaluwarsa kompartemen | 30 detik |

---

## 📊 Sample Data

`DataSeeder` mengisi: 10 pengguna dengan 5 peran, 4 gudang, 3 kurir beserta kendaraan, katalog
layanan, **±375 pesanan tersebar 120 hari ke belakang** (pola harian mingguan + dua lonjakan
kampanye, sehingga grafik volume, SLA, tren bulanan, dan prediksi punya deret nyata) lengkap
dengan riwayat status, pembayaran, invoice, klaim asuransi,
inventaris + pergerakan stok, integrasi marketplace/ERP/CRM, mitra 3PL, smart locker, tiket support,
permintaan pickup, transaksi loyalty, catatan pajak, dan deklarasi bea cukai.

> Seeder berhenti sepenuhnya bila sudah ada baris pengguna — hapus `Data/ngibrid.db` untuk memuat ulang.

---

## 📚 Tech Stack

| Bidang | Teknologi |
|---|---|
| Framework | .NET 10, ASP.NET Core |
| UI | Blazor Server (Interactive Server), CSS custom property, tema terang/gelap |
| Grafik & peta | D3.js v7, Leaflet — keduanya di-*vendor* lokal, tanpa CDN |
| Data | EF Core 9 (SQLite / SQL Server / MySQL / PostgreSQL) |
| Real-time | SignalR (Tracking, Chat, Notification, Courier hub) |
| AI | Semantic Kernel + konektor HTTP untuk Anthropic & Gemini |
| Storage | FileSystem, Azure Blob, AWS S3, MinIO |
| Auth | ASP.NET Core Identity (kunci `long`), cookie, policy |
| API | Minimal API + Swagger |
| Lain-lain | Markdig, QRCoder, encoder Code128 buatan sendiri |

---

## 📖 Dokumentasi Lengkap

| Dokumen | Isi |
|---|---|
| [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md) | Lapisan, struktur folder, keputusan desain, alur data |
| [docs/API.md](docs/API.md) | Referensi seluruh endpoint REST + level akses |
| [docs/CHATBOT.md](docs/CHATBOT.md) | Model, kernel function, lampiran, keamanan chat bot |
| [docs/CONFIGURATION.md](docs/CONFIGURATION.md) | Seluruh key `appsettings.json` dan catatan produksi |
| [docs/README-EN.md](docs/README-EN.md) | English version of this README |

---

## 📄 Lisensi

MIT License

---

**Dibuat dengan ❤️ oleh Tim Ngibrid | Gravicode Studios**
