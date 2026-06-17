# 🎬 Bioskop - Cinema Ticket Booking Platform

Aplikasi pemesanan tiket bioskop modern dengan Blazor Server .NET 10.

## ✨ Fitur Utama

- 🎫 **Pemesanan Tiket Online** - Pilih film, jadwal, kursi interaktif real-time
- 🍿 **Pembelian Snack** - Tambah makanan/minuman saat checkout
- 📱 **Tiket QR Code** - Generate tiket digital dengan QR unik
- 💬 **Curhat Film** - Timeline komunitas film seperti Twitter
- 🤖 **Si Bobby Movie Maniac** - ChatBot AI dengan Semantic Kernel
- 📊 **Dashboard Analitik** - Statistik penjualan, popularitas film, traffic
- 🔐 **Autentikasi** - Login, Register, Role-based (Admin, Operator, User)
- 🎨 **Light/Dark Theme** - UI responsif dengan animasi keren
- 🔌 **REST API** - Minimal API dengan Swagger & ApiKey
- 💳 **Pembayaran** - Simulasi E-Wallet, Kartu Kredit, Transfer Bank, QRIS

## 🚀 Cara Menjalankan

### Prasyarat
- .NET 10 SDK
- SQLite (default, auto-generated)

```bash
cd Bioskop
dotnet run
```

Buka browser: `https://localhost:5001`

## 🔑 Akun Demo

| Role | Email | Password |
|------|-------|----------|
| Admin | admin@bioskop.com | Admin123! |
| Operator | operator@bioskop.com | Operator123! |
| User | budi@email.com | User1234! |
| User | siti@email.com | User1234! |

## 🔌 API & Swagger

- Swagger UI: `https://localhost:5001/swagger`
- API Key: `bioskop-api-key-2024-secret` (header: `X-Api-Key`)

### Endpoint API
```
GET  /api/v1/movies
GET  /api/v1/movies/{id}
GET  /api/v1/showtimes
GET  /api/v1/studios/{id}/seats
GET  /api/v1/showtimes/{id}/seat-status
GET  /api/v1/snacks
POST /api/v1/orders
GET  /api/v1/orders/{id}
GET  /api/v1/tickets/{qrCode}
POST /api/v1/tickets/validate
GET  /api/v1/stats
GET  /api/health
```

## 📁 Struktur Proyek

```
Bioskop/
├── Api/                    # Minimal API endpoints
├── Components/
│   ├── Layout/             # MainLayout, sidebar
│   ├── Pages/
│   │   ├── Admin/          # Admin CRUD (Film, Snack, Dashboard)
│   │   ├── Auth/           # Login, Register, Profile, Reset Password
│   │   ├── Booking/        # Booking flow, My Tickets
│   │   ├── Chat/           # Si Bobby ChatBot
│   │   ├── Curhat/         # Timeline Curhat Film
│   │   └── Dashboard/      # Admin Dashboard
│   ├── Shared/             # Modal, Toast
│   └── App.razor
├── Data/                   # DbContext, Seed Data
├── Models/                 # 17 Entity Models
├── Services/               # 11 Services
├── wwwroot/                # Static files (CSS, uploads)
├── docs/                   # Dokumentasi
├── Program.cs
├── appsettings.json
└── README.md
```

## 🗄️ Database

Support multi-provider (konfigurasi via `appsettings.json`):
- SQLite (default)
- SQL Server
- MySQL
- PostgreSQL

```json
{
  "DatabaseProvider": "SQLite",
  "ConnectionStrings": {
    "DefaultConnection": "Data Source=Bioskop.db"
  }
}
```

## 🤖 ChatBot - Si Bobby Movie Maniac

Menggunakan Semantic Kernel Library. Dukungan model:
- **OpenAI** (GPT-4o)
- **Anthropic** (Claude)
- **Gemini**
- **Ollama** (lokal)

Setting API key di `appsettings.json` > `ChatBot.Models`.

## 📝 Dokumentasi

Lihat folder [docs/](docs/) untuk dokumentasi lengkap.

## 🔧 Tech Stack

- .NET 10 / ASP.NET Core
- Blazor Server (Interactive Server Render Mode)
- Entity Framework Core
- ASP.NET Identity
- Semantic Kernel
- QRCoder, Markdig, ClosedXML, CsvHelper
- Swashbuckle (Swagger)
- SQLite / SQL Server / MySQL / PostgreSQL

---

Made with ❤️ by Gravicode Studios | [Traktir Kopi ☕](https://studios.gravicode.com/products/budax)
