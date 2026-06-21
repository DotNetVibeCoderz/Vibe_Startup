# 📚 Dokumentasi RentalBoil

Selamat datang di dokumentasi lengkap RentalBoil - Platform Rental Kendaraan Modern.

## 📑 Daftar Isi

1. [Arsitektur Sistem](architecture.md)
2. [Panduan Instalasi](installation.md)
3. [Panduan Pengguna](user-guide.md)
4. [API Reference](api-reference.md)
5. [Panduan Pengembang](developer-guide.md)
6. [Keamanan](security.md)
7. [Database](database.md)
8. [Deployment](deployment.md)
9. [Chat Bot AI](chatbot.md)
10. [FAQ](faq.md)

---

## 🎯 Gambaran Umum

RentalBoil adalah platform rental kendaraan full-featured yang menghubungkan pemilik kendaraan (Partner) dengan pelanggan (Customer). Dibangun dengan teknologi modern .NET 10, Blazor Server, dan AI-powered chat bot.

### 🔑 Fitur Utama

| Kategori | Fitur |
|----------|-------|
| 🚗 **Customer** | Pencarian kendaraan, booking online, pembayaran digital, GPS tracking, review & rating |
| 🛠️ **Partner** | Manajemen armada, dashboard pendapatan, manajemen pesanan, laporan keuangan |
| 🖥️ **Admin** | Verifikasi kendaraan, manajemen pengguna, monitoring transaksi, analitik |
| 🤖 **AI** | Chat bot 24/7, rekomendasi kendaraan, 17+ kernel functions |
| 🛰️ **IoT** | GPS real-time tracking, kunci digital, remote engine control |
| 📊 **Laporan** | Filter harian/bulanan/tahunan, export CSV/Excel, visualisasi data |

### 🏗️ Tech Stack

| Layer | Teknologi |
|-------|-----------|
| Framework | .NET 10, Blazor Server |
| Database | SQLite (default), SQL Server, MySQL, PostgreSQL |
| Auth | ASP.NET Core Identity + API Key |
| AI | Semantic Kernel (OpenAI, Anthropic, Gemini, Ollama) |
| Real-time | SignalR |
| UI | Bootstrap 5.3 + Claymorphism CSS |
| Maps | Leaflet.js + OpenStreetMap |
| Export | CsvHelper + ClosedXML |
| Storage | FileSystem / Azure Blob / AWS S3 / MinIO |

---

## 🚀 Quick Links

- [Demo Accounts](#demo-accounts)
- [API Access](#api-access)
- [Konfigurasi AI](chatbot.md)

### Demo Accounts

| Role | Email | Password |
|------|-------|----------|
| 🔑 Admin | admin@rentalboil.com | Admin123! |
| 🚗 Partner | partner1@rentalboil.com | Partner123! |
| 👤 Customer | customer1@rentalboil.com | Customer123! |

### API Access

```bash
# Default API Key
X-Api-Key: rntl-2025-secure-api-key-change-in-production

# Base URL
https://localhost:5001/api

# Swagger Docs
https://localhost:5001/swagger
```

---

*Dokumentasi terakhir diperbarui: 2025*
*Tim Pengembang: Gravicode Studios - Kang Fadhil & Jacky The Code Bender*
