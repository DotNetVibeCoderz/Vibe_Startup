# 🕌 HolySafar - Hajj/Umrah Travel Management System

A comprehensive, modern Blazor Server application for managing Hajj and Umrah travel operations. Built with .NET 10, Entity Framework Core, and Semantic Kernel AI integration.

## ✨ Features

### 🕌 For Pilgrims (Jamaah)
- **Online Registration** - Digital forms with document upload (KTP, Passport, KK, Vaccine)
- **Package Information** - Browse Hajj/Umrah packages with prices, hotels, airlines, and schedules
- **Payment & Installments** - Track payments with installment options
- **Document Tracking** - Real-time status updates for documents, visa, and departure
- **AI Chatbot "Syeikh Jenggot"** - 24/7 AI assistant powered by LLM (OpenAI, Anthropic, Gemini, Ollama)
- **GPS Tracking** - Real-time location tracking on interactive map
- **SOS Emergency Button** - One-tap emergency alert with GPS coordinates
- **Marketplace** - Shop for Hajj/Umrah essentials (luggage, mukena, prayer mats)
- **Edukasi** - Manasik materials, video tutorials, and interactive quizzes
- **Chat & Announcements** - In-app messaging and community announcements

### 🛫 For Travel Agents
- **Package Management** - CRUD for Hajj/Umrah packages with brochures
- **Pilgrim Management** - Complete pilgrim data management
- **Document Verification** - Validate and track pilgrim documents
- **Operational Dashboard** - Monitor departures, visa status, financial reports
- **Notifications** - Payment reminders, manasik schedules, departure updates

### 📊 For Administrators
- **User Management** - Master data with CRUD, filtering, sorting, paging, CSV/Excel export
- **Analytics & Reports** - Statistics on pilgrims, finances, package performance
- **SOS Panel** - Real-time emergency monitoring with map integration
- **Order Management** - Marketplace order processing (Pay → Ship → Deliver)

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

## 🏃 Quick Start

### Prerequisites
- .NET 10 SDK
- (Optional) OpenAI API Key for chatbot

### Run

```bash
cd HolySafar
dotnet run
```

Open https://localhost:5000

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
│       ├── Admin/       # Users, Jamaah, Paket, Operasional, Laporan, SOS, Orders
│       ├── Agen/        # Paket, Jamaah
│       ├── Chatbot.razor    # Syeikh Jenggot AI Chat
│       ├── GpsTracking.razor # GPS Map
│       ├── Sos.razor        # Emergency SOS
│       ├── Marketplace.razor # Shopping
│       ├── Edukasi.razor    # Learning materials
│       └── ...              # Home, Login, Paket, Chat, Pengumuman
├── Models/              # All entity models
├── Data/                # DbContext, DataSeeder
├── Services/            # Auth, Storage, Export, Chatbot, GPS, Notification
├── wwwroot/
│   ├── css/app.css      # Complete design system
│   └── uploads/         # File uploads
└── docs/                # Documentation
```

## ⚙️ Configuration

All settings in `appsettings.json`:

- **Database**: SQLite (default), SQL Server, MySQL, PostgreSQL ready
- **Storage**: FileSystem (default), Azure Blob, S3, MinIO ready
- **Chatbot**: OpenAI (default), Anthropic, Gemini, Ollama ready
- **Theme**: Light/Dark mode
- **Language**: Indonesian (default), English ready

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
