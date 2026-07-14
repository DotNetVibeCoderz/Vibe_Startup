# BlazePoint — Configuration Guide

All configuration lives in `src/BlazePoint/appsettings.json`. Changing providers requires an app restart.

## Database (`Database:Provider` + `ConnectionStrings`)

| Provider | Connection string key | Notes |
|---|---|---|
| `Sqlite` *(default)* | `ConnectionStrings:Sqlite` | Zero-setup; file at `App_Data/blazepoint.db` |
| `SqlServer` | `ConnectionStrings:SqlServer` | |
| `PostgreSql` | `ConnectionStrings:PostgreSql` | Npgsql provider |
| `MySql` | `ConnectionStrings:MySql` | Pomelo provider |

Schema is created via `EnsureCreated` on startup — point at an empty database and BlazePoint bootstraps itself, including sample data. Delete the database to re-seed.

## File Storage (`Storage:Provider`)

| Provider | Settings | Notes |
|---|---|---|
| `FileSystem` *(default)* | `FileSystem:RootPath` | Relative paths resolve under the content root |
| `AzureBlob` | `ConnectionString`, `Container` | Container auto-created |
| `S3` | `AccessKey`, `SecretKey`, `Region`, `Bucket` | AWS S3 |
| `MinIO` | `Endpoint`, `AccessKey`, `SecretKey`, `Bucket` | S3-compatible, path-style |

## Search

### Vector store (`Search:VectorStore`)
- `Local` *(default)* — embeddings stored in the DB, cosine similarity computed in-process. Fine up to tens of thousands of items.
- `Qdrant` — set `Search:Qdrant:Endpoint` (default `http://localhost:6333`); collection auto-created.
- `Chroma` — set `Search:Chroma:Endpoint` (default `http://localhost:8000`).

### Embeddings (`Search:Embeddings:Provider`)
- `Local` *(default)* — deterministic hashed bag-of-words (384 dims). No API key, works offline; quality is lexical rather than truly semantic.
- `OpenAI` — `ApiKey`, `Model` (default `text-embedding-3-small`).
- `Ollama` — `Endpoint`, `Model` (default `nomic-embed-text`). Local, free.

> After switching embeddings/vector store, run **Admin → Pengaturan → Reindex** to rebuild vectors.

## Clippy chatbot (`Clippy`)

| Key | Meaning |
|---|---|
| `Provider` | `OpenAI` \| `Anthropic` \| `Gemini` \| `Ollama` |
| `SystemPrompt` | Clippy's persona (Indonesian by default) |
| `Temperature`, `MaxTokens` | Generation settings |
| `OpenAI:ApiKey/Model/Endpoint` | Endpoint override supports OpenAI-compatible gateways |
| `Anthropic:ApiKey/Model/Endpoint` | Uses Anthropic's OpenAI-compatible endpoint |
| `Gemini:ApiKey/Model` | Google AI Studio key |
| `Ollama:Endpoint/Model` | No key needed — recommended for local dev |
| `Tavily:ApiKey` | Enables the `search_internet` tool ([tavily.com](https://tavily.com)) |

Clippy's tools (always registered): current date/time, date difference, math (`DataTable.Compute`), unit conversion, Tavily web search, URL scraping, file-from-URL reading, and internal data queries (portal statistics, documents, lists, sites, events, discussions, page content).

## Misc (`BlazePoint`)

| Key | Default | Meaning |
|---|---|---|
| `MaxUploadSizeMb` | 100 | Upload cap (UI enforces 100 MB) |
| `RecycleBinRetentionDays` | 30 | Informational — purge is manual (Admin) |
| `DefaultTheme` | light | Users toggle light/dark per browser |

## Roles

| Role | Rights |
|---|---|
| **Admin** | Everything: users, settings, permanent delete, monitoring |
| **Editor** | Create/edit documents, pages, lists, forms, workflows |
| **Viewer** | Read content, discuss, fill forms, chat with Clippy |

New self-registered users get **Viewer**; change roles in Admin → Pengguna.
