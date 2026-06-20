# 🚢 Deployment Guide

Panduan deployment PDA ke production environment.

---

## Deployment Options

| Option | Complexity | Cost | Best For |
|--------|-----------|------|----------|
| **Self-hosted (VM/Bare Metal)** | Medium | $ | Full control |
| **Docker** | Low | $ | Portability |
| **Azure App Service** | Low | $$ | Microsoft ecosystem |
| **AWS EC2/ECS** | Medium | $$ | AWS ecosystem |
| **Linux VPS** | Low | $ | Budget-friendly |

---

## 1. Self-Hosted (Windows/Linux)

### Build

```bash
# Publish release
dotnet publish -c Release -o ./publish

# Output di folder ./publish
```

### Windows (IIS)

1. Install [.NET 10 Hosting Bundle](https://dotnet.microsoft.com/download/dotnet/10.0)
2. Install IIS dengan ASP.NET Core Module
3. Copy folder `publish` ke `C:\inetpub\wwwroot\PDA`
4. Buat Application Pool (No Managed Code)
5. Buat Website pointing ke folder
6. Set bindings (HTTPS)

**web.config** (auto-generated):
```xml
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <location path="." inheritInChildApplications="false">
    <system.webServer>
      <handlers>
        <add name="aspNetCore" path="*" verb="*"
             modules="AspNetCoreModuleV2" resourceType="Unspecified" />
      </handlers>
      <aspNetCore processPath="dotnet" arguments=".\PDA.dll"
                  stdoutLogEnabled="false" stdoutLogFile=".\logs\stdout"
                  hostingModel="inprocess" />
    </system.webServer>
  </location>
</configuration>
```

### Linux (systemd)

```bash
# Copy files
sudo mkdir -p /opt/pda
sudo cp -r ./publish/* /opt/pda/

# Set permissions
sudo chown -R www-data:www-data /opt/pda

# Create systemd service
sudo nano /etc/systemd/system/pda.service
```

**pda.service**:
```ini
[Unit]
Description=PDA Personal Data Analyst
After=network.target

[Service]
WorkingDirectory=/opt/pda
ExecStart=/usr/bin/dotnet /opt/pda/PDA.dll
Restart=always
RestartSec=10
KillSignal=SIGINT
SyslogIdentifier=pda
User=www-data
Environment=ASPNETCORE_ENVIRONMENT=Production
Environment=ASPNETCORE_URLS=http://localhost:5000
Environment=DOTNET_PRINT_TELEMETRY_MESSAGE=false

[Install]
WantedBy=multi-user.target
```

```bash
sudo systemctl enable pda
sudo systemctl start pda
sudo systemctl status pda
```

---

## 2. Docker Deployment

### Dockerfile

```dockerfile
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY ["PDA.csproj", "./"]
RUN dotnet restore
COPY . .
RUN dotnet publish -c Release -o /app

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /app .
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "PDA.dll"]
```

### Build & Run

```bash
# Build image
docker build -t pda:latest .

# Run container
docker run -d \
  --name pda \
  -p 8080:8080 \
  -v $(pwd)/PDA.db:/app/PDA.db \
  -v $(pwd)/KnowledgeBase:/app/KnowledgeBase \
  -e LLM__Providers__OpenAI__ApiKey=sk-... \
  -e RAG__Enabled=true \
  pda:latest
```

### Docker Compose

**docker-compose.yml**:
```yaml
version: '3.8'
services:
  pda:
    build: .
    ports:
      - "8080:8080"
    volumes:
      - ./data:/app/data
      - ./KnowledgeBase:/app/KnowledgeBase
      - ./uploads:/app/wwwroot/uploads
    environment:
      - ASPNETCORE_ENVIRONMENT=Production
      - ASPNETCORE_URLS=http://+:8080
      - ConnectionStrings__DefaultConnection=Data Source=/app/data/PDA.db
      - LLM__Providers__OpenAI__ApiKey=${OPENAI_API_KEY}
      - RAG__Enabled=true
      - RAG__ScanIntervalMinutes=60
    restart: unless-stopped

  # Optional: Qdrant for production RAG
  qdrant:
    image: qdrant/qdrant:latest
    ports:
      - "6333:6333"
    volumes:
      - ./qdrant_data:/qdrant/storage
```

```bash
docker-compose up -d
```

---

## 3. Nginx Reverse Proxy

```nginx
server {
    listen 80;
    server_name pda.example.com;
    return 301 https://$server_name$request_uri;
}

server {
    listen 443 ssl http2;
    server_name pda.example.com;

    ssl_certificate /etc/letsencrypt/live/pda.example.com/fullchain.pem;
    ssl_certificate_key /etc/letsencrypt/live/pda.example.com/privkey.pem;

    # Security headers
    add_header X-Frame-Options "SAMEORIGIN";
    add_header X-Content-Type-Options "nosniff";
    add_header X-XSS-Protection "1; mode=block";
    add_header Referrer-Policy "strict-origin-when-cross-origin";

    location / {
        proxy_pass http://localhost:5000;
        proxy_http_version 1.1;
        proxy_set_header Upgrade $http_upgrade;
        proxy_set_header Connection "upgrade";
        proxy_set_header Host $host;
        proxy_set_header X-Real-IP $remote_addr;
        proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
        proxy_set_header X-Forwarded-Proto $scheme;
        
        # WebSocket support (SignalR/Blazor)
        proxy_read_timeout 86400;
    }

    # Static files caching
    location /_framework/ {
        proxy_pass http://localhost:5000;
        expires 30d;
        add_header Cache-Control "public, immutable";
    }
}
```

---

## 4. Production Database

### Migrate dari SQLite ke SQL Server

```bash
# 1. Ubah connection string di appsettings.json
"ConnectionStrings": {
  "DefaultConnection": "Server=prod-server;Database=PDA;Trusted_Connection=true;TrustServerCertificate=true"
}

# 2. Install EF Core tools
dotnet tool install --global dotnet-ef

# 3. Buat migrasi
dotnet ef migrations add InitialCreate

# 4. Apply migrasi
dotnet ef database update
```

### Backup SQLite

```bash
# Simple file copy
cp PDA.db PDA_backup_$(date +%Y%m%d).db

# Scheduled backup (crontab)
0 2 * * * cp /opt/pda/PDA.db /backups/PDA_$(date +\%Y\%m\%d).db
```

---

## 5. Environment Variables Checklist

### Required
```bash
# Setidaknya satu LLM provider:
LLM__Providers__OpenAI__ApiKey=sk-...
# ATAU
LLM__Providers__Anthropic__ApiKey=sk-ant-...
# ATAU (Ollama - local, no key needed)
LLM__DefaultProvider=Ollama
```

### Recommended
```bash
ASPNETCORE_ENVIRONMENT=Production
ConnectionStrings__DefaultConnection=Data Source=/data/PDA.db
RAG__Enabled=true
RAG__ScanIntervalMinutes=60
```

### Optional
```bash
Storage__Provider=AzureBlob        # Untuk cloud storage
LLM__DefaultTemperature=0.3        # Suhu default
LLM__DefaultMaxTokens=4096         # Max token default
```

---

## 6. Health Checks & Monitoring

### Health Endpoint
```
GET /api/health
→ {"status":"healthy","timestamp":"2024-01-15T10:30:00Z"}
```

### Monitoring Setup
```bash
# Uptime monitoring dengan cron
*/5 * * * * curl -f https://pda.example.com/api/health || echo "PDA DOWN" | mail admin@example.com
```

### Logging
```bash
# Systemd
journalctl -u pda -f

# Docker
docker logs -f pda

# File (jika dikonfigurasi)
tail -f /var/log/pda/*.log
```

---

## 7. Performance Tuning

### Kestrel Configuration (Program.cs)
```csharp
builder.WebHost.ConfigureKestrel(options =>
{
    options.Limits.MaxConcurrentConnections = 100;
    options.Limits.MaxConcurrentUpgradedConnections = 100;
    options.Limits.MaxRequestBodySize = 50 * 1024 * 1024; // 50 MB
});
```

### Response Compression
```csharp
builder.Services.AddResponseCompression(options =>
{
    options.EnableForHttps = true;
    options.MimeTypes = ResponseCompressionDefaults.MimeTypes.Concat(
        new[] { "application/javascript", "text/css" });
});
```

---

## 8. Security Checklist

- [ ] HTTPS enabled (TLS 1.2+)
- [ ] API keys tidak di source code
- [ ] User secrets untuk development
- [ ] Environment variables untuk production
- [ ] Cookie secure flag di production
- [ ] Antiforgery enabled (default)
- [ ] SQL read-only untuk tool queryToDatabase
- [ ] Authentication required untuk semua endpoint
- [ ] Rate limiting (via middleware atau reverse proxy)
- [ ] Regular security updates (`dotnet --info`)
