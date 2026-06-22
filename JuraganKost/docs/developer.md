# 👨‍💻 Developer Guide

## Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
- IDE: Visual Studio 2025+ / VS Code / JetBrains Rider
- SQLite (default, auto-install via NuGet)
- Optional: Docker, SQL Server, PostgreSQL, MySQL

## Setup Development

```bash
# Clone repository
git clone https://github.com/your-repo/JuraganKost.git
cd JuraganKost

# Restore packages
dotnet restore

# Run
dotnet run

# Open browser
# App: http://localhost:5085
# Swagger: http://localhost:5085/swagger
```

## Project Structure

```
JuraganKost/
├── Api/                  # REST API (MinAPI)
├── Components/
│   ├── Layout/           # MainLayout, MinimalLayout
│   ├── Pages/            # Semua halaman Blazor
│   │   └── Auth/         # Login, Register, dll
│   └── Shared/           # Komponen bersama
├── Data/
│   ├── Context/          # AppDbContext
│   └── Models/           # Entity models
├── Services/
│   ├── Chat/             # Semantic Kernel chat service
│   └── Storage/          # Storage providers
├── wwwroot/              # Static files & CSS
├── docs/                 # Dokumentasi
├── Program.cs            # Entry point
└── appsettings.json      # Configuration
```

## Coding Standards

### Naming Conventions
- **Classes/Methods:** PascalCase (`KamarService`, `GetDashboardAsync`)
- **Variables/Parameters:** camelCase (`kostId`, `_selectedKostId`)
- **Private fields:** underscore prefix (`_db`, `_botConfig`)
- **Razor files:** PascalCase (`KamarPage.razor`, `MainLayout.razor`)
- **Async methods:** suffix `Async` (`SendMessageAsync`)

### File Organization
- Satu class per file untuk domain models (kecuali `DomainModels.cs` yang berisi grouped models)
- Service class dikelompokkan berdasarkan domain: `CoreServices.cs`, `OtherServices.cs`
- Kernel functions dikelompokkan dalam class: `CommonFunctions`, `DatabaseFunctions`, dll

### Currency Formatting
Gunakan extension method `.ToRupiah()` untuk semua formatting currency:

```csharp
decimal price = 1500000;
string formatted = price.ToRupiah(); // "Rp1.500.000"
```

**JANGAN** gunakan `.ToString("C0")` — akan menghasilkan `$` pada culture en-US.

### EF Core Queries — SQLite Compatibility
- **Computed properties:** tidak bisa di-translate ke SQL. Gunakan ekspresi langsung.
- **ORDER BY decimal:** cast ke double → `.OrderBy(x => (double)x.HargaSewa)`

### Blazor Key Directive
Gunakan `@key` untuk komponen yang perlu di-recreate saat parameter berubah:

```razor
<DashboardContent @key="@_selectedKostId" KostId="@_selectedKostId" />
```

## Adding New Features

### Menambah Halaman Baru
1. Buat file di `Components/Pages/`
2. Tambah routing: `@page "/nama-halaman"`
3. Inject services: `@inject NamaService Svc`
4. Tambah link di `MainLayout.razor` (dengan role check jika perlu)

### Menambah API Endpoint
1. Tambah di `Api/ApiEndpoints.cs` dalam method `Map()`
2. Gunakan route prefix `/api/v1/`
3. Group dengan `.WithTags("Category")`

### Menambah Kernel Function
1. Tambah method di class yang sesuai (`CommonFunctions`, `DatabaseFunctions`, dll)
2. Decorated dengan `[KernelFunction("nama_fungsi")]` + `[Description("...")]`
3. Parameter decorated dengan `[Description("...")]`
4. Function otomatis tersedia via `FunctionChoiceBehavior.Auto()`

## Testing

```bash
# Build
dotnet build

# Run
dotnet run

# Watch mode (hot reload)
dotnet watch run
```

## Database Migrations

```bash
# Install EF tools (first time)
dotnet tool install --global dotnet-ef

# Create migration
dotnet ef migrations add InitialCreate

# Apply migration
dotnet ef database update
```

## Debugging Tips

- **Blazor errors:** cek browser console + Visual Studio output
- **Server errors:** lihat terminal `dotnet run`
- **DB queries:** enable `Microsoft.EntityFrameworkCore.Database.Command` logging di `appsettings.Development.json`
- **Chat issues:** cek `ChatService.cs` — fallback ke `GetLocalFallback()` jika AI gagal
