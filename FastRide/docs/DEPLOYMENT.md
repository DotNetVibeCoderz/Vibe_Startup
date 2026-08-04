# 🚀 Deployment — FastRide

---

## Prasyarat

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- Workload MAUI hanya bila membangun aplikasi mobile (`dotnet workload install maui`)

---

## Lokal

```bash
dotnet restore

dotnet run --project FastRide.Api          # https://localhost:5001
dotnet run --project FastRide.AdminWeb     # https://localhost:5002
dotnet run --project FastRide.Simulator -- --duration 60
```

| Layanan | HTTPS | HTTP |
|---------|-------|------|
| API | 5001 | 5000 |
| Konsol admin | 5002 | 5003 |

Alamat konsol harus tercantum di `ApiSettings:CorsOrigins` milik API — secara bawaan sudah.

---

## ⚠️ Kunci konfigurasi

Semua pengaturan bisa ditimpa lewat variabel lingkungan. Pemisah tingkatnya **dua garis
bawah**.

```bash
# Database
Database__Provider=PostgreSQL
Database__ConnectionStrings__PostgreSQL="Host=db;Database=FastRide;Username=postgres;Password=..."
Database__AutoSeed=false

# Cache
Cache__Provider=Redis
Cache__Redis__ConnectionString=redis:6379

# Storage
Storage__Provider=S3
Storage__S3__Endpoint=http://minio:9000
Storage__S3__Bucket=fastride-photos
Storage__S3__AccessKey=...
Storage__S3__SecretKey=...
Storage__S3__PublicUrl=https://cdn.example.com/fastride-photos

# Keamanan — wajib diganti
Jwt__Secret="<minimal 32 karakter>"
ApiSettings__CorsOrigins__0=https://admin.example.com
```

> Ini bukan `ConnectionStrings__DefaultConnection`. Connection string berada di bawah
> `Database:ConnectionStrings:<Provider>` sesuai provider yang dipilih.

**API menolak start bila `Jwt:Secret` kurang dari 32 karakter.** Buat satu:

```bash
export Jwt__Secret="$(openssl rand -base64 48)"
```

---

## Sebelum produksi

| Hal | Aksi |
|-----|------|
| Rahasia JWT | Ganti dengan nilai acak, simpan di secret store |
| Database | Pindah dari SQLite ke PostgreSQL/SQL Server |
| Data contoh | `Database__AutoSeed=false` |
| Akun demo | Hapus atau nonaktifkan ketiga akun `Password123` |
| Reset kata sandi | Sambungkan pengirim email di `AuthEndpoints.ForgotPassword` — sekarang kode hanya dikembalikan di Development |
| CORS | Batasi ke domain konsol yang sebenarnya |
| Storage | Pindah dari FileSystem ke S3/Azure bila lebih dari satu instans |
| Cache | `Cache__Provider=Redis` bila lebih dari satu instans — cache in-memory tidak dibagi antar node |
| Skema | Belum ada migrasi EF; siapkan skema sebelum rilis pertama (lihat `PLAN.md` v2.1) |

---

## Docker

`FastRide.Api/Dockerfile`:

```dockerfile
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY FastRide.Shared/ FastRide.Shared/
COPY FastRide.Data/   FastRide.Data/
COPY FastRide.Api/    FastRide.Api/

RUN dotnet publish FastRide.Api/FastRide.Api.csproj -c Release -o /app

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /app .
EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080
ENTRYPOINT ["dotnet", "FastRide.Api.dll"]
```

`FastRide.AdminWeb/Dockerfile`:

```dockerfile
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY FastRide.Shared/   FastRide.Shared/
COPY FastRide.AdminWeb/ FastRide.AdminWeb/

RUN dotnet publish FastRide.AdminWeb/FastRide.AdminWeb.csproj -c Release -o /app

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /app .
EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080
ENTRYPOINT ["dotnet", "FastRide.AdminWeb.dll"]
```

### Docker Compose

```yaml
services:
  db:
    image: postgres:16
    environment:
      POSTGRES_PASSWORD: fastride
      POSTGRES_DB: FastRide
    volumes:
      - pgdata:/var/lib/postgresql/data
    healthcheck:
      test: ["CMD-SHELL", "pg_isready -U postgres"]
      interval: 5s
      retries: 10

  redis:
    image: redis:7-alpine

  minio:
    image: minio/minio
    command: server /data --console-address ":9001"
    environment:
      MINIO_ROOT_USER: minioadmin
      MINIO_ROOT_PASSWORD: minioadmin
    ports: ["9000:9000", "9001:9001"]
    volumes:
      - miniodata:/data

  api:
    build:
      context: .
      dockerfile: FastRide.Api/Dockerfile
    depends_on:
      db:
        condition: service_healthy
    ports: ["5000:8080"]
    environment:
      ASPNETCORE_ENVIRONMENT: Production
      Database__Provider: PostgreSQL
      Database__ConnectionStrings__PostgreSQL: "Host=db;Database=FastRide;Username=postgres;Password=fastride"
      Cache__Provider: Redis
      Cache__Redis__ConnectionString: "redis:6379"
      Storage__Provider: S3
      Storage__S3__Endpoint: "http://minio:9000"
      Storage__S3__Bucket: "fastride-photos"
      Storage__S3__AccessKey: "minioadmin"
      Storage__S3__SecretKey: "minioadmin"
      Jwt__Secret: "ganti-dengan-rahasia-produksi-minimal-32-karakter"
      ApiSettings__CorsOrigins__0: "http://localhost:5002"

  admin:
    build:
      context: .
      dockerfile: FastRide.AdminWeb/Dockerfile
    depends_on: [api]
    ports: ["5002:8080"]
    environment:
      ASPNETCORE_ENVIRONMENT: Production
      ApiSettings__BaseUrl: "http://api:8080"

volumes:
  pgdata:
  miniodata:
```

```bash
docker compose up -d
curl http://localhost:5000/api/health
```

Buat bucket MinIO lebih dulu lewat konsolnya di `http://localhost:9001`.

---

## Azure App Service

```bash
az group create --name FastRide-RG --location southeastasia
az appservice plan create --name FastRide-Plan --resource-group FastRide-RG --sku B1 --is-linux

az webapp create --name fastride-api --resource-group FastRide-RG \
  --plan FastRide-Plan --runtime "DOTNETCORE:10.0"

dotnet publish FastRide.Api -c Release -o ./publish/api
(cd publish/api && zip -r ../api.zip .)
az webapp deploy --name fastride-api --resource-group FastRide-RG --src-path publish/api.zip
```

### Azure SQL

```bash
az sql server create --name fastride-sql --resource-group FastRide-RG \
  --location southeastasia --admin-user fastride --admin-password "<StrongPassword>"

az sql db create --name FastRide --resource-group FastRide-RG \
  --server fastride-sql --service-objective S0

az webapp config appsettings set --name fastride-api --resource-group FastRide-RG --settings \
  Database__Provider=SqlServer \
  Database__ConnectionStrings__SqlServer="Server=tcp:fastride-sql.database.windows.net,1433;Database=FastRide;User ID=fastride;Password=<StrongPassword>;Encrypt=true" \
  Jwt__Secret="<rahasia>"
```

Blazor Server memerlukan WebSocket dan sesi lengket:

```bash
az webapp config set --name fastride-admin --resource-group FastRide-RG --web-sockets-enabled true
az webapp update  --name fastride-admin --resource-group FastRide-RG --client-affinity-enabled true
```

---

## Self-hosted

### Nginx

Konsol admin memakai WebSocket; tanpa blok `Upgrade` di bawah ini, halaman akan memuat lalu
diam.

```nginx
server {
    listen 443 ssl http2;
    server_name api.example.com;

    location / {
        proxy_pass         http://127.0.0.1:5000;
        proxy_http_version 1.1;
        proxy_set_header   Host $host;
        proxy_set_header   X-Forwarded-For $proxy_add_x_forwarded_for;
        proxy_set_header   X-Forwarded-Proto $scheme;
    }
}

server {
    listen 443 ssl http2;
    server_name admin.example.com;

    location / {
        proxy_pass         http://127.0.0.1:5003;
        proxy_http_version 1.1;
        proxy_set_header   Upgrade $http_upgrade;
        proxy_set_header   Connection "upgrade";
        proxy_set_header   Host $host;
        proxy_set_header   X-Forwarded-For $proxy_add_x_forwarded_for;
        proxy_set_header   X-Forwarded-Proto $scheme;
        proxy_read_timeout 100s;
    }
}
```

Karena berjalan di belakang proxy, aktifkan header terusan di aplikasi
(`UseForwardedHeaders`) agar batas laju per-IP melihat alamat klien yang sebenarnya, bukan
alamat proxy.

### systemd

```ini
[Unit]
Description=FastRide API
After=network.target

[Service]
WorkingDirectory=/var/www/fastride-api
ExecStart=/usr/bin/dotnet /var/www/fastride-api/FastRide.Api.dll
Restart=always
RestartSec=10
User=www-data
Environment=ASPNETCORE_ENVIRONMENT=Production
Environment=ASPNETCORE_URLS=http://127.0.0.1:5000
EnvironmentFile=/etc/fastride/api.env

[Install]
WantedBy=multi-user.target
```

Simpan rahasia di `/etc/fastride/api.env` dengan izin `600`.

---

## Aplikasi mobile

```bash
# Android
dotnet publish FastRide.RiderApp -f net10.0-android -c Release

# Windows
dotnet publish FastRide.RiderApp -f net10.0-windows10.0.19041.0 -c Release
```

Sebelum rilis, ubah `ApiEndpoint.BaseUrl` di `MauiProgram.cs` ke alamat produksi. Build
Debug menerima sertifikat apa pun agar bisa bicara dengan sertifikat pengembangan; build
Release tidak, jadi endpoint produksi harus punya sertifikat yang sah.

---

## Pemeriksaan setelah deploy

```bash
curl https://api.example.com/api/health
```

Sehat bila `status` bernilai `healthy` dan provider yang tertera sesuai harapan. Nilai
`degraded` dengan HTTP 503 berarti database tidak bisa dihubungi.
