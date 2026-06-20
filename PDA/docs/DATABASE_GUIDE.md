# 🗄️ Database Guide

Skema database, migrasi, dan sample data.

---

## Database Providers

PDA menggunakan **Entity Framework Core** dan mendukung:

| Provider | NuGet Package | Status |
|----------|--------------|--------|
| SQLite | `Microsoft.EntityFrameworkCore.Sqlite` | ✅ Default |
| SQL Server | `Microsoft.EntityFrameworkCore.SqlServer` | ✅ Supported |
| PostgreSQL | `Npgsql.EntityFrameworkCore.PostgreSQL` | ✅ Supported |
| MySQL | `Pomelo.EntityFrameworkCore.MySql` | 🔧 Configurable |

---

## Entity Relationship Diagram

```
┌─────────────────────┐
│   ApplicationUser   │ (ASP.NET Identity)
│─────────────────────│
│ + Id                │──┐
│ + UserName          │  │
│ + Email             │  │
│ + FullName          │  │
│ + AvatarUrl         │  │
│ + CreatedAt         │  │
│ + LastLoginAt       │  │
│ + IsActive          │  │
│ + ThemePreference   │  │
└─────────────────────┘  │
                          │ 1:N
         ┌────────────────┼────────────────┐
         │                │                │
         ▼                ▼                ▼
┌──────────────┐  ┌─────────────┐  ┌──────────────┐
│ChatSession   │  │DatabaseConn │  │AuditLog      │
│──────────────│  │─────────────│  │──────────────│
│+ Id          │  │+ Id         │  │+ Id          │
│+ Title       │  │+ Name       │  │+ Timestamp   │
│+ UserId (FK) │  │+ Desc       │  │+ Category    │
│+ DbConnId(FK)│  │+ DBType     │  │+ Action      │
│+ ModelProvdr │  │+ ConnString │  │+ Description │
│+ ModelName   │  │+ FilePath   │  │+ Details     │
│+ Temperature │  │+ UserId (FK)│  │+ IpAddress   │
│+ MaxTokens   │  │+ CreatedAt  │  │+ UserAgent   │
│+ SystemPrmpt │  │+ LastUsedAt │  │+ DurationMs  │
│+ CreatedAt   │  │+ IsActive   │  │+ IsSuccess   │
│+ UpdatedAt   │  └─────────────┘  │+ UserId (FK) │
│+ IsActive    │                    └──────────────┘
└──────────────┘
      │ 1:N
      ▼
┌──────────────┐
│ChatMessage   │
│──────────────│
│+ Id          │
│+ SessionId   │
│+ Role        │
│+ Content     │
│+ DashboardH  │
│+ PromptTkns  │
│+ CompletionT │
│+ TotalTokens │
│+ ResponseTm  │
│+ Attachments │
│+ Timestamp   │
└──────────────┘

┌──────────────────┐
│RagIndexedDocument│
│──────────────────│
│+ Id              │
│+ FileName        │
│+ FilePath        │
│+ FileType        │
│+ FileSize        │
│+ IndexedAt       │
│+ FileModifiedAt  │
│+ ChunkCount      │
│+ VectorProvider  │
│+ ContentHash     │
│+ Status          │
│+ ErrorMessage    │
│+ Keywords        │
└──────────────────┘
```

---

## Tabel Detail

### ApplicationUser (ASP.NET Identity)

Extends `IdentityUser` dengan field tambahan:

| Column | Type | Description |
|--------|------|-------------|
| Id | nvarchar(450) | Primary key |
| UserName | nvarchar(256) | Username |
| Email | nvarchar(256) | Email |
| FullName | nvarchar(100) | Nama lengkap |
| AvatarUrl | nvarchar(500) | URL avatar |
| CreatedAt | datetime | Tanggal registrasi |
| LastLoginAt | datetime | Login terakhir |
| IsActive | bit | Status aktif |
| ThemePreference | nvarchar(50) | 'dark' or 'light' |

### DatabaseConnection

| Column | Type | Description |
|--------|------|-------------|
| Id | int | Primary key (auto-increment) |
| Name | nvarchar(200) | Nama koneksi |
| Description | nvarchar(1000) | Keterangan |
| DatabaseType | nvarchar(50) | SQLite, SQLServer, PostgreSQL, dll |
| ConnectionString | nvarchar(2000) | Connection string |
| FilePath | nvarchar(500) | Path file (untuk SQLite/CSV) |
| UserId | nvarchar(450) | FK → ApplicationUser |
| CreatedAt | datetime | Tanggal dibuat |
| LastUsedAt | datetime | Terakhir digunakan |
| IsActive | bit | Status aktif |

### ChatSession

| Column | Type | Description |
|--------|------|-------------|
| Id | int | Primary key (auto-increment) |
| Title | nvarchar(300) | Judul sesi |
| UserId | nvarchar(450) | FK → ApplicationUser |
| DatabaseConnectionId | int? | FK → DatabaseConnection |
| ModelProvider | nvarchar(50) | OpenAI, Anthropic, Gemini, Ollama |
| ModelName | nvarchar(100) | gpt-4o, claude-3-5-sonnet, dll |
| Temperature | float | 0.0 - 1.0 |
| MaxTokens | int | Max output tokens |
| SystemPrompt | nvarchar(4000) | Custom system prompt |
| CreatedAt | datetime | Tanggal dibuat |
| UpdatedAt | datetime | Update terakhir |
| IsActive | bit | Status aktif |

### ChatMessage

| Column | Type | Description |
|--------|------|-------------|
| Id | int | Primary key (auto-increment) |
| ChatSessionId | int | FK → ChatSession |
| Role | nvarchar(20) | user, assistant, system, tool |
| Content | nvarchar(max) | Isi pesan |
| DashboardHtml | nvarchar(max) | HTML dashboard (nullable) |
| PromptTokens | int? | Token input |
| CompletionTokens | int? | Token output |
| TotalTokens | int? | Total token |
| ResponseTimeMs | float? | Waktu respons (ms) |
| Attachments | nvarchar(max) | JSON attachment (nullable) |
| Timestamp | datetime | Waktu pesan |

### AuditLog

| Column | Type | Description |
|--------|------|-------------|
| Id | bigint | Primary key (auto-increment) |
| Timestamp | datetime | Waktu aktivitas |
| Category | nvarchar(50) | Auth, Chat, Query, Database, RAG |
| Action | nvarchar(100) | Nama aksi |
| Description | nvarchar(2000) | Deskripsi |
| Details | nvarchar(max) | JSON detail |
| IpAddress | nvarchar(100) | IP address |
| UserAgent | nvarchar(500) | Browser user agent |
| DurationMs | float? | Durasi (ms) |
| IsSuccess | bit | Status sukses |
| ErrorMessage | nvarchar(1000) | Pesan error (nullable) |
| UserId | nvarchar(450) | FK → ApplicationUser |

### RagIndexedDocument

| Column | Type | Description |
|--------|------|-------------|
| Id | bigint | Primary key (auto-increment) |
| FileName | nvarchar(500) | Nama file |
| FilePath | nvarchar(2000) | Path relatif |
| FileType | nvarchar(50) | pdf, docx, xlsx, txt, csv, pptx |
| FileSize | bigint | Ukuran (bytes) |
| IndexedAt | datetime | Waktu index |
| FileModifiedAt | datetime | Modifikasi file |
| ChunkCount | int | Jumlah chunk |
| VectorProvider | nvarchar(50) | InMemory, Qdrant, Chroma |
| ContentHash | nvarchar(128) | SHA256 hash |
| Status | nvarchar(20) | Indexed, Failed, Processing |
| ErrorMessage | nvarchar(2000) | Error (nullable) |
| Keywords | nvarchar(max) | Keyword (nullable) |

---

## Indexes

### Performance Indexes

```sql
-- ChatSessions
CREATE INDEX IX_ChatSessions_UserId ON ChatSessions(UserId);
CREATE INDEX IX_ChatSessions_UpdatedAt ON ChatSessions(UpdatedAt);

-- ChatMessages
CREATE INDEX IX_ChatMessages_ChatSessionId ON ChatMessages(ChatSessionId);
CREATE INDEX IX_ChatMessages_Timestamp ON ChatMessages(Timestamp);

-- AuditLogs
CREATE INDEX IX_AuditLogs_Timestamp ON AuditLogs(Timestamp);
CREATE INDEX IX_AuditLogs_Category ON AuditLogs(Category);
CREATE INDEX IX_AuditLogs_Action ON AuditLogs(Action);
CREATE INDEX IX_AuditLogs_UserId ON AuditLogs(UserId);

-- DatabaseConnections
CREATE INDEX IX_DatabaseConnections_UserId ON DatabaseConnections(UserId);
CREATE INDEX IX_DatabaseConnections_Name ON DatabaseConnections(Name);

-- RagIndexedDocuments
CREATE INDEX IX_RagIndexedDocuments_FilePath ON RagIndexedDocuments(FilePath);
CREATE INDEX IX_RagIndexedDocuments_Status ON RagIndexedDocuments(Status);
CREATE INDEX IX_RagIndexedDocuments_IndexedAt ON RagIndexedDocuments(IndexedAt);
```

---

## Migrations

### Membuat Migrasi

```bash
# Install EF Core tools
dotnet tool install --global dotnet-ef

# Buat migrasi baru
dotnet ef migrations add NamaMigrasi

# Apply migrasi
dotnet ef database update

# Rollback
dotnet ef database update NamaMigrasiSebelumnya
```

### SQLite Notes

- SQLite tidak mendukung beberapa operasi migration (ALTER COLUMN, dll)
- Gunakan `EnsureCreated()` untuk development
- Untuk production, gunakan SQL Server atau PostgreSQL

---

## Sample Data

### Users (dari DataSeeder)

```sql
-- Admin (semua akses)
Email: admin@pda.com
Password: Admin@123
Role: Admin

-- User (standar)
Email: user@pda.com
Password: User@1234
Role: User

-- Analyst
Email: analyst@pda.com
Password: Analyst@123
Role: Analyst
```

### Sample Database Connection

```sql
INSERT INTO DatabaseConnections (Name, DatabaseType, ConnectionString, UserId, CreatedAt, IsActive)
VALUES ('Sample SQLite Database', 'SQLite', 'Data Source=SampleData.db', '<admin-id>', datetime('now'), 1);
```

### Sample Chat Session

```sql
INSERT INTO ChatSessions (Title, UserId, DatabaseConnectionId, ModelProvider, ModelName, Temperature, CreatedAt, UpdatedAt)
VALUES ('Getting Started - Chat with Your Data', '<admin-id>', 1, 'OpenAI', 'gpt-4o', 0.3, datetime('now'), datetime('now'));
```

---

## Backup & Restore

### SQLite Backup

```bash
# Backup
cp PDA.db backups/PDA_$(date +%Y%m%d_%H%M%S).db

# Restore
cp backups/PDA_20240115_120000.db PDA.db
```

### SQL Server Backup

```sql
-- Backup
BACKUP DATABASE PDA TO DISK = 'PDA_backup.bak'

-- Restore
RESTORE DATABASE PDA FROM DISK = 'PDA_backup.bak'
```

### Maintenance Query (SQLite)

```sql
-- Optimize database
VACUUM;

-- Check integrity
PRAGMA integrity_check;

-- Database info
PRAGMA page_count;
PRAGMA page_size;
```
