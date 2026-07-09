# ⛽ FuelStation - Sistem Manajemen Pom Bensin Mini

FuelStation adalah sistem manajemen SPBU Mini yang komprehensif dan modern, dibangun dengan Blazor Server .NET 10. Mengusung desain neo-brutalism dengan warna-warna lembut, dukungan tema gelap/terang, dan dioptimalkan untuk layar sentuh.

## 🔑 Fitur Utama

- **POS Touch-Screen** untuk transaksi cepat
- **Multi Fuel Station** support
- **Pembayaran Digital**: QRIS, E-Wallet, Kartu Debit/Kredit, Transfer Bank
- **Cetak Struk** ESC/POS thermal printer
- **Monitoring Stok Real-Time** dengan visualisasi interaktif
- **Laporan Keuangan** harian/mingguan/bulanan dengan grafik
- **Marketplace Non-BBM** dengan shopping cart
- **Membership & Loyalitas** dengan poin reward
- **Chat AI "Bang Jenggo"** dengan Semantic Kernel
- **IoT Sensor Monitoring** dengan simulator
- **Background Simulator** untuk stress testing
- **REST API** dengan Swagger

## 🚀 Memulai

```bash
dotnet build
dotnet run
```

Buka `https://localhost:5001` di browser.

### Akun Demo

| Role | Email | Password |
|------|-------|----------|
| Admin | admin@fuelstation.com | Admin123! |
| Supervisor | supervisor@fuelstation.com | Super123! |
| Operator | operator1@fuelstation.com | Oper123! |

## ⚙️ Konfigurasi

Edit `appsettings.json` untuk mengubah:
- Database provider (SQLite/SQLServer/MySQL/PostgreSQL)
- Storage provider (FileSystem/AzureBlob/S3/MinIO)
- ChatBot AI provider & API key
- Simulator settings

---

Dibuat oleh **Gravicode Studios** - dipimpin oleh Kang Fadhil 🚀
