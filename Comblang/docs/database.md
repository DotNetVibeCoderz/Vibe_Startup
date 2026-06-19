# 🗄️ Database Setup — Dokumentasi

## Provider yang Didukung

Comblang menggunakan **Entity Framework Core** dan mendukung 4 database engine:

| Provider | Key | Package |
|----------|-----|---------|
| **SQLite** | `SQLite` | `Microsoft.EntityFrameworkCore.Sqlite` |
| **SQL Server** | `SqlServer` | `Microsoft.EntityFrameworkCore.SqlServer` |
| **MySQL** | `MySql` | `Pomelo.EntityFrameworkCore.MySql` |
| **PostgreSQL** | `PostgreSql` | `Npgsql.EntityFrameworkCore.PostgreSQL` |

---

## Konfigurasi

Ubah provider di `appsettings.json`:

```json
{
  "DatabaseProvider": "SQLite",
  "ConnectionStrings": {
    "SQLite": "Data Source=comblang.db",
    "SqlServer": "Server=localhost;Database=Comblang;Trusted_Connection=True;",
    "MySql": "Server=localhost;Database=Comblang;User=root;Password=xxx;",
    "PostgreSql": "Host=localhost;Database=Comblang;Username=postgres;Password=xxx;"
  }
}
```

## SQLite (Default)

✅ **Langsung jalan tanpa setup tambahan!**

```json
{
  "DatabaseProvider": "SQLite",
  "ConnectionStrings": {
    "SQLite": "Data Source=comblang.db"
  }
}
```

Database otomatis dibuat di root folder proyek (`comblang.db`) saat aplikasi pertama kali dijalankan.

---

## SQL Server

```json
{
  "DatabaseProvider": "SqlServer",
  "ConnectionStrings": {
    "SqlServer": "Server=localhost;Database=Comblang;Trusted_Connection=True;TrustServerCertificate=True;"
  }
}
```

### Dengan Docker:
```bash
docker run -d --name sqlserver \
  -e "ACCEPT_EULA=Y" \
  -e "SA_PASSWORD=YourPassword123!" \
  -p 1433:1433 \
  mcr.microsoft.com/mssql/server:2022-latest
```

Connection string:
```
Server=localhost;Database=Comblang;User=sa;Password=YourPassword123!;TrustServerCertificate=True;
```

---

## MySQL

```json
{
  "DatabaseProvider": "MySql",
  "ConnectionStrings": {
    "MySql": "Server=localhost;Database=Comblang;User=root;Password=mypassword;"
  }
}
```

### Dengan Docker:
```bash
docker run -d --name mysql \
  -e MYSQL_ROOT_PASSWORD=mypassword \
  -e MYSQL_DATABASE=Comblang \
  -p 3306:3306 \
  mysql:8.0
```

---

## PostgreSQL

```json
{
  "DatabaseProvider": "PostgreSql",
  "ConnectionStrings": {
    "PostgreSql": "Host=localhost;Database=Comblang;Username=postgres;Password=mypassword;"
  }
}
```

### Dengan Docker:
```bash
docker run -d --name postgres \
  -e POSTGRES_PASSWORD=mypassword \
  -e POSTGRES_DB=Comblang \
  -p 5432:5432 \
  postgres:16
```

---

## Entity Relationship Diagram (Simplified)

```
User (1) ────── (1) Profile
User (1) ────── (N) InterestTag
User (1) ────── (N) Swipe
User (1) ────── (N) Message
User (1) ────── (N) Match
User (1) ────── (N) Boost
User (1) ────── (N) Report
User (1) ────── (N) AuditLog
User (M) ────── (N) UserBlock (self-referencing)

Match ── UserId1 → User
Match ── UserId2 → User

Message ── SenderId → User
Message ── ReceiverId → User (nullable)
Message ── GroupRoomId → GroupRoom (nullable)

GroupRoom (1) ── (N) GroupMember
GroupRoom (1) ── (N) Message

GiftTransaction ── SenderId → User
GiftTransaction ── ReceiverId → User
GiftTransaction ── GiftId → Gift

Event (1) ── (N) EventParticipant
EventParticipant ── UserId → User
```

---

## Migrations

Saat ini menggunakan `EnsureCreatedAsync()` untuk auto-create database. Untuk production, gunakan migrations:

```bash
# Install EF Core tools
dotnet tool install --global dotnet-ef

# Create migration
dotnet ef migrations add InitialCreate

# Apply migration
dotnet ef database update
```

Ubah `Program.cs`:
```csharp
// Ganti EnsureCreatedAsync dengan:
await db.Database.MigrateAsync();
```

---

## Seed Data

Untuk menambahkan seed data, buat class `Data/DataSeeder.cs`:

```csharp
public static class DataSeeder
{
    public static async Task SeedAsync(AppDbContext db)
    {
        if (!await db.Users.AnyAsync())
        {
            db.Users.Add(new User { Email = "admin@comblang.com", Username = "admin", ... });
            await db.SaveChangesAsync();
        }
    }
}
```

Panggil di `Program.cs`:
```csharp
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await db.Database.EnsureCreatedAsync();
    await DataSeeder.SeedAsync(db);
}
```
