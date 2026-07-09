# ⛽ FuelStation - Pom Bensin Mini Management System

![.NET](https://img.shields.io/badge/.NET-10.0-512BD4?style=flat&logo=dotnet)
![Blazor](https://img.shields.io/badge/Blazor-Server-512BD4?style=flat&logo=blazor)
![License](https://img.shields.io/badge/License-MIT-green)

**FuelStation** is a comprehensive, modern fuel station management system built with **Blazor Server .NET 10**. It features a neo-brutalism design with soft colors, dark/light theme support, and is optimized for touch-screen operation.

---

## 📋 Daftar Isi

- [Fitur Utama](#-fitur-utama)
- [Teknologi](#-teknologi)
- [Memulai](#-memulai)
- [Konfigurasi](#-konfigurasi)
- [Arsitektur](#-arsitektur)
- [API](#-api)
- [Chat Bot AI](#-chat-bot-ai-bang-jenggo)
- [Simulator](#-simulator)
- [Database](#-database)
- [Storage](#-storage)
- [Development](#-development)

---

## 🔑 Fitur Utama

### Transaksi & POS
- 🖥️ **Touch-screen optimized POS** untuk pengoperasian cepat
- ⛽ Multi fuel station support
- 💳 Pembayaran digital: **QRIS, E-Wallet, Debit/Kredit, Bank Transfer**
- 🖨️ Cetak struk ESC/POS

### Manajemen Stok
- 📊 Monitoring **real-time** kapasitas tangki
- ⚠️ Peringatan stok menipis & kebocoran
- 📈 Prediksi kebutuhan dengan ML.NET
- 🎨 Visualisasi interaktif (SVG/CSS)

### Laporan & Dashboard
- 📊 Rekap harian, mingguan, bulanan
- 💹 Grafik penjualan & tren konsumsi
- 📋 Filter, sort, export CSV/Excel

### Marketplace Non-BBM
- 🛒 Katalog produk (oli, minuman, aksesoris)
- 🛍️ Shopping cart
- 🧾 Print total order

### Pelanggan & Loyalitas
- ⭐ Membership tiers (Regular, Silver, Gold, Platinum)
- 🎁 Poin reward & diskon
- 📝 Feedback & rating system

### Chat AI "Bang Jenggo"
- 🤖 Semantic Kernel dengan multi-model (OpenAI, Anthropic, Gemini, Ollama)
- 💬 Multi-session chat
- 🖼️ Attach gambar & dokumen
- 📊 Query data real-time (harga, stok, penjualan)

### IoT & Sensor
- 📡 Tank sensor integration
- 🔥 Emergency alert system
- 🛢️ Leak detection & quality monitoring

### Background Simulator
- 🚗 Simulasi kendaraan datang & isi BBM
- ⚡ Parallel order creation (stress test)
- 📊 Benchmark metrics

### REST API
- 📡 Swagger/OpenAPI documentation
- 🔌 CRUD endpoints untuk integrasi eksternal
- 📟 IoT sensor data endpoint

---

## 💻 Teknologi

| Layer | Teknologi |
|-------|-----------|
| **Framework** | .NET 10, Blazor Server |
| **Database** | SQLite / SQL Server / MySQL / PostgreSQL |
| **ORM** | Entity Framework Core |
| **Auth** | ASP.NET Core Identity |
| **AI/Chat** | Semantic Kernel |
| **ML** | ML.NET (prediksi stok) |
| **Visualisasi** | SkiaSharp, CSS/SVG Charts |
| **Storage** | File System / Azure Blob / S3 / MinIO |
| **Print** | ESC/POS thermal printer |
| **Real-time** | SignalR |
| **API Docs** | Swagger / Swashbuckle |

---

## 🚀 Memulai

### Prasyarat
- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
- Database (SQLite otomatis, atau SQL Server/MySQL/PostgreSQL)

### Install & Run

```bash
# Clone repository
git clone https://github.com/your-org/FuelStation.git
cd FuelStation

# Build & Run
dotnet build
dotnet run

# Buka browser
# App: https://localhost:5001
# Swagger: https://localhost:5001/swagger
```

### Demo Accounts

| Role | Email | Password |
|------|-------|----------|
| Admin | admin@fuelstation.com | Admin123! |
| Supervisor | supervisor@fuelstation.com | Super123! |
| Operator | operator1@fuelstation.com | Oper123! |

---

## ⚙️ Konfigurasi

Semua konfigurasi ada di `appsettings.json`:

```json
{
  "Database": {
    "Provider": "SQLite"  // atau "SQLServer", "MySQL", "PostgreSQL"
  },
  "Storage": {
    "Provider": "FileSystem"  // atau "AzureBlob", "S3", "MinIO"
  },
  "ChatBot": {
    "Provider": "OpenAI",
    "Model": "gpt-4o",
    "ApiKey": "YOUR_API_KEY_HERE"
  },
  "Simulator": {
    "Enabled": false,
    "IntervalMs": 5000
  }
}
```

---

## 🏗️ Arsitektur

```
FuelStation/
├── Models/              # Domain entities
├── Data/                # DbContext & config
├── Services/            # Business logic
│   ├── ChatBotService   # AI Chat (Semantic Kernel)
│   ├── SimulatorService # Background simulator
│   ├── PrinterService   # Receipt printing
│   └── StorageService   # Multi-provider storage
├── Controllers/         # REST API endpoints
├── Components/
│   ├── Layout/          # Main layout, nav
│   ├── Pages/           # All pages
│   │   ├── Dashboard    # Home dashboard
│   │   ├── Transactions # POS & history
│   │   ├── MasterData   # CRUD operations
│   │   ├── Reports      # Financial reports
│   │   ├── Chat         # Bang Jenggo AI
│   │   ├── Simulator    # Stress testing
│   │   ├── IoT          # Sensor monitoring
│   │   └── Marketplace  # Non-fuel sales
│   └── Shared/          # Reusable components
└── wwwroot/             # Static files, CSS
```

---

## 📡 API

REST API tersedia dengan dokumentasi Swagger di `/swagger`.

### Endpoints Utama

| Method | Endpoint | Description |
|--------|----------|-------------|
| GET | `/api/fuelapi/stations` | List all stations |
| GET | `/api/fuelapi/products` | List fuel products |
| GET | `/api/fuelapi/tanks/{stationId}` | Tank readings |
| POST | `/api/fuelapi/tanks/{tankId}/reading` | Update sensor |
| POST | `/api/fuelapi/transactions` | Create transaction |
| GET | `/api/fuelapi/summary/daily` | Daily summary |
| GET | `/api/fuelapi/alerts` | Emergency alerts |

---

## 🤖 Chat Bot AI (Bang Jenggo)

Bang Jenggo adalah asisten AI yang menggunakan **Semantic Kernel**:

### Supported Models
- **OpenAI**: GPT-4o, GPT-4o-mini
- **Anthropic**: Claude 3.5 Sonnet
- **Google**: Gemini 2.0
- **Ollama**: Llama 3, Mistral (local)

### Kernel Functions
- `get_current_time` - Waktu Jakarta (WIB)
- `get_fuel_prices` - Harga BBM terkini
- `get_station_info` - Info stasiun
- `get_customer_loyalty` - Poin loyalitas
- `get_daily_sales` - Penjualan hari ini
- `search_internet` - Tavily search
- `scrape_webpage` - Scrape halaman web
- `math_calculate` - Kalkulasi matematika

---

## 🚗 Simulator

Simulator berjalan di background untuk testing:

- **Kendaraan simulasi** datang otomatis
- **Parallel orders** untuk stress test
- **Benchmark metrics** (response time, throughput)
- Enable via `appsettings.json`: `"Simulator": { "Enabled": true }`

---

## 🗄️ Database

### Provider Support

| Provider | Connection String Example |
|----------|--------------------------|
| SQLite | `Data Source=FuelStation.db` |
| SQL Server | `Server=.;Database=FuelStation;...` |
| MySQL | `Server=localhost;Database=FuelStation;...` |
| PostgreSQL | `Host=localhost;Database=FuelStation;...` |

---

## 📦 Storage

### Provider Support

| Provider | Configuration |
|----------|--------------|
| FileSystem | Local `uploads/` folder |
| Azure Blob | Connection string + container |
| AWS S3 | Access key + bucket |
| MinIO | Endpoint + credentials |

---

## 🧪 Sample Data

Aplikasi otomatis mengisi database dengan sample data:

- 3 stasiun SPBU
- 5 produk BBM (Pertalite, Pertamax, Pertamax Turbo, Bio Solar, Dexlite)
- 5 pelanggan membership
- 6 karyawan (operator + supervisor)
- 5 produk non-BBM
- 50 transaksi simulasi
- 10 feedback pelanggan

---

## 📝 Development

```bash
# Watch mode (hot reload)
dotnet watch run

# Add migration
dotnet ef migrations add MigrationName

# Update database
dotnet ef database update
```

---

## 🤝 Kontribusi

Dibuat dengan ❤️ oleh **Gravicode Studios** dipimpin oleh **Kang Fadhil**.

---

*FuelStation - Modern Fuel Station Management* ⛽
