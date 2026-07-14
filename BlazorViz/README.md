# BlazorViz

> 🇮🇩 [Baca dalam Bahasa Indonesia](README.id.md)

**BlazorViz** is a self-hosted, Power BI-like web application for data analysis and visualization,
built with **Blazor Server on .NET 10**. Connect to almost any data source, shape data with a UI-driven
ETL pipeline or scripts (C#, JavaScript, Python), build drag-and-drop dashboards with 20+ chart types,
chat with your data through the **Data Wizard** AI assistant, and share everything with your team.

![.NET 10](https://img.shields.io/badge/.NET-10-6C5CE7) ![Blazor Server](https://img.shields.io/badge/Blazor-Server-00B894)

## ✨ Features

### Core
- **Multi-database connectors** — SQLite, SQL Server, PostgreSQL, MySQL, Oracle, Excel, CSV, REST API, GraphQL.
- **Import / export** — upload Excel/CSV/JSON files; export any dataset or panel to CSV, JSON, Excel, PDF or PNG image.
- **ETL pipeline** — filter, select, rename, compute, sort, aggregate, join, distinct, limit and pivot steps composed in the UI.
- **Scripting** — transform datasets with C# (Roslyn), JavaScript (Jint) or Python (external interpreter), with 15+ built-in ready-to-modify templates.
- **Real-time refresh** — per-dataset cache TTL and per-dashboard auto-refresh interval.

### Visualization
- **20+ chart types** (Apache ECharts): line, area, bar, stacked bar, horizontal bar, pie, donut, rose, scatter, bubble, radar, waterfall, treemap, sunburst, heatmap, gauge, funnel, sankey, boxplot, candlestick, KPI cards and data tables.
- **Custom visuals** — write your own panel code against **ECharts, Chart.js or D3.js**.
- **Geo maps** — Leaflet-based point/bubble maps from lat/lng columns.
- **Advanced dashboards** — multi-tab, multi-panel, drag-and-drop + resize (Gridstack), versioned with rollback.
- **Interactive filters** — slicer chips, dropdown, multi-select, date range; applied across all panels of the same dataset.
- **Neo-brutalism soft design** — responsive, with dark/light theme toggle.

### AI & Automation
- **Data Wizard** — chat with your data via **Semantic Kernel**; providers: OpenAI, Anthropic, Gemini, Ollama (local). Streaming replies rendered as rich markdown (tables, code, images). Attach images (sent as image content) and documents (linked + inlined).
- **Kernel functions** — math evaluation, date/time, internet search, URL scraping, plus dataset/dashboard query tools so the assistant answers from *real* data.
- **RAG** — index PDF / Word / Excel / text documents into a vector store (**InMemory**, **Qdrant**, **Chroma** or **Azure AI Search**) using `Microsoft.Extensions.VectorData` attributes; the Data Wizard cites relevant excerpts automatically. Works offline out of the box via a local hashing embedder.
- **Predictive analytics (ML.NET)** — SSA time-series forecasting, SDCA regression, K-Means clustering.
- **Smart recommendations** — chart suggestions inferred from your dataset's column types.

### Security & Collaboration
- **Authentication** — ASP.NET Core Identity: register, login, password reset, profiles, 2FA, passkeys.
- **Role-based access** — Admin / Analyst / Viewer.
- **Audit logs** — every notable action recorded, filterable and sortable.
- **Sharing** — public share links (`/share/{token}`) and `<iframe>` embed code per dashboard.
- **Version control** — every dashboard save creates a version; roll back any time.

### Monitoring & Admin
- **Usage analytics** — queries, chats, estimated LLM tokens, API calls; per-day charts and top users.
- **Performance dashboard** — live request timing, traffic by path, memory/CPU/uptime.
- **Storage backends** — FileSystem (default), Azure Blob, S3 / MinIO.
- **REST API + Swagger** — `/api/v1` (datasets, query, export, dashboards, chat) secured by API keys; interactive docs at `/swagger`.
- **Plugin system** — drop `.csx` files in `plugins/` to add custom Data Wizard functions.

## 🚀 Getting started

### Prerequisites
- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
- (optional) Python 3 on PATH — only for Python dataset scripts
- (optional) [Ollama](https://ollama.com) or an OpenAI/Anthropic/Gemini API key — for AI features

### Run

```bash
git clone <this-repo>
cd BlazorViz
dotnet run --project src/BlazorViz
```

Open the printed URL. The database is migrated and seeded automatically with sample data:

| User | Password | Role |
|------|----------|------|
| `admin@blazorviz.local` | `Admin123!` | Admin |
| `analyst@blazorviz.local` | `Analyst123!` | Analyst |
| `viewer@blazorviz.local` | `Viewer123!` | Viewer |

Two sample dashboards (**Sales Overview**, **World & Web Analytics**), four sample datasets and a demo API key
are created on first run.

### Configure AI

Edit the `Ai` section in `src/BlazorViz/appsettings.json` (provider, model, persona, temperature) — see
[docs/configuration.md](docs/configuration.md). Put secrets in user-secrets or environment variables:

```bash
dotnet user-secrets set "Ai:Providers:OpenAI:ApiKey" "sk-…" --project src/BlazorViz
```

The default provider is **Ollama** (`http://localhost:11434`, model `llama3.1`) so everything works without a cloud key.
RAG works offline out of the box with the local embedder and in-memory vector index.

## 📚 Documentation

| Doc | Contents |
|-----|----------|
| [docs/architecture.md](docs/architecture.md) | Solution layout, services, data flow |
| [docs/user-guide.md](docs/user-guide.md) | End-to-end usage walkthrough (EN) |
| [docs/user-guide.id.md](docs/user-guide.id.md) | Panduan pengguna (ID) |
| [docs/configuration.md](docs/configuration.md) | appsettings reference: AI, RAG, storage |
| [docs/api.md](docs/api.md) | REST API reference & examples |
| [docs/etl-and-scripting.md](docs/etl-and-scripting.md) | ETL steps & script templates |
| [docs/plugins.md](docs/plugins.md) | Writing Data Wizard plugins |

## 🧱 Tech stack

Blazor Server (.NET 10) · EF Core + SQLite (app data) · Semantic Kernel · Microsoft.Extensions.VectorData ·
ML.NET · Apache ECharts · Chart.js · D3.js · Leaflet · Gridstack · ClosedXML · QuestPDF · PdfPig ·
Markdig · Jint · Roslyn scripting · Swashbuckle.

## 📄 License

Sample project — use freely.
