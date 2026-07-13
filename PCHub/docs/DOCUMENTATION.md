# PCHub - Dokumentasi Lengkap

## 📚 Daftar Isi
1. [Arsitektur Sistem](#arsitektur-sistem)
2. [Tech Stack](#tech-stack)
3. [Struktur Project](#struktur-project)
4. [Konfigurasi](#konfigurasi)
5. [Deployment](#deployment)
6. [API Reference](#api-reference)
7. [Database](#database)
8. [ChatBot Koh Dedi](#chatbot-koh-dedi)
9. [Development Guide](#development-guide)

---

## Arsitektur Sistem

```
┌──────────────────────────────────────────────────────┐
│                   PCHub System                       │
├─────────────────────┬────────────────────────────────┤
│   Admin Web (Blazor)│      Client App (WPF)          │
│   Blazor Server     │      .NET WPF                  │
│   Port: 5001        │      Desktop App               │
├─────────────────────┴────────────────────────────────┤
│               REST API (Minimal API)                 │
│            /api/* endpoints (JSON)                   │
├──────────────────────────────────────────────────────┤
│              PCHub.Shared Library                    │
│   Models | DTOs | Interfaces | Services | Enums      │
├──────────────────────────────────────────────────────┤
│              Database Layer (EF Core)                │
│   SQLite | SQL Server | PostgreSQL | MySQL           │
└──────────────────────────────────────────────────────┘
```

### Komponen:
- **PCHub.Admin** - Blazor Server web app untuk admin/operator
- **PCHub.Client** - WPF desktop app untuk PC client/user
- **PCHub.Shared** - Shared library (models, services, DTOs, interfaces)

---

## Tech Stack

| Komponen | Teknologi |
|---|---|
| Framework | .NET 9.0 |
| Admin UI | Blazor Server (Interactive Server) |
| Client UI | WPF (.NET 9.0 Windows) |
| ORM | Entity Framework Core 9.0 |
| Auth | Cookie Auth + JWT Token |
| DB | SQLite / SQL Server / PostgreSQL / MySQL |
| Export | ClosedXML (Excel) + CsvHelper (CSV) |
| Password | BCrypt.Net-Next |
| Swagger | Swashbuckle.AspNetCore |
| Container | Docker + Docker Compose |

---

## Struktur Project

```
PCHub/
├── src/
│   ├── PCHub.Shared/              # Shared Library
│   │   ├── Data/                  # AppDbContext, SeedData
│   │   ├── DTOs/                  # Request/Response DTOs
│   │   ├── Enums/                 # Shared Enums
│   │   ├── Interfaces/            # Service Interfaces
│   │   ├── Models/                # Entity Models
│   │   ├── Services/              # Business Logic
│   │   └── Utilities/             # Helper Functions
│   ├── PCHub.Admin/               # Blazor Server Admin
│   │   ├── Components/            # Razor Components
│   │   │   ├── Layout/            # Layout Components
│   │   │   └── Pages/             # Admin Pages
│   │   ├── Endpoints/             # API Endpoints
│   │   ├── wwwroot/               # Static Files
│   │   ├── appsettings.json       # Configuration
│   │   └── Program.cs             # Entry Point
│   └── PCHub.Client/              # WPF Client App
│       ├── Converters/            # XAML Converters
│       ├── Resources/             # Resources
│       ├── Services/              # Client Services
│       ├── ViewModels/            # MVVM ViewModels
│       └── Views/                 # XAML Pages
├── docs/                          # Documentation
├── Dockerfile                     # Docker Build
├── docker-compose.yml             # Docker Compose
├── PCHub.sln                      # Solution File
├── README.md                      # English
└── README.id.md                   # Indonesia
```

---

## Konfigurasi

### Admin (appsettings.json)

```json
{
  "Database": {
    "Provider": "SQLite",           // SQLite | SqlServer | PostgreSQL | MySQL
    "SQLite": "Data Source=PCHub.db"
  },
  "Auth": {
    "JwtSecret": "...",
    "TokenExpiryHours": 72
  },
  "ChatBot": {
    "Provider": "LocalRule",        // LocalRule | OpenAI | Anthropic | Gemini | Ollama
    "Model": "gpt-4o-mini",
    "Temperature": 0.7
  },
  "Storage": {
    "Provider": "FileSystem",       // FileSystem | AzureBlob | S3 | MinIO
    "BasePath": "uploads"
  },
  "Email": {
    "Smtp": { "Server": "smtp.gmail.com", "Port": 587 }
  }
}
```

### Client (app.config)

```xml
<appSettings>
  <add key="ApiBaseUrl" value="https://localhost:5001/api"/>
  <add key="DarkMode" value="false"/>
  <add key="Language" value="id"/>
</appSettings>
```

---

## Deployment

### Docker
```bash
# Build & run
docker compose up -d

# View logs
docker compose logs -f

# Stop
docker compose down
```

### Manual
```bash
# Admin
cd src/PCHub.Admin
dotnet run

# Client
cd src/PCHub.Client
dotnet run
```

---

## API Reference

Base URL: `https://localhost:5001/api`

### Auth
| Method | Endpoint | Description |
|---|---|---|
| POST | `/api/auth/login` | Login user |
| POST | `/api/auth/register` | Register user |
| GET | `/api/auth/profile/{id}` | Get user profile |

### PCs
| Method | Endpoint | Description |
|---|---|---|
| GET | `/api/pcs` | List all PCs (paged) |
| GET | `/api/pcs/{id}` | Get PC by ID |
| POST | `/api/pcs` | Create PC |
| PUT | `/api/pcs` | Update PC |
| DELETE | `/api/pcs/{id}` | Delete PC |
| PUT | `/api/pcs/{id}/resources` | Update resource usage |

### Games
| Method | Endpoint | Description |
|---|---|---|
| GET | `/api/games` | List games (paged) |
| POST | `/api/games` | Create game |
| PUT | `/api/games` | Update game |
| DELETE | `/api/games/{id}` | Delete game |

### Billing
| Method | Endpoint | Description |
|---|---|---|
| POST | `/api/billing/start` | Start billing session |
| POST | `/api/billing/stop/{id}` | Stop billing session |
| GET | `/api/billing/active/{userId}` | Get active session |
| GET | `/api/billing/history/{userId}` | Get billing history |

### ChatBot
| Method | Endpoint | Description |
|---|---|---|
| GET | `/api/chat/sessions` | List chat sessions |
| POST | `/api/chat/sessions` | Create session |
| POST | `/api/chat/send` | Send message |
| DELETE | `/api/chat/sessions/{id}` | Delete session |

### Dashboard
| Method | Endpoint | Description |
|---|---|---|
| GET | `/api/dashboard/stats` | Get dashboard statistics |

### Reservations
| Method | Endpoint | Description |
|---|---|---|
| GET | `/api/reservations` | List reservations |
| POST | `/api/reservations` | Create reservation |
| PUT | `/api/reservations/{id}/cancel` | Cancel reservation |

Swagger UI: `https://localhost:5001/swagger`

---

## Database

### Entity Relationship Diagram (Konseptual)

```
Users ──┬── BillingSessions ──── Pcs
        ├── Reservations ─────── Pcs
        ├── UserMemberships ──── Memberships
        ├── SupportTickets ───── SupportReplies
        └── TournamentParticipants ─── Tournaments ─── Games

Pcs ──── PcSessions ──── Games

Promos (standalone)
Notifications (standalone)
SystemConfigs (standalone)
FinancialReports (standalone)
ActivityLogs (standalone)
```

### Seed Data
- 17 Users (admin, operator, members)
- 15 PCs (Standard, High, Premium, Streaming)
- 12 Games (FPS, MOBA, RPG, Battle Royale, etc.)
- 5 Membership tiers
- 4 Active Promos
- 3 Tournaments

---

## ChatBot Koh Dedi

### Arsitektur
```
User Message → ChatBotService → AI Provider (OpenAI/Anthropic/Gemini/Ollama)
                               ↓ (fallback jika offline)
                        Local Rule Engine → Response
```

### Konfigurasi (appsettings.json)
```json
"ChatBot": {
  "Name": "Koh Dedi",
  "Provider": "LocalRule",
  "Model": "gpt-4o-mini",
  "Temperature": 0.7,
  "SystemPrompt": "Anda adalah Koh Dedi...",
  "FallbackToLocalRule": true
}
```

### Provider yang Didukung
- **LocalRule** - Rule-based (default, tanpa API key)
- **OpenAI** - GPT-4o, GPT-4o-mini
- **Anthropic** - Claude 3 Haiku/Sonnet/Opus
- **Gemini** - Gemini 1.5 Flash/Pro
- **Ollama** - Model lokal (Llama, Mistral, dll)

### Common Functions (Kernel Functions)
- Query harga & membership
- Cek ketersediaan PC  
- Daftar game
- Info turnamen
- Jam operasional
- Support & FAQ

---

## Development Guide

### Prerequisites
- .NET 9.0 SDK
- Visual Studio 2022 / VS Code / Rider
- Git

### Setup Development
```bash
# Clone & restore
git clone <repo-url>
cd PCHub
dotnet restore PCHub.sln

# Build
dotnet build PCHub.sln

# Run Admin
cd src/PCHub.Admin
dotnet run

# Run Client (separate terminal)
cd src/PCHub.Client
dotnet run
```

### Default Login
- Username: `admin`
- Password: `Admin123!`

### Adding New Features
1. Add models in `PCHub.Shared/Models/`
2. Add DTOs in `PCHub.Shared/DTOs/`
3. Add service interface in `PCHub.Shared/Interfaces/`
4. Implement service in `PCHub.Shared/Services/`
5. Register in `Program.cs`
6. Add API endpoint in `ApiEndpoints.cs`
7. Add admin page in `PCHub.Admin/Components/Pages/`
8. Add client feature in `PCHub.Client/`

### Coding Standards
- C# naming conventions (PascalCase public, camelCase private)
- XML documentation comments on public APIs
- Async/await for all I/O operations
- Service layer pattern (Interface → Implementation)
- MVVM pattern for WPF client
- Neo-Brutalism design principles for UI

---

*Dokumentasi terakhir diperbarui: 2025*
