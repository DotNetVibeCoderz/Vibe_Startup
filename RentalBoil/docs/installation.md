# 🚀 Panduan Instalasi

## Prasyarat

- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
- Browser modern (Chrome, Firefox, Edge, Safari)
- (Opsional) [Ollama](https://ollama.com) untuk AI chat lokal

---

## Quick Start (2 menit)

```bash
# Clone atau buka folder project
cd RentalBoil

# Restore packages
dotnet restore

# Jalankan aplikasi
dotnet run
```

Aplikasi berjalan di:
- **Web App**: `https://localhost:5001`
- **Swagger API Docs**: `https://localhost:5001/swagger`

Database SQLite (`RentalBoil.db`) dibuat otomatis dengan data sample.

---

## Konfigurasi Database

### SQLite (Default)

Tidak perlu konfigurasi. Database dibuat otomatis.

### SQL Server

```json
{
  "Database": {
    "Provider": "SqlServer"
  },
  "ConnectionStrings": {
    "DefaultConnection": "Server=.;Database=RentalBoil;Trusted_Connection=true;TrustServerCertificate=true"
  }
}
```

### MySQL

```json
{
  "Database": {
    "Provider": "MySQL"
  },
  "ConnectionStrings": {
    "DefaultConnection": "Server=localhost;Database=RentalBoil;User=root;Password=yourpassword;"
  }
}
```

### PostgreSQL

```json
{
  "Database": {
    "Provider": "PostgreSQL"
  },
  "ConnectionStrings": {
    "DefaultConnection": "Host=localhost;Database=RentalBoil;Username=postgres;Password=yourpassword;"
  }
}
```

---

## Konfigurasi AI Chat Bot

RentalBoil mendukung 4 AI provider. Pilih salah satu:

### OpenAI

```json
{
  "AI": {
    "Provider": "OpenAI",
    "OpenAI": {
      "ApiKey": "sk-your-openai-api-key",
      "Model": "gpt-4o-mini"
    }
  }
}
```

### Anthropic Claude

```json
{
  "AI": {
    "Provider": "Anthropic",
    "Anthropic": {
      "ApiKey": "sk-ant-your-anthropic-key",
      "Model": "claude-3-haiku-20240307"
    }
  }
}
```

### Google Gemini

```json
{
  "AI": {
    "Provider": "Gemini",
    "Gemini": {
      "ApiKey": "AIza-your-gemini-key",
      "Model": "gemini-2.0-flash"
    }
  }
}
```

### Ollama (Local)

```bash
# Install Ollama dulu
ollama pull llama3.2
ollama serve
```

```json
{
  "AI": {
    "Provider": "Ollama",
    "Ollama": {
      "Endpoint": "http://localhost:11434",
      "Model": "llama3.2"
    }
  }
}
```

---

## Konfigurasi Storage

### File System (Default)

```json
{
  "Storage": {
    "Provider": "FileSystem",
    "BasePath": "wwwroot/uploads",
    "BaseUrl": "/uploads"
  }
}
```

### Azure Blob Storage

```json
{
  "Storage": {
    "Provider": "AzureBlob",
    "AzureBlob": {
      "ConnectionString": "DefaultEndpointsProtocol=https;AccountName=...;AccountKey=...;",
      "ContainerName": "rentalboil"
    }
  }
}
```

### AWS S3

```json
{
  "Storage": {
    "Provider": "S3",
    "S3": {
      "AccessKey": "AKIA...",
      "SecretKey": "...",
      "Region": "ap-southeast-1",
      "BucketName": "rentalboil"
    }
  }
}
```

### MinIO (Self-hosted S3-compatible)

```json
{
  "Storage": {
    "Provider": "MinIO",
    "MinIO": {
      "Endpoint": "localhost:9000",
      "AccessKey": "minioadmin",
      "SecretKey": "minioadmin",
      "BucketName": "rentalboil",
      "UseSSL": false
    }
  }
}
```

---

## Konfigurasi GPS Simulator

```json
{
  "GPS": {
    "SimulatorEnabled": true,
    "UpdateIntervalSeconds": 3,
    "UpdateMode": "DirectDB",
    "UpdateModeOptions": ["DirectDB", "Api"],
    "UseBatchUpdate": false,
    "ApiBaseUrl": "https://localhost:5001"
  }
}
```

| Setting | Deskripsi |
|---------|-----------|
| `SimulatorEnabled` | `true` = simulator jalan otomatis |
| `UpdateIntervalSeconds` | Interval update GPS (detik) |
| `UpdateMode` | `DirectDB` = update langsung DB, `Api` = via REST API |
| `UseBatchUpdate` | `true` = update banyak kendaraan sekaligus |

---

## Konfigurasi Tavily Search

```json
{
  "Tavily": {
    "ApiKey": "tvly-your-tavily-key"
  }
}
```

Dapatkan API key gratis di: https://tavily.com

---

## Troubleshooting

### ❌ Port 5001 sudah digunakan

```bash
# Ganti port di Properties/launchSettings.json
"applicationUrl": "https://localhost:5002"
```

### ❌ Ollama connection refused

```bash
# Pastikan Ollama running
ollama serve
# Atau ganti provider ke OpenAI/Gemini/Anthropic
```

### ❌ SQLite Error 19: UNIQUE constraint

Hapus file database dan restart:
```bash
rm RentalBoil.db
dotnet run
```

### ❌ ObjectDisposedException di GPS Simulator

Ini normal saat development. Simulator sudah di-fix dengan `IServiceScopeFactory`.
Pastikan versi terbaru.
