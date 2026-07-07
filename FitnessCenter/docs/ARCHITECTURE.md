# FitnessCenter Documentation

## Architecture Overview

### Design Pattern
Aplikasi menggunakan **Clean Architecture** sederhana dengan pemisahan tanggung jawab yang jelas:

- **Models** — Domain models, enums, dan entities
- **Data** — Entity Framework Core DbContext dan konfigurasi database
- **Services** — Business logic layer
- **Api** — Minimal API endpoints
- **Components** — Blazor UI components (Pages, Features, Layout, Shared)

### Database
Mendukung 4 provider database:
- SQLite (default untuk development)
- SQL Server (production)
- MySQL
- PostgreSQL

Konfigurasi di `appsettings.json`:
```json
{
  "Database": {
    "Provider": "SQLite",
    "ConnectionStrings": {
      "SQLite": "Data Source=fitness_center.db"
    }
  }
}
```

### Storage
Mendukung 4 provider penyimpanan:
- File System (default)
- Azure Blob Storage
- AWS S3
- MinIO

### Authentication & Authorization
Menggunakan ASP.NET Core Identity dengan 4 role:
- **Admin** — Akses penuh ke semua fitur
- **Trainer** — Kelola kelas, lihat member
- **Member** — Booking kelas, forum, workout tracking
- **Staff** — Check-in/out, bantu operasional

### ChatBot "Coach Tommy"
Menggunakan multi-provider AI:
- OpenAI (GPT-4o)
- Anthropic (Claude 3.5 Sonnet)
- Google Gemini (2.0 Flash)
- Ollama (Llama 3.2 - local)

System prompt, temperature, dan model dikonfigurasi di `appsettings.json`.

### API Endpoints
Semua endpoint tersedia di `/api/v1/`:
- `GET /api/v1/members`
- `GET /api/v1/memberships`
- `GET /api/v1/trainers`
- `GET /api/v1/classes`
- `GET /api/v1/classes/schedule`
- `POST /api/v1/attendance/checkin/{userId}`
- `GET /api/v1/payments`
- `GET /api/v1/revenue`
- `GET /api/v1/feedback`
- `POST /api/v1/feedback`
- `GET /api/v1/events`
- `GET /api/v1/forum/posts`
- `GET /api/v1/leaderboard`
- `GET /api/v1/achievements/{userId}`
- `GET /api/v1/notifications/{userId}`
- `GET /api/v1/chat/sessions/{userId}`
- `POST /api/v1/chat/send`
- `GET /api/v1/export/members/csv`
- `GET /api/v1/export/members/excel`

Swagger: `/api/docs`

## Development

### Setup
1. Install .NET 10 SDK
2. Clone repository
3. `dotnet restore`
4. `dotnet run`

### Migrations
```bash
dotnet ef migrations add InitialCreate
dotnet ef database update
```

### Theme Customization
CSS custom properties di `wwwroot/app.css`:
- `:root` — Light theme
- `[data-theme="dark"]` — Dark theme
