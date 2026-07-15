# Getting Started

## Prerequisites

- **.NET 10 SDK** (`dotnet --version` should print `10.x`)
- Optional: `python` on PATH (for the *Run Python* workflow action)
- Optional: API keys for OpenAI / Anthropic / Gemini, or a local **Ollama** install (for AI features)

## Run

```bash
dotnet run --project src/AppBender.Web
```

On first start AppBender:

1. Creates the SQLite database (`appbender.db`) with the full schema (no migrations needed).
2. Seeds roles, three sample users, a demo organization, sample entities with data, forms, workflows, connectors, code snippets, and a published demo app.

## Sample users

| Email | Password | Role | Can do |
|---|---|---|---|
| admin@appbender.io | Admin123! | **Admin** | Everything incl. user management & settings |
| dev@appbender.io | Dev123! | **Developer** | Build entities, forms, workflows, connectors, use AI Studio |
| user@appbender.io | User123! | **EndUser** | Run published apps, fill forms, chat with App Guru |

## A 5-minute tour

1. **Home** — dashboard with stats and quick actions.
2. **Data Hub** (`/datahub`) — open *Customers*, browse records, edit the schema.
3. **Forms** (`/forms`) — open *Customer Registration* in the designer; drag components from the palette, watch the live preview, press ▶ to fill it.
4. **Workflows** (`/workflows`) — open *Welcome New Customer*; hit **▶ Test Run** and inspect the step logs. Create a customer record and watch the workflow fire automatically (entity trigger).
5. **Connectors** (`/connectors`) — open the *Open-Meteo Weather* custom connector and use the 🧪 test console with `{"latitude": -6.2, "longitude": 106.8}`.
6. **App Guru** (`/chat`) — chat with the AI assistant (needs an API key; see below). Try "Berapa 2^32? pakai kalkulator".
7. **AI Studio** (`/ai-studio`) — try *Prompt-to-App*: "Aplikasi manajemen event: event, peserta, registrasi".
8. **Apps** (`/apps`) — the seeded *Customer Portal* is published at `/a/customer-portal`.
9. **Analytics** (`/monitoring`) — usage, tokens, response times, run status.
10. **Swagger** (`/swagger`) — the auto-generated Data API.

## Configuration cheat-sheet (`appsettings.json`)

| Section | Purpose |
|---|---|
| `ConnectionStrings:DefaultConnection` | SQLite by default; see [deployment.md](deployment.md) for other DBs |
| `AI` | Providers (openai/anthropic/gemini/ollama), default provider, persona (`SystemPrompt`), temperature, max tokens |
| `Tavily:ApiKey` | Enables the internet-search tool & workflow action |
| `Storage` | `FileSystem` (default), `S3` (MinIO-compatible), or `AzureBlob` |
| `Email` | SMTP settings for the *Send Email* action |
| `Api:Keys` | API keys for machine-to-machine Data API access (`X-Api-Key` header) |

> ⚠️ Change `Api:Keys` before exposing the app anywhere public.
