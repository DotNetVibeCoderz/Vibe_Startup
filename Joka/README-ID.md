# 🚀 Joka - Online Travel Agent Ultimate

> *"Jelajahi Dunia Tanpa Batas"*

**Joka** adalah aplikasi Online Travel Agent (OTA) komprehensif yang terinspirasi dari Tiket.com dan Traveloka, dibangun dengan **Blazor Server .NET 10**. Pesan tiket pesawat, kereta, hotel, rental mobil, aktivitas, dan paket travel — semua dalam satu platform keren dengan asisten AI bernama **Mas Bolang**!

---

## ✨ Fitur Utama

### 🛫 Travel Inti
- **✈️ Tiket Pesawat:** Pencarian multi-maskapai, filter harga/jadwal, booking langsung
- **🚂 Tiket Kereta:** Siap integrasi KAI, pilih kelas, filter stasiun
- **🚌 Bus & Shuttle:** Rute antar kota, bus dan shuttle door-to-door, filter jenis armada dan kelas
- **🏨 Hotel & Akomodasi:** Booking dengan review, rating, tipe kamar (Hotel, Villa, Resort, Apartment)
- **🚗 Rental Mobil:** Dengan/supir, durasi fleksibel, berbagai tipe kendaraan
- **🎯 Aktivitas & Event:** Konser, tur, workshop, olahraga, atraksi wisata
- **🎁 Paket Travel:** Bundling hemat tiket+hotel+aktivitas

### 💳 Pembayaran & Keuangan
- Multi-pembayaran: Transfer bank, e-wallet, kartu kredit, QRIS
- PayLater: Cicilan tanpa kartu kredit
- Promo & Cashback: Diskon musiman, kode voucher, loyalty points
- Asuransi Perjalanan: Proteksi keterlambatan, pembatalan, kesehatan

### 📊 Pengalaman Pengguna
- 🌓 **Tema Gelap/Terang** — Neo Brutalism Soft + Minimalism + Flat Design
- 🌐 **Multi-bahasa:** Bahasa Indonesia & Inggris
- 💱 **Multi-mata uang:** IDR, USD, SGD, EUR, JPY, dll
- ❤️ **Wishlist & Favorit**
- 🔔 **Notifikasi Real-time** (SignalR)

### 🤖 ChatBot AI — Mas Bolang
- Didukung **Semantic Kernel**
- Multi-model: OpenAI, Anthropic Claude, Google Gemini, Ollama (lokal)
- Chat multi-sesi dengan buat/hapus/reset
- Render Markdown lengkap (tabel, kode, gambar, video)
- Upload lampiran gambar & dokumen
- Kernel Functions: Pencarian Tavily, scraping web, tanggal/waktu, matematika, konversi mata uang
- Function query database: mencari penerbangan, kereta, bus, hotel, aktivitas, paket, promo, dan asuransi milik Joka, serta cek status booking lewat kode — jawabannya dari inventori asli, bukan karangan

### 🔧 Teknis
- **.NET 10 Blazor Server** dengan interactive rendering
- **EF Core** multi-DB: SQLite, SQL Server, MySQL, PostgreSQL
- **Multi-storage:** FileSystem, Azure Blob, AWS S3, MinIO
- **Minimal API** dengan dokumentasi Swagger
- **D3.js** untuk visualisasi data
- Semua pengaturan dapat diubah via `appsettings.json`

---

## 🚀 Mulai Cepat

### Prasyarat
- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
- SQLite (default, tidak perlu setup)

### Menjalankan
```bash
cd Joka
dotnet run
# Buka http://localhost:5000
# Swagger: http://localhost:5000/swagger
```

### Database
Default: **SQLite** (`Data/joka.db` — otomatis dibuat dengan data sampel).

Untuk mengganti database, edit `appsettings.json`:
```json
{
  "Database": {
    "Provider": "SQLServer",  // atau MySQL, Postgre
    "ConnectionStrings": {
      "SQLServer": "Server=...;Database=Joka;..."
    }
  }
}
```

### Setup ChatBot
Konfigurasi provider AI di `appsettings.json`:
```json
{
  "ChatBot": {
    "Provider": "OpenAI",
    "Providers": {
      "OpenAI": {
        "ApiKey": "sk-api-key-anda",
        "Model": "gpt-4o-mini"
      }
    }
  }
}
```

---

## 📁 Struktur Proyek
```
Joka/
├── Components/
│   ├── Layout/          # MainLayout (.razor + .css)
│   └── Pages/           # Halaman Blazor (9 halaman)
├── Models/              # 30+ model domain
├── Services/
│   ├── Chat/            # ChatBotService (Semantic Kernel)
│   ├── Storage/         # Storage multi-provider
│   └── MarkdownService  # Renderer Markdig
├── Data/                # DbContext & Seed Data
├── Program.cs           # Entry point + Minimal API
├── appsettings.json     # Konfigurasi
├── docs/                # Dokumentasi
└── wwwroot/             # Aset statis
```

---

## 🔌 Endpoint API
| Method | Endpoint | Deskripsi |
|--------|----------|-----------|
| GET | `/api/flights` | Cari penerbangan |
| GET | `/api/hotels` | Cari hotel |
| GET | `/api/hotels/{id}` | Detail hotel + kamar + review |
| GET | `/api/trains` | Cari kereta |
| GET | `/api/buses` | Cari bus & shuttle (from, to, date, type) |
| GET | `/api/bus-terminals` | Daftar terminal bus |
| GET | `/api/activities` | Cari aktivitas |
| GET | `/api/promos` | Voucher promo aktif |
| POST | `/api/chat/send` | Kirim pesan ke Mas Bolang |
| GET | `/api/config` | Konfigurasi aplikasi |
| GET | `/api/dashboard/stats` | Statistik dashboard |

---

## 🎨 Sistem Desain
**Neo Brutalism Soft × Minimalism × Flat Design**
- Border tegas dengan shadow lembut
- Warna aksen cerah (#FF5C35 oranye, #FFB800 kuning)
- Toggle tema Gelap/Terang
- Sidebar navigasi responsif
- Animasi halus & skeleton loading

---

## 🛠️ Tech Stack
- **Framework:** Blazor Server (.NET 10)
- **ORM:** Entity Framework Core 9 (Pomelo/MySQL belum rilis untuk EF 10)
- **AI:** Microsoft Semantic Kernel
- **Markdown:** Markdig
- **Chart:** D3.js
- **API Docs:** Swashbuckle / Swagger
- **Storage:** Azure Blob, AWS S3, MinIO
- **DB:** SQLite, SQL Server, MySQL, PostgreSQL

---

## 📝 Lisensi
Dibuat dengan ❤️ oleh **Gravicode Studios** — [Kang Fadhil](https://studios.gravicode.com)

---

## 🇬🇧 English
Lihat [README.md](README.md) untuk dokumentasi dalam Bahasa Inggris.
