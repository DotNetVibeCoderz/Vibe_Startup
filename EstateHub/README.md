# 🏠 EstateHub - Property Management Platform

[![.NET](https://img.shields.io/badge/.NET-10.0-blueviolet)](https://dotnet.microsoft.com/)
[![Blazor](https://img.shields.io/badge/Blazor-Server-purple)](https://dotnet.microsoft.com/apps/aspnet/web-apps/blazor)
[![License](https://img.shields.io/badge/license-MIT-green)](LICENSE)

> Platform manajemen properti modern dengan AI, peta interaktif, simulasi KPR, dan chatbot pintar "Tante Rita".

---

## 📑 Daftar Isi

- [Fitur Utama](#-fitur-utama)
- [Tech Stack](#-tech-stack)
- [Struktur Proyek](#-struktur-proyek)
- [Instalasi](#-instalasi)
- [Konfigurasi](#-konfigurasi)
- [Database](#-database)
- [API](#-api)
- [ChatBot AI](#-chatbot-ai)
- [Pengembangan](#-pengembangan)
- [Tim](#-tim)

---

## ✨ Fitur Utama

### 🏠 Untuk Pembeli/Penyewa
- 🔍 **Pencarian Properti** dengan filter lokasi, harga, tipe, luas, fasilitas
- 🗺️ **Peta Interaktif** integrasi Leaflet/OpenStreetMap dengan pin properti
- ❤️ **Favorit & Wishlist** simpan properti yang diminati
- 🎥 **Virtual Tour** foto 360°, video walkthrough
- 📅 **Booking & Jadwal** buat janji temu dengan agen
- 💰 **Simulasi KPR** kalkulator cicilan lengkap dengan amortisasi
- 💬 **Chat & Call** komunikasi dengan agen via WhatsApp & built-in chat
- 💳 **Pembayaran Online** integrasi e-wallet & transfer bank
- ⭐ **Review & Rating** ulasan properti dan agen

### 🏢 Untuk Pemilik/Agen
- 📝 **Listing Properti** unggah foto, deskripsi, harga
- 📊 **Dashboard Penjualan** grafik, leads, performa iklan
- 📄 **Kontrak Digital** e-signature, template, generate dengan AI
- 📢 **Promosi & Iklan** paket premium, push marketplace
- 👥 **CRM Agen** manajemen leads, follow-up otomatis
- 🔔 **Notifikasi Real-time** update status & booking baru

### ⚙️ Untuk Admin
- 👤 **Manajemen User** kontrol akun
- ✅ **Verifikasi Dokumen** KTP, sertifikat, IMB
- 💵 **Monitoring Transaksi** audit pembayaran
- 📈 **Laporan & Analitik** statistik, tren harga

### 🚀 Fitur Kompetitif
- 🤖 **AI Tante Rita** chatbot pintar 24/7 berbasis LLM
- 🧠 **AI Recommendation** rekomendasi properti sesuai profil
- 📉 **Price Prediction** analisis tren harga
- 🏛️ **Integrasi Pajak** hitung PPh, BPHTB
- 🌐 **Multi-language & Multi-currency**

---

## 🛠️ Tech Stack

| Layer | Technology |
|-------|-----------|
| **Framework** | .NET 10, ASP.NET Core |
| **UI** | Blazor Server, MudBlazor |
| **Database** | SQLite / SQL Server / MySQL / PostgreSQL |
| **ORM** | Entity Framework Core |
| **AI/ML** | Semantic Kernel, ML.NET |
| **Maps** | Leaflet.js + OpenStreetMap |
| **Real-time** | SignalR |
| **Storage** | File System / Azure Blob / S3 / MinIO |
| **API** | Minimal API + Swagger |
| **Export** | ClosedXML (Excel), CsvHelper |

---

## 📁 Struktur Proyek

```
EstateHub/
├── Components/
│   ├── Layout/           # MainLayout, NavMenu
│   ├── Pages/
│   │   ├── Admin/        # Dashboard admin
│   │   ├── Agent/        # Dashboard agen
│   │   ├── Chat/         # ChatBot Tante Rita
│   │   ├── Property/     # Search, Detail, Map
│   │   └── User/         # KPR, Profile, etc.
│   └── Shared/           # Reusable components
├── Data/
│   └── AppDbContext.cs   # EF Core context
├── Models/               # Domain entities
├── Services/             # Business logic
├── wwwroot/              # Static files
├── docs/                 # Documentation
├── appsettings.json      # Configuration
├── Program.cs            # App entry point
└── README.md
```

---

## 🚀 Instalasi

### Prerequisites
- .NET 10 SDK
- SQLite (default) or other database

### Quick Start

```bash
# Clone repository
git clone https://github.com/gravicode/estatehub.git
cd EstateHub

# Restore dependencies
dotnet restore

# Run the application
dotnet run

# Open browser
# https://localhost:5001
```

### Visual Studio
1. Buka `EstateHub.csproj` di Visual Studio 2022+
2. Tekan F5 untuk menjalankan

---

## ⚙️ Konfigurasi

### Database Provider
Edit `appsettings.json`:
```json
{
  "DatabaseProvider": "SQLite",  // SQLite | SqlServer | MySql | PostgreSql
  "ConnectionStrings": {
    "DefaultConnection": "Data Source=EstateHub.db",
    "SqlServer": "Server=localhost;Database=EstateHub;...",
    "MySql": "Server=localhost;Database=EstateHub;...",
    "PostgreSql": "Host=localhost;Database=EstateHub;..."
  }
}
```

### AI ChatBot (Tante Rita)
```json
{
  "ChatBot": {
    "Provider": "OpenAI",  // OpenAI | Anthropic | Gemini | Ollama
    "Models": {
      "OpenAI": {
        "ApiKey": "your-api-key",
        "ModelId": "gpt-4o"
      }
    },
    "Temperature": 0.7,
    "MaxTokens": 4096
  }
}
```

### Storage Provider
```json
{
  "StorageProvider": "FileSystem",  // FileSystem | AzureBlob | S3 | MinIO
}
```

---

## 🗄️ Database

### Entity Relationship
```
Users ──┬── Properties (Owner)
        ├── Bookings
        ├── WishlistItems
        ├── Reviews
        ├── ChatMessages (Sender/Receiver)
        ├── Payments
        ├── Contracts (Buyer/Seller)
        ├── ChatSessions
        ├── Leads
        └── Notifications

Properties ──┬── Bookings
             ├── WishlistItems
             ├── Reviews
             ├── Contracts
             └── Advertisements
```

### Sample Data
Aplikasi otomatis membuat sample data:
- 4 Users (Admin, Agent, Buyer, Tenant)
- 5 Properties (Rumah, Apartemen, Ruko, Villa, Tanah)
- 2 Reviews

---

## 🔌 API

### Swagger Documentation
Buka `/api/docs` untuk dokumentasi interaktif.

### Endpoints

| Method | Endpoint | Description |
|--------|----------|-------------|
| GET | `/api/health` | Health check |
| GET | `/api/properties` | List properties (paginated) |
| GET | `/api/properties/{id}` | Property detail |
| POST | `/api/properties` | Create property |
| PUT | `/api/properties/{id}` | Update property |
| DELETE | `/api/properties/{id}` | Delete property |
| GET | `/api/users` | List users |
| GET | `/api/bookings` | List bookings |
| GET | `/api/payments` | Payment list |
| GET | `/api/contracts` | Contract list |
| POST | `/api/kpr/calculate` | KPR calculation |
| POST | `/api/chatbot/send` | Chat with AI |
| GET | `/api/export/properties/csv` | Export CSV |
| GET | `/api/export/properties/excel` | Export Excel |

---

## 🤖 ChatBot AI - Tante Rita

Tante Rita adalah asisten AI yang membantu pengguna dengan:
- Rekomendasi properti
- Simulasi KPR
- Informasi pajak properti
- Tips membeli/menyewa properti
- Dan banyak lagi!

Mendukung multiple AI provider: OpenAI, Anthropic Claude, Google Gemini, dan Ollama (local).

---

## 👨‍💻 Pengembangan

### Build
```bash
dotnet build
```

### Run Development
```bash
dotnet run --environment Development
```

### Watch Mode
```bash
dotnet watch run
```

---

## 👥 Tim

Dibangun dengan ❤️ oleh **Gravicode Studios**

- Project Lead: Kang Fadhil
- AI Assistant: Jacky the Code Bender

---

## 📄 License

MIT License - Copyright © 2024 Gravicode Studios

---

**🏠 Happy Property Hunting!**
