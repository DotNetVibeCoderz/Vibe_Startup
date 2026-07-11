# 🚗 SmartDrive Academy

**Sistem Manajemen Belajar Menyetir Mobil**

Aplikasi manajemen lengkap untuk sekolah mengemudi yang mencakup kebutuhan siswa, instruktur, dan admin. Dibangun dengan Blazor Server .NET 10.

## ✨ Fitur Utama

### 🚗 Untuk Siswa
- 📝 Registrasi dengan upload KTP/SIM
- 📊 Dashboard progres belajar
- 📅 Booking jadwal latihan online
- 💰 Pembayaran online (e-wallet, transfer, kartu kredit)
- 📚 Modul teori lengkap
- 📝 Simulasi ujian teori
- 📈 Tracking progres & statistik
- ⭐ Feedback dari instruktur
- 🔔 Notifikasi otomatis

### 👨‍🏫 Untuk Instruktur
- 📅 Manajemen jadwal mengajar
- 👤 Profil dengan rating
- 📝 Catatan evaluasi siswa
- 📍 GPS tracking & simulator
- 💬 Chat dengan siswa
- 📊 Dashboard kinerja

### 🛠️ Untuk Admin
- 🚗 Manajemen kendaraan
- 👥 Manajemen instruktur & siswa
- 💰 Laporan keuangan
- 📊 Analitik bisnis
- ⚙️ Konfigurasi sistem

### 🤖 Om Bambang AI
- Chat bot pintar 24/7
- Bisa attach gambar & dokumen
- Dukungan banyak model AI
- Query data langsung ke database

---

## 🚀 Cara Menjalankan

```bash
# Persyaratan: .NET 10 SDK

# Clone & masuk folder
cd SmartDrive

# Jalankan
dotnet restore
dotnet run

# Buka http://localhost:5000
```

### Akun Default

| Role | Email | Password |
|------|-------|----------|
| Admin | admin@smartdrive.com | Admin123! |
| Instruktur | budi@smartdrive.com | Instructor123! |
| Siswa | andi@email.com | Student123! |

---

## ⚙️ Konfigurasi

Edit `appsettings.json`:

```json
{
  "Database": {
    "Provider": "SQLite"  // SQLite, SQLServer, MySQL, PostgreSQL
  },
  "ChatBot": {
    "ModelProvider": "OpenAI",   // OpenAI, Anthropic, Gemini, Ollama
    "ModelId": "gpt-4",
    "ApiKey": "your-api-key"
  },
  "Storage": {
    "Provider": "FileSystem"  // FileSystem, AzureBlob, S3, MinIO
  }
}
```

---

## 📚 Dokumentasi Lengkap
Lihat folder `docs/` untuk dokumentasi teknis lengkap.

---

## 👨‍💻 Dibuat Oleh

**Jacky the Code Bender** - GraviCode Studios  
Dipimpin oleh **Kang Fadhil**

---

© 2024 GraviCode Studios. All rights reserved.
