# FitnessCenter Documentation

Tangkapan layar seluruh halaman ada di **[SCREENSHOTS.md](SCREENSHOTS.md)**.

## Architecture Overview

### Design Pattern
Aplikasi menggunakan **Clean Architecture** sederhana dengan pemisahan tanggung jawab yang jelas:

- **Models** — Domain models, enums, dan entities
- **Data** — Entity Framework Core DbContext dan konfigurasi database
- **Services** — Business logic layer
- **Api** — Minimal API endpoints
- **Components** — Blazor UI components (Pages, Features, Layout, Shared)

### Database
Mendukung 4 provider database:
- SQLite (default untuk development)
- SQL Server (production)
- MySQL
- PostgreSQL

Konfigurasi di `appsettings.json`:
```json
{
  "Database": {
    "Provider": "SQLite",
    "ConnectionStrings": {
      "SQLite": "Data Source=fitness_center.db"
    }
  }
}
```

### Storage
Mendukung 4 provider penyimpanan:
- File System (default)
- Azure Blob Storage
- AWS S3
- MinIO

### Payment Gateway
`Services/Payments/` mengikuti pola yang sama dengan storage: satu antarmuka
`IPaymentProvider`, empat implementasi, dan `PaymentGatewayService` sebagai fasad.

| Provider | Kunci yang dibutuhkan | Callback |
|----------|----------------------|----------|
| Manual (bawaan) | `BankName`, `AccountNumber`, `AccountHolder` | — diverifikasi admin |
| Midtrans Snap | `ServerKey` | `/api/v1/payments/webhook/midtrans` |
| Xendit Invoice | `ApiKey`, `CallbackToken` | `/api/v1/payments/webhook/xendit` |
| Stripe Checkout | `SecretKey`, `WebhookSecret` | `/api/v1/payments/webhook/stripe` |

Aturan yang dipegang:
- Provider tanpa kunci API otomatis dilewati; pembayaran jatuh ke Manual.
- Tiap callback diverifikasi tanda tangannya (SHA512 untuk Midtrans, token header
  untuk Xendit, HMAC-SHA256 untuk Stripe). Tanpa secret, callback ditolak.
- Status yang sudah `Completed` tidak pernah diturunkan, kecuali menjadi `Refunded`.
- Semua penulisan ke database dilakukan `PaymentGatewayService`, bukan provider.

### Authentication & Authorization
Menggunakan ASP.NET Core Identity dengan 4 role:
- **Admin** — Akses penuh ke semua fitur
- **Trainer** — Kelola kelas, lihat member
- **Member** — Booking kelas, forum, workout tracking
- **Staff** — Check-in/out, bantu operasional

### ChatBot "Coach Tommy"
Menggunakan multi-provider AI:
- OpenAI (GPT-4o)
- Anthropic (Claude 3.5 Sonnet)
- Google Gemini (2.0 Flash)
- Ollama (Llama 3.2 - local)

System prompt, temperature, dan model dikonfigurasi di `appsettings.json`.

### API Endpoints
Semua endpoint tersedia di `/api/v1/`:
- `GET /api/v1/members`
- `GET /api/v1/memberships`
- `GET /api/v1/trainers`
- `GET /api/v1/classes`
- `GET /api/v1/classes/schedule`
- `POST /api/v1/attendance/checkin/{userId}`
- `GET /api/v1/payments`
- `GET /api/v1/payments/providers`
- `POST /api/v1/payments/{id}/charge`
- `POST /api/v1/payments/{id}/sync`
- `POST /api/v1/payments/webhook/{provider}` — dipanggil server provider, tanpa autentikasi
- `GET /api/v1/revenue`
- `GET /api/v1/feedback`
- `POST /api/v1/feedback`
- `GET /api/v1/events`
- `GET /api/v1/forum/posts`
- `GET /api/v1/leaderboard`
- `GET /api/v1/achievements/{userId}`
- `GET /api/v1/notifications/{userId}`
- `GET /api/v1/chat/sessions/{userId}`
- `POST /api/v1/chat/send`
- `GET /api/v1/export/members/csv`
- `GET /api/v1/export/members/excel`

Swagger: `/api/docs`

## Development

### Setup
1. Install .NET 10 SDK
2. Clone repository
3. `dotnet restore`
4. `dotnet run`

### Data contoh (seeding)

`DataSeedService.SeedAsync()` hanya berjalan bila tabel Users masih kosong. Isinya
dirancang supaya setiap halaman punya sesuatu untuk ditampilkan:

| Entitas | Jumlah | Catatan |
|---------|--------|---------|
| Member | 40 | `member1@email.com` … `member40@email.com`, password `Member123!` |
| Trainer | 6 | masing-masing punya spesialisasi berbeda |
| Kelas | 12 | mencakup 11 dari 12 `ClassType`, satu di antaranya kelas virtual |
| Jadwal kelas | 30 | jam dan hari berbeda per kelas, bukan satu pola untuk semua |
| Booking kelas | ~180 | indeks unik (ScheduleId, UserId) dijaga saat seeding |
| MemberMembership | 40 | status Active, Expired, dan Suspended tercampur |
| Pembayaran | ~120 | seluruh `PaymentStatus` terwakili, termasuk Refunded |
| Absensi | 45 hari | akhir pekan lebih ramai; sebagian punya check-out |
| Forum | 10 post | plus komentar dan reaksi |
| Event | 6 | Published, Completed, dan Draft |
| Lainnya | — | workout log, meal plan, feedback, lencana, notifikasi, satu sesi chat |

Untuk memuat ulang data contoh, hapus berkas database lalu jalankan aplikasi lagi:

```bash
rm fitness_center.db fitness_center.db-shm fitness_center.db-wal
dotnet run
```

### Gambar sampul

`wwwroot/images/` berisi 16 berkas SVG buatan sendiri dengan palet yang sama seperti
antarmuka: satu sampul per jenis kelas, ditambah sampul event dan forum. Kelas yang
`ImageUrl`-nya kosong otomatis memakai sampul jenisnya
(`/images/classes/{type}.svg`), sehingga kartu kelas tidak pernah menampilkan gambar rusak.

### Skema database
Proyek ini **belum memakai migrations**. `Program.cs` memanggil `EnsureCreatedAsync()`,
sehingga perubahan pada `Models/DomainModels.cs` tidak sampai ke database yang sudah ada.

Pilihan saat mengubah model:
1. Hapus `fitness_center.db*` — database dibuat ulang dan di-seed lagi.
2. Tambahkan kolom lewat `EnsureNewColumnsAsync` di `Program.cs` (khusus SQLite,
   idempoten, dipakai untuk kolom payment gateway agar data lama tidak hilang).
3. Beralih ke migrations sungguhan:
   ```bash
   dotnet ef migrations add InitialCreate
   dotnet ef database update
   ```

### Theme Customization
Design system ada di `wwwroot/app.css`, disusun bernomor per bagian:
- `:root` — token tema terang, termasuk palet plate (`--plate-red` … `--plate-violet`)
- `[data-theme="dark"]` — token tema gelap
- Warna plate dipakai sebagai bahasa status; ganti nilainya untuk mengubah identitas warna.
- Tipografi diatur `--font-display` / `--font-body` / `--font-mono`.

Micro-interaction ada di `wwwroot/app.js` (`data-countup`, `data-meter`, `data-stagger`,
riak tombol, `window.fitnessUI`). Semuanya mematuhi `prefers-reduced-motion`.
