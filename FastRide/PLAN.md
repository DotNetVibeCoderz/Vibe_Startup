# 🚖 FastRide — Development Plan & Progress

> Status: **Phase 2 Complete — Multi-DB & Multi-Storage Implemented**

## 📊 Progress

```
Phase 1 ████████████████████ 100% Core Foundation
Phase 2 ████████████████████ 100% API (37 endpoints + multi-DB + multi-storage)
Phase 3 ████████████████████ 100% Admin Dashboard
Phase 4 ███████████████████░  90% Simulator
Phase 5 ███████████████████░  90% MAUI Apps
Phase 6 ██████░░░░░░░░░░░░░░  30% Auth & Security
Phase 7 ██░░░░░░░░░░░░░░░░░░  10% DevOps
─────────────────────────────────
TOTAL  ████████████████░░░   73%
```

## 🆕 Multi-Database Support

| Provider | EF Core Package | Config Key |
|----------|----------------|------------|
| SQLite | Microsoft.EntityFrameworkCore.Sqlite | `"Provider": "SQLite"` |
| SQL Server | Microsoft.EntityFrameworkCore.SqlServer | `"Provider": "SqlServer"` |
| PostgreSQL | Npgsql.EntityFrameworkCore.PostgreSQL | `"Provider": "PostgreSQL"` |
| MySQL | Pomelo.EntityFrameworkCore.MySql | `"Provider": "MySQL"` |

## 🆕 Multi-Storage Support

| Provider | Description | Config Key |
|----------|------------|------------|
| FileSystem | Local disk (default) | `"Provider": "FileSystem"` |
| S3/MinIO | S3-compatible (AWS/MinIO/DigitalOcean) | `"Provider": "S3"` |
| Azure Blob | Azure Blob Storage | `"Provider": "Azure"` |

## 📸 Profile Photo Architecture
- `User.PhotoUrl` → public URL on configured storage
- `User.ProfilePhotoBase64` → legacy fallback
- `IStorageProvider` interface → Upload/Download/Delete/Exists
- SVG avatar auto-generated on register
- Multipart form + base64 upload both supported
- Static file serving for FileSystem provider

## 🚀 Run

```bash
# API (SQLite + FileSystem by default)
dotnet run --project FastRide.Api

# Switch to SQL Server + S3
"Database__Provider": "SqlServer",
"Storage__Provider": "S3"

# Demo login
budi.santoso@email.com / Password123
```
