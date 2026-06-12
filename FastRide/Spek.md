# 🚖 Ride-Hailing Platform Solution

Name: Fast Ride

## 📌 Overview
Platform ride-hailing modern dengan Rider App, Driver App, Admin Web, dan Simulator.  
Dibangun dengan .NET ekosistem (Minimal API, Blazor, MAUI) dan mendukung simulasi paralel order creation.

---

## 🎯 Requirements
- **API**: .NET Minimal API (REST/GRPC)
- **Web Admin**: Blazor Server .NET
- **Mobile Rider**: MAUI Blazor
- **Mobile Driver**: MAUI Blazor
- **Simulator**: Console App (Spectre.Console)
- **UI/UX**: Modern, responsive, light/dark theme
- **Dashboard**: Analytic realtime dengan chart interactive, filter advanced, tabular data
- **Database**: SQLite, SQL Server, MySQL, PostgreSQL
- **Storage**: File System, MinIO, S3, Azure Blob
- **Cache**: IMemoryCache, Redis
- **Auth**: Register, Login/Logout, Reset Password, User Profile
- **Optimasi**: Algoritma efisien untuk performa cepat dan ringan

---

## 🏛️ Architecture

### Layered Architecture
- **Presentation Layer**
  - Rider App (MAUI Blazor)
  - Driver App (MAUI Blazor)
  - Admin Web (Blazor Server)
- **Application Layer**
  - Minimal API (REST/GRPC)
  - Auth Service
  - Order Service
  - Payment Service
  - Notification Service
- **Infrastructure Layer**
  - Database (EF Core, multi-provider)
  - Storage (File/Blob)
  - Cache (Redis/Memory)
- **Simulation Layer**
  - Console App untuk Rider & Driver simulasi paralel

---

## 📱 Modules

### Rider App
- Registrasi/Login
- Pemesanan perjalanan
- GPS tracking driver
- Estimasi tarif
- Multi-stop trip
- Pembayaran (cash/e-wallet/kartu)
- Rating & review

### Driver App
- Login & verifikasi dokumen
- Terima order
- Navigasi GPS
- Manajemen pendapatan
- Close order

### Admin Web
- Dashboard analytic realtime
- Manajemen tarif & promo
- Monitoring driver & order
- Laporan keuangan
- User management

### Simulator
- Rider: buat order, bergerak dari titik A ke B
- Driver: ambil order, close order
- Parallel order creation: simulasi banyak order realtime
- Output tabular interactive (Spectre.Console)

---

## ⚙️ Tech Stack
- **API**: .NET 10 Minimal API (REST/GRPC)
- **UI Framework**: Blazor Server, MAUI Blazor
- **Database**: EF Core dengan provider SQLite, SQL Server, MySQL, PostgreSQL
- **Storage**: File System, MinIO, S3, Azure Blob
- **Cache**: IMemoryCache, Redis
- **Auth**: Identity + JWT
- **Dashboard**: ChartJs.Blazor.Fork, Bootstrap, Tailwind
- **Simulator**: Spectre.Console untuk tabular interactive

---

## 📊 Dashboard & Analytic
- Realtime chart: jumlah order, driver aktif, pendapatan
- Filter advanced: waktu, lokasi, kategori kendaraan
- Tabular data: order detail, driver ranking, user scoring
- Export ke CSV/Excel

---

## 🚀 Run Instructions
1. Clone repository
2. Setup database connection string di `appsettings.json`
3. Jalankan API dengan `dotnet run`
4. Jalankan Rider/Driver App (MAUI Blazor)
5. Jalankan Admin Web (Blazor Server)
6. Jalankan Simulator (Console App)

---

## 📘 Documentation
- `README.md` → Overview & setup
- `API.md` → Dokumentasi endpoint REST/GRPC
- `AUTH.md` → Dokumentasi autentikasi & otorisasi
- `SIMULATOR.md` → Dokumentasi simulasi Rider/Driver
- `DASHBOARD.md` → Dokumentasi dashboard analytic
