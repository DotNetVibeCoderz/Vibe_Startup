# 📚 HolySafar — Complete Documentation

## Table of Contents
1. [Architecture Overview](#architecture-overview)
2. [Getting Started](#getting-started)
3. [REST API Reference](#rest-api-reference)
4. [Database Schema](#database-schema)
5. [Configuration Guide](#configuration-guide)
6. [Deployment Guide](#deployment-guide)

---

## Architecture Overview

```
┌─────────────────────────────────────────────────────────┐
│                    HolySafar System                      │
│                                                         │
│  ┌───────────┐  ┌───────────┐  ┌──────────────────┐    │
│  │  Blazor   │  │  REST API │  │  Swagger UI      │    │
│  │  Server   │  │  (MinAPI) │  │  /swagger         │    │
│  │  (SSR+IS) │  │  /api/*   │  │                   │    │
│  └─────┬─────┘  └─────┬─────┘  └──────────────────┘    │
│        │              │                                  │
│  ┌─────┴──────────────┴─────────────────────────────┐   │
│  │              Service Layer                        │   │
│  │  AuthService │ ChatbotService │ StorageService    │   │
│  │  ExportService│ NotifService  │ GpsSimulatorSrvc │   │
│  └──────────────────────┬───────────────────────────┘   │
│                         │                                │
│  ┌──────────────────────┴───────────────────────────┐   │
│  │              EF Core (AppDbContext)               │   │
│  │    SQLite | SQL Server | MySQL | PostgreSQL       │   │
│  └──────────────────────────────────────────────────┘   │
│                                                         │
│  ┌──────────────────────────────────────────────────┐   │
│  │              External Services                    │   │
│  │  OpenAI/Gemini/Anthropic/Ollama (LLM)            │   │
│  │  Azure Blob / S3 / MinIO (Storage)               │   │
│  │  Tavily (Web Search)                              │   │
│  │  Leaflet/OSM (Maps)                               │   │
│  └──────────────────────────────────────────────────┘   │
└─────────────────────────────────────────────────────────┘
```

### Rendering Mode
- **Blazor Server** with **Interactive Server** rendering
- SignalR circuit per browser tab
- Services registered as **Scoped** (per circuit)

---

## Getting Started

### Prerequisites
- .NET 10 SDK
- (Optional) LLM API key for Chatbot

### Quick Start
```bash
cd HolySafar
dotnet run
# Open http://localhost:5083
# Swagger: http://localhost:5083/swagger
```

### Demo Accounts
| Role   | Username  | Password    |
|--------|-----------|-------------|
| Admin  | `admin`   | `admin123`  |
| Agent  | `agen1`   | `agen123`   |
| Pilgrim| `jamaah1` | `jamaah123` |

---

## REST API Reference

### Authentication
All API endpoints require **X-Api-Key** header:
```
X-Api-Key: HolySafar-API-Key-2025!
```
Default key in `appsettings.json` → `AppSettings.ApiKey`

### Base URL: `http://localhost:5083/api`

### Endpoints

#### Jamaah (Pilgrims)
| Method | Endpoint | Description |
|--------|----------|-------------|
| GET | `/api/jamaah` | All pilgrims (with package info) |
| GET | `/api/jamaah/{id}` | Single pilgrim |
| GET | `/api/jamaah/search?nama=X&nik=Y` | Search by name/NIK |
| GET | `/api/jamaah/gps/active` | Active GPS positions |

#### GPS Tracking
| Method | Endpoint | Description |
|--------|----------|-------------|
| POST | `/api/jamaah/{id}/gps` | Update pilgrim GPS position |

**Body:**
```json
{ "latitude": 21.4225, "longitude": 39.8262 }
```

#### Users
| Method | Endpoint | Description |
|--------|----------|-------------|
| GET | `/api/users` | All users (no passwords) |

#### Paket (Packages)
| Method | Endpoint | Description |
|--------|----------|-------------|
| GET | `/api/paket` | Active packages |
| GET | `/api/paket/{id}` | Single package |

#### Pembayaran (Payments)
| Method | Endpoint | Description |
|--------|----------|-------------|
| GET | `/api/pembayaran` | Payment list (100 latest) |
| GET | `/api/pembayaran/{id}` | Single payment |
| POST | `/api/pembayaran/{id}/cicilan` | Add installment |

**Cicilan Body:**
```json
{ "jumlah": 5000000, "metode": "Transfer", "catatan": "Cicilan ke-2" }
```

#### SOS Emergency
| Method | Endpoint | Description |
|--------|----------|-------------|
| GET | `/api/sos` | SOS alerts (50 latest) |
| POST | `/api/sos` | Create SOS alert |

**Body:**
```json
{ "jamaahId": 1, "latitude": 21.4225, "longitude": 39.8262, "pesan": "Need help!" }
```

#### Other Endpoints
| Method | Endpoint | Description |
|--------|----------|-------------|
| GET | `/api/keberangkatan` | Departures |
| GET | `/api/produk` | Marketplace products |
| GET | `/api/pengumuman` | Announcements |
| GET | `/api/kontak-darurat` | Emergency contacts |
| GET | `/api/materi-manasik` | Manasik materials |
| GET | `/api/kuis` | Quiz questions |
| GET | `/api/orders` | Marketplace orders |
| GET | `/api/chat` | Chat messages |
| GET | `/api/dashboard` | Dashboard summary |

### cURL Examples
```bash
# Get all pilgrims
curl -H "X-Api-Key: HolySafar-API-Key-2025!" http://localhost:5083/api/jamaah

# Update GPS
curl -X POST -H "X-Api-Key: HolySafar-API-Key-2025!" \
  -H "Content-Type: application/json" \
  -d '{"latitude":21.4225,"longitude":39.8262}' \
  http://localhost:5083/api/jamaah/1/gps

# Create SOS
curl -X POST -H "X-Api-Key: HolySafar-API-Key-2025!" \
  -H "Content-Type: application/json" \
  -d '{"jamaahId":1,"latitude":21.4225,"longitude":39.8262,"pesan":"Help!"}' \
  http://localhost:5083/api/sos

# Dashboard
curl -H "X-Api-Key: HolySafar-API-Key-2025!" http://localhost:5083/api/dashboard
```

---

## Database Schema

### Core Tables
| Table | Description |
|-------|-------------|
| `Users` | User accounts (Admin/Agen/Jamaah) |
| `Jamaah` | Pilgrim data (KTP, passport, family) |
| `DokumenJamaah` | Uploaded documents |

### Business Tables
| Table | Description |
|-------|-------------|
| `Paket` | Hajj/Umrah packages |
| `Pembayaran` | Payment records |
| `Cicilan` | Installment records |
| `Keberangkatan` | Departure schedules |
| `Orders` / `OrderItems` | Marketplace orders |

### Communication
| Table | Description |
|-------|-------------|
| `ChatMessages` | User-to-user chat |
| `Pengumuman` | System announcements |
| `Notifikasi` | User notifications |

### Chatbot
| Table | Description |
|-------|-------------|
| `ChatSessions` | AI chat sessions |
| `ChatbotMessages` | Chat history |

### Emergency
| Table | Description |
|-------|-------------|
| `SOSTriggers` | SOS alerts |
| `KontakDarurat` | Emergency contacts |

### Education & Marketplace
| Table | Description |
|-------|-------------|
| `MateriManasik` | Learning materials |
| `Kuis` | Quiz questions |
| `Produk` | Products |
| `CartItems` | Shopping cart |

---

## Configuration Guide

### Database Provider
```json
"Database": {
    "Provider": "Postgre",       // SQLite | SqlServer | MySQL | Postgre
    "ConnectionStrings": {
        "Postgre": "Host=localhost;Database=HolySafar;Username=postgres;Password=;"
    }
}
```

### Storage Provider
```json
"Storage": {
    "Provider": "AzureBlob",     // FileSystem | AzureBlob | S3 | MinIO
    "AzureBlob": {
        "ConnectionString": "...",
        "ContainerName": "holysafar"
    }
}
```

### LLM Provider
```json
"Chatbot": {
    "Provider": "Ollama",        // OpenAI | Anthropic | Gemini | Ollama
    "Providers": {
        "Ollama": {
            "Endpoint": "http://localhost:11434/v1",
            "Model": "llama3.1"
        }
    },
    "Temperature": 0.7,
    "TavilyApiKey": "tvly-..."   // For internet search
}
```

### API Key
```json
"AppSettings": {
    "ApiKey": "HolySafar-API-Key-2025!"
}
```

---

## Deployment Guide

### Production Checklist
1. Change `AppSettings.ApiKey` to a strong random value
2. Set `Database.Provider` and fill connection string
3. Set `Storage.Provider` and fill credentials  
4. Remove `appsettings.Development.json` sensitive data
5. Set `ASPNETCORE_ENVIRONMENT=Production`
6. Configure HTTPS certificate
7. Set up reverse proxy (nginx/IIS)

### Docker (example)
```dockerfile
FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY ./publish .
ENV ASPNETCORE_URLS=http://+:5000
EXPOSE 5000
ENTRYPOINT ["dotnet", "HolySafar.dll"]
```

---

*Built by Jacky the Code Bender @ Gravicode Studios*
