# ⚡ AppBender

> **Low-Code Platform + Data Hub + AI-Assisted Development** — build forms, automate workflows, and publish apps in minutes.
> Built with **.NET 10** and **Blazor Server**.

*Baca dalam Bahasa Indonesia: [README.id.md](README.id.md)*

## What is AppBender?

AppBender combines the ideas of **Power Apps** (visual form designer), **Power Automate** (block-based workflow automation), and **Dataverse** (a unified Data Hub) into a single self-hosted platform — with an AI co-pilot ("**App Guru**") wired through the whole product.

| Pillar | What you get |
|---|---|
| 📝 **Form Designer** | Drag & drop 30+ UI components, live preview, entity binding, publishing |
| ⚡ **Workflows** | Triggers (manual, cron schedule, webhook, entity events, form submit) → 35+ actions incl. AI actions, C#/JS/Python scripting, conditions/loops/switch |
| 🗄️ **Data Hub** | Visual entity & relationship designer, 20+ field types (incl. Formula & AutoNumber), auto-exposed **REST + GraphQL API with Swagger** |
| 🔌 **Connectors** | REST, GraphQL, SQL Server, PostgreSQL, MySQL, SQLite, S3/MinIO, Azure Blob, FileSystem, SMTP, Slack/Teams/Discord webhooks, Tavily — plus a **Custom Connector Builder** (JSON spec) |
| 🤖 **AI (App Guru)** | Multi-provider LLM (OpenAI, Anthropic, Gemini, Ollama) via **Semantic Kernel**, tool calling (calculator, date/time, internet search, URL scraping, dataset queries), multi-session chat with image/document attachments and full markdown rendering |
| 🧠 **AI Studio** | Schema generator, **Prompt-to-App**, workflow assistant, auto-dashboard generator, native ML model builder, **RAG knowledge base** (PDF/Word/Excel/CSV), AI test-case generator, deployment advisor |
| 🌐 **Publishing** | Bundle forms into apps with unique URLs (`/a/{slug}`), role-based access (Admin/Developer/EndUser), optional anonymous access, multi-tenant data isolation |
| 📈 **Operations** | Usage analytics & performance dashboards, workflow run monitor with step logs, audit logs, versioning + rollback, JSON import/export |

## Quick start

```bash
# prerequisites: .NET 10 SDK
dotnet run --project src/AppBender.Web
```

Open the printed URL and log in with a sample user:

| User | Password | Role |
|---|---|---|
| `admin@appbender.io` | `Admin123!` | Admin |
| `dev@appbender.io` | `Dev123!` | Developer |
| `user@appbender.io` | `User123!` | EndUser |

The database (SQLite) is created and seeded automatically on first run with sample entities (customers, products, orders, tasks), forms, workflows, connectors, snippets, and a published demo app at `/a/customer-portal`.

## Enable AI features

Add an API key for at least one provider in `src/AppBender.Web/appsettings.json`:

```jsonc
"AI": {
  "DefaultProvider": "openai",          // openai | anthropic | gemini | ollama
  "Providers": {
    "openai":   { "ApiKey": "sk-...", "Model": "gpt-4o-mini" },
    "anthropic":{ "ApiKey": "sk-ant-...", "Model": "claude-sonnet-5" },
    "gemini":   { "ApiKey": "...", "Model": "gemini-2.0-flash" },
    "ollama":   { "Model": "llama3.2" } // local, no key needed
  }
},
"Tavily": { "ApiKey": "tvly-..." }      // optional: internet search tool
```

## API in 30 seconds

Every Data Hub entity is automatically exposed (Swagger UI at `/swagger`):

```bash
# REST
curl -H "X-Api-Key: dev-api-key-change-me" \
  "http://localhost:5210/api/data/customers?filter=status eq Active&pageSize=10"

# GraphQL
curl -X POST -H "X-Api-Key: dev-api-key-change-me" -H "Content-Type: application/json" \
  -d '{"query":"{ products(top: 5, sortBy: \"price\", desc: true) { name price } }"}' \
  http://localhost:5210/api/graphql

# Webhook-triggered workflow (seeded example)
curl -X POST -H "Content-Type: application/json" \
  -d '{"customerId":"<id>","qty":2,"unitPrice":150000}' \
  http://localhost:5210/api/webhooks/order-intake
```

## Documentation

Full docs live in [`docs/`](docs/):

- [Getting Started](docs/getting-started.md) · [Architecture](docs/architecture.md)
- [Data Hub](docs/data-hub.md) · [Forms](docs/forms.md) · [Workflows](docs/workflows.md) · [Connectors](docs/connectors.md)
- [AI & App Guru](docs/ai.md) · [Data API](docs/api.md)
- [Security & Governance](docs/security.md) · [Deployment](docs/deployment.md)

## Development

```bash
dotnet build              # build everything
dotnet test               # run unit tests
dotnet run --project src/AppBender.Web
```

Solution layout:

```
src/AppBender.Core   # domain models, Data Hub, workflow engine, connectors, AI services
src/AppBender.Web    # Blazor Server UI + REST/GraphQL API + Swagger
tests/AppBender.Tests
docs/                # documentation
```

## License

Sample/demo project — use freely.
