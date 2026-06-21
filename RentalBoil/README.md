# 🚗 RentalBoil - Vehicle Rental Application

[![.NET](https://img.shields.io/badge/.NET-10.0-blue)](https://dotnet.microsoft.com/)
[![Blazor](https://img.shields.io/badge/Blazor-Server-purple)](https://dotnet.microsoft.com/apps/aspnet/web-apps/blazor)
[![Swagger](https://img.shields.io/badge/API-Swagger-green)](https://swagger.io/)
[![AI](https://img.shields.io/badge/AI-Multi%20Model-orange)]()

**Rental Kendaraan Anti Ngambek!** | **No Fuss Vehicle Rental!**

Full-featured vehicle rental platform with Blazor Server .NET 10, REST API, Swagger, AI Chat Bot with multi-model support, GPS tracking, and claymorphism UI.

---

## ✨ Key Features

### 🚗 For Customers
- Vehicle search with filters (type, price, brand, transmission, capacity, location)
- Detailed vehicle pages with photos, specs, IoT status, and reviews
- Online booking with date selection, dynamic pricing, and coupon codes
- Booking tracking, history, and status monitoring
- AI-powered chat bot "Bang Tony Brewok" with 17+ kernel functions

### 🛠️ For Partners
- Vehicle fleet management (CRUD)
- Revenue dashboard with statistics
- Order management (accept/reject/complete bookings)

### 🖥️ For Admins
- User management and account suspension
- Vehicle verification workflow
- Transaction monitoring and analytics

### 🔌 REST API (Minimal API + Swagger)
- Full CRUD endpoints for vehicles, bookings, users, reviews, payments
- GPS/IoT control endpoints (lock, engine, tracking)
- Chat bot API endpoint
- Dashboard statistics endpoint
- **ApiKey authentication** (X-Api-Key header)
- **Swagger UI** at `/swagger`

### 🤖 AI Chat Bot - Bang Tony Brewok
- **4 AI Providers**: OpenAI, Anthropic Claude, Google Gemini, Ollama (local)
- **17 Kernel Functions**: search vehicles, create booking, check order, GPS position,
  internet search (Tavily), web scraping, file reading, math, currency, weather, FAQ, promos
- Multi-session chat with database persistence
- Markdown rendering in chat responses

### ⚙️ Competitive Features
- GPS Tracking Simulator (background thread)
- IoT: Digital lock/unlock, engine on/off, motion status
- Dynamic Pricing with multipliers
- Membership & Loyalty (Basic → Silver → Gold → Platinum)
- Multi-language (ID/EN)
- Multi-Database: SQLite, SQL Server, MySQL, PostgreSQL
- Multi-Storage: File System, Azure Blob, S3, MinIO

---

## 🚀 Quick Start

```bash
cd RentalBoil
dotnet run
```

- **App**: `https://localhost:5001`
- **Swagger API Docs**: `https://localhost:5001/swagger`

### Demo Accounts

| Role | Email | Password |
|------|-------|----------|
| 🔑 Admin | admin@rentalboil.com | Admin123! |
| 🚗 Partner | partner1@rentalboil.com | Partner123! |
| 👤 Customer | customer1@rentalboil.com | Customer123! |

### API Access
```bash
# Example API call
curl -H "X-Api-Key: rntl-2025-secure-api-key-change-in-production" \
     https://localhost:5001/api/vehicles

# Swagger UI (no auth needed for swagger itself)
open https://localhost:5001/swagger
```

---

## 🤖 AI Configuration

Set in `appsettings.json`:

```json
{
  "AI": {
    "Provider": "OpenAI",     // OpenAI | Anthropic | Gemini | Ollama
    "OpenAI": {
      "ApiKey": "sk-your-key",
      "Model": "gpt-4o-mini"
    },
    "Anthropic": {
      "ApiKey": "sk-ant-your-key",
      "Model": "claude-3-haiku-20240307"
    },
    "Gemini": {
      "ApiKey": "AIza-your-key",
      "Model": "gemini-2.0-flash"
    },
    "Ollama": {
      "Endpoint": "http://localhost:11434",
      "Model": "llama3.2"
    }
  }
}
```

### Kernel Functions (Bang Tony Brewok can:)
| Function | Description |
|----------|-------------|
| `search_vehicles_db` | Search vehicles from database |
| `create_booking_via_chat` | Book a vehicle via chat |
| `check_booking_status` | Check order status |
| `get_vehicle_position` | GPS position & IoT status |
| `search_internet` | Tavily search API |
| `scrap_web_page` | Extract text from webpage |
| `read_file_from_url` | Read file content from URL |
| `math_calculate` | Mathematical calculations |
| `convert_currency_simulation` | Currency conversion |
| `get_weather_info` | Simulated weather info |
| `calculate_rental_price` | Price estimation |
| `get_active_promotions` | Active promos & coupons |
| `get_faqs` | FAQ search |
| `get_platform_stats` | Platform statistics |
| `get_current_datetime` | Current WIB time |
| `get_day_of_week` | Day from date |
| `get_vehicle_detail_db` | Vehicle details |

---

## 📁 Project Structure

```
RentalBoil/
├── Api/                   # REST API (Minimal API)
│   ├── ApiEndpoints.cs    # All /api/* endpoints
│   └── ApiKeyMiddleware.cs # X-Api-Key auth
├── Components/
│   ├── Layout/            # MainLayout
│   └── Pages/             # Blazor pages
│       ├── Admin/         # Admin dashboard
│       ├── Partner/       # Partner dashboard
│       ├── Customer/      # Customer pages
│       └── Chat/          # Chat bot UI
├── Data/                  # DbContext & Seed
├── Hubs/                  # SignalR Hubs
├── Models/                # Entity models
├── Services/
│   ├── BotService.cs      # AI (OpenAI/Anthropic/Gemini/Ollama)
│   ├── BotKernelFunctions.cs  # 17 kernel functions
│   ├── BookingService.cs  # Booking logic
│   ├── VehicleService.cs  # Vehicle logic
│   └── ...                # Other services
├── wwwroot/app.css        # Claymorphism CSS
└── Program.cs             # Entry point
```

---

Made with ❤️ by **Gravicode Studios**  
**Kang Fadhil** & **Jacky The Code Bender**
