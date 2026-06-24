# 🕌 HolySafar - Sistem Manajemen Travel Haji & Umroh

Aplikasi Blazor Server modern dan komprehensif untuk mengelola operasional travel Haji dan Umroh. Dibangun dengan .NET 10, Entity Framework Core, dan integrasi AI Semantic Kernel.

## ✨ Fitur Utama

### 🕌 Untuk Jamaah
- **Pendaftaran Online** - Formulir digital dengan upload dokumen (KTP, Paspor, KK, Vaksin)
- **Informasi Paket** - Browse paket Haji/Umroh lengkap dengan harga, hotel, maskapai, jadwal
- **Pembayaran & Cicilan** - Pantau status pembayaran dengan opsi cicilan
- **Tracking Dokumen** - Update real-time status dokumen, visa, keberangkatan
- **Chatbot AI "Syeikh Jenggot"** - Asisten AI 24/7 didukung LLM (OpenAI, Anthropic, Gemini, Ollama)
- **GPS Tracking** - Pemantauan lokasi real-time di peta interaktif
- **Tombol Darurat SOS** - Tombol sekali tekan dengan koordinat GPS
- **Marketplace** - Belanja perlengkapan Haji/Umroh (koper, mukena, sajadah)
- **Edukasi** - Materi manasik, video tutorial, kuis interaktif
- **Chat & Pengumuman** - Pesan dalam aplikasi dan pengumuman komunitas

### 🛫 Untuk Agen Travel
- **Manajemen Paket** - CRUD paket Haji/Umroh dengan brosur
- **Manajemen Jamaah** - Pengelolaan data jamaah lengkap
- **Verifikasi Dokumen** - Validasi dan tracking dokumen jamaah
- **Dashboard Operasional** - Pantau keberangkatan, visa, laporan keuangan
- **Notifikasi** - Pengingat pembayaran, jadwal manasik, update keberangkatan

### 📊 Untuk Administrator
- **Manajemen User** - Master data dengan CRUD, filter, sort, paging, export CSV/Excel
- **Analitik & Laporan** - Statistik jamaah, keuangan, performa paket
- **Panel SOS** - Monitoring darurat real-time dengan integrasi peta
- **Manajemen Order** - Proses order marketplace (Bayar → Kirim → Selesai)

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

## 🏃 Mulai Cepat

### Prasyarat
- .NET 10 SDK
- (Opsional) OpenAI API Key untuk chatbot

### Jalankan

```bash
cd HolySafar
dotnet run
```

Buka https://localhost:5000

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
│       ├── Admin/       # Users, Jamaah, Paket, Operasional, Laporan, SOS, Orders
│       ├── Agen/        # Paket, Jamaah
│       ├── Chatbot.razor    # Chat AI Syeikh Jenggot
│       ├── GpsTracking.razor # Peta GPS
│       ├── Sos.razor        # Tombol Darurat SOS
│       ├── Marketplace.razor # Belanja
│       ├── Edukasi.razor    # Materi pembelajaran
│       └── ...              # Home, Login, Paket, Chat, Pengumuman
├── Models/              # Semua model entity
├── Data/                # DbContext, DataSeeder
├── Services/            # Auth, Storage, Export, Chatbot, GPS, Notification
├── wwwroot/
│   ├── css/app.css      # Design system lengkap
│   └── uploads/         # Upload file
└── docs/                # Dokumentasi
```

## ⚙️ Konfigurasi

Semua pengaturan di `appsettings.json`:

- **Database**: SQLite (default), siap SQL Server, MySQL, PostgreSQL
- **Storage**: FileSystem (default), siap Azure Blob, S3, MinIO
- **Chatbot**: OpenAI (default), siap Anthropic, Gemini, Ollama
- **Tema**: Mode Terang/Gelap
- **Bahasa**: Indonesia (default), siap English

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
