# Joka OTA - Technical Documentation

## Table of Contents
1. [Architecture Overview](#architecture-overview)
2. [Database Setup](#database-setup)
3. [Storage Configuration](#storage-configuration)
4. [ChatBot Configuration](#chatbot-configuration)
5. [API Reference](#api-reference)
6. [Payment Integration](#payment-integration)
7. [Security](#security)
8. [Deployment](#deployment)

---

## Architecture Overview

Joka is built on **Blazor Server** with **.NET 10**. The application uses a server-side rendering model where UI interactions are handled via SignalR connection.

### Design Pattern
- **Server-side Blazor** — Components execute on server, UI updates sent via SignalR
- **EF Core Repository** — Database access through `AppDbContext`
- **Service Layer** — Business logic in `/Services`
- **Minimal API** — RESTful endpoints for external integration
- **Semantic Kernel** — AI orchestration for chatbot

### Key Dependencies
| Package | Purpose |
|---------|---------|
| `Microsoft.EntityFrameworkCore` | ORM for database access |
| `Microsoft.SemanticKernel` | AI/LLM orchestration |
| `Markdig` | Markdown to HTML rendering |
| `Swashbuckle.AspNetCore` | Swagger/OpenAPI docs |
| `QRCoder` | QR code generation |
| `D3.js` | Client-side data visualization |

---

## Database Setup

### Supported Providers
1. **SQLite** (default, development)
2. **SQL Server**
3. **MySQL**
4. **PostgreSQL**

### Configuration
```json
{
  "Database": {
    "Provider": "SQLite",
    "ConnectionStrings": {
      "SQLite": "Data Source=Data/joka.db",
      "SQLServer": "Server=localhost;Database=Joka;Trusted_Connection=true;",
      "MySQL": "Server=localhost;Database=Joka;User=root;Password=;",
      "Postgre": "Host=localhost;Database=Joka;Username=postgres;Password=postgres;"
    }
  }
}
```

### Entity Relationship Diagram
```
User ──┬── WishlistItem
       ├── UserNotification
       ├── LoyaltyTransaction
       ├── FlightBooking
       ├── TrainBooking
       ├── HotelBooking
       ├── CarRentalBooking
       ├── ActivityBooking
       ├── TravelPackageBooking
       ├── PaymentTransaction
       └── ChatSession ─── ChatMessage ─── ChatAttachment

Flight ─── Airline, Airport (Departure/Arrival)
Hotel ─── Room, HotelReview
TrainSchedule ─── Train, TrainStation
Activity ─── ActivityBooking
PromoVoucher ─── UserVoucher
```

### Seed Data
The `SeedData.cs` initializer creates:
- 3 demo users
- 10 airports (CGK, DPS, SUB, YIA, KNO, UPG, BPN, SIN, KUL, BKK)
- 7 airlines (Garuda, AirAsia, Lion, Super Air Jet, Batik, Citilink, Singapore Airlines)
- 8 flights with realistic schedules and pricing
- 6 train stations + 4 trains + 4 schedules
- 5 hotels + 15 rooms (3 per hotel)
- 5 activities/events
- 4 car rental options
- 4 promo vouchers
- 3 insurance plans
- 3 travel packages

---

## Storage Configuration

### Providers
```json
{
  "Storage": {
    "Provider": "FileSystem",  // or AzureBlob, S3, MinIO
    "FileSystem": { "BasePath": "wwwroot/uploads" },
    "AzureBlob": { "ConnectionString": "...", "ContainerName": "joka-storage" },
    "S3": { "AccessKey": "...", "SecretKey": "...", "Region": "ap-southeast-1" },
    "MinIO": { "Endpoint": "localhost:9000", "AccessKey": "minioadmin" }
  }
}
```

---

## ChatBot Configuration

### Available AI Providers
1. **OpenAI** — GPT-4o, GPT-4o-mini
2. **Anthropic** — Claude 3 Sonnet/Haiku
3. **Google Gemini** — Gemini 1.5 Pro
4. **Ollama** — Local models (Llama, Mistral, etc.)

### Settings
```json
{
  "ChatBot": {
    "Name": "Mas Bolang",
    "Provider": "OpenAI",
    "DefaultModel": "gpt-4o-mini",
    "Temperature": 0.7,
    "MaxTokens": 4096,
    "SystemPrompt": "Kamu adalah Mas Bolang...",
    "Providers": { ... },
    "Tavily": { "ApiKey": "" }
  }
}
```

### Kernel Functions (Tools)
| Function | Description |
|----------|-------------|
| `search_internet` | Tavily web search |
| `scrape_webpage` | Extract content from URL |
| `get_current_time` | Current date/time with timezone |
| `calculate_math` | Math expression evaluation |
| `read_file_from_url` | Read text from file URL |
| `get_date_info` | Detailed date information |
| `convert_currency` | Currency conversion (simulated) |

---

## API Reference

Full Swagger documentation available at `/swagger` when running in Development mode.

### Base URL: `/api`

### Flights
```
GET /api/flights?from=CGK&to=DPS&date=2025-01-15&maxPrice=2000000&sort=price_asc
```
Returns filtered flight list with airline and airport details.

### Hotels
```
GET /api/hotels?city=Bali&minStars=4
GET /api/hotels/{id}
```

### Trains
```
GET /api/trains?from=GMR&to=BD
```

### Activities
```
GET /api/activities?city=Jakarta&category=Concert
```

### Chat
```
POST /api/chat/send
Body: { "message": "Cari tiket pesawat Jakarta Bali", "attachments": [] }

POST /api/chat/reset
```

### Others
```
GET /api/promos
GET /api/config
GET /api/dashboard/stats
```

---

## Payment Integration

### Supported Gateways
- **Midtrans** (default for Indonesian market)
- **Xendit**

Configuration in `appsettings.json`:
```json
{
  "Payment": {
    "DefaultGateway": "Midtrans",
    "Gateways": {
      "Midtrans": {
        "ServerKey": "SB-Mid-server-...",
        "ClientKey": "SB-Mid-client-...",
        "IsProduction": false
      }
    }
  }
}
```

---

## Deployment

### Production Checklist
1. Set `"Provider"` under `Database` to production DB
2. Configure real API keys for ChatBot providers
3. Set `"IsProduction": true` for payment gateways
4. Enable HTTPS
5. Set strong CORS policies
6. Configure proper logging

### Docker (Coming Soon)
```dockerfile
FROM mcr.microsoft.com/dotnet/aspnet:9.0
WORKDIR /app
COPY . .
ENTRYPOINT ["dotnet", "Joka.dll"]
```

---

*Documentation version: 1.0.0 | Last updated: July 2025*
