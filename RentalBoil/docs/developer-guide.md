# 🔧 Panduan Pengembang

## Development Setup

```bash
# Prasyarat
dotnet --version  # Harus 10.0.x

# Clone & restore
cd RentalBoil
dotnet restore

# Run with hot reload
dotnet watch run

# Build
dotnet build

# Run tests (jika ada)
dotnet test
```

---

## Architecture Patterns

### Service Layer

Semua business logic ada di `Services/`. Setiap service bertanggung jawab untuk satu domain:

```csharp
public class VehicleService
{
    private readonly AppDbContext _db;

    public async Task<(List<Vehicle>, int)> GetVehiclesAsync(
        string? search, VehicleType? type, string? brand, ...)
    {
        var query = _db.Vehicles.Include(v => v.Photos).AsQueryable();
        // Case-insensitive search
        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.ToLowerInvariant();
            query = query.Where(v => v.Name.ToLower().Contains(s) || ...);
        }
        // ... filters, sort, paging
    }
}
```

### Database Access Pattern

**PENTING**: Semua akses database dari background thread (GPS simulator, timer) HARUS pakai `IServiceScopeFactory`:

```csharp
// ✅ BENAR
public class GpsSimulatorService
{
    private readonly IServiceScopeFactory _scopeFactory;

    private async Task WithDbAsync(Func<AppDbContext, Task> action)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await action(db);
    }
}

// ❌ SALAH — akan kena ObjectDisposedException
public class GpsSimulatorService
{
    private readonly AppDbContext _db; // JANGAN simpan sebagai field
}
```

---

## Adding New Pages

### 1. Buat file Razor component

```razor
@page "/my-new-page"
@attribute [Authorize]  @* opsional *@

<h1>My New Page</h1>
```

### 2. Tambah ke sidebar (MainLayout.razor)

```razor
<a href="/my-new-page" class="nav-item">
    <i class="bi bi-star"></i> <span>My Page</span>
</a>
```

---

## Adding New API Endpoints

Tambah di `Api/ApiEndpoints.cs`:

```csharp
var myGroup = api.MapGroup("/my-resource").WithTags("My Resource");

myGroup.MapGet("/", async (AppDbContext db) =>
{
    var items = await db.MyEntities.ToListAsync();
    return Results.Ok(items);
}).WithName("GetMyResource");
```

---

## Adding New Kernel Functions

Tambah di `Services/BotKernelFunctions.cs`:

```csharp
[KernelFunction("my_new_function")]
[Description("Deskripsi apa yang dilakukan fungsi ini")]
public async Task<string> MyNewFunction(
    [Description("Parameter 1")] string param1,
    [Description("Parameter 2")] int param2)
{
    var db = GetDb(); // Akses database via IServiceScopeFactory
    // ... logic
    return "Hasil dalam format Markdown";
}
```

Fungsi otomatis tersedia untuk AI chat bot setelah rebuild.

---

## CSS Guidelines

### Claymorphism Variables

```css
:root {
    --clay-bg: #f0f2f5;            /* Background */
    --clay-surface: rgba(255,255,255,0.7);  /* Card surface */
    --clay-shadow: 6px 6px 16px ...;        /* Outer shadow */
    --clay-shadow-inset: inset 4px 4px ...;  /* Inner shadow */
    --accent: #6C5CE7;              /* Primary color */
    --clay-radius: 16px;            /* Border radius */
}
```

### Component Classes

| Class | Usage |
|-------|-------|
| `.clay-card` | Card container |
| `.clay-card-inset` | Inset (inner shadow) card |
| `.btn-clay` | Button |
| `.btn-clay-primary` | Primary button (accent) |
| `.btn-clay-success/danger/warning` | Colored buttons |
| `.btn-icon` | Icon button (circle) |
| `.form-clay` | Form input |
| `.badge-clay` | Badge/tag |
| `.badge-clay-success/danger/warning/info/accent` | Colored badges |
| `.table-clay` | Table |
| `.stat-card` | Statistics card (icon + value) |
| `.vehicle-card` | Vehicle card (image + info) |
| `.filter-bar` | Filter/search bar |
| `.pagination-clay` | Pagination |
| `.grid-2/3/4` | CSS grid (2/3/4 columns) |

---

## Mobile Responsiveness

### Breakpoints

| Width | Mode |
|-------|------|
| > 1024px | Full sidebar (260px) |
| 768-1024px | Icon-only sidebar (72px) |
| < 768px | Hidden sidebar + hamburger drawer |

### Hamburger Menu

Mobile sidebar menggunakan class `.sidebar-open` yang di-toggle via `sidebarOpen` bool di `MainLayout.razor`:

```csharp
private bool sidebarOpen;
private void ToggleSidebar() => sidebarOpen = !sidebarOpen;
```

---

## GPS Simulator

### Arsitektur

```
GpsSimulatorHostedService (BackgroundService)
    └── loop setiap N detik
        └── cari booking Active
            └── GpsSimulatorService.SimulateGpsUpdateAsync()
                └── WithDbAsync() → update koordinat + speed + heading
```

### Testing Simulator

```bash
# Demo via GPS Tracking page
1. Buka /gps
2. Klik "Mulai Simulasi Demo"

# Via API
curl -X POST -H "X-Api-Key: ..." \
     -d '{"latitude":-6.209,"longitude":106.846,"speed":45}' \
     https://localhost:5001/api/vehicles/1/simulator-update
```

---

## Troubleshooting

### Blazor Components Not Responding

Pastikan `App.razor` punya `@rendermode`:

```razor
<Routes @rendermode="Microsoft.AspNetCore.Components.Web.RenderMode.InteractiveServer" />
<HeadOutlet @rendermode="Microsoft.AspNetCore.Components.Web.RenderMode.InteractiveServer" />
```

### Same-Component Navigation

Blazor tidak re-init komponen yang sama. Gunakan `OnParametersSetAsync`:

```csharp
protected override async Task OnParametersSetAsync()
{
    if (dataLoaded && BookingId != lastBookingId)
    {
        lastBookingId = BookingId;
        await LoadSelectedBooking();
        StateHasChanged();  // ← WAJIB
    }
}
```

### Chat Bot Tidak Merespon

1. Cek `AI:Provider` di `appsettings.json`
2. Cek API key provider (OpenAI/Gemini/Anthropic)
3. Cek Ollama running (`ollama serve`)
4. Cek `ToolCallBehavior.AutoInvokeKernelFunctions` di `CreateExecutionSettings()`
