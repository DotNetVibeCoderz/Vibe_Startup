# ⚡ AppBender

> **Platform Low-Code + Data Hub + Pengembangan Berbantuan AI** — bangun form, otomasi workflow, dan publikasikan aplikasi dalam hitungan menit.
> Dibangun dengan **.NET 10** dan **Blazor Server**.

*Read in English: [README.md](README.md)*

## Apa itu AppBender?

AppBender menggabungkan konsep **Power Apps** (desainer form visual), **Power Automate** (otomasi workflow berbasis blok), dan **Dataverse** (Data Hub terpadu) dalam satu platform self-hosted — lengkap dengan co-pilot AI ("**App Guru**") di seluruh produk.

| Pilar | Yang Anda dapatkan |
|---|---|
| 📝 **Form Designer** | Drag & drop 30+ komponen UI, preview real-time, binding ke entity, publish |
| ⚡ **Workflows** | Trigger (manual, jadwal cron, webhook, event entity, submit form) → 35+ action termasuk action AI, scripting C#/JS/Python, kondisi/loop/switch |
| 🗄️ **Data Hub** | Desainer entity & relasi visual, 20+ tipe field (termasuk Formula & AutoNumber), otomatis ter-expose sebagai **REST + GraphQL API dengan Swagger** |
| 🔌 **Connectors** | REST, GraphQL, SQL Server, PostgreSQL, MySQL, SQLite, S3/MinIO, Azure Blob, FileSystem, SMTP, webhook Slack/Teams/Discord, Tavily — plus **Custom Connector Builder** (spesifikasi JSON) |
| 🤖 **AI (App Guru)** | LLM multi-provider (OpenAI, Anthropic, Gemini, Ollama) via **Semantic Kernel**, tool calling (kalkulator, tanggal/waktu, pencarian internet, scraping URL, query dataset), chat multi-sesi dengan lampiran gambar/dokumen dan rendering markdown lengkap |
| 🧠 **AI Studio** | Generator skema, **Prompt-to-App**, asisten workflow, generator dashboard otomatis, model builder ML native, **knowledge base RAG** (PDF/Word/Excel/CSV), generator test case AI, penasihat deployment |
| 🌐 **Publishing** | Bundel form menjadi aplikasi dengan URL unik (`/a/{slug}`), akses berbasis role (Admin/Developer/EndUser), opsi akses anonim, isolasi data multi-tenant |
| 📈 **Operasional** | Dashboard analitik penggunaan & performa, monitor eksekusi workflow dengan log per langkah, audit log, versioning + rollback, import/export JSON |

## Mulai cepat

```bash
# prasyarat: .NET 10 SDK
dotnet run --project src/AppBender.Web
```

Buka URL yang tercetak lalu login dengan user contoh:

| User | Password | Role |
|---|---|---|
| `admin@appbender.io` | `Admin123!` | Admin |
| `dev@appbender.io` | `Dev123!` | Developer |
| `user@appbender.io` | `User123!` | EndUser |

Database (SQLite) dibuat dan diisi otomatis saat pertama kali dijalankan: entity contoh (customers, products, orders, tasks), form, workflow, connector, snippet, dan aplikasi demo yang sudah dipublish di `/a/customer-portal`.

## Mengaktifkan fitur AI

Tambahkan API key minimal satu provider di `src/AppBender.Web/appsettings.json`:

```jsonc
"AI": {
  "DefaultProvider": "openai",          // openai | anthropic | gemini | ollama
  "Providers": {
    "openai":   { "ApiKey": "sk-...", "Model": "gpt-4o-mini" },
    "anthropic":{ "ApiKey": "sk-ant-...", "Model": "claude-sonnet-5" },
    "gemini":   { "ApiKey": "...", "Model": "gemini-2.0-flash" },
    "ollama":   { "Model": "llama3.2" } // lokal, tanpa key
  }
},
"Tavily": { "ApiKey": "tvly-..." }      // opsional: tool pencarian internet
```

## API dalam 30 detik

Setiap entity Data Hub otomatis ter-expose (Swagger UI di `/swagger`):

```bash
# REST
curl -H "X-Api-Key: dev-api-key-change-me" \
  "http://localhost:5210/api/data/customers?filter=status eq Active&pageSize=10"

# GraphQL
curl -X POST -H "X-Api-Key: dev-api-key-change-me" -H "Content-Type: application/json" \
  -d '{"query":"{ products(top: 5, sortBy: \"price\", desc: true) { name price } }"}' \
  http://localhost:5210/api/graphql

# Workflow terpicu webhook (contoh bawaan)
curl -X POST -H "Content-Type: application/json" \
  -d '{"customerId":"<id>","qty":2,"unitPrice":150000}' \
  http://localhost:5210/api/webhooks/order-intake
```

## Dokumentasi

Dokumentasi lengkap ada di [`docs/`](docs/):

- [Getting Started](docs/getting-started.md) · [Arsitektur](docs/architecture.md)
- [Data Hub](docs/data-hub.md) · [Forms](docs/forms.md) · [Workflows](docs/workflows.md) · [Connectors](docs/connectors.md)
- [AI & App Guru](docs/ai.md) · [Data API](docs/api.md)
- [Keamanan & Tata Kelola](docs/security.md) · [Deployment](docs/deployment.md)

## Pengembangan

```bash
dotnet build              # build semua
dotnet test               # jalankan unit test
dotnet run --project src/AppBender.Web
```

Struktur solusi:

```
src/AppBender.Core   # model domain, Data Hub, workflow engine, connector, layanan AI
src/AppBender.Web    # UI Blazor Server + REST/GraphQL API + Swagger
tests/AppBender.Tests
docs/                # dokumentasi
```

## Lisensi

Proyek contoh/demo — bebas digunakan.
