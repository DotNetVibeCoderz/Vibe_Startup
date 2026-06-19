# 📘 Comblang — Architecture Overview

## High-Level Architecture

```
┌──────────────────────────────────────────────────────┐
│                    Browser / Client                    │
│  ┌──────────┐  ┌──────────┐  ┌───────────────────┐  │
│  │  Blazor  │  │  REST    │  │  SignalR WebSocket │  │
│  │  Server  │  │  API     │  │  (Real-time)       │  │
│  └────┬─────┘  └────┬─────┘  └────────┬──────────┘  │
└───────┼─────────────┼─────────────────┼──────────────┘
        │             │                 │
┌───────┴─────────────┴─────────────────┴──────────────┐
│                   ASP.NET Core 10                      │
│  ┌──────────────────────────────────────────────┐    │
│  │            Middleware Pipeline                 │    │
│  │  Auth → Audit → StaticFiles → Endpoints       │    │
│  └──────────────────────────────────────────────┘    │
│  ┌──────────┐ ┌──────────┐ ┌───────────────────┐    │
│  │  Auth    │ │  Swagger │ │  SignalR Hubs      │    │
│  │  Service │ │  /api/docs│ │  /hubs/chat        │    │
│  └──────────┘ └──────────┘ └───────────────────┘    │
└──────────────────────────────────────────────────────┘
        │
┌───────┴──────────────────────────────────────────────┐
│                   Service Layer                        │
│  ┌──────────┐ ┌──────────┐ ┌───────────────────┐    │
│  │ Matching │ │  Chat    │ │  AI (Semantic      │    │
│  │ Engine   │ │  Service │ │  Kernel)           │    │
│  └──────────┘ └──────────┘ └───────────────────┘    │
│  ┌──────────┐ ┌──────────┐ ┌───────────────────┐    │
│  │ Traffic  │ │  Geo     │ │  Storage           │    │
│  │ Service  │ │  Service │ │  Providers          │    │
│  └──────────┘ └──────────┘ └───────────────────┘    │
└──────────────────────────────────────────────────────┘
        │
┌───────┴──────────────────────────────────────────────┐
│                    Data Layer                          │
│  ┌──────────────────────────────────────────────┐    │
│  │              AppDbContext (EF Core)            │    │
│  │  17 Entity Models + Fluent API Configuration  │    │
│  └──────────────────────────────────────────────┘    │
│  ┌──────────┐ ┌──────────┐ ┌───────────────────┐    │
│  │  SQLite  │ │  SQL     │ │  PostgreSQL/MySQL  │    │
│  │  (Dev)   │ │  Server  │ │  (Production)      │    │
│  └──────────┘ └──────────┘ └───────────────────┘    │
└──────────────────────────────────────────────────────┘
```

## Design Patterns

### Dependency Injection (DI)
Semua service diregistrasi di `Program.cs` menggunakan built-in .NET DI container. Service lifetimes:
- **Singleton**: `KernelService`, `IStorageProvider`
- **Scoped**: `AuthService`, `MatchEngine`, `ChatService`, `TrafficService`
- **Transient**: (tidak digunakan secara eksplisit)

### Strategy Pattern — Storage
```csharp
// Interface tunggal, banyak implementasi
IStorageProvider
├── FileStorageProvider     // Lokal filesystem
├── S3StorageProvider       // AWS S3
├── AzureBlobStorageProvider // Azure Blob
└── MinioStorageProvider    // MinIO (S3-compatible)
```
`StorageProviderFactory.Create(config, rootPath)` memilih provider berdasarkan `appsettings.json`.

### Strategy Pattern — AI Model
```csharp
KernelService mendukung 4 backend AI:
├── OpenAI    → AddOpenAIChatCompletion (native)
├── Anthropic → AddOpenAIChatCompletion (via API endpoint)
├── Gemini    → AddOpenAIChatCompletion (via API endpoint)
└── Ollama    → AddOpenAIChatCompletion (via local /v1 endpoint)
```
Semua model menggunakan interface `IChatCompletionService` dari Semantic Kernel.

### Repository Pattern (via EF Core)
`AppDbContext` berfungsi sebagai Unit of Work + Repository. Service langsung mengakses DbSet untuk query.

## SignalR Real-Time Flow

```
Client A                     Server                     Client B
   │                           │                           │
   │── SendMessage ───────────▶│                           │
   │                           │── ReceiveMessage ───────▶│
   │                           │                           │
   │                           │◀── Typing ───────────────│
   │◀── UserTyping ───────────│                           │
```

## Authentication Flow

```
1. Cookie Auth (Blazor Pages)
   Login → Validate → Set Cookie → Redirect

2. JWT Auth (REST API)
   Login → Generate JWT → Return Token → Bearer Auth

3. API Key Auth (REST API)
   X-Api-Key header → Validate against config
```
