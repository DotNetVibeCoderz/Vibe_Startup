# 🔷 BlazePoint

**A SharePoint-style collaboration platform rebuilt with .NET 10 Blazor Server.**

> 🇮🇩 Baca dalam [Bahasa Indonesia](README.id.md)

BlazePoint brings document management, custom lists, CMS pages with webparts, workflow automation, team sites, and an AI assistant ("Clippy") into a single fast, lightweight Blazor Server application.

![.NET 10](https://img.shields.io/badge/.NET-10-purple) ![Blazor Server](https://img.shields.io/badge/Blazor-Server-blueviolet) ![License MIT](https://img.shields.io/badge/license-MIT-green)

## ✨ Features

| Area | Highlights |
|---|---|
| 📁 **Document Management** | Upload, auto-versioning with rollback, metadata tagging, preview (image/PDF/video/audio/text), recycle bin |
| 📋 **Lists & Libraries** | Custom lists with dynamic columns (Text/Number/Date/Choice/Boolean/Url), filtering, grouping, sorting |
| 🔍 **Search Engine** | Full-text search + semantic vector search (Local in-DB / Qdrant / Chroma) with pluggable embeddings (Local hash / OpenAI / Ollama) |
| 🔐 **Authentication** | Login/logout, registration, password reset, role-based access (Admin / Editor / Viewer) via ASP.NET Core Identity |
| ⚙️ **Workflow Automation** | Visual drag-and-drop workflow designer (Start/Approval/Condition/Notify/End nodes), approval tasks, notifications |
| 📄 **CMS** | Page editor with drag-and-drop webparts (Text/Markdown, Image, Clock, Calculator, Weather, Leaflet Map, Documents, Events), custom masterpage layouts (News / Intranet / Web), publish versioning & rollback |
| 🧭 **Navigation** | UI-managed top navigation and quick-launch side menu |
| 🏢 **Team Sites** | Site collections per department with scoped documents, lists, events, and discussions |
| 💬 **Discussion Boards** | Threaded replies, @mentions with notifications, markdown |
| 📅 **Calendar** | Shared month view, reminders, ICS feed for Outlook/Google Calendar subscription, "Add to Google Calendar" links |
| 🔗 **File Sharing** | Public/private links, expiration dates, download counters |
| 📝 **Form Designer** | Drag-and-drop form builder, JSON schema export/import, template library, submissions view |
| 📊 **Dashboard** | KPI cards, ChartJs.Blazor charts (upload trend, storage by type, activity), recent-activity feed |
| 🤖 **Clippy AI Chatbot** | Semantic Kernel with OpenAI / Anthropic / Gemini / Ollama, multi-session chat, image & document attachments, streaming markdown answers, tool calling (Tavily web search, URL scraping, file reading, date/time, math, internal data queries) |
| 🛠️ **Admin Panel** | User & role management, site settings, monitoring/audit log, search reindex |
| 🌐 **APIs** | REST endpoints under `/api/*` and GraphQL at `/graphql` |
| 🎨 **UI/UX** | Facebook-style responsive design with light/dark theme |

## 🚀 Quick Start

```bash
# prerequisites: .NET 10 SDK
cd src/BlazePoint
dotnet run
# open http://localhost:5112
```

On first run the app creates a SQLite database (`App_Data/blazepoint.db`), seeds sample data, and builds the search index. **No API keys are required** for the core features — semantic search falls back to a local hash embedder.

### Demo accounts

| Role | Email | Password |
|---|---|---|
| 👑 Admin | `admin@blazepoint.local` | `Blaze123!` |
| ✏️ Editor | `editor@blazepoint.local` | `Blaze123!` |
| 👁️ Viewer | `viewer@blazepoint.local` | `Blaze123!` |

## ⚙️ Configuration (`appsettings.json`)

Everything is provider-based and configured in `src/BlazePoint/appsettings.json`:

| Section | Options |
|---|---|
| `Database:Provider` | `Sqlite` (default) · `SqlServer` · `PostgreSql` · `MySql` — connection strings under `ConnectionStrings` |
| `Storage:Provider` | `FileSystem` (default) · `AzureBlob` · `S3` · `MinIO` |
| `Search:VectorStore` | `Local` (default, in-database cosine) · `Qdrant` · `Chroma` |
| `Search:Embeddings:Provider` | `Local` (default, no key needed) · `OpenAI` · `Ollama` |
| `Clippy:Provider` | `OpenAI` · `Anthropic` · `Gemini` · `Ollama` — plus `SystemPrompt`, `Temperature`, `MaxTokens` per spec |
| `Clippy:Tavily:ApiKey` | enables Clippy's internet search tool |

To enable Clippy, set an API key for your chosen provider, e.g.:

```json
"Clippy": {
  "Provider": "Ollama",
  "Ollama": { "Endpoint": "http://localhost:11434", "Model": "llama3.2" }
}
```

(Ollama needs no API key — great for local testing.)

## 📚 Documentation

- [Architecture](docs/architecture.md) — layers, data model, request flow
- [API Reference](docs/api-reference.md) — REST & GraphQL
- [Configuration Guide](docs/configuration.md) — every provider explained
- [Custom WebParts & Masterpages](docs/custom-webparts.md) — extend the CMS with your own Razor components
- [Sample Data & Users](docs/sample-data.md) — what gets seeded

## 🧱 Tech Stack

.NET 10 · Blazor Server · EF Core 10 (SQLite/SQL Server/PostgreSQL/MySQL) · ASP.NET Core Identity · Semantic Kernel · Microsoft.Extensions.AI · HotChocolate GraphQL · ChartJs.Blazor.Fork · Markdig · Leaflet.js · Azure Blob / AWS S3 / MinIO SDKs

## 📦 Deployment

```bash
dotnet publish src/BlazePoint -c Release -o publish
# run publish/BlazePoint.exe (Windows) or dotnet publish/BlazePoint.dll
```

Set `ASPNETCORE_ENVIRONMENT=Production` and configure a reverse proxy (IIS/Nginx/Caddy) with WebSockets enabled (required by Blazor Server).

## License

MIT
