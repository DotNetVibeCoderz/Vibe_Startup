# 👨‍💻 DEV_GUIDE.md - Panduan Developer

## 🏗️ Arsitektur Aplikasi

```
LandLord/
├── Components/          # Blazor UI Components
│   ├── Layout/          # MainLayout, NavMenu
│   ├── Pages/           # Route pages
│   │   ├── Auth/        # Login, Register, Reset
│   │   ├── Home.razor
│   │   ├── Maps.razor
│   │   ├── MasterData.razor
│   │   ├── Dashboard.razor
│   │   ├── Chat.razor
│   │   ├── Settings.razor
│   │   └── Profile.razor
│   └── Shared/          # Shared components
├── Models/              # Entity models
│   ├── Tanah.cs         # Land metadata model
│   ├── Bangunan.cs      # Building metadata model
│   ├── Document.cs      # Document/attachment model
│   ├── User.cs          # User authentication model
│   └── ChatModels.cs    # Chat session & message
├── Data/
│   ├── AppDbContext.cs  # EF Core DbContext
│   └── SeedData.cs      # Sample data seeder
├── Services/            # Business logic
│   ├── IAuthService.cs / AuthService.cs
│   ├── ITanahService.cs / TanahService.cs
│   ├── IBangunanService.cs / BangunanService.cs
│   ├── IDocumentService.cs / DocumentService.cs
│   ├── IChatService.cs / ChatService.cs
│   ├── ISettingsService.cs / SettingsService.cs
│   ├── IStorageService.cs / FileSystemStorageService.cs
├── wwwroot/
│   ├── css/
│   │   └── neo-brutalism.css  # Main theme
│   └── uploads/               # File uploads
├── Program.cs           # App startup & DI
├── appsettings.json     # Configuration
└── LandLord.csproj      # Project file
```

---

## 🔧 Teknologi Stack

| Layer | Technology |
|-------|-----------|
| Framework | Blazor Server (.NET 10) |
| ORM | Entity Framework Core |
| Database | SQLite / SQL Server / MySQL / PostgreSQL |
| AI | Semantic Kernel |
| Auth | ASP.NET Core Cookie Authentication |
| CSS | Neo Brutalism (Custom) |
| Chart | Custom CSS-based |
| Export | CsvHelper + ClosedXML |

---

## 🚀 Development Setup

```bash
# Clone & restore
dotnet restore

# Run with hot reload
dotnet watch run

# Build
dotnet build

# Add migration
dotnet ef migrations add InitialCreate
dotnet ef database update
```

---

## 📦 NuGet Packages

- `Microsoft.EntityFrameworkCore.*` - ORM
- `Microsoft.SemanticKernel` - AI integration
- `BCrypt.Net-Next` - Password hashing
- `CsvHelper` - CSV export
- `ClosedXML` - Excel export
- `Pomelo.EntityFrameworkCore.MySql` - MySQL support
- `Npgsql.EntityFrameworkCore.PostgreSQL` - PostgreSQL support

---

## 🔌 Integrasi LLM

Chat bot Frengky Ganteng menggunakan pattern service sederhana yang bisa diperluas dengan Semantic Kernel:

```csharp
// Contoh integrasi Semantic Kernel
var kernel = Kernel.CreateBuilder()
    .AddOpenAIChatCompletion("gpt-4o", apiKey)
    .Build();

// Tambahkan plugin/functions
kernel.ImportPluginFromObject(new DatabasePlugin(context));
```

---

## 🎨 Theme Customization

Edit `wwwroot/css/neo-brutalism.css`:
- CSS Variables di `:root` untuk light theme
- `.dark-theme` selector untuk dark mode
- Neo brutalism style menggunakan border dan box-shadow

---

## 🔒 Authentication

Cookie-based authentication dengan:
- Login/Logout flows
- Role-based authorization (Admin, User, Viewer)
- Password hashing dengan BCrypt
- Reset password flow

---

## 📝 Adding New Features

1. Tambahkan model di `Models/`
2. Buat interface di `Services/`
3. Implement service
4. Register di `Program.cs`
5. Buat Blazor component di `Components/Pages/`
6. Tambahkan route dengan `@page`

---

**Created by GraviCode Studios**
