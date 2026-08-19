# 🕌 HolySafar - Hajj/Umrah Travel Management System

A comprehensive, modern Blazor Server application for managing Hajj and Umrah travel operations. Built with .NET 10, Entity Framework Core, and Semantic Kernel AI integration.

## ✨ Features

### 🕌 For Pilgrims (Jamaah)
- **Online Registration** - Digital forms with document upload (KTP, Passport, KK, Vaccine)
- **Package Information** - Browse Hajj/Umrah packages with prices, hotels, airlines, and schedules
- **Payment & Installments** - Pay in full or by installment through **Xendit, Midtrans, Stripe, QRIS**, or manual transfer with proof upload
- **Document Upload** - Upload KTP, passport, family card, and vaccine certificate; see verification status and reviewer notes
- **Process Tracking** - Live timeline: registration to documents to payment to visa to SISKOHAT to departure
- **Digital Itinerary** - Day-by-day plan with ziarah schedule, transport, and hotel locations on an interactive map
- **Pilgrim Forum** - Threaded discussions with attachments
- **AI Chatbot "Syeikh Jenggot"** - 24/7 AI assistant powered by LLM (OpenAI, Anthropic, Gemini, Ollama)
- **GPS Tracking** - Real-time location tracking on interactive map
- **SOS Emergency Button** - One-tap emergency alert with GPS coordinates
- **Marketplace** - Shop for Hajj/Umrah essentials (luggage, mukena, prayer mats)
- **Edukasi** - Manasik materials, video tutorials, and interactive quizzes
- **Chat & Announcements** - In-app messaging and community announcements

### 🛫 For Travel Agents
- **Package Management** - CRUD for Hajj/Umrah packages with brochures
- **Pilgrim Management** - Complete pilgrim data management
- **Operational Dashboard** - Monitor departures, visa status, financial reports
- **Document Verification** & Visa - Approve/reject documents with notes, manage visa status and numbers
- **Automatic Reminders** - Background service issues payment-due, departure, manasik, and missing-document reminders

### 📊 For Administrators
- **User Management** - Master data with CRUD, filtering, sorting, paging, CSV/Excel export
- **Analytics & Reports** - Statistics on pilgrims, finances, package performance
- **SOS Panel** - Real-time emergency monitoring with map integration
- **Order Management** - Marketplace order processing (Pay to Ship to Deliver)
- **Transaction Monitor** - All payment gateway transactions, with manual confirmation fallback
- **Runtime Settings UI** - Change payment keys, chatbot model, storage provider, and reminder cadence without a restart
- **Backup & Compliance** - Consistent SQLite snapshot or full multi-sheet Excel export
- **SISKOHAT Integration** - Validate pilgrim data against Kemenag (live endpoint or local simulation)

## 🚀 Tech Stack

| Technology | Purpose |
|------------|---------|
| **.NET 10** | Runtime |
| **Blazor Server** | UI Framework (Interactive Server rendering) |
| **Entity Framework Core** | ORM with SQLite |
| **Semantic Kernel** | AI/Chatbot integration |
| **Markdig** | Markdown rendering |
| **ClosedXML** | Excel export |
| **CsvHelper** | CSV export |
| **Leaflet.js** | Interactive maps |
| **Bootstrap Icons** | Icon library |
| **Xendit / Midtrans / Stripe / QRIS** | Payment gateways |
| **Cookie Authentication + PBKDF2** | Session & password security |

## 🏃 Quick Start

### Prerequisites
- .NET 10 SDK
- (Optional) OpenAI API Key for chatbot

### Run

```bash
cd HolySafar
dotnet run
```

Open http://localhost:5083 (or https://localhost:7174 with `dotnet run --launch-profile https`)

### Demo Accounts

| Role | Username | Password |
|------|----------|----------|
| Admin | `admin` | `admin123` |
| Agent | `agen1` | `agen123` |
| Pilgrim | `jamaah1` | `jamaah123` |

## 📁 Project Structure

```
HolySafar/
├── Components/
│   ├── Layout/          # MainLayout, LoginLayout
│   └── Pages/
│       ├── Admin/       # Users, Jamaah, Paket, Itinerary, Dokumen, Transaksi,
│       │                #   Operasional, Laporan, SOS, Orders, Pengaturan
│       ├── Agen/        # Paket, Jamaah
│       ├── Chatbot.razor    # Syeikh Jenggot AI Chat
│       ├── GpsTracking.razor # GPS Map
│       ├── Sos.razor        # Emergency SOS
│       ├── Marketplace.razor # Shopping
│       ├── Edukasi.razor    # Learning materials
│       ├── PembayaranSaya.razor # Bills + payment gateway
│       ├── Tracking.razor    # Process timeline
│       ├── Perjalanan.razor  # Itinerary + map
│       ├── DokumenSaya.razor # Document upload
│       ├── Forum.razor       # Pilgrim forum
│       └── ...              # Home, Login, Paket, Chat, Pengumuman
├── Models/              # All entity models
├── Data/                # DbContext, DataSeeder
├── Services/            # Auth, Payment, Storage, Export, Chatbot, GPS,
│                        # Notification, Reminder, Siskohat, Backup, Settings, Localization
├── wwwroot/
│   ├── css/app.css      # Complete design system
│   └── uploads/         # File uploads
└── docs/                # Documentation
```

## ⚙️ Configuration

Settings live in `appsettings.json` **and can be overridden at runtime** from
**Admin → Pengaturan** (stored in the database, no restart needed):

- **Database**: SQLite (default), SQL Server, MySQL, PostgreSQL
- **Storage**: FileSystem, Azure Blob (default), S3, MinIO
- **Chatbot**: OpenAI (default), Anthropic, Gemini, Ollama
- **Payment**: Manual, Xendit, Midtrans, Stripe, QRIS — see [docs/PAYMENT-GATEWAY.md](docs/PAYMENT-GATEWAY.md)
- **Reminders**: enable/disable, interval, days-before thresholds
- **SISKOHAT**: endpoint + API key (empty = local simulation mode)
- **Theme**: Light/Dark mode
- **Language**: Indonesian (default) / English, switchable from the topbar

## 🔐 Security

- Session cookie `hsauth` is written server-side with `HttpOnly` + `SameSite=Lax` + `Secure`,
  so it cannot be read or forged from JavaScript.
- Login and logout are antiforgery-protected form posts.
- Passwords use PBKDF2-SHA256 (210k iterations, per-user salt); older SHA256 hashes are
  upgraded automatically on next login.
- Every route is guarded by `[Authorize]` attributes enforced by `AuthorizeRouteView`.
- Payment webhooks are rejected unless the provider's callback token / signing secret is configured.

## 🤖 Syeikh Jenggot AI Chatbot

The AI chatbot is powered by Semantic Kernel with:
- Multi-model support (OpenAI, Anthropic, Gemini, Ollama)
- Configurable system prompt, temperature, and model
- Built-in functions: internet search (Tavily), web scraping, math, time, countdown
- Database context for personalized responses
- Multi-session chat with history
- Image and document attachment support
- Full Markdown rendering (tables, code, media)

## 🎨 Design System

- Clean, modern, elegant UI
- Responsive mobile-friendly layout
- Dark/Light theme toggle
- CSS custom properties for theming
- Bootstrap Icons integration
- Consistent component design

---

**Built with ❤️ by Jacky the Code Bender @ Gravicode Studios**

*Support us: https://studios.gravicode.com/products/budax*
