# 🚢 Deployment

## Production Deployment

### Prasyarat Server
- .NET 10 Runtime / SDK
- Reverse proxy: Nginx, Apache, IIS, atau cloud (Azure App Service, AWS Elastic Beanstalk)
- (Opsional) Database server: SQL Server, MySQL, atau PostgreSQL
- (Opsional) Storage: Azure Blob, AWS S3, atau MinIO

---

## Deployment Options

### 1. Self-Hosted (Linux)

```bash
# Build
dotnet publish -c Release -o ./publish

# Run with systemd
sudo cp ./publish /var/www/rentalboil
sudo systemctl enable rentalboil
sudo systemctl start rentalboil
```

**Systemd Service** (`/etc/systemd/system/rentalboil.service`):
```ini
[Unit]
Description=RentalBoil Web App
After=network.target

[Service]
WorkingDirectory=/var/www/rentalboil
ExecStart=/usr/bin/dotnet /var/www/rentalboil/RentalBoil.dll
Restart=always
RestartSec=10
KillSignal=SIGINT
SyslogIdentifier=rentalboil
User=www-data
Environment=ASPNETCORE_ENVIRONMENT=Production
Environment=ASPNETCORE_URLS=https://localhost:5001

[Install]
WantedBy=multi-user.target
```

### 2. Nginx Reverse Proxy

```nginx
server {
    listen 80;
    server_name rentalboil.com;
    return 301 https://$server_name$request_uri;
}

server {
    listen 443 ssl;
    server_name rentalboil.com;

    ssl_certificate /etc/letsencrypt/live/rentalboil.com/fullchain.pem;
    ssl_certificate_key /etc/letsencrypt/live/rentalboil.com/privkey.pem;

    location / {
        proxy_pass https://localhost:5001;
        proxy_http_version 1.1;
        proxy_set_header Upgrade $http_upgrade;
        proxy_set_header Connection "upgrade";
        proxy_set_header Host $host;
        proxy_set_header X-Real-IP $remote_addr;
        proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
        proxy_set_header X-Forwarded-Proto $scheme;
    }

    # WebSocket (SignalR)
    location /_blazor {
        proxy_pass https://localhost:5001;
        proxy_http_version 1.1;
        proxy_set_header Upgrade $http_upgrade;
        proxy_set_header Connection "upgrade";
    }
}
```

### 3. Docker

```dockerfile
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
WORKDIR /app
EXPOSE 80
EXPOSE 443

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY . .
RUN dotnet restore
RUN dotnet publish -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "RentalBoil.dll"]
```

```bash
docker build -t rentalboil .
docker run -d -p 5001:443 --name rentalboil rentalboil
```

### 4. Azure App Service

```bash
# Deploy via Azure CLI
az webapp deploy --resource-group myResourceGroup \
    --name rentalboil-app \
    --src-path ./publish

# Atau via Visual Studio: Publish → Azure App Service
```

### 5. AWS Elastic Beanstalk

```bash
# Build
dotnet publish -c Release -o ./publish

# Create deployment zip
cd publish && zip -r ../rentalboil.zip . && cd ..

# Deploy via AWS CLI
aws elasticbeanstalk create-application-version \
    --application-name rentalboil \
    --version-label v1 \
    --source-bundle S3Bucket=my-bucket,S3Key=rentalboil.zip
```

---

## Configuration for Production

### Environment Variables

```bash
export ASPNETCORE_ENVIRONMENT=Production
export ConnectionStrings__DefaultConnection="Server=prod-server;Database=RentalBoil;..."
export AI__Provider=OpenAI
export AI__OpenAI__ApiKey=sk-prod-key
export ApiSettings__ApiKey=rntl-production-secure-key
```

### Production `appsettings.Production.json`
```json
{
  "Database": {
    "Provider": "SqlServer"
  },
  "GPS": {
    "SimulatorEnabled": true,
    "UpdateMode": "Api"
  },
  "Logging": {
    "LogLevel": {
      "Default": "Warning",
      "Microsoft.AspNetCore": "Warning"
    }
  }
}
```

---

## Production Checklist

| Item | Action |
|------|--------|
| ✅ HTTPS enforced | `app.UseHttpsRedirection()` + `app.UseHsts()` |
| ✅ Database provider | Ganti dari SQLite ke SQL Server/MySQL/PostgreSQL |
| ✅ API Key | Ganti dari default key |
| ✅ CORS | Batasi origin di `ApiCors` |
| ✅ Logging | Gunakan Serilog/Application Insights |
| ✅ Monitoring | Setup health checks endpoint |
| ✅ Backup | Database backup rutin |
| ✅ SSL | Let's Encrypt atau managed cert |
| ✅ Firewall | Batasi port (hanya 443) |
| ✅ Rate Limiting | Tambahkan untuk API endpoints |
