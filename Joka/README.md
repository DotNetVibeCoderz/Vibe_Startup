# 🚀 Joka - Ultimate Online Travel Agent

> *"Jelajahi Dunia Tanpa Batas" - Explore the World Without Limits*

**Joka** is a comprehensive Online Travel Agent (OTA) application inspired by Tiket.com and Traveloka, built with **Blazor Server .NET 10**. Book flights, trains, hotels, car rentals, activities, and travel packages — all in one beautiful platform with a built-in AI assistant named **Mas Bolang**!

---

## ✨ Features

### 🛫 Core Travel
- **✈️ Flight Tickets:** Multi-airline search with price/schedule filters, direct booking
- **🚂 Train Tickets:** KAI integration ready, seat selection, class filtering
- **🚌 Bus & Shuttle:** Intercity routes, bus and door-to-door shuttle, filter by fleet type/class
- **🏨 Hotels & Accommodation:** Booking with reviews, ratings, room types (Hotel, Villa, Resort, Apartment)
- **🚗 Car Rental:** With/without driver, flexible duration, multiple vehicle types
- **🎯 Activities & Events:** Concerts, tours, workshops, sports, attractions
- **🎁 Travel Packages:** Bundled flight+hotel+activities deals

### 💳 Payment & Financial
- Multi-payment: Bank transfer, e-wallet, credit card, QRIS
- PayLater: Installments without credit card
- Promo & Cashback: Seasonal discounts, voucher codes, loyalty points
- Travel Insurance: Delay, cancellation, medical protection

### 📊 User Experience
- 🌓 **Dark/Light Theme** — Neo Brutalism Soft + Minimalism + Flat Design
- 🌐 **Multi-language:** Bahasa Indonesia & English
- 💱 **Multi-currency:** IDR, USD, SGD, EUR, JPY, etc.
- ❤️ **Wishlist & Favorites**
- 🔔 **Real-time Notifications** (SignalR)

### 🤖 AI Chatbot — Mas Bolang
- Powered by **Semantic Kernel**
- Multi-model: OpenAI, Anthropic Claude, Google Gemini, Ollama (local)
- Multi-session chat with create/delete/reset
- Markdown rendering (tables, code, images, video)
- Image & document attachment upload
- Kernel Functions: Tavily search, web scraping, date/time, math, currency conversion
- Database functions: searches Joka's own flights, trains, buses, hotels, activities, packages, promos and insurance, and looks up any booking by code — answers come from real inventory, not invented

### 🔧 Technical
- **.NET 10 Blazor Server** with interactive rendering
- **EF Core** with multi-DB: SQLite, SQL Server, MySQL, PostgreSQL
- **Multi-storage:** FileSystem, Azure Blob, AWS S3, MinIO
- **Minimal API** with Swagger docs
- **D3.js** for data visualization
- All settings configurable via `appsettings.json`

---

## 🚀 Quick Start

### Prerequisites
- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
- SQLite (default, no setup needed)

### Run
```bash
cd Joka
dotnet run
# Open http://localhost:5000
# Swagger: http://localhost:5000/swagger
```

### Database
Default: **SQLite** (`Data/joka.db` — auto-created with sample data).

To switch database, edit `appsettings.json`:
```json
{
  "Database": {
    "Provider": "SQLServer",  // or MySQL, Postgre
    "ConnectionStrings": {
      "SQLServer": "Server=...;Database=Joka;..."
    }
  }
}
```

### ChatBot Setup
Configure your AI provider in `appsettings.json`:
```json
{
  "ChatBot": {
    "Provider": "OpenAI",
    "Providers": {
      "OpenAI": {
        "ApiKey": "sk-your-key-here",
        "Model": "gpt-4o-mini"
      }
    }
  }
}
```

---

## 📁 Project Structure
```
Joka/
├── Components/
│   ├── Layout/          # MainLayout (.razor + .css)
│   └── Pages/           # Blazor pages (9 pages)
├── Models/              # 30+ domain models
│   ├── Common/          # BaseEntity
│   ├── Users/           # User, Wishlist, Notifications
│   ├── Flights/         # Airport, Airline, Flight, Booking
│   ├── Trains/          # Station, Train, Schedule, Booking
│   ├── Hotels/          # Hotel, Room, Booking, Review
│   ├── Payments/        # Payment, Voucher, Loyalty, Insurance, Car, Package
│   ├── Activities/      # Activity, Booking
│   └── Chat/            # Session, Message, Attachment
├── Services/
│   ├── Chat/            # ChatBotService (Semantic Kernel)
│   ├── Storage/         # Multi-provider storage
│   └── MarkdownService  # Markdig renderer
├── Data/
│   ├── AppDbContext     # EF Core context
│   └── SeedData         # Sample data seeder
├── Program.cs           # App entry + Minimal API
├── appsettings.json     # Configuration
├── docs/                # Documentation
└── wwwroot/             # Static assets
```

---

## 🔌 API Endpoints
| Method | Endpoint | Description |
|--------|----------|-------------|
| GET | `/api/flights` | Search flights (from, to, date, maxPrice) |
| GET | `/api/hotels` | Search hotels (city, minStars) |
| GET | `/api/hotels/{id}` | Hotel details with rooms & reviews |
| GET | `/api/trains` | Search trains (from, to, date) |
| GET | `/api/buses` | Search buses & shuttles (from, to, date, type) |
| GET | `/api/bus-terminals` | List bus terminals |
| GET | `/api/activities` | Search activities (city, category) |
| GET | `/api/promos` | Active promo vouchers |
| POST | `/api/chat/send` | Send message to Mas Bolang |
| POST | `/api/chat/reset` | Reset chat session |
| GET | `/api/config` | App configuration |
| GET | `/api/dashboard/stats` | Dashboard statistics |

---

## 🎨 Design System
**Neo Brutalism Soft × Minimalism × Flat Design**
- Bold borders with soft shadows
- Vibrant accent colors (#FF5C35 orange, #FFB800 yellow)
- Dark/Light mode toggle
- Responsive sidebar navigation
- Smooth animations & skeleton loading states

---

## 🛠️ Tech Stack
- **Framework:** Blazor Server (.NET 10)
- **ORM:** Entity Framework Core 9 (pinned: Pomelo/MySQL has no EF 10 release yet)
- **AI:** Microsoft Semantic Kernel
- **Markdown:** Markdig
- **Charts:** D3.js
- **API Docs:** Swashbuckle / Swagger
- **Storage:** Azure Blob, AWS S3, MinIO
- **DB:** SQLite, SQL Server, MySQL, PostgreSQL

---

## 📝 License
Made with ❤️ by **Gravicode Studios** — [Kang Fadhil](https://studios.gravicode.com)

---

## 🇮🇩 Bahasa Indonesia
Lihat [README-ID.md](README-ID.md) untuk dokumentasi dalam Bahasa Indonesia.
