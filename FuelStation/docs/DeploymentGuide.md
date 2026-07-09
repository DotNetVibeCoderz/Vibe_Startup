# 🚀 FuelStation - Panduan Deployment

## Daftar Isi
1. [Prasyarat](#1-prasyarat)
2. [Deployment Lokal (Development)](#2-deployment-lokal)
3. [Deployment dengan Docker](#3-deployment-dengan-docker)
4. [Deployment ke IIS (Windows Server)](#4-deployment-ke-iis)
5. [Deployment ke Linux (systemd)](#5-deployment-ke-linux)
6. [Deployment ke Azure App Service](#6-deployment-ke-azure-app-service)
7. [Konfigurasi Production](#7-konfigurasi-production)
8. [Monitoring & Logging](#8-monitoring--logging)
9. [Troubleshooting](#9-troubleshooting)

---

## 1. Prasyarat

### Minimal Requirements
| Komponen | Development | Production |
|----------|-------------|------------|
| .NET SDK | 10.0+ | 10.0+ Runtime |
| RAM | 4 GB | 2 GB |
| CPU | 2 core | 1 core |
| Disk | 1 GB | 500 MB + data |
| OS | Windows/macOS/Linux | Windows Server/Linux |

---

## 2. Deployment Lokal

```bash
# Clone / copy project
cd FuelStation

# Restore packages
dotnet restore

# Build
dotnet build -c Release

# Run
dotnet run -c Release

# Akses: https://localhost:5001
# Swagger: https://localhost:5001/swagger
```

---

## 3. Deployment dengan Docker

### 3.1 Build & Run dengan Docker Compose

```bash
# Build and start
docker-compose up -d --build

# Lihat logs
docker-compose logs -f fuelstation

# Stop
docker-compose down

# Stop + hapus volumes (reset data)
docker-compose down -v
```

### 3.2 Build Image Manual

```bash
docker build -t fuelstation:latest .
docker run -d -p 8080:8080 \
  -v fuelstation-data:/app/data \
  -v fuelstation-uploads:/app/uploads \
  --name fuelstation-app \
  fuelstation:latest
```

### 3.3 Dengan PostgreSQL (via docker-compose)

Uncomment section `postgres` di `docker-compose.yml`, lalu set environment:

```yaml
environment:
  - Database__Provider=PostgreSQL
  - ConnectionStrings__DefaultConnection=Host=postgres;Database=FuelStation;Username=fuelstation;Password=FuelStation123!
```

### 3.4 Dengan MySQL

Uncomment section `mysql`, lalu set:

```yaml
environment:
  - Database__Provider=MySQL
  - ConnectionStrings__DefaultConnection=Server=mysql;Database=FuelStation;User=fuelstation;Password=FuelStation123!
```

### 3.5 Dengan MinIO (Storage)

Uncomment section `minio`, lalu set:

```yaml
environment:
  - Storage__Provider=MinIO
  - Storage__Endpoint=minio:9000
  - Storage__AccessKey=minioadmin
  - Storage__SecretKey=minioadmin
```

---

## 4. Deployment ke IIS (Windows Server)

### 4.1 Prasyarat IIS
- IIS dengan ASP.NET Core Module (Hosting Bundle .NET 10)
- WebSocket enabled

### 4.2 Publish

```bash
dotnet publish -c Release -o C:\Deploy\FuelStation
```

### 4.3 IIS Setup
1. Buat **Application Pool**: `.NET CLR Version = No Managed Code`
2. Buat **Website**: pointing ke `C:\Deploy\FuelStation`
3. Set bindings: `http:*:80:`
4. Set Application Pool Identity permission ke folder deploy

### 4.4 web.config (auto-generated)

```xml
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <location path="." inheritInChildApplications="false">
    <system.webServer>
      <handlers>
        <add name="aspNetCore" path="*" verb="*"
             modules="AspNetCoreModuleV2" resourceType="Unspecified" />
      </handlers>
      <aspNetCore processPath="dotnet"
                  arguments=".\FuelStation.dll"
                  stdoutLogEnabled="true"
                  stdoutLogFile=".\logs\stdout"
                  hostingModel="inprocess">
        <environmentVariables>
          <environmentVariable name="ASPNETCORE_ENVIRONMENT" value="Production" />
        </environmentVariables>
      </aspNetCore>
    </system.webServer>
  </location>
</configuration>
```

---

## 5. Deployment ke Linux (systemd)

### 5.1 Publish

```bash
dotnet publish -c Release -o /opt/fuelstation
```

### 5.2 Buat systemd Service

```bash
sudo nano /etc/systemd/system/fuelstation.service
```

```ini
[Unit]
Description=FuelStation Blazor Server
After=network.target

[Service]
WorkingDirectory=/opt/fuelstation
ExecStart=/usr/bin/dotnet /opt/fuelstation/FuelStation.dll
Restart=always
RestartSec=10
KillSignal=SIGINT
SyslogIdentifier=fuelstation
User=www-data
Environment=ASPNETCORE_ENVIRONMENT=Production
Environment=ASPNETCORE_URLS=http://+:5000

[Install]
WantedBy=multi-user.target
```

```bash
sudo systemctl daemon-reload
sudo systemctl enable fuelstation
sudo systemctl start fuelstation
sudo systemctl status fuelstation
```

### 5.3 Reverse Proxy dengan Nginx

```bash
sudo nano /etc/nginx/sites-available/fuelstation
```

```nginx
server {
    listen 80;
    server_name fuelstation.domain.com;

    location / {
        proxy_pass http://localhost:5000;
        proxy_http_version 1.1;
        proxy_set_header Upgrade $http_upgrade;
        proxy_set_header Connection "upgrade";
        proxy_set_header Host $host;
        proxy_set_header X-Real-IP $remote_addr;
        proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
        proxy_set_header X-Forwarded-Proto $scheme;
        proxy_read_timeout 86400;
    }

    location /notificationHub {
        proxy_pass http://localhost:5000;
        proxy_http_version 1.1;
        proxy_set_header Upgrade $http_upgrade;
        proxy_set_header Connection "upgrade";
        proxy_set_header Host $host;
    }
}
```

```bash
sudo ln -s /etc/nginx/sites-available/fuelstation /etc/nginx/sites-enabled/
sudo nginx -t && sudo systemctl reload nginx
```

---

## 6. Deployment ke Azure App Service

### 6.1 Via Azure CLI

```bash
# Login
az login

# Buat resource group
az group create --name FuelStationRG --location southeastasia

# Buat App Service Plan (Linux)
az appservice plan create \
  --name FuelStationPlan \
  --resource-group FuelStationRG \
  --sku B1 \
  --is-linux

# Buat Web App
az webapp create \
  --name fuelstation-app \
  --plan FuelStationPlan \
  --resource-group FuelStationRG \
  --runtime "DOTNET:10.0"

# Deploy
dotnet publish -c Release -o ./publish
cd publish
zip -r ../FuelStation.zip .
az webapp deployment source config-zip \
  --resource-group FuelStationRG \
  --name fuelstation-app \
  --src ../FuelStation.zip
```

### 6.2 Environment Variables di Azure

Set via Azure Portal → App Service → Configuration:

```
Database__Provider = SQLServer
ConnectionStrings__DefaultConnection = Server=tcp:xxx.database.windows.net;...
ChatBot__ApiKey = sk-...
```

---

## 7. Konfigurasi Production

### 7.1 appsettings.Production.json

```json
{
  "Database": {
    "Provider": "SQLServer"
  },
  "ConnectionStrings": {
    "DefaultConnection": "Server=prod-server;Database=FuelStation;..."
  },
  "Storage": {
    "Provider": "AzureBlob",
    "ConnectionString": "...",
    "ContainerName": "fuelstation-prod"
  },
  "Logging": {
    "LogLevel": {
      "Default": "Warning",
      "Microsoft.AspNetCore": "Warning"
    }
  },
  "Simulator": {
    "Enabled": false
  },
  "IoTSimulator": {
    "Enabled": false
  }
}
```

### 7.2 Checklist Production

- [ ] Ganti password default admin
- [ ] Set `Simulator__Enabled = false`
- [ ] Set `IoTSimulator__Enabled = false`
- [ ] Database pakai SQL Server / PostgreSQL (bukan SQLite)
- [ ] Storage pakai Azure Blob / S3 (bukan FileSystem)
- [ ] Set ChatBot API Key
- [ ] Enable HTTPS
- [ ] Set HSTS headers
- [ ] Configure firewall / WAF
- [ ] Setup backup database harian

---

## 8. Monitoring & Logging

### 8.1 Log Files
Logging otomatis ke console. Untuk file logging, tambahkan Serilog:

```bash
dotnet add package Serilog.AspNetCore
```

### 8.2 Application Insights (Azure)

```bash
dotnet add package Microsoft.ApplicationInsights.AspNetCore
```

```csharp
// Program.cs
builder.Services.AddApplicationInsightsTelemetry();
```

### 8.3 Health Check Endpoint

Tambahkan:
```csharp
builder.Services.AddHealthChecks()
    .AddDbContextCheck<AppDbContext>();
app.MapHealthChecks("/health");
```

---

## 9. Troubleshooting

| Masalah | Solusi |
|---------|--------|
| **Blazor circuit disconnected** | Cek WebSocket enabled di reverse proxy |
| **Database locked (SQLite)** | Jangan pakai SQLite di production multi-user |
| **SignalR not connecting** | Cek WebSocket + `/notificationHub` routing |
| **ChatBot not responding** | Cek API Key di appsettings / environment variable |
| **Memory usage tinggi** | SignalR circuit timeout: atur `CircuitOptions.DisconnectedCircuitMaxRetained` |
| **CSS tidak update** | Hard refresh (Ctrl+Shift+R) setelah deploy |
| **File upload gagal** | Cek permission folder / koneksi storage cloud |

### Log Lokasi

| Environment | Log Path |
|-------------|----------|
| Development | Console output |
| Docker | `docker-compose logs fuelstation` |
| Linux systemd | `journalctl -u fuelstation -f` |
| IIS | `.\logs\stdout_*.log` |
| Azure | App Service → Log stream |

---

*FuelStation Deployment Guide v1.0 — Gravicode Studios*
