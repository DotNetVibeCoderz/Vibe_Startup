# 🧺 WashUp - Sistem Manajemen Laundry

> Aplikasi manajemen laundry modern berbasis Blazor Server .NET dengan AI Chatbot, IoT Monitoring, GPS Tracking, dan dukungan Multi-Cabang.

---

## ✨ Fitur Unggulan

### 🏢 Pemilik & Admin
- 📊 **Dashboard Laundry** - Ringkasan order, pemasukan, pengeluaran, profit
- 📦 **Manajemen Order** - Tracking status lengkap (Diterima → Dicuci → Disetrika → Selesai → Dikirim)
- 👥 **Manajemen Pelanggan** - Data pelanggan, riwayat, preferensi
- 💰 **Keuangan** - Invoice otomatis, laporan keuangan, grafik
- 📦 **Inventaris** - Stok bahan, peringatan habis
- 👷 **Staff** - Data pegawai, jadwal, gaji, performa
- 🏢 **Multi Cabang** - Kelola banyak outlet
- 🏛️ **Pajak** - Kalkulasi PPh, laporan pajak

### 👤 Pelanggan
- 📝 **Registrasi Online** - Daftar akun dengan mudah
- 🔐 **Autentikasi** - Login, logout, reset password
- 📦 **Order Online** - Cuci kering, setrika, express, kiloan
- 💳 **Pembayaran** - OVO, GoPay, Dana, QRIS, transfer
- 📍 **Tracking** - Status order real-time
- 🔔 **Notifikasi** - Reminder, promo, pengumuman
- 📝 **Komplain** - Form pengaduan + tracking
- ⭐ **Loyalty** - Poin reward, diskon member

### 🚚 Kurir & Operasional
- 🚚 **Pickup & Delivery** - Jadwal penjemputan/pengantaran
- 🗺️ **GPS Tracking** - Lokasi kurir real-time (simulator tersedia)
- 🔧 **IoT** - Monitoring mesin cuci, listrik, air (simulator)
- 🔑 **Role Access** - Pemilik, Admin, Kurir, Pelanggan

### 🤖 ChatBot "Mbok Inem"
- 🧠 Multi AI model (OpenAI, Anthropic, Gemini, Ollama)
- 💬 Multi-session chat
- 🖼️ Attach gambar & dokumen
- 🔍 Kernel functions: search, scrape, kalkulasi, query DB
- 📝 Markdown rendering lengkap

---

## 🚀 Memulai

### Persyaratan
- .NET 10 SDK
- SQLite (default), PostgreSQL, atau SQL Server

### Instalasi

```bash
git clone https://github.com/your-org/WashUp.git
cd WashUp
dotnet run
```

Buka browser: **http://localhost:5000**

### Akun Demo

| Role | Email | Password |
|------|-------|----------|
| Pemilik | owner@washup.id | WashUp@2024 |
| Admin | admin@washup.id | WashUp@2024 |
| Kurir | kurir1@washup.id | WashUp@2024 |
| Pelanggan | pelanggan1@email.com | Pelanggan@123 |

---

## 🏗️ Teknologi

- **Framework**: .NET 10 + Blazor Server
- **Database**: SQLite / PostgreSQL / SQL Server / MySQL
- **Auth**: ASP.NET Identity + Role-based
- **AI**: Semantic Kernel + Multi-model
- **Storage**: FileSystem / Azure / S3 / MinIO
- **API**: Minimal API + Swagger + JWT bearer (`POST /api/auth/token`)
- **Export**: Laporan CSV (Excel) via `/api/reports/export/*` + cetak PDF

---

## 📄 Lisensi

MIT License - © 2024 GraviCode Studios

Dibuat dengan ❤️ oleh tim GraviCode Studios | Dipimpin Kang Fadhil | Asisten AI: Jacky the Code Bender

---

**🧺 WashUp - Laundry bersih, hati senang!**
