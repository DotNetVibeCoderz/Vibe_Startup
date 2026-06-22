# 📝 Changelog

## v1.0.0 — Rilis Pertama

### 🏠 Core Features
- ✅ Dashboard Kost — ringkasan okupansi, pemasukan, status kamar
- ✅ Manajemen Kamar — CRUD, filter, pagination, export CSV/Excel
- ✅ Pengelolaan Penghuni — data, kontrak, riwayat, detail modal
- ✅ Manajemen Kontrak — CRUD kontrak sewa
- ✅ Tagihan — auto-generate bulanan + tracking status
- ✅ Pembayaran — tracking + verifikasi
- ✅ Komplain — form pengaduan + tracking status + respon
- ✅ Inventaris — CRUD dengan kategori & status
- ✅ Staff — CRUD dengan posisi, gaji, status
- ✅ Laporan Keuangan — ringkasan per kost

### 🔐 Authentication
- ✅ ASP.NET Identity dengan 5 roles
- ✅ Login, Register, Logout, Profil
- ✅ Role-based menu di MainLayout
- ✅ Badge role di topbar

### 🔌 REST API
- ✅ 20+ endpoint (Kamar, Penghuni, Tagihan, Pembayaran, Komplain, IoT, Dashboard, Marketplace, Storage, Chat, Export)
- ✅ Swagger UI
- ✅ Export CSV & Excel

### 🤖 Chat Bot — Mpok Inem
- ✅ Chat page dengan multi-session
- ✅ Semantic Kernel + 4 provider (OpenAI, Anthropic, Gemini, Ollama)
- ✅ 25 kernel functions (tanggal, kalkulator, DB query, search, dll)
- ✅ Auto function calling via `FunctionChoiceBehavior.Auto()`
- ✅ Session persistence ke database
- ✅ File upload via storage provider
- ✅ Markdown rendering
- ✅ Fallback offline mode

### 🗄️ Database
- ✅ 16 tabel (14 domain + 2 chat)
- ✅ Multi-provider: SQLite, SQL Server, PostgreSQL, MySQL
- ✅ Seed data: 13 users, 2 kost, 12 kamar, 9 penghuni, dll

### 🗄️ Storage
- ✅ 4 provider: FileSystem, Azure Blob, AWS S3, MinIO
- ✅ Interface `IStorageProvider`
- ✅ API endpoint upload/list/delete

### 🌟 UI/UX
- ✅ Neo-brutalism soft theme
- ✅ Dark/Light mode
- ✅ Responsive design
- ✅ Rupiah formatting (`.ToRupiah()`)
- ✅ Role-based menu

### 🏪 Marketplace
- ✅ Listing kost publik
- ✅ Detail modal dengan kamar & review
- ✅ Rating & review system (⭐ + emoji + komentar)

### 📡 IoT
- ✅ Dashboard sensor (listrik, air, suhu, kelembaban)
- ✅ Simulasi data sensor
- ✅ REST API IoT endpoints

### 📚 Dokumentasi
- ✅ 10 file dokumentasi lengkap di folder `docs/`
- ✅ README.md
- ✅ PLAN.md

---

## Known Issues / Future Plans

### 🔜 Planned
- [ ] Email notifications (tagihan, kontrak habis)
- [ ] Payment gateway integration (Midtrans, Xendit)
- [ ] Digital signature for contracts
- [ ] Mobile app (MAUI Hybrid)
- [ ] Analytics dashboard with charts
- [ ] Tax calculation (PPh)
- [ ] Real-time IoT via SignalR
- [ ] Push notifications
- [ ] Multi-language support (EN/ID)

### ⚠️ Limitations
- Blazor Server — setiap user butuh koneksi SignalR persisten
- Ollama model — function calling terbatas pada model yang mendukung
- SQLite — tidak cocok untuk high-concurrency production
