# 🏋️ Aplikasi Manajemen FitnessCenter

Aplikasi manajemen fitness center modular dan scalable dibangun dengan **Blazor Server .NET 10**, mengusung desain neo-brutalism futuristik modern dengan dukungan tema dark/light.

## 🚀 Fitur

### 🏋️ Fitur Utama
- **Pendaftaran Member** — Registrasi online/offline dengan integrasi KTP/email/nomor HP
- **Paket Membership** — Paket harian, mingguan, bulanan, tahunan dengan auto-renewal
- **Tracking Kehadiran** — Scan QR/Barcode untuk check-in/out
- **Penjadwalan Kelas** — Yoga, Zumba, HIIT, Pilates dengan booking online
- **Manajemen Trainer** — Profil trainer, jadwal, rating, assignment ke member

### 💳 Pembayaran & Keuangan
- **Payment Gateway** — Integrasi e-wallet, kartu kredit, transfer bank (Midtrans/Stripe/Xendit)
- **Billing & Invoicing** — Faktur otomatis, pengingat pembayaran, laporan keuangan
- **Diskon & Promosi** — Kupon, bonus referral, promo musiman

### 📊 Analitik & Dashboard
- **Analitik Member** — Statistik kehadiran, progress latihan, retention rate
- **Dashboard Pendapatan** — Grafik pendapatan bulanan, per paket, per kelas
- **Performa Trainer** — KPI berdasarkan jumlah kelas, rating, feedback

### 📱 Keterlibatan Member
- **Tracking Latihan** — Log latihan dengan integrasi Fitbit/Apple Watch
- **Rencana Nutrisi** — Rekomendasi diet, meal plan
- **Push Notification** — Pengingat kelas, promo, motivasi harian
- **Forum Komunitas** — Diskusi member, sharing tips, challenge mingguan dengan gambar, emoji, likes

### 🔒 Keamanan & Akses
- **Akses Berbasis Peran** — Admin, Trainer, Member, Staff dengan hak akses berbeda
- **Peringatan Darurat** — Tombol panik, notifikasi ke staff

### 🚀 Fitur Lanjutan
- **AI ChatBot "Coach Tommy"** — Didukung OpenAI/Anthropic/Gemini/Ollama
- **Kelas Virtual** — Streaming via Zoom/Teams
- **Gamifikasi** — Poin, badge, leaderboard
- **API Integrasi** — Minimal API dengan dokumentasi Swagger
- **Manajemen Event** — Kompetisi, workshop, seminar dengan timeline blog

## 🛠️ Teknologi

- **Framework:** .NET 10 Blazor Server
- **Database:** SQLite (default), SQL Server, MySQL, PostgreSQL
- **Penyimpanan:** File System (default), Azure Blob, S3, MinIO
- **AI:** OpenAI, Anthropic Claude, Google Gemini, Ollama
- **Library:** Entity Framework Core, ClosedXML, CsvHelper, QRCoder, Markdig, Semantic Kernel

## 📁 Struktur Proyek

```
FitnessCenter/
├── Models/              # Model domain & enums
├── Data/                # EF Core DbContext
├── Services/            # Layanan logika bisnis
├── Api/                 # Endpoint Minimal API
├── Components/
│   ├── Layout/          # MainLayout, MinimalLayout
│   ├── Pages/           # Halaman utama (Home, Login, Error)
│   ├── Shared/          # Komponen bersama
│   └── Features/        # Modul fitur
│       ├── Members/
│       ├── Membership/
│       ├── Attendance/
│       ├── Classes/
│       ├── Trainers/
│       ├── Payments/
│       ├── Forum/
│       ├── Events/
│       ├── Workout/
│       ├── Nutrition/
│       ├── Feedback/
│       ├── Gamification/
│       ├── Discounts/
│       ├── ChatBot/
│       └── Analytics/
├── wwwroot/             # File statis & CSS
└── docs/                # Dokumentasi
```

## 🚀 Memulai

### Prasyarat
- .NET 10 SDK
- SQLite (default, tanpa instalasi)

### Menjalankan
```bash
cd FitnessCenter
dotnet run
```

### Akun Default
| Peran   | Email                        | Password    |
|---------|------------------------------|-------------|
| Admin   | admin@fitnesscenter.com      | Admin123!   |
| Trainer | trainer1@fitnesscenter.com   | Trainer123! |
| Member  | member1@email.com            | Member123!  |

### Konfigurasi
Edit `appsettings.json` untuk mengubah provider database, penyimpanan, payment gateway, provider AI, dll.

## 📡 Dokumentasi API
Akses Swagger UI di: `/api/docs`

## 🎨 Tema
Desain neo-brutalism futuristik dengan toggle tema dark/light. Preferensi tema disimpan di localStorage.

## 📄 Lisensi
MIT License

---

**Dibuat dengan ❤️ oleh Gravicode Studios**
