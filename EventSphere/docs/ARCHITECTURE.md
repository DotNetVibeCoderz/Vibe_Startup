# 🏗️ EventSphere Architecture

## Overview

EventSphere is a full-stack Event/Wedding Organizer platform built with **Blazor Server .NET 10**. It utilizes the server-side rendering model where UI updates are handled via SignalR real-time connections.

```
┌─────────────────────────────────────────────────────┐
│                    CLIENT BROWSER                    │
│  ┌──────────────┐  ┌─────────────┐  ┌────────────┐ │
│  │ Neomorphism  │  │  Blazor     │  │  JavaScript │ │
│  │ CSS Theme    │  │ Components  │  │  (utils.js) │ │
│  └──────────────┘  └─────────────┘  └────────────┘ │
│           │                │                │        │
│           └────────────────┼────────────────┘        │
│                    SignalR WebSocket                  │
└────────────────────────┬────────────────────────────┘
                         │
┌────────────────────────┴────────────────────────────┐
│                   BLAZOR SERVER                       │
│  ┌─────────────────────────────────────────────────┐│
│  │              Components Layer                     ││
│  │  ┌──────────┐ ┌──────────┐ ┌──────────────────┐││
│  │  │  Layout  │ │  Pages   │ │    Shared        │││
│  │  │MainLayout│ │19 Pages  │ │ Components       │││
│  │  │ MinLayout│ │          │ │                  │││
│  │  └──────────┘ └──────────┘ └──────────────────┘││
│  └─────────────────────────────────────────────────┘│
│  ┌─────────────────────────────────────────────────┐│
│  │              Services Layer (12 Services)        ││
│  │  Event │ Vendor │ Guest │ Budget │ Task │ Chat  ││
│  │  Notification │ Media │ Storage │ Export │ AI   ││
│  │  Dashboard │ DataSeeder                          ││
│  └─────────────────────────────────────────────────┘│
│  ┌─────────────────────────────────────────────────┐│
│  │              Data Layer                          ││
│  │  ┌──────────────────┐ ┌───────────────────────┐ ││
│  │  │  AppDbContext    │ │  7 Model Files        │ ││
│  │  │  (EF Core)       │ │  20+ Entities         │ ││
│  │  └──────────────────┘ └───────────────────────┘ ││
│  └─────────────────────────────────────────────────┘│
└────────────────────────┬────────────────────────────┘
                         │
┌────────────────────────┴────────────────────────────┐
│              EXTERNAL SERVICES & STORAGE              │
│  ┌──────────┐ ┌──────────┐ ┌──────────┐ ┌─────────┐│
│  │ SQLite   │ │ OpenAI   │ │ Tavily   │ │  File   ││
│  │SQLServer │ │ Gemini   │ │  Search  │ │ System  ││
│  │  MySQL   │ │ Anthropic│ │   API    │ │AzureBlob││
│  │PostgreSQL│ │  Ollama  │ │          │ │S3/MinIO ││
│  └──────────┘ └──────────┘ └──────────┘ └─────────┘│
└─────────────────────────────────────────────────────┘
```

## Layer Architecture

### 1. Presentation Layer (`Components/`)
- **Layout**: `MainLayout.razor` (sidebar + top bar), `MinimalLayout.razor` (login/register)
- **Pages**: 19 Blazor pages covering all features
- **Shared**: Reusable components like `RedirectToLogin`

### 2. Services Layer (`Services/`)
12 service classes implementing business logic:

| Service | Responsibility |
|---------|---------------|
| `EventService` | Event CRUD, status, budget tracking |
| `VendorService` | Vendor directory, contracts, invoices, reviews |
| `GuestService` | RSVP, seating, attendee management |
| `BudgetService` | Budget items, estimation vs actual |
| `TaskService` | Task/checklist with priority & progress |
| `ChatService` | Real-time messaging sessions |
| `NotificationService` | System notifications & reminders |
| `MediaService` | Gallery, documents, forum, feedback, loyalty |
| `StorageService` | Multi-provider file storage |
| `ExportService` | CSV & Excel export |
| `AiChatService` | Semantic Kernel + multi-model AI |
| `DashboardService` | Analytics & statistics |

### 3. Data Layer (`Data/`)
- **Models**: 7 files, 20+ entity classes
- **Context**: `AppDbContext` with multi-provider support
- **Seeding**: `DataSeeder` for development data

### 4. Security Layer
- ASP.NET Core Identity with 6 roles
- Role-based navigation filtering
- Authorization policies per page

## Design Patterns

| Pattern | Usage |
|---------|-------|
| **Repository** | EF Core DbContext as unit of work |
| **Dependency Injection** | All services registered as Scoped |
| **Role-Based Access Control** | 6 roles, policy-based authorization |
| **Auto-Invoke (AI)** | Semantic Kernel FunctionChoiceBehavior.Auto() |
| **Multi-Provider** | Database, Storage, AI models selectable via config |

## Key Technologies

| Layer | Technology |
|-------|-----------|
| Framework | .NET 10, Blazor Server, C# |
| Database | EF Core, SQLite/SQLServer/MySQL/PostgreSQL |
| Auth | ASP.NET Core Identity |
| AI/LLM | Microsoft Semantic Kernel |
| UI | Custom Neomorphism CSS |
| Export | CsvHelper + ClosedXML |
| Storage | Azure.Storage.Blobs, AWSSDK.S3, Minio SDK |
| Markdown | Markdig |
