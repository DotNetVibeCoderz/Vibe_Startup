# 🚗 SmartDrive Academy

**Platform Manajemen Belajar Menyetir Mobil**

Aplikasi manajemen lengkap untuk sekolah mengemudi yang mencakup kebutuhan siswa, instruktur, dan admin. Dibangun dengan Blazor Server .NET 10.

---

## ✨ Fitur Utama

### 🚗 Untuk Siswa (Learners)
- Registrasi akun dengan upload KTP/SIM
- Profil siswa dengan progres belajar
- Booking jadwal latihan online
- Pembayaran online (e-wallet, transfer bank, kartu kredit)
- Modul materi teori digital
- Simulasi ujian teori dengan pembahasan
- Tracking progres dan statistik jam latihan
- Feedback dari instruktur
- Notifikasi otomatis (jadwal, pembayaran, ujian)

### 👨‍🏫 Untuk Instruktur
- Manajemen jadwal mengajar
- Profil dengan rating dan pengalaman
- Catatan evaluasi siswa
- GPS tracking real-time dengan simulator
- Chat real-time dengan siswa (gambar, emoji, like)
- Dashboard kinerja pribadi

### 🛠️ Untuk Admin
- Manajemen kendaraan (CRUD, status servis)
- Manajemen instruktur dan siswa
- Laporan keuangan lengkap
- Analitik bisnis
- Konfigurasi sistem dari UI

### 🌟 Fitur Kompetitif
- **Gamifikasi**: Badge, level, dan XP untuk motivasi
- **Peta Lokasi**: CRUD lokasi latihan dengan search & filter
- **Marketplace**: Kursus tambahan, cart, order, print struk
- **Integrasi Asuransi**: Proteksi kecelakaan saat latihan

### 🤖 Om Bambang AI Chat Bot
- Multi-session chat
- Attach gambar dan dokumen
- Semantic Kernel dengan dukungan OpenAI, Anthropic, Gemini, Ollama
- Kernel functions: search internet, scrape page, kalkulasi, query database
- Markdown rendering lengkap

---

## 🔧 Teknologi Stack

| Teknologi | Keterangan |
|-----------|------------|
| .NET 10 | Framework |
| Blazor Server | UI Framework |
| Entity Framework Core | ORM |
| SQLite / SQL Server / MySQL / PostgreSQL | Database |
| ASP.NET Identity | Authentication & Authorization |
| Semantic Kernel | AI/Chat Bot |
| Swagger | API Documentation |
| Markdig | Markdown Rendering |
| CsvHelper & ClosedXML | Export CSV/Excel |
| Serilog | Logging |

---

## 🚀 Quick Start

### Prerequisites
- .NET 10 SDK
- Visual Studio 2022+ atau VS Code

### Run

```bash
# Clone & masuk folder
cd SmartDrive

# Restore & run
dotnet restore
dotnet run

# Buka browser
http://localhost:5000
```

### Default Users

| Role | Email | Password |
|------|-------|----------|
| Admin | admin@smartdrive.com | Admin123! |
| Instruktur | budi@smartdrive.com | Instructor123! |
| Siswa | andi@email.com | Student123! |

---

## 📁 Struktur Proyek

```
SmartDrive/
├── Components/
│   ├── Layout/          # MainLayout, AuthLayout
│   ├── Pages/
│   │   ├── Auth/        # Login, Register
│   │   ├── Admin/       # Dashboard, CRUD Vehicles, etc.
│   │   ├── Instructor/  # Instructor pages
│   │   ├── Student/     # Student pages
│   │   ├── Chat/        # Om Bambang AI
│   │   └── Marketplace/ # Marketplace
│   └── Shared/          # Shared components
├── Data/
│   ├── SmartDriveDbContext.cs
│   └── Seed/            # Database seeder
├── Models/
│   ├── Entities/        # Database entities
│   ├── ViewModels/      # View models
│   └── Enums/           # Enumerations
├── Services/
│   ├── ChatBotService.cs
│   ├── GpsSimulatorService.cs
│   ├── StorageService.cs
│   ├── ExportService.cs
│   └── NotificationService.cs
├── Api/
│   └── Endpoints/       # Minimal API
├── wwwroot/
│   ├── css/
│   └── uploads/
├── docs/                # Documentation
├── Program.cs
└── appsettings.json
```

---

## ⚙️ Konfigurasi

### Database
Ubah provider di `appsettings.json`:
```json
{
  "Database": {
    "Provider": "SQLite"  // SQLite | SQLServer | MySQL | PostgreSQL
  },
  "ConnectionStrings": {
    "DefaultConnection": "Data Source=smartdrive.db"
  }
}
```

### Storage
```json
{
  "Storage": {
    "Provider": "FileSystem",  // FileSystem | AzureBlob | S3 | MinIO
    "BasePath": "uploads"
  }
}
```

### Chat Bot AI
```json
{
  "ChatBot": {
    "ModelProvider": "OpenAI",   // OpenAI | Anthropic | Gemini | Ollama
    "ModelId": "gpt-4",
    "ApiKey": "your-api-key",
    "Temperature": 0.7,
    "MaxTokens": 2000
  }
}
```

---

## 🔌 REST API

Swagger UI tersedia di `/swagger` saat development mode.

### Endpoints:
- `GET /api/health` - Health check
- `GET /api/vehicles` - List kendaraan
- `GET /api/vehicles/{id}` - Detail kendaraan
- `GET /api/locations` - List lokasi latihan
- `GET /api/bookings` - List booking
- `POST /api/gps/push` - Push data GPS
- `GET /api/gps/{bookingId}` - Get GPS data
- `GET /api/stats` - Dashboard stats
- `GET /api/marketplace/products` - List produk

---

## 🎨 Theme

Mendukung Dark Mode dan Light Mode. Toggle di pojok kanan atas header.

---

## 📝 Changelog

### v1.0.0
- ✅ Blazor Server .NET 10
- ✅ Multi-database support
- ✅ Authentication & Authorization (RBAC)
- ✅ CRUD untuk semua master data
- ✅ Export CSV & Excel
- ✅ Column filter, sort, paging
- ✅ Om Bambang AI Chat Bot
- ✅ GPS Tracking Simulator
- ✅ Marketplace dengan cart & receipt
- ✅ REST API dengan Swagger
- ✅ Dark/Light theme
- ✅ Responsive design
- ✅ Sample data & users

---

## 👨‍💻 Author

Dibuat oleh **Jacky the Code Bender** dari **GraviCode Studios**

Dipimpin oleh **Kang Fadhil**

---

## 📄 License

Proprietary - GraviCode Studios © 2024
