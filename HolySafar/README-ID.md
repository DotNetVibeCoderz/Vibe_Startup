# 🕌 HolySafar - Sistem Manajemen Travel Haji & Umroh

Aplikasi Blazor Server modern dan komprehensif untuk mengelola operasional travel Haji dan Umroh. Dibangun dengan .NET 10, Entity Framework Core, dan integrasi AI Semantic Kernel.

## ✨ Fitur Utama

### 🕌 Untuk Jamaah
- **Pendaftaran Online** - Formulir digital dengan upload dokumen (KTP, Paspor, KK, Vaksin)
- **Informasi Paket** - Browse paket Haji/Umroh lengkap dengan harga, hotel, maskapai, jadwal
- **Pembayaran & Cicilan** - Bayar lunas atau cicil lewat **Xendit, Midtrans, Stripe, QRIS**, atau transfer manual dengan unggah bukti
- **Upload Dokumen** - Unggah KTP, paspor, kartu keluarga, sertifikat vaksin; lihat status verifikasi dan catatan petugas
- **Tracking Proses** - Timeline real-time: pendaftaran → dokumen → pembayaran → visa → SISKOHAT → keberangkatan
- **Itinerary Digital** - Rencana harian dengan jadwal ziarah, transportasi, dan lokasi hotel di peta interaktif
- **Forum Jamaah** - Diskusi berulir dengan lampiran gambar/dokumen
- **Chatbot AI "Syeikh Jenggot"** - Asisten AI 24/7 didukung LLM (OpenAI, Anthropic, Gemini, Ollama)
- **GPS Tracking** - Pemantauan lokasi real-time di peta interaktif
- **Tombol Darurat SOS** - Tombol sekali tekan dengan koordinat GPS
- **Marketplace** - Belanja perlengkapan Haji/Umroh (koper, mukena, sajadah)
- **Edukasi** - Materi manasik, video tutorial, kuis interaktif
- **Chat & Pengumuman** - Pesan dalam aplikasi dan pengumuman komunitas

### 🛫 Untuk Agen Travel
- **Manajemen Paket** - CRUD paket Haji/Umroh dengan brosur
- **Manajemen Jamaah** - Pengelolaan data jamaah lengkap
- **Verifikasi Dokumen & Visa** - Setujui/tolak berkas beserta catatan, kelola status dan nomor visa
- **Dashboard Operasional** - Pantau keberangkatan, visa, laporan keuangan
- **Reminder Otomatis** - Background service mengirim pengingat jatuh tempo, keberangkatan, manasik, dan dokumen yang belum lengkap

### 📊 Untuk Administrator
- **Manajemen User** - Master data dengan CRUD, filter, sort, paging, export CSV/Excel
- **Analitik & Laporan** - Statistik jamaah, keuangan, performa paket
- **Panel SOS** - Monitoring darurat real-time dengan integrasi peta
- **Manajemen Order** - Proses order marketplace (Bayar → Kirim → Selesai)
- **Monitor Transaksi** - Seluruh transaksi payment gateway, lengkap dengan konfirmasi manual
- **Pengaturan Runtime** - Ubah kunci payment, model chatbot, provider storage, dan cadence reminder tanpa restart
- **Backup & Compliance** - Snapshot SQLite konsisten atau ekspor Excel seluruh tabel
- **Integrasi SISKOHAT** - Validasi data jamaah ke Kemenag (endpoint live atau mode simulasi)

## 🚀 Teknologi

| Teknologi | Fungsi |
|-----------|--------|
| **.NET 10** | Runtime |
| **Blazor Server** | Framework UI (Interactive Server rendering) |
| **Entity Framework Core** | ORM dengan SQLite |
| **Semantic Kernel** | Integrasi AI/Chatbot |
| **Markdig** | Render Markdown |
| **ClosedXML** | Export Excel |
| **CsvHelper** | Export CSV |
| **Leaflet.js** | Peta interaktif |
| **Bootstrap Icons** | Library ikon |
| **Xendit / Midtrans / Stripe / QRIS** | Payment gateway |
| **Cookie Authentication + PBKDF2** | Keamanan sesi & password |

## 🏃 Mulai Cepat

### Prasyarat
- .NET 10 SDK
- (Opsional) OpenAI API Key untuk chatbot

### Jalankan

```bash
cd HolySafar
dotnet run
```

Buka http://localhost:5083 (atau https://localhost:7174 dengan `dotnet run --launch-profile https`)

### Akun Demo

| Role   | Username | Password   |
|--------|----------|------------|
| Admin  | `admin`    | `admin123`   |
| Agen   | `agen1`    | `agen123`    |
| Jamaah | `jamaah1`  | `jamaah123`  |

## 📁 Struktur Proyek

```
HolySafar/
├── Components/
│   ├── Layout/          # MainLayout, LoginLayout
│   └── Pages/
│       ├── Admin/       # Users, Jamaah, Paket, Itinerary, Dokumen, Transaksi,
│       │                #   Operasional, Laporan, SOS, Orders, Pengaturan
│       ├── Agen/        # Paket, Jamaah
│       ├── Chatbot.razor    # Chat AI Syeikh Jenggot
│       ├── GpsTracking.razor # Peta GPS
│       ├── Sos.razor        # Tombol Darurat SOS
│       ├── Marketplace.razor # Belanja
│       ├── Edukasi.razor    # Materi pembelajaran
│       ├── PembayaranSaya.razor # Tagihan + payment gateway
│       ├── Tracking.razor    # Timeline proses jamaah
│       ├── Perjalanan.razor  # Itinerary + peta
│       ├── DokumenSaya.razor # Unggah dokumen
│       ├── Forum.razor       # Forum jamaah
│       └── ...              # Home, Login, Paket, Chat, Pengumuman
├── Models/              # Semua model entity
├── Data/                # DbContext, DataSeeder
├── Services/            # Auth, Payment, Storage, Export, Chatbot, GPS,
│                        # Notifikasi, Reminder, Siskohat, Backup, Settings, Localization
├── wwwroot/
│   ├── css/app.css      # Design system lengkap
│   └── uploads/         # Upload file
└── docs/                # Dokumentasi
```

## ⚙️ Konfigurasi

Pengaturan ada di `appsettings.json` **dan bisa di-override saat runtime** dari
**Admin → Pengaturan** (tersimpan di database, tanpa perlu restart):

- **Database**: SQLite (default), SQL Server, MySQL, PostgreSQL
- **Storage**: FileSystem, Azure Blob (default), S3, MinIO
- **Chatbot**: OpenAI (default), Anthropic, Gemini, Ollama
- **Pembayaran**: Manual, Xendit, Midtrans, Stripe, QRIS — lihat [docs/PAYMENT-GATEWAY.md](docs/PAYMENT-GATEWAY.md)
- **Reminder**: aktif/nonaktif, interval, ambang H- pengingat
- **SISKOHAT**: endpoint + API key (kosong = mode simulasi lokal)
- **Tema**: Mode Terang/Gelap
- **Bahasa**: Indonesia (default) / English, bisa diganti dari topbar

## 🔐 Keamanan

- Cookie sesi `hsauth` ditulis server-side dengan `HttpOnly` + `SameSite=Lax` + `Secure`,
  sehingga tidak bisa dibaca atau dipalsukan dari JavaScript.
- Login dan logout berupa form POST yang dilindungi antiforgery token.
- Password memakai PBKDF2-SHA256 (210.000 iterasi, salt per user); hash SHA256 lama
  otomatis di-upgrade saat login berikutnya.
- Setiap route dijaga atribut `[Authorize]` yang ditegakkan `AuthorizeRouteView`.
- Webhook pembayaran ditolak bila callback token / signing secret provider belum dikonfigurasi.

## 🤖 Chatbot AI Syeikh Jenggot

Chatbot AI didukung Semantic Kernel dengan:
- Dukungan multi-model (OpenAI, Anthropic, Gemini, Ollama)
- System prompt, temperature, dan model dapat dikonfigurasi
- Fungsi bawaan: pencarian internet (Tavily), scraping web, matematika, waktu, hitung mundur
- Konteks database untuk respons yang dipersonalisasi
- Multi-session chat dengan history
- Dukungan lampiran gambar dan dokumen
- Render Markdown lengkap (tabel, kode, media)

---

**Dibuat dengan ❤️ oleh Jacky the Code Bender @ Gravicode Studios**

*Traktir pulsa: https://studios.gravicode.com/products/budax*
