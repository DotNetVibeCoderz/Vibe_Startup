# 🏠 JuraganKost - Aplikasi Manajemen Kostan

**JuraganKost** adalah aplikasi manajemen kostan modern berbasis web yang dibangun dengan .NET 10 Blazor Server. Aplikasi ini dirancang untuk memenuhi kebutuhan **pemilik kost**, **admin operasional**, dan **penghuni** dalam satu platform terintegrasi.

---

## ✨ Fitur Utama

### 🏠 Untuk Pemilik & Admin
- ✅ **Dashboard Kost** - Ringkasan okupansi, pemasukan, pengeluaran, dan status kamar
- ✅ **Manajemen Kamar** - CRUD kamar, status (kosong/terisi/booking), harga, fasilitas
- ✅ **Pengelolaan Penghuni** - Data penghuni, kontrak, riwayat pembayaran, kontak darurat
- ✅ **Keuangan & Tagihan** - Pembuatan tagihan otomatis (bulanan), tracking pembayaran
- ✅ **Laporan Keuangan** - Grafik pemasukan, pengeluaran, profit, piutang
- ✅ **Manajemen Inventaris** - Catatan barang, status perawatan, penggantian
- ✅ **Notifikasi & Reminder** - Pengingat pembayaran, kontrak habis, perawatan rutin
- ✅ **Manajemen Staff** - Data penjaga, cleaning service, jadwal kerja, gaji

### 👤 Untuk Penghuni
- ✅ **Pendaftaran Online** - Booking kamar, upload dokumen
- ✅ **Pembayaran Online** - Tracking pembayaran dengan berbagai metode
- ✅ **Portal Penghuni** - Akses tagihan, kontrak, riwayat pembayaran
- ✅ **Layanan Komplain** - Form pengaduan (air, listrik, fasilitas rusak), tracking status
- ✅ **Notifikasi Digital** - Reminder pembayaran, pengumuman

### ⚙️ Backend & API
- ✅ **Autentikasi & Role** - Login pemilik, admin, penghuni (ASP.NET Identity)
- ✅ **REST API** - MinAPI dengan Swagger untuk integrasi eksternal
- ✅ **IoT Simulator** - Dashboard sensor listrik, air, suhu, kelembaban
- ✅ **Multi-Database** - SQLite, SQL Server, PostgreSQL, MySQL
- ✅ **Export Data** - CSV & Excel untuk kamar dan penghuni
- ✅ **Storage Support** - FileSystem, dapat diperluas ke AzureBlob, S3, MinIO

### 🤖 Chat Bot "Mpok Inem"
- ✅ Chat page interaktif dengan tampilan modern
- ✅ Multi-session & reset
- ✅ File/image attachment
- ✅ Keyword-based response system (siap integrasi Semantic Kernel + LLM)
- ✅ Konfigurasi di appsettings

### 🌟 Fitur Kompetitif
- ✅ **Marketplace Kost** - Listing kost untuk publik
- ✅ **Rating & Review** - Feedback dari penghuni
- ✅ **Multi Kost Management** - Satu akun kelola banyak properti
- ✅ **Dark/Light Mode** - Tampilan neo-brutalism soft
- ✅ **Responsive Design** - Mobile-friendly

---

## 🚀 Quick Start

### Prasyarat
- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
- SQLite (default, sudah include)

### Menjalankan Aplikasi

```bash
# Clone repository
git clone https://github.com/your-repo/JuraganKost.git
cd JuraganKost

# Jalankan aplikasi
dotnet run

# Buka browser
# Aplikasi: https://localhost:5001
# Swagger API: https://localhost:5001/swagger
```

### Akun Demo

| Role | Email | Password |
|------|-------|----------|
| Super Admin | superadmin@juragankost.com | Admin123! |
| Pemilik | pemilik@juragankost.com | Pemilik123! |
| Admin | admin@juragankost.com | Admin123! |
| Penghuni | penghuni1@juragankost.com | Penghuni123! |

---

## 📁 Struktur Proyek

```
JuraganKost/
├── Api/                    # REST API (MinAPI + Swagger)
│   └── ApiEndpoints.cs
├── Components/
│   ├── Layout/             # MainLayout, MinimalLayout
│   ├── Pages/              # Semua halaman Blazor
│   │   ├── Auth/           # Login, Register, Logout
│   │   ├── Chat.razor      # Chat Bot "Mpok Inem"
│   │   ├── IoTMonitor.razor # IoT Dashboard
│   │   └── ...
│   └── Shared/             # Shared components
├── Data/
│   ├── Models/             # Domain & Identity models
│   └── Context/            # EF Core DbContext
├── Services/               # Business logic services
├── wwwroot/                # Static files & CSS
├── docs/                   # Dokumentasi
├── appsettings.json        # Konfigurasi aplikasi
├── Program.cs              # Entry point
└── PLAN.md                 # Development plan
```

---

## ⚙️ Konfigurasi

### Database
Edit `appsettings.json` untuk mengganti provider database:

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

### Chat Bot
```json
{
  "ChatBot": {
    "Name": "Mpok Inem",
    "ModelProvider": "OpenAI",
    "Providers": {
      "OpenAI": { "ModelId": "gpt-4o", "ApiKey": "YOUR_KEY" },
      "Ollama": { "ModelId": "llama3.1:latest", "Endpoint": "http://localhost:11434" }
    }
  }
}
```

---

## 🔧 Teknologi

- **Backend**: .NET 10, ASP.NET Core, Blazor Server
- **Database**: Entity Framework Core (SQLite, SQL Server, PostgreSQL, MySQL)
- **Auth**: ASP.NET Identity
- **API**: Minimal API + Swagger
- **Export**: ClosedXML (Excel), CsvHelper
- **UI**: Neo-brutalism soft theme, responsive
- **Markdown**: Markdig

---

## 📝 License

MIT License - Gravicode Studios © 2025

---

Dibuat dengan ❤️ oleh **Jacky the Code Bender** dari **Gravicode Studios**  
_Pimpinan: Kang Fadhil_

---

> **Butuh bantuan?** Buka halaman **Mpok Inem** di aplikasi atau hubungi kami di [studios.gravicode.com](https://studios.gravicode.com)
