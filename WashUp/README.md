# 🧺 WashUp - Laundry Management System

> Aplikasi manajemen laundry modern dengan Blazor Server .NET, AI Chatbot, IoT Monitoring, GPS Tracking, dan Multi-Cabang.

![WashUp Banner](https://img.shields.io/badge/.NET-10.0-purple?style=flat-square&logo=.net)
![Blazor](https://img.shields.io/badge/Blazor-Server-purple?style=flat-square&logo=blazor)
![License](https://img.shields.io/badge/license-MIT-green?style=flat-square)
![Status](https://img.shields.io/badge/status-active-success?style=flat-square)

---

## ✨ Fitur Utama

### 🏢 Untuk Pemilik & Admin
- **Dashboard** - Ringkasan order, pemasukan, pengeluaran, profit real-time
- **Manajemen Order** - Input order, tracking status (Diterima → Dicuci → Disetrika → Selesai → Dikirim)
- **Manajemen Pelanggan** - Data pelanggan, riwayat transaksi, preferensi layanan
- **Keuangan & Tagihan** - Invoice otomatis, laporan keuangan, grafik pemasukan
- **Inventaris & Stok** - Catatan bahan, peringatan stok habis
- **Manajemen Staff** - Data pegawai, jadwal kerja, gaji, performa
- **Multi Cabang** - Kelola banyak outlet dalam satu akun
- **Pajak** - Hitung PPh, laporan pajak otomatis

### 👤 Untuk Pelanggan
- **Registrasi & Login** - Autentikasi aman dengan ASP.NET Identity + reset password
- **Order Online** - Pemesanan layanan (Cuci Kering, Setrika, Express, Kiloan)
- **Pembayaran Online** - Integrasi e-wallet & transfer bank
- **Tracking Order** - Status pengerjaan real-time
- **Notifikasi** - Reminder, promo, pengumuman
- **Komplain** - Form pengaduan dengan tracking status
- **Loyalty & Membership** - Poin reward, diskon member, paket langganan

### 🚚 Operasional & Kurir
- **Pickup & Delivery** - Jadwal penjemputan/pengantaran
- **GPS Tracking** - Lokasi kurir real-time + simulator GPS
- **IoT Monitoring** - Sensor mesin cuci, listrik, air + simulator
- **Role-based Access** - Pemilik, Admin, Kurir, Pelanggan

### 🤖 ChatBot AI "Mbok Inem"
- Multi-model AI (OpenAI, Anthropic, Gemini, Ollama)
- Multi-session chat dengan attach gambar & dokumen
- Kernel functions: search internet, scrape URL, query database, kalkulasi
- Markdown rendering (tabel, gambar, video, code)

### 🌟 Fitur Kompetitif
- **Marketplace** - Listing layanan publik
- **Rating & Review** - Feedback pelanggan
- **Analitik Tren** - Prediksi permintaan
- **UI Dark/Light** - Tampilan modern dengan nuansa ungu
- **REST API** - Minimal API + Swagger
- **Multi-Database** - SQLite, PostgreSQL, SQL Server, MySQL
- **Multi-Storage** - FileSystem, Azure Blob, S3, MinIO

---

## 🚀 Quick Start

### Prerequisites
- .NET 10 SDK
- SQLite (default) / PostgreSQL / SQL Server / MySQL

### Installation

```bash
# Clone repository
git clone https://github.com/your-org/WashUp.git
cd WashUp

# Run the application
dotnet run

# Open browser
# http://localhost:5000
```

### Demo Accounts

| Role | Email | Password |
|------|-------|----------|
| Pemilik | owner@washup.id | WashUp@2024 |
| Admin | admin@washup.id | WashUp@2024 |
| Kurir | kurir1@washup.id | WashUp@2024 |
| Pelanggan | pelanggan1@email.com | Pelanggan@123 |

---

## 🏗️ Tech Stack

- **Framework**: .NET 10 + Blazor Server
- **Database**: SQLite / PostgreSQL / SQL Server / MySQL (EF Core)
- **Authentication**: ASP.NET Identity + Role-based Access
- **AI/Chat**: Semantic Kernel + OpenAI/Anthropic/Gemini/Ollama
- **Storage**: FileSystem / Azure Blob / AWS S3 / MinIO
- **Charts**: ApexCharts (Blazor-ApexCharts)
- **API**: Minimal API + Swagger/OpenAPI
- **Markdown**: Markdig
- **QR Code**: QRCoder

---

## 📁 Project Structure

```
WashUp/
├── Components/
│   ├── Layout/          # MainLayout, sidebar, theme
│   ├── Pages/           # All pages
│   │   ├── Auth/        # Login, Register
│   │   ├── Dashboard/   # Dashboard utama
│   │   ├── Orders/      # Manajemen order
│   │   ├── Customers/   # Manajemen pelanggan
│   │   ├── Finance/     # Keuangan & tagihan
│   │   ├── Inventory/   # Inventaris & stok
│   │   ├── Staff/       # Manajemen staff
│   │   ├── Courier/     # Kurir & GPS tracking
│   │   ├── IoT/         # IoT monitoring
│   │   ├── Chat/        # ChatBot Mbok Inem
│   │   ├── Marketplace/ # Marketplace laundry
│   │   ├── Branches/    # Multi cabang
│   │   └── Complaints/  # Layanan komplain
│   └── Shared/          # Shared components
├── Data/                # DbContext & Seed Data
├── Models/              # Entity models
├── Services/            # Business logic services
├── Hubs/                # SignalR hubs
├── wwwroot/             # Static files (CSS, JS)
├── docs/                # Documentation
├── Program.cs           # Application entry
└── appsettings.json     # Configuration
```

---

## ⚙️ Configuration

### Database
Edit `appsettings.json`:
```json
{
  "DatabaseProvider": "SQLite",  // or "PostgreSQL", "SqlServer", "MySQL"
  "MySqlServerVersion": "8.0.36",
  "ConnectionStrings": {
    "DefaultConnection": "Data Source=WashUp.db",
    "MySQL": "Server=localhost;Port=3306;Database=WashUp;User=root;Password=root;"
  }
}
```

### AI ChatBot
```json
{
  "AI": {
    "Provider": "OpenAI",
    "OpenAI": {
      "ApiKey": "your-api-key",
      "Model": "gpt-4o"
    }
  }
}
```

### Storage
```json
{
  "FileStorage": {
    "Provider": "FileSystem",  // or "AzureBlob", "S3", "MinIO"
    "BasePath": "wwwroot/uploads"
  }
}
```

---

## 🔌 API Endpoints

| Method | Endpoint | Description |
|--------|----------|-------------|
| POST | /api/auth/token | Login → JWT bearer token |
| GET | /api/health | Health check |
| GET | /api/orders | List orders (auth) |
| GET | /api/orders/{no} | Order detail |
| GET | /api/customers | List customers (auth) |
| GET | /api/branches | List branches |
| GET | /api/marketplace | Public marketplace |
| GET | /api/dashboard/summary | Dashboard data (auth) |
| GET | /api/reports/export/orders | Export order CSV (auth) |
| GET | /api/reports/export/finance | Export keuangan CSV (auth) |
| GET | /swagger | Swagger UI |

Endpoint ber-auth menerima **cookie Identity** (login via browser) atau **JWT bearer** dari `/api/auth/token`.

---

## 🎨 Theme

WashUp features a modern purple-themed UI with dark/light mode:
- **Light Mode**: Clean white with purple accents
- **Dark Mode**: Deep purple with soft violet tones
- **Responsive**: Mobile-friendly, Facebook-like layout
- **Accessible**: High contrast, readable typography

---

## 📝 License

MIT License - Copyright (c) 2024 GraviCode Studios

---

## 👨‍💻 Credits

Developed by **Gravicode Studios**  
Lead: Kang Fadhil  
AI Assistant: Jacky the Code Bender

---

**🧺 WashUp - Laundry jadi lebih mudah, bersih, dan menyenangkan!**
