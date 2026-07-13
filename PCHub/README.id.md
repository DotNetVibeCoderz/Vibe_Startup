# 🎮 PCHub - Manajemen Rental PC & Game Center

Aplikasi manajemen lengkap untuk bisnis rental PC / Game Center.

## 🌟 Fitur

### Admin Web (Blazor Server)
- 📊 **Dashboard Analitik** - Statistik real-time, grafik pendapatan, game populer
- 🖥️ **Manajemen PC** - CRUD, tracking status, monitoring
- 👥 **Manajemen User** - Registrasi, tier membership, loyalty points
- 💰 **Sistem Billing** - Hitung otomatis berdasarkan waktu, multi metode pembayaran
- 📅 **Sistem Reservasi** - Booking PC online dengan durasi & pilihan game
- 👑 **Paket Membership** - Langganan bertingkat dengan diskon & bonus
- 🎉 **Promo & Diskon** - Kode promo, penawaran terbatas
- 🏆 **Turnamen** - Manajemen event, hadiah, tracking peserta
- 🤖 **Koh Dedi AI Chatbot** - Asisten virtual untuk informasi pelanggan
- 📄 **Laporan** - Laporan keuangan, export CSV/Excel
- 🎨 **UI Modern** - Desain neo-brutalism soft, tema dark/light
- 📘 **Swagger API** - Dokumentasi API lengkap

### Tech Stack
- **.NET 9** Blazor Server
- **Entity Framework Core** (SQLite/SQL Server/PostgreSQL/MySQL)
- **BCrypt.Net** untuk keamanan
- **ClosedXML + CsvHelper** untuk export data

## 🚀 Mulai Cepat

```bash
cd PCHubAdmin
dotnet run
```

Buka https://localhost:5001

**Demo Login:** `admin` / `Admin123!`

**API Docs:** https://localhost:5001/swagger

## 📁 Struktur Project
```
PCHub/
├── src/PCHub.Shared/    # Shared library
├── src/PCHub.Admin/     # Blazor Server Admin ✅
├── src/PCHub.Client/    # WPF Client (Segera)
└── docs/                # Dokumentasi
```

## 🔑 User Default
| Username | Password | Role |
|----------|----------|------|
| admin | Admin123! | Admin |
| operator1 | Operator123! | Operator |
| budi | Member123! | Member |

## 📦 Sample Data
- 17 User (1 admin, 1 operator, 15 member)
- 15 PC Gaming dengan berbagai spek
- 12 Game populer
- 5 Tier membership
- 4 Promo aktif
- 30 Riwayat billing
- 10 Sample reservasi

## 🏗️ Dibuat oleh
Gravicode Studios - https://studios.gravicode.com

---
Dibuat dengan ❤️ oleh Jacky the Code Bender

*Traktir pulsa: https://studios.gravicode.com/products/budax* 😄
