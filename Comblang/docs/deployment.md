# 🚀 Deployment Guide — Comblang

## Prasyarat Server

- **.NET 10 Runtime** (Linux/Windows/macOS)
- Database engine (SQLite built-in, atau SQL Server/MySQL/PostgreSQL)
- (Opsional) Redis untuk caching
- (Opsional) MinIO/S3/Azure Blob untuk storage production

---

## Deployment Options

### 1. Self-Contained Publishing (Recommended)

```bash
# Publish sebagai self-contained app (tidak perlu install .NET runtime)
dotnet publish -c Release -r linux-x64 --self-contained true -o ./publish

# Copy ke server
scp -r ./publish user@server:/opt/comblang/

# Di server, jalankan
cd /opt/comblang
chmod +x Comblang
./Comblang
```

### 2. Framework-Dependent Publishing

```bash
# Publish (perlu .NET Runtime di server)
dotnet publish -c Release -o ./publish

# Di server (setelah install .NET Runtime)
dotnet Comblang.dll
```

### 3. Docker

Buat `Dockerfile`:

```dockerfile
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY . .
RUN dotnet publish -c Release -o /app

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /app .
ENV ASPNETCORE_URLS=http://+:5000
EXPOSE 5000
ENTRYPOINT ["dotnet", "Comblang.dll"]
```

Build & run:
```bash
docker build -t comblang .
docker run -d -p 5000:5000 --name comblang comblang
```

### 4. Docker Compose (Full Stack)

`docker-compose.yml`:
```yaml
version: '3.8'
services:
  comblang:
    build: .
    ports:
      - "5000:5000"
    environment:
      - DatabaseProvider=PostgreSql
      - ConnectionStrings__PostgreSql=Host=postgres;Database=Comblang;Username=postgres;Password=secret
      - Storage__Provider=MinIO
      - Storage__MinIO__Endpoint=minio:9000
      - Storage__MinIO__AccessKey=minioadmin
      - Storage__MinIO__SecretKey=minioadmin
    depends_on:
      - postgres
      - minio
      - redis

  postgres:
    image: postgres:16
    environment:
      POSTGRES_PASSWORD: secret
      POSTGRES_DB: Comblang
    volumes:
      - pgdata:/var/lib/postgresql/data

  minio:
    image: minio/minio
    command: server /data --console-address ":9001"
    environment:
      MINIO_ROOT_USER: minioadmin
      MINIO_ROOT_PASSWORD: minioadmin
    volumes:
      - miniodata:/data

  redis:
    image: redis:7-alpine

volumes:
  pgdata:
  miniodata:
```

```bash
docker-compose up -d
```

---

## Konfigurasi Environment Variables

Semua setting di `appsettings.json` bisa di-override via environment variable. Format: gunakan `__` (double underscore) untuk nested key.

```bash
# Database
export DatabaseProvider=PostgreSql
export ConnectionStrings__PostgreSql="Host=db.example.com;Database=Comblang;Username=app;Password=xxx"

# Storage
export Storage__Provider=S3
export Storage__S3__Bucket=comblang-prod
export Storage__S3__Region=ap-southeast-1
export Storage__S3__AccessKey=AKIAXXXX
export Storage__S3__SecretKey=xxxx

# AI
export AI__Models__OpenAI__ApiKey=sk-xxxx
export SiMakComblang__Model=OpenAI

# JWT
export Jwt__Secret=super-secret-key-min-32-chars!!
```

---

## Production Checklist

- [ ] Ganti `Jwt:Secret` dengan key kuat (min 32 karakter)
- [ ] Ganti `ApiKey` dengan key acak
- [ ] Gunakan database production (SQL Server / PostgreSQL)
- [ ] Gunakan storage production (S3 / Azure Blob)
- [ ] Set `ASPNETCORE_ENVIRONMENT=Production`
- [ ] Pasang SSL/TLS (Let's Encrypt + reverse proxy)
- [ ] Setup monitoring & logging
- [ ] Backup database secara berkala
- [ ] Rate limiting untuk API endpoints
- [ ] HSTS header enabled (otomatis di Production mode)

---

## Reverse Proxy (Nginx)

```nginx
server {
    listen 80;
    server_name comblang.example.com;
    return 301 https://$host$request_uri;
}

server {
    listen 443 ssl http2;
    server_name comblang.example.com;

    ssl_certificate /etc/letsencrypt/live/comblang.example.com/fullchain.pem;
    ssl_certificate_key /etc/letsencrypt/live/comblang.example.com/privkey.pem;

    location / {
        proxy_pass http://localhost:5000;
        proxy_http_version 1.1;
        proxy_set_header Upgrade $http_upgrade;
        proxy_set_header Connection "upgrade";
        proxy_set_header Host $host;
        proxy_set_header X-Real-IP $remote_addr;
        proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
        proxy_set_header X-Forwarded-Proto $scheme;
    }

    # WebSocket for SignalR
    location /hubs/ {
        proxy_pass http://localhost:5000;
        proxy_http_version 1.1;
        proxy_set_header Upgrade $http_upgrade;
        proxy_set_header Connection "upgrade";
    }
}
```

---

## Monitoring

Untuk production, tambahkan health check endpoint:

```csharp
// Di Program.cs
app.MapGet("/health", () => Results.Ok(new { status = "healthy", timestamp = DateTime.UtcNow }));
```

Gunakan tools seperti:
- **UptimeRobot** untuk uptime monitoring
- **Application Insights** atau **Serilog + Seq** untuk logging
- **Prometheus + Grafana** untuk metrics
