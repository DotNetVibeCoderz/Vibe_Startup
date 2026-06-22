# 🚀 Deployment

## Prerequisites

- .NET 10 Runtime / SDK
- Database server (sesuai provider yang dipilih)
- Reverse proxy (IIS, Nginx, Apache) untuk production

---

## Deployment Options

### 1. 🖥️ IIS (Windows)

```bash
# Publish
dotnet publish -c Release -o ./publish

# Copy folder publish ke server
# Buat Application Pool "No Managed Code"
# Buat Website → point ke folder publish
# Set environment variables di web.config atau IIS
```

**web.config** (jika diperlukan):
```xml
<?xml version="1.0" encoding="UTF-8"?>
<configuration>
  <system.webServer>
    <aspNetCore processPath="dotnet" arguments=".\JuraganKost.dll"
                stdoutLogEnabled="false" stdoutLogFile=".\logs\stdout">
      <environmentVariables>
        <environmentVariable name="ASPNETCORE_ENVIRONMENT" value="Production" />
      </environmentVariables>
    </aspNetCore>
  </system.webServer>
</configuration>
```

### 2. 🐳 Docker

```dockerfile
# Dockerfile
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
ENTRYPOINT ["dotnet", "JuraganKost.dll"]
```

```bash
# Build & run
docker build -t juragankost .
docker run -d -p 8080:80 --name juragankost juragankost
```

### 3. ☁️ Azure App Service

```bash
# Deploy via CLI
az webapp deploy --resource-group MyGroup --name juragankost --src-path ./publish

# Atau via Visual Studio: Publish → Azure App Service
```

**Environment Variables di Azure:**
- `ASPNETCORE_ENVIRONMENT` = `Production`
- `OPENAI_API_KEY` = `sk-...` (untuk chat AI)

### 4. 🐧 Linux + Nginx

```bash
# Publish
dotnet publish -c Release -o /var/www/juragankost

# Buat service systemd
sudo nano /etc/systemd/system/juragankost.service
```

```
[Unit]
Description=JuraganKost Web App
After=network.target

[Service]
WorkingDirectory=/var/www/juragankost
ExecStart=/usr/bin/dotnet /var/www/juragankost/JuraganKost.dll
Restart=always
RestartSec=10
KillSignal=SIGINT
Environment=ASPNETCORE_ENVIRONMENT=Production
Environment=ASPNETCORE_URLS=http://localhost:5000

[Install]
WantedBy=multi-user.target
```

```bash
sudo systemctl enable juragankost
sudo systemctl start juragankost
```

**Nginx config:**
```nginx
server {
    listen 80;
    server_name juragankost.com;

    location / {
        proxy_pass http://localhost:5000;
        proxy_http_version 1.1;
        proxy_set_header Upgrade $http_upgrade;
        proxy_set_header Connection "upgrade";
        proxy_set_header Host $host;
        proxy_set_header X-Real-IP $remote_addr;
    }
}
```

---

## Environment Variables

| Variable | Deskripsi | Default |
|---|---|---|
| `ASPNETCORE_ENVIRONMENT` | `Development` / `Production` | `Production` |
| `OPENAI_API_KEY` | API key untuk chat AI | `""` |
| `ANTHROPIC_API_KEY` | API key Anthropic | `""` |
| `GOOGLE_API_KEY` | API key Gemini | `""` |

---

## Production Checklist

- [ ] Set `ASPNETCORE_ENVIRONMENT=Production`
- [ ] Ganti database provider ke SQL Server / PostgreSQL
- [ ] Set `DatabaseProvider` di `appsettings.json`
- [ ] Konfigurasi storage (Azure Blob / S3 — jangan FileSystem)
- [ ] Set API keys via environment variables (bukan di appsettings.json)
- [ ] Enable HTTPS
- [ ] Setup logging (Serilog sinks)
- [ ] Hapus akun demo atau ganti password
- [ ] Testing: login, CRUD, chat, export, API
