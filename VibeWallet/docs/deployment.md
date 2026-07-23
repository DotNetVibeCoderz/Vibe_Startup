# VibeWallet Deployment Guide

## Prerequisites

- **.NET 10 SDK** or later
- Database server (optional, SQLite works out of the box)
- AI API keys (optional, for chat bot)

## Quick Deploy (Development)

```bash
# Clone the project
git clone https://github.com/vibewallet/vibewallet.git
cd VibeWallet

# Restore packages
dotnet restore

# Run the application
dotnet run

# Open browser at https://localhost:5001
```

## Database Configuration

### SQLite (Default)
No setup needed. Database is created automatically at `Data/VibeWallet.db`.

### SQL Server
Update `appsettings.json`:
```json
{
  "ConnectionStrings": {
    "Provider": "SQLServer",
    "SQLServer": "Server=your-server;Database=VibeWallet;User Id=sa;Password=your-password;"
  }
}
```

### MySQL
```json
{
  "ConnectionStrings": {
    "Provider": "MySQL",
    "MySQL": "Server=localhost;Database=VibeWallet;User=root;Password=your-password;"
  }
}
```

### PostgreSQL
```json
{
  "ConnectionStrings": {
    "Provider": "Postgre",
    "Postgre": "Host=localhost;Database=VibeWallet;Username=postgres;Password=your-password;"
  }
}
```

## Storage Configuration

### FileSystem (Default)
Files stored in `wwwroot/uploads/`. No additional configuration needed.

### Azure Blob Storage
```json
{
  "Storage": {
    "Provider": "AzureBlob",
    "AzureBlob": {
      "ConnectionString": "DefaultEndpointsProtocol=https;AccountName=...",
      "ContainerName": "vibewallet"
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
      "BucketName": "vibewallet",
      "Region": "ap-southeast-1"
    }
  }
}
```

### MinIO
```json
{
  "Storage": {
    "Provider": "MinIO",
    "MinIO": {
      "Endpoint": "localhost:9000",
      "AccessKey": "minioadmin",
      "SecretKey": "minioadmin",
      "BucketName": "vibewallet"
    }
  }
}
```

## Production Deployment

### Linux with Nginx

1. Publish the application:
```bash
dotnet publish -c Release -o /var/www/vibewallet
```

2. Configure Nginx:
```nginx
server {
    listen 80;
    server_name vibewallet.id;

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

3. Create systemd service:
```ini
[Unit]
Description=VibeWallet Web Application

[Service]
WorkingDirectory=/var/www/vibewallet
ExecStart=/usr/bin/dotnet /var/www/vibewallet/VibeWallet.dll
Restart=always
RestartSec=10
Environment=ASPNETCORE_ENVIRONMENT=Production

[Install]
WantedBy=multi-user.target
```

### Docker

```dockerfile
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY . .
RUN dotnet restore
RUN dotnet publish -c Release -o /app

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /app .
EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080
ENTRYPOINT ["dotnet", "VibeWallet.dll"]
```

## Environment Variables

Override any appsettings.json value:

```bash
export ConnectionStrings__Provider=SQLServer
export ConnectionStrings__SQLServer="Server=...;Database=..."
export ChatBot__Models__OpenAI__ApiKey=sk-...
```

## Security Checklist

- [ ] Change default admin password
- [ ] Set strong API keys for AI models
- [ ] Enable HTTPS in production
- [ ] Configure firewall rules
- [ ] Set up database backups
- [ ] Enable logging to file/server
- [ ] Review transaction limits
- [ ] Configure CORS for API

## Monitoring

- Logs are written to `Logs/vibewallet-{date}.log`
- Health check endpoint: `GET /api/v1/health`
- Serilog structured logging support

## Backup

```bash
# SQLite backup
cp Data/VibeWallet.db Data/VibeWallet-backup-$(date +%Y%m%d).db

# SQL Server backup (via sqlcmd)
sqlcmd -S server -Q "BACKUP DATABASE VibeWallet TO DISK='backup.bak'"
```
