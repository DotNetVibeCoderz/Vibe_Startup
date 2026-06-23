# 🚀 Deployment Guide

## Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
- Database server (SQLite built-in, or SQL Server/MySQL/PostgreSQL)

---

## Quick Start (Development)

```bash
cd EventSphere
dotnet restore
dotnet run
```
Open `https://localhost:5001`

---

## Configuration

### 1. Database (`appsettings.json`)
```json
{
  "Database": { "Provider": "SQLite" },
  "ConnectionStrings": {
    "DefaultConnection": "Data Source=EventSphere.db"
  }
}
```

### 2. Storage (`appsettings.json`)
```json
{
  "Storage": {
    "Provider": "FileSystem",
    "FileSystem": { "BasePath": "wwwroot/uploads" }
  }
}
```

### 3. AI Providers (`appsettings.json`)
```json
{
  "AI": {
    "DefaultProvider": "OpenAI",
    "ChatBot": {
      "Name": "Tante Sherly",
      "SystemPrompt": "...",
      "Temperature": 0.7,
      "MaxTokens": 2000
    },
    "Providers": {
      "OpenAI": { "ApiKey": "sk-...", "Model": "gpt-4o" },
      "Anthropic": { "ApiKey": "...", "Model": "claude-3-5-sonnet-20241022" },
      "Gemini": { "ApiKey": "...", "Model": "gemini-2.0-flash" },
      "Ollama": { "Model": "llama3", "Endpoint": "http://localhost:11434" }
    },
    "Tavily": { "ApiKey": "tvly-..." }
  }
}
```

---

## Production Deployment

### Option A: Self-Hosted (IIS/Nginx/Apache)

```bash
# Publish
dotnet publish -c Release -o publish

# Run
dotnet EventSphere.dll --urls "http://0.0.0.0:5000"
```

**Nginx reverse proxy:**
```nginx
server {
    listen 80;
    server_name eventsphere.example.com;
    location / {
        proxy_pass http://localhost:5000;
        proxy_http_version 1.1;
        proxy_set_header Upgrade $http_upgrade;
        proxy_set_header Connection "upgrade";
        proxy_set_header Host $host;
    }
}
```

### Option B: Docker

```dockerfile
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
WORKDIR /app
EXPOSE 80

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY . .
RUN dotnet restore
RUN dotnet publish -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "EventSphere.dll"]
```

```bash
docker build -t eventsphere .
docker run -d -p 80:80 -v $(pwd)/data:/app/data eventsphere
```

### Option C: Azure App Service

```bash
# Deploy via Azure CLI
az webapp deploy --resource-group EventSphereRG --name eventsphere-app --src-path ./publish
```

---

## Environment Variables

Override `appsettings.json` with environment variables:

| Variable | Description |
|----------|-------------|
| `Database__Provider` | SQLite, SqlServer, MySQL, PostgreSQL |
| `ConnectionStrings__DefaultConnection` | Database connection string |
| `Storage__Provider` | FileSystem, AzureBlob, S3, MinIO |
| `AI__DefaultProvider` | OpenAI, Anthropic, Gemini, Ollama |
| `AI__Providers__OpenAI__ApiKey` | OpenAI API key |
| `AI__Tavily__ApiKey` | Tavily search API key |

---

## Database Migration

SQLite auto-creates on first run. For other providers:

```bash
# Install EF Core tools
dotnet tool install -g dotnet-ef

# Create migration
dotnet ef migrations add InitialCreate

# Apply migration
dotnet ef database update
```

---

## Performance Tuning

| Setting | Recommendation |
|---------|---------------|
| SignalR | Default connection (no additional config needed) |
| Database | Add indexes for frequently queried columns |
| AI | Set `MaxTokens: 2000` to limit response size |
| File Upload | Max 10MB per file (configurable in code) |
| Caching | Consider Redis for production multi-instance |

---

## Security Checklist

- [ ] Change default admin password
- [ ] Set `EmailConfirmed = false` and enable email verification
- [ ] Configure HTTPS in production
- [ ] Store API keys in Azure Key Vault / AWS Secrets Manager
- [ ] Enable CORS only for trusted domains
- [ ] Regular database backups
