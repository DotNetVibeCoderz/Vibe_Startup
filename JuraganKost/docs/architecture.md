# 🏗️ Arsitektur Sistem

## Overview

JuraganKost menggunakan arsitektur **Blazor Server** dengan pola **Clean Architecture** sederhana. Semua logika bisnis berjalan di server, UI dirender sebagai HTML melalui SignalR.

```
┌─────────────────────────────────────────────┐
│                  Browser                     │
│           (SignalR Connection)              │
└──────────────────┬──────────────────────────┘
                   │
┌──────────────────▼──────────────────────────┐
│            ASP.NET Core Host                 │
│  ┌──────────────────────────────────────┐   │
│  │        Blazor Server App             │   │
│  │  ┌──────────┐  ┌──────────────────┐  │   │
│  │  │ Components│  │      Pages       │  │   │
│  │  │  Layout   │  │  Auth/Pages/*    │  │   │
│  │  │  Shared   │  │  Chat, Kamar..   │  │   │
│  │  └──────────┘  └──────────────────┘  │   │
│  └──────────────────────────────────────┘   │
│  ┌──────────────────────────────────────┐   │
│  │         Services Layer               │   │
│  │  KamarSvc, PenghuniSvc, KostSvc...   │   │
│  │  ChatService (Semantic Kernel)       │   │
│  │  Storage Providers (IStorageProvider)│   │
│  └──────────────────────────────────────┘   │
│  ┌──────────────────────────────────────┐   │
│  │          Data Layer                  │   │
│  │  AppDbContext (EF Core)              │   │
│  │  Domain Models                       │   │
│  └──────────────────────────────────────┘   │
│  ┌──────────────────────────────────────┐   │
│  │        REST API (MinAPI)             │   │
│  │  /api/v1/kamar, /api/v1/penghuni... │   │
│  └──────────────────────────────────────┘   │
└─────────────────────────────────────────────┘
```

## Struktur Folder

```
JuraganKost/
├── Api/                         # REST API Endpoints (Minimal API)
│   └── ApiEndpoints.cs
├── Components/
│   ├── App.razor                 # Root component
│   ├── Routes.razor              # Routing configuration
│   ├── _Imports.razor            # Global usings untuk Blazor
│   ├── Layout/
│   │   ├── MainLayout.razor      # Layout utama (sidebar + topbar)
│   │   └── MinimalLayout.razor   # Layout untuk auth pages
│   ├── Pages/
│   │   ├── Auth/                 # Login, Register, Logout, dll
│   │   ├── Chat.razor            # Chat bot Mpok Inem
│   │   ├── Home.razor            # Dashboard
│   │   ├── KamarPage.razor       # Manajemen kamar
│   │   ├── PenghuniPage.razor    # Manajemen penghuni
│   │   └── ...                   # Halaman lainnya
│   └── Shared/
│       └── DashboardContent.razor
├── Data/
│   ├── Context/
│   │   └── AppDbContext.cs        # EF Core DbContext
│   └── Models/
│       ├── DomainModels.cs        # Kost, Kamar, Penghuni, dll
│       ├── ApplicationUser.cs     # Identity user extended
│       └── ChatModels.cs          # ChatThread, ChatMessageDb
├── Services/
│   ├── CoreServices.cs            # KamarSvc, PenghuniSvc, dll
│   ├── OtherServices.cs           # KomplainSvc, StaffSvc, dll
│   ├── KostService.cs             # Dashboard logic
│   ├── ExportAndSeedServices.cs   # Export CSV/Excel + Seed data
│   ├── Chat/
│   │   └── ChatService.cs         # Semantic Kernel + kernel functions
│   └── Storage/
│       ├── IStorageProvider.cs    # Interface storage
│       ├── FileSystemStorageProvider.cs
│       ├── AzureBlobStorageProvider.cs
│       ├── S3StorageProvider.cs
│       └── MinIOStorageProvider.cs
├── wwwroot/
│   └── app.css                    # Neo-brutalism soft theme
├── Program.cs                     # Entry point + DI configuration
├── appsettings.json               # Configuration
└── CurrencyHelper.cs              # Rupiah formatting helper
```

## Dependency Injection Flow

```
Program.cs
  │
  ├── AddDbContext<AppDbContext>
  ├── AddIdentity<ApplicationUser, IdentityRole>
  ├── AddRazorComponents().AddInteractiveServerComponents()
  ├── AddStorageProvider() ──► IStorageProvider (singleton)
  ├── AddScoped<KamarService>() ... AddScoped<SeedService>()
  ├── AddSingleton<ChatService>() ──► IServiceScopeFactory + IStorageProvider
  ├── AddSwaggerGen()
  └── Build → Run
```

## Design Patterns

| Pattern | Implementasi |
|---|---|
| **Repository** | Via EF Core DbSet langsung di services |
| **Strategy** | `IStorageProvider` — FileSystem / Azure / S3 / MinIO |
| **Singleton** | `ChatService` — menyimpan session di memory + DB |
| **Factory** | `IServiceScopeFactory` — buat scope DB di singleton |
| **Extension Methods** | `ToRupiah()` — formatting currency |

## Blazor Lifecycle

Setiap halaman Blazor mengikuti lifecycle:

1. `OnInitializedAsync()` — load data awal
2. `OnParametersSetAsync()` — saat parameter berubah
3. `StateHasChanged()` — trigger re-render
4. `Dispose()` — cleanup

**Catatan:** `@key` directive digunakan di `Home.razor` untuk memaksa Blazor menghancurkan & membuat ulang `DashboardContent` saat user memilih kost berbeda.
