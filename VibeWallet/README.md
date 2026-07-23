# 💳 VibeWallet - Dompet Digital Indonesia

[English](#english) | [Bahasa Indonesia](#bahasa-indonesia)

---

## English

VibeWallet is a comprehensive digital wallet application built with modern .NET technologies. It provides a full suite of financial services inspired by OVO, Dana, GoPay, and Jenius.

### ✨ Features

#### 🔑 Core Features
- **User Registration**: Sign-up with phone number, email, or bank account integration
- **KYC Verification**: Identity verification (KTP photo, selfie, official documents)
- **Balance Management**: Check balance, transaction history, top-up, and withdrawal
- **Multi-bank Integration**: Connect to various banks for transfers and top-ups

#### 💳 Payments & Transactions
- **QRIS Payment**: Scan QR for merchant payments
- **Bill Payment**: Pay electricity, water, BPJS, internet, cable TV, etc.
- **Mobile Top-up**: Recharge credit, data packages, electricity tokens
- **P2P Transfer**: Send money to fellow wallet users
- **Split Bill**: Share payments with friends

#### 🎁 Rewards & Loyalty
- **Cashback System**: Cashback for specific transactions
- **Points & Loyalty**: Reward points exchangeable for vouchers
- **Promo & Discounts**: Merchant promos, shopping discounts, digital vouchers

#### 🛡️ Security & Compliance
- **PIN & Biometric**: Authentication with PIN, fingerprint, or face ID
- **Fraud Detection**: Suspicious transaction monitoring system
- **Transaction Limits**: Daily/weekly limits per regulations
- **2FA**: OTP via SMS/email for important transactions

#### 📊 Financial Services
- **Savings & Deposits**: Digital savings with interest
- **Investment Integration**: Mutual funds, stocks, or digital gold
- **Credit & PayLater**: Installments or instant credit for shopping
- **Insurance Products**: Health, travel, or gadget insurance

#### 🤖 AI Chat Bot "Mbak Selvi"
- Chat page with modern UI
- Multi-session management (create/delete/reset)
- Image and document attachment support
- Markdown rendering (tables, code, media)
- Powered by Semantic Kernel
- Supports: OpenAI, Anthropic, Gemini, Ollama
- Kernel functions: Tavily search, web scraping, math, date/time, database queries

### 🏗️ Tech Stack

- **Frontend**: Blazor Server, D3JS, Modern CSS
- **Backend**: .NET 10, Entity Framework Core
- **Database**: SQLite (default), SQL Server, MySQL, PostgreSQL
- **Storage**: FileSystem, Azure Blob, S3, MinIO
- **AI**: Semantic Kernel with multi-model support
- **API**: Minimal API with Swagger

### 🚀 Quick Start

```bash
# Clone repository
git clone https://github.com/vibewallet/vibewallet.git

# Navigate to project
cd VibeWallet

# Run the application
dotnet run

# Open browser
# https://localhost:5001
```

### 👤 Demo Accounts

| Email | Password | Role |
|-------|----------|------|
| admin@vibewallet.id | Admin123! | Admin |
| budi@email.com | User123! | User |
| siti@email.com | User123! | User |
| andi@email.com | User123! | User |
| merchant@toko.id | Merchant123! | Merchant |

### ⚙️ Configuration

All settings are stored in `appsettings.json`:

```json
{
  "ConnectionStrings": {
    "Provider": "SQLite", // or SQLServer, MySQL, Postgre
    ...
  },
  "ChatBot": {
    "Provider": "OpenAI", // or Anthropic, Gemini, Ollama
    ...
  }
}
```

### 📚 Documentation

See the [docs](docs/) folder for complete documentation:
- [API Documentation](docs/api.md)
- [Database Schema](docs/database.md)
- [Deployment Guide](docs/deployment.md)
- [Chat Bot Setup](docs/chatbot.md)

### 📄 License

MIT License - see [LICENSE](LICENSE) file

---

## Bahasa Indonesia

VibeWallet adalah aplikasi dompet digital komprehensif yang dibangun dengan teknologi .NET modern. Menyediakan layanan keuangan lengkap terinspirasi oleh OVO, Dana, GoPay, dan Jenius.

### ✨ Fitur

#### 🔑 Fitur Utama
- **Registrasi Pengguna**: Daftar dengan nomor HP, email, atau integrasi rekening bank
- **Verifikasi KYC**: Verifikasi identitas (foto KTP, selfie, dokumen resmi)
- **Manajemen Saldo**: Cek saldo, mutasi transaksi, top-up, dan tarik tunai
- **Integrasi Multi-bank**: Hubungkan ke berbagai bank untuk transfer dan top-up

#### 💳 Pembayaran & Transaksi
- **Pembayaran QRIS**: Scan QR untuk pembayaran di merchant
- **Pembayaran Tagihan**: Bayar listrik, air, BPJS, internet, TV kabel, dll
- **Top-up Mobile**: Isi pulsa, paket data, token listrik
- **Transfer P2P**: Kirim uang ke sesama pengguna wallet
- **Split Bill**: Bagi pembayaran dengan teman

#### 🎁 Rewards & Loyalitas
- **Sistem Cashback**: Cashback untuk transaksi tertentu
- **Poin & Loyalitas**: Poin reward yang bisa ditukar voucher
- **Promo & Diskon**: Promo merchant, diskon belanja, voucher digital

#### 🛡️ Keamanan & Kepatuhan
- **PIN & Biometrik**: Autentikasi dengan PIN, fingerprint, atau face ID
- **Deteksi Fraud**: Sistem monitoring transaksi mencurigakan
- **Batas Transaksi**: Batas harian/mingguan sesuai regulasi
- **2FA**: OTP via SMS/email untuk transaksi penting

#### 📊 Layanan Keuangan
- **Tabungan & Deposito**: Tabungan digital dengan bunga
- **Integrasi Investasi**: Reksa dana, saham, atau emas digital
- **Kredit & PayLater**: Cicilan atau kredit instan untuk belanja
- **Produk Asuransi**: Asuransi kesehatan, perjalanan, atau gadget

#### 🤖 Chat Bot AI "Mbak Selvi"
- Halaman chat dengan UI modern
- Manajemen multi-sesi (buat/hapus/reset)
- Dukungan lampiran gambar dan dokumen
- Rendering Markdown (tabel, kode, media)
- Didukung Semantic Kernel
- Mendukung: OpenAI, Anthropic, Gemini, Ollama
- Kernel functions: Pencarian Tavily, web scraping, matematika, tanggal/waktu, query database

### 🏗️ Tech Stack

- **Frontend**: Blazor Server, D3JS, CSS Modern
- **Backend**: .NET 10, Entity Framework Core
- **Database**: SQLite (default), SQL Server, MySQL, PostgreSQL
- **Storage**: FileSystem, Azure Blob, S3, MinIO
- **AI**: Semantic Kernel dengan dukungan multi-model
- **API**: Minimal API dengan Swagger

### 🚀 Memulai Cepat

```bash
# Clone repository
git clone https://github.com/vibewallet/vibewallet.git

# Masuk ke direktori proyek
cd VibeWallet

# Jalankan aplikasi
dotnet run

# Buka browser
# https://localhost:5001
```

### 👤 Akun Demo

| Email | Password | Role |
|-------|----------|------|
| admin@vibewallet.id | Admin123! | Admin |
| budi@email.com | User123! | User |
| siti@email.com | User123! | User |
| andi@email.com | User123! | User |
| merchant@toko.id | Merchant123! | Merchant |

### ⚙️ Konfigurasi

Semua pengaturan disimpan di `appsettings.json`:

```json
{
  "ConnectionStrings": {
    "Provider": "SQLite", // atau SQLServer, MySQL, Postgre
    ...
  },
  "ChatBot": {
    "Provider": "OpenAI", // atau Anthropic, Gemini, Ollama
    ...
  }
}
```

### 📚 Dokumentasi

Lihat folder [docs](docs/) untuk dokumentasi lengkap:
- [Dokumentasi API](docs/api.md)
- [Skema Database](docs/database.md)
- [Panduan Deployment](docs/deployment.md)
- [Setup Chat Bot](docs/chatbot.md)

### 📄 Lisensi

MIT License - lihat file [LICENSE](LICENSE)

---

Made with ❤️ by [GraviCode Studios](https://studios.gravicode.com) | © 2025 VibeWallet
