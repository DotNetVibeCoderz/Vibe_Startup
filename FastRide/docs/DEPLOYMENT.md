# 🚀 Deployment Guide — FastRide

> Complete deployment guide for various environments: local, Docker, Azure, and self-hosted.

---

## 📖 Overview

This guide covers deploying the FastRide platform components in different environments.

---

## 🖥️ Local Development

### Prerequisites

- Windows 10/11, macOS, or Linux
- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- [MAUI Workload](https://learn.microsoft.com/en-us/dotnet/maui/get-started/installation) (optional, for mobile apps)

### Steps

```bash
# 1. Restore packages
dotnet restore

# 2. Run API (Terminal 1)
dotnet run --project FastRide.Api

# 3. Run Admin Dashboard (Terminal 2)
dotnet run --project FastRide.AdminWeb

# 4. Run Simulator (Terminal 3)
dotnet run --project FastRide.Simulator
```

### URLs

| Service | URL |
|---------|-----|
| API | `https://localhost:5001` |
| Admin Dashboard | `https://localhost:5002` |

---

## 🐳 Docker Deployment

### Dockerfile for API

Create `FastRide.Api/Dockerfile`:

```dockerfile
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy project files
COPY FastRide.Shared/ FastRide.Shared/
COPY FastRide.Data/ FastRide.Data/
COPY FastRide.Api/ FastRide.Api/

# Restore and build
WORKDIR /src/FastRide.Api
RUN dotnet restore
RUN dotnet publish -c Release -o /app

# Runtime
FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /app .
EXPOSE 80 443
ENTRYPOINT ["dotnet", "FastRide.Api.dll"]
```

### Docker Compose

```yaml
version: '3.8'
services:
  api:
    build:
      context: .
      dockerfile: FastRide.Api/Dockerfile
    ports:
      - "5001:80"
      - "5000:443"
    environment:
      - ASPNETCORE_ENVIRONMENT=Production
      - ConnectionStrings__DefaultConnection=Data Source=/data/FastRide.db
    volumes:
      - fastride-data:/data

  admin:
    build:
      context: .
      dockerfile: FastRide.AdminWeb/Dockerfile
    ports:
      - "5002:80"
    environment:
      - ApiBaseUrl=http://api:80

volumes:
  fastride-data:
```

### Running with Docker Compose

```bash
docker-compose up -d
```

---

## ☁️ Azure Deployment

### Azure App Service

```bash
# 1. Create Resource Group
az group create --name FastRide-RG --location southeastasia

# 2. Create App Service Plan
az appservice plan create --name FastRide-Plan --resource-group FastRide-RG --sku B1

# 3. Create Web App for API
az webapp create --name fastride-api --resource-group FastRide-RG --plan FastRide-Plan

# 4. Publish API
dotnet publish FastRide.Api -c Release -o ./publish/api
cd publish/api
zip -r ../api.zip .
az webapp deploy --name fastride-api --resource-group FastRide-RG --src-path ../api.zip

# 5. Create Web App for Admin
az webapp create --name fastride-admin --resource-group FastRide-RG --plan FastRide-Plan

# 6. Publish Admin
dotnet publish FastRide.AdminWeb -c Release -o ./publish/admin
cd publish/admin
zip -r ../admin.zip .
az webapp deploy --name fastride-admin --resource-group FastRide-RG --src-path ../admin.zip
```

### Azure SQL Database

```bash
# Create SQL Server
az sql server create --name fastride-sql --resource-group FastRide-RG --location southeastasia --admin-user fastride --admin-password <StrongPassword>

# Create Database
az sql db create --name FastRide --resource-group FastRide-RG --server fastride-sql --service-objective S0

# Update connection string in Azure App Settings
az webapp config appsettings set --name fastride-api --resource-group FastRide-RG --settings "ConnectionStrings__DefaultConnection=Server=tcp:fastride-sql.database.windows.net;Database=FastRide;..."
```

---

## 🏠 Self-Hosted (IIS / Nginx)

### Windows IIS

```powershell
# 1. Install IIS and .NET Hosting Bundle
# Download from: https://dotnet.microsoft.com/download

# 2. Publish
dotnet publish FastRide.Api -c Release -o C:\inetpub\wwwroot\FastRideApi

# 3. Create IIS Site
New-IISSite -Name "FastRide API" -PhysicalPath "C:\inetpub\wwwroot\FastRideApi" -BindingInformation "*:5001:"
```

### Linux Nginx

```bash
# 1. Install .NET Runtime
wget https://dot.net/v1/dotnet-install.sh
chmod +x dotnet-install.sh
./dotnet-install.sh --channel 10.0 --runtime aspnetcore

# 2. Publish
dotnet publish FastRide.Api -c Release -o /var/www/fastride-api

# 3. Create systemd service
sudo cat > /etc/systemd/system/fastride-api.service << EOF
[Unit]
Description=FastRide API
After=network.target

[Service]
WorkingDirectory=/var/www/fastride-api
ExecStart=/usr/share/dotnet/dotnet FastRide.Api.dll
Restart=always
RestartSec=10
Environment=ASPNETCORE_ENVIRONMENT=Production
Environment=ASPNETCORE_URLS=http://localhost:5000

[Install]
WantedBy=multi-user.target
EOF

# 4. Start service
sudo systemctl enable fastride-api
sudo systemctl start fastride-api

# 5. Configure Nginx reverse proxy
sudo cat > /etc/nginx/sites-available/fastride << EOF
server {
    listen 80;
    server_name api.fastride.com;

    location / {
        proxy_pass http://localhost:5000;
        proxy_http_version 1.1;
        proxy_set_header Upgrade \$http_upgrade;
        proxy_set_header Connection keep-alive;
        proxy_set_header Host \$host;
        proxy_cache_bypass \$http_upgrade;
    }
}
EOF

sudo ln -s /etc/nginx/sites-available/fastride /etc/nginx/sites-enabled/
sudo nginx -t && sudo systemctl reload nginx
```

---

## 🔐 Production Checklist

- [ ] Use **HTTPS** with valid SSL certificates
- [ ] Set `ASPNETCORE_ENVIRONMENT=Production`
- [ ] Use a **strong database** (SQL Server/PostgreSQL, not SQLite)
- [ ] Configure **CORS** to specific origins only
- [ ] Enable **JWT authentication** with strong secret
- [ ] Set up **logging** (Application Insights, Serilog, ELK)
- [ ] Configure **rate limiting** on API
- [ ] Use **secrets manager** (Azure Key Vault, AWS Secrets Manager)
- [ ] Set up **monitoring** (health checks, alerts)
- [ ] Configure **database backups**
- [ ] Review **firewall rules** and network security

---

## 📊 Environment Comparison

| Feature | Development | Staging | Production |
|---------|------------|---------|------------|
| Database | SQLite | SQL Server | SQL Server |
| Logging | Console | Serilog File | App Insights |
| SSL | Dev cert | Let's Encrypt | Paid cert |
| CORS | Allow all | Specific origins | Specific origins |
| Auth | Optional | JWT | JWT + 2FA |
| Cache | Memory | Memory | Redis |
| Monitoring | None | Basic | Full (APM) |
