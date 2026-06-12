# 🚖 FastRide — Platform Ride-Hailing

> **Solusi multi-project .NET ride-hailing kelas production** dengan Rider App, Driver App, Admin Dashboard, dan Simulator Paralel.

[![.NET](https://img.shields.io/badge/.NET-10.0-512BD4?style=flat&logo=dotnet)](https://dotnet.microsoft.com)
[![C#](https://img.shields.io/badge/C%23-12.0-239120?style=flat&logo=csharp)](https://learn.microsoft.com/en-us/dotnet/csharp/)

---

## 🎯 Gambaran Umum

FastRide adalah platform ride-hailing modern dan lengkap yang dibangun dengan ekosistem .NET. Mencakup semua yang dibutuhkan untuk menjalankan layanan ride-hailing: aplikasi mobile rider, aplikasi mobile driver, dashboard admin web, dan simulator yang powerful untuk load testing dan demo.

### Fitur Utama

| Fitur | Deskripsi |
|-------|-----------|
| 🚗 **Multi Kategori Kendaraan** | Economy, Comfort, Premium, Bike, Electric |
| 💰 **Kalkulasi Tarif Dinamis** | Tarif dasar + biaya per kilometer dengan surge multiplier |
| 🛑 **Perjalanan Multi-Stop** | Dukungan untuk waypoint dan banyak pemberhentian |
| 💳 **Banyak Metode Pembayaran** | Tunai, E-Wallet, Kartu Kredit, Transfer Bank |
| ⭐ **Sistem Rating & Review** | Rating dua arah (rider ↔ driver) |
| 🎫 **Promo & Diskon** | Promo persentase atau nominal tetap dengan batas penggunaan |
| 📊 **Analitik Real-time** | Dashboard dengan chart, filter lanjutan, dan ekspor |
| 🔐 **Siap Auth** | Scaffold autentikasi berbasis JWT |
| 🎮 **Simulator Paralel** | Simulasi live berbasis Spectre.Console untuk load testing |

---

## 🏛️ Arsitektur

```
FastRide/
├── FastRide.Shared/        # 📦 Shared Models, DTOs, Enums
├── FastRide.Data/          # 🗄️ EF Core DbContext + Sample Data Seeder
├── FastRide.Api/           # 🚀 Minimal API (.NET 10 REST/GRPC)
├── FastRide.AdminWeb/      # 🖥️ Blazor Server Admin Dashboard
├── FastRide.RiderApp/      # 📱 MAUI Blazor Hybrid (iOS/Android/Windows)
├── FastRide.DriverApp/     # 📱 MAUI Blazor Hybrid (iOS/Android/Windows)
├── FastRide.Simulator/     # 🎮 Console App (Spectre.Console live simulation)
├── FastRide.sln            # 📋 Solution file
└── docs/                   # 📘 Dokumentasi lengkap
```

### Layer Architecture

```
┌──────────────────────────────────────────────────────┐
│                PRESENTATION LAYER                     │
│  Rider App (MAUI)  │  Driver App (MAUI)  │  Admin    │
├──────────────────────────────────────────────────────┤
│                APPLICATION LAYER                      │
│     Minimal API (REST/GRPC)    │   Auth Service      │
│     Order Service              │   Payment Service   │
│     Notification Service       │                     │
├──────────────────────────────────────────────────────┤
│                INFRASTRUCTURE LAYER                    │
│  EF Core (SQLite/SQL Server)   │   File/Blob Storage │
│  Redis/Memory Cache            │   Identity + JWT    │
├──────────────────────────────────────────────────────┤
│                SIMULATION LAYER                       │
│  Console App — Simulasi Paralel Rider & Driver       │
└──────────────────────────────────────────────────────┘
```

---

## 🚀 Memulai Cepat

### Prasyarat

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- [MAUI Workload](https://learn.microsoft.com/en-us/dotnet/maui/get-started/installation) (untuk aplikasi mobile)

### 1. Clone & Restore

```bash
git clone <repository-url>
cd FastRide
dotnet restore
```

### 2. Jalankan API

```bash
dotnet run --project FastRide.Api
# API berjalan di https://localhost:5001
# Sample data otomatis di-seed saat pertama kali!
```

### 3. Jalankan Admin Dashboard

```bash
dotnet run --project FastRide.AdminWeb
# Dashboard berjalan di https://localhost:5002
```

### 4. Jalankan Simulator

```bash
dotnet run --project FastRide.Simulator
# Saksikan simulasi live 10 rider + 5 driver selama 30 detik!
```

### 5. Jalankan Aplikasi Mobile (butuh MAUI workload)

```bash
dotnet workload install maui
dotnet run --project FastRide.RiderApp
dotnet run --project FastRide.DriverApp
```

---

## 📊 Sample Data

API secara otomatis mengisi database dengan data sampel yang kaya saat pertama kali dijalankan:

| Entitas | Jumlah | Detail |
|---------|--------|--------|
| 👤 Rider | **50** | Nama beragam, email, tanggal registrasi |
| 🚗 Driver | **30** | Dengan profil, kendaraan, rating, pendapatan |
| 👑 Admin | **1** | `admin@fastride.com` |
| 📋 Order | **200+** | Status campuran, lokasi & tarif realistis |
| 💰 Pembayaran | **110+** | Untuk order yang selesai |
| ⭐ Review | **140+** | Review realistis bahasa Indonesia |
| 🎫 Promo | **8** | Welcome, weekend, payday, musiman |
| 🔔 Notifikasi | **40+** | Notifikasi selamat datang + update order |

---

## 🛠️ Tech Stack

| Layer | Teknologi |
|-------|-----------|
| **API** | .NET 10 Minimal API |
| **Web Admin** | Blazor Server (.NET 10) |
| **Mobile** | MAUI Blazor Hybrid |
| **Database** | EF Core (default SQLite, siap SQL Server/MySQL/PostgreSQL) |
| **Auth** | Identity + JWT scaffold |
| **Simulator** | Spectre.Console 0.49 |
| **Chart** | Chart.js 4.4 |
| **CSS** | Bootstrap 5.3 + Custom Dark Theme |

---

## 📘 Dokumentasi Lengkap

Dokumentasi lengkap tersedia di folder [`docs/`](docs/):

| Dokumen | Deskripsi |
|----------|-------------|
| [`API.md`](API.md) | Referensi lengkap endpoint API (REST/GRPC) |
| [`AUTH.md`](AUTH.md) | Panduan autentikasi & otorisasi |
| [`DATABASE.md`](DATABASE.md) | Skema database, migrasi, seeding |
| [`SIMULATOR.md`](SIMULATOR.md) | Penggunaan & konfigurasi simulator |
| [`DASHBOARD.md`](DASHBOARD.md) | Panduan fitur admin dashboard |
| [`ARCHITECTURE.md`](ARCHITECTURE.md) | Arsitektur sistem mendalam |
| [`DEPLOYMENT.md`](DEPLOYMENT.md) | Panduan deployment (Docker, Azure, self-host) |

---

## 🔧 Konfigurasi

### Database

Edit `FastRide.Api/appsettings.json`:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Data Source=FastRide.db"
  }
}
```

Untuk SQL Server:
```json
"DefaultConnection": "Server=.;Database=FastRide;Trusted_Connection=true;TrustServerCertificate=true"
```

---

## 👥 Akun Demo

| Role | Email | Password |
|------|-------|----------|
| Admin | `admin@fastride.com` | `Password123` |
| Rider | `budi.santoso@email.com` | `Password123` |
| Driver | `andi.santoso@drive.com` | `Password123` |

---

## 📄 Lisensi

MIT License — lihat [LICENSE](LICENSE) untuk detail.

---

## 👨‍💻 Kredit

Dibuat dengan ❤️ oleh **Jacky the Code Bender** di [Gravicode Studios](https://studios.gravicode.com)

> 💡 *Kalau berkenan, traktir pulsa ya!* → https://studios.gravicode.com/products/budax
