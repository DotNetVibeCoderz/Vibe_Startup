# ⚙️ Configuration Guide

Panduan lengkap konfigurasi `appsettings.json` dan environment variables.

---

## appsettings.json Structure

```json
{
  "ConnectionStrings": {},
  "Logging": {},
  "AppSettings": {},
  "LLM": {},
  "Storage": {},
  "RAG": {},
  "Dashboard": {}
}
```

---

## 1. Connection Strings

### SQLite (Default)

```json
"ConnectionStrings": {
  "DefaultConnection": "Data Source=PDA.db"
}
```

### SQL Server

```json
"ConnectionStrings": {
  "DefaultConnection": "Server=localhost;Database=PDADb;Trusted_Connection=true;TrustServerCertificate=true"
}
```

### PostgreSQL

```json
"ConnectionStrings": {
  "DefaultConnection": "Host=localhost;Database=PDADb;Username=postgres;Password=YourPassword"
}
```

### MySQL

```json
"ConnectionStrings": {
  "DefaultConnection": "Server=localhost;Database=PDADb;User=root;Password=YourPassword"
}
```

---

## 2. App Settings

```json
"AppSettings": {
  "AppName": "PDA - Personal Data Analyst",
  "AppVersion": "1.0.0",
  "Environment": "Development"
}
```

| Key | Type | Default | Description |
|-----|------|---------|-------------|
| AppName | string | PDA | Nama aplikasi |
| AppVersion | string | 1.0.0 | Versi aplikasi |
| Environment | string | Development | Environment (Development/Staging/Production) |

---

## 3. LLM Configuration

### OpenAI

```json
"LLM": {
  "DefaultProvider": "OpenAI",
  "DefaultModel": "gpt-4o",
  "DefaultTemperature": 0.3,
  "DefaultMaxTokens": 4096,
  "Providers": {
    "OpenAI": {
      "ApiKey": "sk-your-key",
      "Endpoint": "https://api.openai.com/v1",
      "Models": ["gpt-4o", "gpt-4o-mini", "gpt-4-turbo", "gpt-3.5-turbo"]
    }
  }
}
```

### Anthropic (Claude)

```json
"Anthropic": {
  "ApiKey": "sk-ant-your-key",
  "Endpoint": "https://api.anthropic.com/v1",
  "Models": ["claude-3-5-sonnet-20241022", "claude-3-opus-20240229", "claude-3-haiku-20240307"]
}
```

### Google Gemini

```json
"Gemini": {
  "ApiKey": "your-gemini-api-key",
  "Endpoint": "https://generativelanguage.googleapis.com/v1beta",
  "Models": ["gemini-2.0-flash", "gemini-1.5-pro", "gemini-1.5-flash"]
}
```

### Ollama (Local - Gratis)

```json
"Ollama": {
  "ApiKey": "",
  "Endpoint": "http://localhost:11434",
  "Models": ["llama3.1", "mistral", "codellama", "phi3"]
}
```

### OpenAI Compatible (Custom)

```json
"OpenAICompatible": {
  "ApiKey": "your-key",
  "Endpoint": "https://your-custom-endpoint.com/v1",
  "Models": ["custom-model-1", "custom-model-2"]
}
```

### LLM Parameters

| Key | Type | Default | Description |
|-----|------|---------|-------------|
| DefaultProvider | string | "OpenAI" | Provider default |
| DefaultModel | string | "gpt-4o" | Model default |
| DefaultTemperature | double | 0.3 | Suhu (0=presisi, 1=kreatif) |
| DefaultMaxTokens | int | 4096 | Max token per respons |

### Temperature Guide

| Value | Use Case |
|-------|----------|
| 0.0 - 0.2 | Query SQL presisi, data extraction |
| 0.3 - 0.5 | Analisis data, reporting |
| 0.6 - 0.8 | Insight kreatif, narasi |
| 0.9 - 1.0 | Brainstorming, eksplorasi |

---

## 4. Storage Configuration

### FileSystem (Default)

```json
"Storage": {
  "Provider": "FileSystem",
  "FileSystem": {
    "BasePath": "wwwroot/uploads"
  }
}
```

### Azure Blob

```json
"Storage": {
  "Provider": "AzureBlob",
  "AzureBlob": {
    "ConnectionString": "DefaultEndpointsProtocol=https;AccountName=...;AccountKey=...",
    "ContainerName": "pda-uploads"
  }
}
```

### AWS S3

```json
"Storage": {
  "Provider": "S3",
  "S3": {
    "AccessKey": "AKIA...",
    "SecretKey": "your-secret-key",
    "BucketName": "pda-uploads",
    "Endpoint": "",
    "Region": "us-east-1"
  }
}
```

### MinIO

```json
"Storage": {
  "Provider": "MinIO",
  "MinIO": {
    "Endpoint": "localhost:9000",
    "AccessKey": "minioadmin",
    "SecretKey": "minioadmin",
    "BucketName": "pda-uploads"
  }
}
```

---

## 5. RAG Configuration

```json
"RAG": {
  "Enabled": true,
  "KnowledgeBasePath": "KnowledgeBase",
  "ScanIntervalMinutes": 30,
  "VectorProvider": "InMemory",
  "ChunkSize": 1000,
  "ChunkOverlap": 200,
  "MaxFileSizeMb": 50,
  "VectorStores": {
    "FileSystem": { "Path": "VectorStore" },
    "Qdrant": {
      "Endpoint": "http://localhost:6333",
      "CollectionName": "pda-knowledge"
    },
    "AzureAISearch": {
      "Endpoint": "https://...",
      "ApiKey": "...",
      "IndexName": "pda-knowledge"
    },
    "Chroma": {
      "Endpoint": "http://localhost:8000",
      "CollectionName": "pda-knowledge"
    }
  }
}
```

### RAG Parameters

| Key | Type | Default | Description |
|-----|------|---------|-------------|
| Enabled | bool | true | Enable/disable RAG |
| KnowledgeBasePath | string | "KnowledgeBase" | Path ke folder dokumen |
| ScanIntervalMinutes | int | 30 | Interval scan (menit) |
| VectorProvider | string | "InMemory" | Vector store provider |
| ChunkSize | int | 1000 | Ukuran chunk (karakter) |
| ChunkOverlap | int | 200 | Overlap antar chunk |
| MaxFileSizeMb | int | 50 | Batas ukuran file |

### Vector Store Providers

| Provider | Keterangan |
|----------|------------|
| **InMemory** | Default, no setup needed |
| **FileSystem** | Persistent local storage |
| **Qdrant** | Self-hosted vector DB |
| **Azure AI Search** | Cloud managed |
| **Chroma** | Open-source vector DB |

---

## 6. Dashboard Configuration

```json
"Dashboard": {
  "MaxDataRows": 1000,
  "ChartLibrary": "Chart.js",
  "ExportImageFormat": "png",
  "DefaultTheme": "professional"
}
```

---

## Environment Variables

Semua setting di `appsettings.json` bisa di-override via environment variables.

### Format

```
Section__Key=Nilai
```

### Contoh

```bash
# Linux/Mac
export LLM__Providers__OpenAI__ApiKey="sk-..."
export RAG__Enabled="true"
export ConnectionStrings__DefaultConnection="Data Source=prod.db"

# Windows PowerShell
$env:LLM__Providers__OpenAI__ApiKey = "sk-..."
$env:RAG__Enabled = "true"
```

### Docker

```bash
docker run -e LLM__Providers__OpenAI__ApiKey=sk-... -e RAG__Enabled=true pda-app
```

---

## appsettings.Development.json

Override untuk development:
```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Debug",
      "PDA": "Debug"
    }
  },
  "RAG": {
    "ScanIntervalMinutes": 5
  }
}
```

---

## Security Best Practices

1. ❌ **Jangan commit API keys** ke repository
2. ✅ Gunakan **User Secrets** untuk development:
   ```bash
   dotnet user-secrets set "LLM:Providers:OpenAI:ApiKey" "sk-..."
   ```
3. ✅ Gunakan **Environment Variables** untuk production
4. ✅ Gunakan **Azure Key Vault** / **AWS Secrets Manager** untuk cloud
5. ✅ Rotate API keys secara berkala
