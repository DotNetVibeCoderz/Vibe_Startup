# Dokumentasi WashUp

## 📑 Daftar Isi

1. [Arsitektur Sistem](#arsitektur-sistem)
2. [Model Database](#model-database)
3. [Role & Permission](#role--permission)
4. [API Reference](#api-reference)
5. [ChatBot Configuration](#chatbot-configuration)
6. [IoT Simulator](#iot-simulator)
7. [GPS Simulator](#gps-simulator)
8. [Storage Providers](#storage-providers)
9. [Deployment Guide](#deployment-guide)

---

## Catatan Render Mode (penting)

Aplikasi memakai **InteractiveServer render mode global** (diatur di `Components/App.razor` lewat `HttpContext.AcceptsInteractiveRouting()`), sehingga semua `@onclick`/`@bind` berjalan di atas circuit SignalR.

Pengecualian — halaman auth dirender **statis (SSR)** dengan atribut `[ExcludeFromInteractiveRouting]` karena Identity harus menulis cookie pada request HTTP biasa:
- `/auth/login`, `/auth/register`, `/auth/forgot-password`, `/auth/reset-password` → form SSR dengan `FormName` + `[SupplyParameterFromForm]`
- Logout lewat endpoint `GET /auth/logout` (bukan dari circuit)

Reset password berjalan dalam **mode demo**: karena SMTP tidak dikonfigurasi, tautan reset ditampilkan langsung di halaman lupa password.

---

## Autentikasi API (JWT)

Integrasi eksternal memakai bearer token:

```
POST /api/auth/token
{ "email": "admin@washup.id", "password": "WashUp@2024" }
→ { "access_token": "...", "token_type": "Bearer", "expires_in": 28800 }
```

Endpoint ber-auth memakai policy `Api` yang menerima cookie Identity **atau** JWT bearer. Konfigurasi kunci ada di `appsettings.json` bagian `Jwt` (ganti `Key` di produksi!).

Export laporan (CSV kompatibel Excel, delimiter `;`, UTF-8 BOM):
- `GET /api/reports/export/orders?from=&to=`
- `GET /api/reports/export/finance?from=&to=`

---

## Arsitektur Sistem

WashUp menggunakan arsitektur **Blazor Server** dengan **SignalR** untuk komunikasi real-time.

```
[Browser] <--SignalR--> [Blazor Server] <--EF Core--> [Database]
                              |
                    +---------+---------+
                    |         |         |
               [IoT Sim] [GPS Sim] [AI Chat]
```

### Alur Order:
1. Pelanggan membuat order (online/offline)
2. Admin menerima dan memproses
3. Operator mencuci & menyetrika
4. Kurir mengantar (dengan GPS tracking)
5. Pelanggan menerima & memberikan review

---

## Model Database

### Entity Relationship
- **ApplicationUser** - Extended Identity user
- **Branch** - Multi-cabang
- **Order** - Order laundry
- **Invoice** - Tagihan
- **InventoryItem** - Stok bahan
- **StaffMember** - Data pegawai
- **CourierAssignment** - Tugas kurir
- **IoTDevice** - Perangkat IoT
- **Review** - Ulasan pelanggan
- **Complaint** - Komplain
- **ChatSession / ChatMessage** - Chat AI

---

## Role & Permission

| Fitur | Pemilik | Admin | Kurir | Pelanggan |
|-------|---------|-------|-------|-----------|
| Dashboard | ✅ | ✅ | ❌ | ✅ (own) |
| Order Management | ✅ | ✅ | ✅ (delivery) | ✅ (own) |
| Customer Data | ✅ | ✅ | ❌ | ❌ |
| Finance | ✅ | ✅ | ❌ | ❌ |
| Inventory | ✅ | ✅ | ❌ | ❌ |
| Staff Management | ✅ | ✅ | ❌ | ❌ |
| IoT Monitoring | ✅ | ✅ | ❌ | ❌ |
| GPS Tracking | ✅ | ✅ | ✅ | ❌ |
| Branches | ✅ | ❌ | ❌ | ❌ |
| Chat AI | ✅ | ✅ | ✅ | ✅ |
| Profile | ✅ | ✅ | ✅ | ✅ |

---

## API Reference

### Base URL: `https://your-domain.com/api`

### Authentication
Semua endpoint (kecuali marketplace & health) menggunakan cookie authentication.

### Endpoints
Lihat `Program.cs` untuk semua endpoint atau akses `/swagger` untuk Swagger UI.

---

## ChatBot Configuration

ChatBot "Mbok Inem" dikonfigurasi di `appsettings.json`:

```json
{
  "ChatBot": {
    "Name": "Mbok Inem",
    "SystemPrompt": "Kamu adalah Mbok Inem...",
    "Temperature": 0.8,
    "MaxTokens": 2000
  }
}
```

### Kernel Functions:
- `SearchInternet` - Tavily search API
- `ScrapeUrl` - Web scraping
- `GetDateTime` - Current date/time
- `Calculate` - Math expression
- `QueryOrders` - Database order query
- `QueryCustomers` - Database customer query
- `ReadFileFromUrl` - File reader
- `GetPricing` - Harga layanan
- `GetBranches` - Info cabang

---

## IoT Simulator

IoT Simulator berjalan di thread terpisah dan menghasilkan data sensor setiap 5 detik.

### Device Types:
- **MesinCuci**: RPM + Suhu
- **Listrik**: kWh + Voltage
- **Air**: Liter + Pressure
- **SensorSuhu**: Celsius + Humidity

### Control:
- Start/Stop dari halaman IoT Monitoring
- Status simulator ditampilkan real-time

---

## GPS Simulator

GPS Simulator mensimulasikan pergerakan kurir menuju titik tujuan.

### Features:
- Update posisi setiap 3 detik
- Kalkulasi jarak ke tujuan
- Auto-detect arrival (dalam radius ~100m)
- Log tracking disimpan ke database

---

## Storage Providers

WashUp mendukung 4 provider storage:

### FileSystem (Default)
- Path: `wwwroot/uploads/`
- Cocok untuk development

### Azure Blob
```json
{
  "FileStorage": {
    "Provider": "AzureBlob",
    "AzureBlob": {
      "ConnectionString": "...",
      "ContainerName": "washup"
    }
  }
}
```

### AWS S3 / MinIO
```json
{
  "FileStorage": {
    "Provider": "S3",
    "S3": {
      "AccessKey": "...",
      "SecretKey": "...",
      "BucketName": "washup",
      "Region": "us-east-1"
    }
  }
}
```

---

## Deployment Guide

### 1. Build
```bash
dotnet publish -c Release -o ./publish
```

### 2. Database
- Development: SQLite (auto-created)
- Production: PostgreSQL, SQL Server, atau MySQL direkomendasikan

Provider dipilih lewat `DatabaseProvider`: `SQLite` | `PostgreSQL` | `SqlServer` | `MySQL`.
Untuk MySQL (provider Pomelo), versi server di-pin lewat `MySqlServerVersion`
(default `8.0.36`) agar tidak perlu koneksi AutoDetect saat startup.

### 3. Environment Variables
```bash
DatabaseProvider=PostgreSQL          # atau MySQL / SqlServer / SQLite
ConnectionStrings__PostgreSQL=Host=...
ConnectionStrings__MySQL=Server=localhost;Port=3306;Database=WashUp;User=washup;Password=...
MySqlServerVersion=8.0.36
AI__Provider=OpenAI
AI__OpenAI__ApiKey=sk-...
```

### 4. Run
```bash
cd publish
dotnet WashUp.dll
```

### 5. Reverse Proxy (Nginx)
```nginx
server {
    listen 80;
    server_name washup.id;
    location / {
        proxy_pass http://localhost:5000;
        proxy_http_version 1.1;
        proxy_set_header Upgrade $http_upgrade;
        proxy_set_header Connection "upgrade";
    }
}
```

---

## 🎉 Selamat Menggunakan WashUp!

Untuk pertanyaan lebih lanjut, silakan hubungi tim GraviCode Studios.
