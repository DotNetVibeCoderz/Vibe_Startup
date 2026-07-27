# Joka OTA - Panduan Pengembang

## Daftar Isi
1. [Memulai](#memulai)
2. [Struktur Kode](#struktur-kode)
3. [Menambah Fitur Baru](#menambah-fitur-baru)
4. [Testing](#testing)
5. [Kontribusi](#kontribusi)

---

## Memulai

### Clone & Install
```bash
git clone <repository-url>
cd Joka
dotnet restore
dotnet build
```

### Development Mode
```bash
dotnet watch run
```
Aplikasi akan berjalan di `http://localhost:5000` dengan hot reload.

### Struktur Database
Database SQLite (`Data/joka.db`) dibuat otomatis saat pertama kali aplikasi dijalankan. 
Sample data dimuat melalui `SeedData.InitializeAsync()`.

---

## Struktur Kode

### Konvensi Penamaan
- **Models:** PascalCase, singular (e.g., `Flight`, `HotelBooking`)
- **Pages:** PascalCase, plural untuk list (e.g., `Flights.razor`, `Hotels.razor`)
- **Services:** PascalCase dengan suffix `Service` (e.g., `ChatBotService`)
- **CSS Classes:** kebab-case (e.g., `.travel-card`, `.chat-bubble`)

### Menambah Model Baru
1. Buat class di folder `Models/` yang sesuai
2. Turunkan dari `BaseEntity` untuk mendapatkan `Id`, `CreatedAt`, dll
3. Tambahkan `DbSet<T>` di `AppDbContext`
4. Buat migrasi: `dotnet ef migrations add NamaMigrasi`

### Menambah Halaman Baru
1. Buat file `.razor` di `Components/Pages/`
2. Tambahkan `@page "/route"` directive
3. Gunakan `@inject AppDbContext DbContext` untuk akses database
4. Tambahkan link navigasi di `MainLayout.razor`

### Menambah API Endpoint
Tambahkan di `Program.cs` dalam `apiGroup`:
```csharp
apiGroup.MapGet("/new-endpoint", async (AppDbContext db) =>
{
    var data = await db.SomeTable.ToListAsync();
    return Results.Ok(data);
});
```

---

## Menambah Fitur Baru

### AI ChatBot — Menambah Provider Baru
Di `ChatBotService.cs`, tambahkan case di `InitializeAsync()`:
```csharp
case "NewProvider":
    builder.AddOpenAIChatCompletion(model, apiKey);
    break;
```

### Storage — Menambah Provider Baru
1. Implement `IStorageService` interface
2. Daftarkan di DI container
3. Tambahkan case di `StorageServiceFactory.Create()`

---

## Testing

### Unit Tests
```bash
dotnet test
```

### Manual Testing Checklist
- [ ] Home page loads with stats
- [ ] Flight search with filters works
- [ ] Hotel search with city/stars works  
- [ ] Booking modal opens and creates booking
- [ ] Chat page creates/deletes sessions
- [ ] Dark/light theme toggle works
- [ ] API endpoints return correct data
- [ ] Swagger UI loads at `/swagger`

---

## Kontribusi

1. Fork repository
2. Buat branch fitur (`git checkout -b fitur/namafitur`)
3. Commit perubahan (`git commit -m 'Tambah fitur X'`)
4. Push ke branch (`git push origin fitur/namafitur`)
5. Buat Pull Request

### Code Style
- Gunakan C# conventions (4 spaces, PascalCase, camelCase)
- Tambahkan XML comments untuk public methods
- Gunakan `string.Empty` bukan `""`
- Gunakan `var` untuk type inference yang jelas

---

*Dokumentasi versi: 1.0.0 | Terakhir diperbarui: Juli 2025*
