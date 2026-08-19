# HolySafar Documentation

## Architecture Overview

HolySafar is built on **Blazor Server** (.NET 10) with **Interactive Server rendering mode**. All UI interactions are handled via SignalR connection between the browser and server.

### Key Design Decisions

1. **SQLite as Default Database** - Zero-configuration, file-based database perfect for demos and small deployments. Easily switchable to SQL Server, MySQL, or PostgreSQL.

2. **Semantic Kernel for AI** - Microsoft's Semantic Kernel library provides a unified interface for multiple LLM providers. The chatbot "Syeikh Jenggot" can use OpenAI, Anthropic, Gemini, or Ollama.

3. **FileSystem Storage** - Default storage provider writes to `wwwroot/uploads/`. Swappable to Azure Blob, AWS S3, or MinIO.

4. **Session-based Auth** - Simple session-based authentication using `IHttpContextAccessor` and session storage. No ASP.NET Identity dependency.

5. **CSS Custom Properties** - Theme system using CSS variables for light/dark mode without JavaScript overhead.

---

## Database Schema

### Core Tables
- `ApplicationUser` - User accounts with role-based access
- `Jamaah` - Pilgrim/passenger data
- `DokumenJamaah` - Uploaded documents (KTP, Paspor, etc.)

### Package & Payment
- `Paket` - Hajj/Umrah packages
- `Pembayaran` - Payment records
- `Cicilan` - Installment records

### Operations
- `Keberangkatan` - Departure schedules

### Communication
- `ChatMessage` - User-to-user messages
- `Pengumuman` - System announcements
- `Notifikasi` - System notifications

### Education
- `MateriManasik` - Manasik learning materials
- `Kuis` - Quiz questions

### Emergency
- `SOSTrigger` - SOS alerts
- `KontakDarurat` - Emergency contacts

### Marketplace
- `Produk` - Products
- `CartItem` - Shopping cart
- `Order` / `OrderItem` - Orders

### Chatbot
- `ChatSession` - AI chat sessions
- `ChatbotMessage` - Chat history

---

## Adding a New LLM Provider

1. Add API key to `appsettings.json`:
```json
"Chatbot": {
  "Provider": "OpenAI",
  "Providers": {
    "OpenAI": { "ApiKey": "sk-...", "Model": "gpt-4o" }
  }
}
```

2. Update `ChatbotService.CreateKernel()` to handle the new provider.

---

## GPS Simulator

The `GpsSimulatorService` runs a background timer that randomly moves pilgrims around Masjidil Haram (21.4225, 39.8262). Configure interval in `appsettings.json`:
```json
"AppSettings": {
  "SimulatorIntervalMs": 2000
}
```

---

## Data Seeding

`DataSeeder.SeedAsync()` runs automatically on first startup. It creates:
- 10 users (1 admin, 2 agents, 7 pilgrims)
- 3 packages
- 7 pilgrim records
- Payments, installments, departures
- Educational content, quizzes
- Emergency contacts
- Marketplace products
- Announcements

To reset: delete `Data/holysafar.db` and restart.

---

*For more information, contact Gravicode Studios.*

---

# Pembaruan Arsitektur (modul tambahan)

## Autentikasi (diperbarui)

Autentikasi **tidak lagi** memakai cookie yang ditulis `document.cookie` dari JavaScript.
Sekarang memakai **ASP.NET Core cookie authentication**:

- Cookie `hsauth` ditulis server-side oleh `POST /auth/login` dengan `HttpOnly`, `SameSite=Lax`,
  dan `Secure` pada koneksi HTTPS — tidak dapat dibaca atau dipalsukan dari JavaScript.
- `Login.razor` adalah form POST biasa dengan `<AntiforgeryToken />` (cookie tidak bisa ditulis
  dari dalam circuit SignalR). Logout juga POST ber-antiforgery.
- Password disimpan sebagai **PBKDF2-SHA256**, 210.000 iterasi, salt acak per user
  (format `pbkdf2$iterasi$salt$hash`). Hash SHA256 lama tetap bisa login dan otomatis
  di-upgrade saat login berhasil.
- `OnValidatePrincipal` memeriksa ulang tiap request apakah user masih ada dan aktif,
  sehingga menonaktifkan akun langsung memutus sesi yang sedang berjalan.
- Otorisasi halaman ditegakkan `AuthorizeRouteView` + atribut `[Authorize]` di tiap halaman.
  Sebelumnya menu admin hanya disembunyikan dari sidebar, sementara URL-nya tetap terbuka.

`AuthService` kini pembungkus tipis di atas `AuthenticationStateProvider`. Panggil
`await AuthService.EnsureAsync()` di awal `OnInitializedAsync` sebelum membaca
`CurrentUserId` / `CurrentUserRole`.

## Konfigurasi berlapis

`SettingsService` (singleton, ber-cache) menyelesaikan sebuah kunci dengan urutan:

1. Tabel `Pengaturan` di database (diisi dari **Admin → Pengaturan**)
2. `appsettings.json`
3. Nilai default di kode

Artinya kredensial payment gateway, model chatbot, provider storage, cadence reminder,
dan endpoint SISKOHAT bisa diubah dari UI tanpa restart. Untuk apa pun yang boleh diatur admin,
baca konfigurasi lewat `SettingsService.Get(...)`, bukan `IConfiguration`.

## Modul baru

| Modul | Berkas utama | Halaman |
|-------|--------------|---------|
| Payment gateway | `Services/PaymentGatewayService.cs` | `/pembayaran`, `/admin/transaksi`, checkout marketplace |
| Dokumen jamaah | entitas `DokumenJamaah` | `/dokumen` (unggah), `/admin/dokumen` (verifikasi) |
| Tracking proses | — | `/tracking` (timeline dokumen → bayar → visa → SISKOHAT → berangkat) |
| Manajemen perjalanan | entitas `ItineraryItem` | `/perjalanan`, `/admin/itinerary` |
| Forum jamaah | `ForumTopik`, `ForumBalasan` | `/forum`, `/forum/{id}` |
| Reminder otomatis | `Services/ReminderService.cs` (BackgroundService) | notifikasi in-app |
| Integrasi SISKOHAT | `Services/SiskohatService.cs` | `/admin/dokumen` |
| Backup & compliance | `Services/BackupService.cs` | `/admin/pengaturan` → Backup |
| Multi bahasa ID/EN | `Services/LocalizationService.cs`, `/set-culture` | tombol ID/EN di topbar |
| Asuransi perjalanan | entitas `Asuransi` | `/sos`, dikelola di `/admin/pengaturan` |

## Reminder otomatis

`ReminderService` berjalan sebagai hosted service dan memeriksa berkala
(`Reminder:IntervalHours`, default 6 jam):

- tagihan mendekati jatuh tempo (`Reminder:PaymentDaysBefore`, default H-7/H-3/H-1) dan yang terlambat
- keberangkatan H-7 dan H-1
- jadwal manasik dari itinerary berjenis `Manasik`
- dokumen wajib yang belum diunggah

Anti-duplikat: satu judul notifikasi maksimal sekali per user per hari, sehingga restart
aplikasi tidak membanjiri jamaah.

## Integrasi SISKOHAT

SISKOHAT Kemenag tidak menyediakan API publik. `SiskohatService` karenanya punya dua mode:

- **Live** — bila `Siskohat:Endpoint` diisi, data jamaah di-POST ke endpoint tersebut
  dengan header `X-Api-Key`, respons `{ status, no_porsi }` disimpan.
- **Simulasi** (default) — validasi dijalankan lokal: format NIK 16 digit, kelengkapan nama,
  tanggal lahir, dan nomor paspor; nomor porsi dibangkitkan deterministik dari NIK.

Setiap sinkronisasi dicatat di `SiskohatLog` dan hasilnya menempel di `Jamaah.SiskohatStatus`
serta `Jamaah.NoPorsi`.

## Backup

`BackupService` menyediakan dua bentuk:

- **Snapshot SQLite** lewat `VACUUM INTO` — konsisten dan aman dijalankan saat aplikasi hidup
  (tidak menyalin file yang sedang ditulis WAL).
- **Ekspor Excel multi-sheet** seluruh tabel — dipakai untuk provider selain SQLite dan untuk
  audit/compliance. Kolom sensitif (hash password, token) tidak diekspor.

Setiap backup dicatat di `BackupLog`.

## Multi bahasa

Bahasa dipilih lewat `GET /set-culture?culture=id|en&redirectUri=...` yang menulis cookie budaya
ASP.NET Core, sehingga format tanggal dan mata uang ikut menyesuaikan. Teks antarmuka diambil dari
kamus di `LocalizationService` (`L["nav.jamaah"]`). Kunci yang belum diterjemahkan otomatis
jatuh ke teks Indonesia, jadi terjemahan bisa dilengkapi bertahap. Saat ini kerangka aplikasi
(sidebar, topbar, label umum) sudah dwibahasa; isi halaman masih berbahasa Indonesia.
