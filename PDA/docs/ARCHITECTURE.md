# 🏗️ Architecture

Arsitektur sistem PDA (Personal Data Analyst) secara detail.

---

## Overview Arsitektur

```
┌─────────────────────────────────────────────────────────────┐
│                      Browser (Client)                        │
│  ┌─────────┐ ┌────────┐ ┌──────────┐ ┌──────────────────┐  │
│  │  HTML    │ │  CSS   │ │ Chart.js │ │  SignalR WebSocket│  │
│  │ (Blazor) │ │(NeoBru)│ │(Charts)  │ │  (Real-time)     │  │
│  └─────────┘ └────────┘ └──────────┘ └──────────────────┘  │
└───────────────────────┬─────────────────────────────────────┘
                        │ HTTPS + SignalR
┌───────────────────────▼─────────────────────────────────────┐
│                    ASP.NET Core Server                        │
│  ┌─────────────────────────────────────────────────────┐    │
│  │              Blazor Server (Razor Components)         │    │
│  │  ┌──────┐ ┌──────┐ ┌───────┐ ┌────────┐ ┌────────┐ │    │
│  │  │Layout│ │Pages │ │Shared │ │Forms   │ │Auth    │ │    │
│  │  └──────┘ └──────┘ └───────┘ └────────┘ └────────┘ │    │
│  └─────────────────────────────────────────────────────┘    │
│                                                               │
│  ┌─────────────────────────────────────────────────────┐    │
│  │                   Service Layer                       │    │
│  │  ┌───────────┐ ┌──────────┐ ┌──────────────────┐   │    │
│  │  │ChatAgent  │ │LlmProvider│ │SchemaExtraction  │   │    │
│  │  │Service    │ │Factory    │ │Service           │   │    │
│  │  └───────────┘ └──────────┘ └──────────────────┘   │    │
│  │  ┌───────────┐ ┌──────────┐ ┌──────────────────┐   │    │
│  │  │RAG Index  │ │Storage   │ │Monitoring        │   │    │
│  │  │Service    │ │Service   │ │Service           │   │    │
│  │  └───────────┘ └──────────┘ └──────────────────┘   │    │
│  │  ┌───────────┐ ┌──────────┐ ┌──────────────────┐   │    │
│  │  │AuditLog   │ │Database  │ │CommonFunctions   │   │    │
│  │  │Service    │ │Connector │ │Service           │   │    │
│  │  └───────────┘ └──────────┘ └──────────────────┘   │    │
│  └─────────────────────────────────────────────────────┘    │
│                                                               │
│  ┌─────────────────────────────────────────────────────┐    │
│  │                   Data Layer                          │    │
│  │  ┌──────────────────────┐ ┌──────────────────────┐  │    │
│  │  │   AppDbContext (EF)   │ │  Identity (ASP.NET)   │  │    │
│  │  └──────────────────────┘ └──────────────────────┘  │    │
│  │  ┌──────────────────────┐ ┌──────────────────────┐  │    │
│  │  │   SQLite / SQLServer  │ │   Vector Store        │  │    │
│  │  │   / MySQL             │ │   (In-Memory/Qdrant)  │  │    │
│  │  └──────────────────────┘ └──────────────────────┘  │    │
│  └─────────────────────────────────────────────────────┘    │
└─────────────────────────────────────────────────────────────┘
                        │
┌───────────────────────▼─────────────────────────────────────┐
│                   External Services                          │
│  ┌──────────┐ ┌──────────┐ ┌────────┐ ┌──────────────┐    │
│  │ OpenAI   │ │Anthropic │ │ Gemini │ │   Ollama     │    │
│  │ API      │ │ API      │ │  API   │ │  (Local)     │    │
│  └──────────┘ └──────────┘ └────────┘ └──────────────┘    │
│  ┌──────────┐ ┌──────────┐ ┌────────┐ ┌──────────────┐    │
│  │ User DBs │ │Knowledge │ │ Azure  │ │ S3/MinIO     │    │
│  │(External)│ │  Base    │ │ Blob   │ │ Storage      │    │
│  └──────────┘ └──────────┘ └────────┘ └──────────────┘    │
└─────────────────────────────────────────────────────────────┘
```

---

## Layer Detail

### 1. Presentation Layer (Blazor Server)

**Teknologi**: Blazor Server dengan SignalR WebSocket

**Komponen Utama**:
- `MainLayout.razor` - Layout dengan sidebar navigasi
- `Routes.razor` - Router dengan auth guard
- Halaman: Home, Chat, Connections, Monitoring, AuditLogs, RAG Index, Profile
- `DashboardPanel.razor` - Komponen dashboard expandable
- `RedirectToLogin.razor` - Redirect handler

**State Management**:
- Blazor Server state via SignalR circuit
- CascadingAuthenticationState untuk auth
- Component-level state untuk chat messages

### 2. Service Layer

**ChatAgentService** - Inti aplikasi:
1. Menerima pesan user
2. Membangun system prompt dengan schema context
3. Memanggil LLM dengan tools
4. Menjalankan tool loop (max 5 iterasi)
5. Menyimpan response dan dashboard

**LlmProviderFactory** - Factory pattern:
- `OpenAiProvider` - OpenAI & OpenAI-compatible APIs
- `AnthropicProvider` - Anthropic Claude API
- `GeminiProvider` - Google Gemini API
- `OllamaProvider` - Ollama local API

**SchemaExtractionService** - Ekstraksi schema:
- Menggunakan `DbConnection.GetSchema()` untuk membaca metadata
- Mendeteksi relasi antar tabel
- Generate teks schema untuk system prompt LLM

**RagIndexingService** - Document indexing:
- Scan folder KnowledgeBase secara rekursif
- Extract text dari berbagai format
- Chunking dengan overlap
- Index ke vector store (In-Memory default)

**RagBackgroundWorker** - Background service:
- Berjalan periodik (configurable, default 30 menit)
- Scan folder KnowledgeBase
- Index dokumen baru/modified

**PdaMonitoringService** - In-memory metrics:
- ConcurrentDictionary untuk counters
- ConcurrentQueue untuk time-series data
- Thread-safe dengan Interlocked

**AuditLogService** - Activity logging:
- Mencatat semua aktivitas ke database
- Mendukung filter, sort, pagination

**DatabaseConnectorFactory** - Multi-DB:
- `IDbConnection` factory berdasarkan tipe
- Support: SQLite, SQLServer, PostgreSQL
- Connection testing

### 3. Data Layer

**AppDbContext** (EF Core):
- Identity tables (AspNetUsers, AspNetRoles, dll)
- DatabaseConnections
- ChatSessions & ChatMessages
- AuditLogs
- RagIndexedDocuments

**Entity Relationships**:
```
ApplicationUser (1) ──→ (*) ChatSessions
ApplicationUser (1) ──→ (*) DatabaseConnections
ApplicationUser (1) ──→ (*) AuditLogs
DatabaseConnection (1) ──→ (*) ChatSessions
ChatSession (1) ──→ (*) ChatMessages
```

---

## Alur Kerja Utama

### Chat Flow
```
User Input → ChatAgentService.ProcessMessageAsync()
  ├── Save user message
  ├── Build system prompt (schema context)
  ├── Get LLM provider
  ├── Call LLM with tools
  │   ├── Tool: queryToDatabase → Execute SQL
  │   ├── Tool: createDashboard → Generate HTML
  │   ├── Tool: searchKnowledgeBase → RAG search
  │   └── Tool: readDataFromUrl → HTTP fetch
  ├── Save assistant response
  ├── Log audit
  └── Update monitoring
```

### RAG Flow
```
RagBackgroundWorker (periodic)
  ├── Scan KnowledgeBase folder
  ├── Compute file hash (SHA256)
  ├── Check if already indexed
  ├── Extract text content
  ├── Chunk text (with overlap)
  ├── Index to vector store
  └── Update database record
```

### Auth Flow
```
Request → Authentication Middleware
  ├── Cookie check
  ├── [Authorize] attribute check
  ├── Role check ([Authorize(Roles = "Admin")])
  └── Redirect to /login if not authenticated
```

---

## Design Patterns

| Pattern | Implementasi |
|---------|-------------|
| **Factory** | LlmProviderFactory, DatabaseConnectorFactory, StorageServiceFactory |
| **Strategy** | ILlmProvider (multiple LLM strategies) |
| **Singleton** | MonitoringService, RagIndexingService |
| **Scoped** | ChatAgentService, AuditLogService, DbContext |
| **Background Service** | RagBackgroundWorker |
| **Repository** | AppDbContext (via EF Core) |

---

## Thread Safety

- `PdaMonitoringService`: ConcurrentDictionary + ConcurrentQueue + Interlocked
- `RagIndexingService`: lock pada vector stores
- `AppDbContext`: Scoped per request (tidak shared)
- `ChatAgentService`: Scoped, state via database
