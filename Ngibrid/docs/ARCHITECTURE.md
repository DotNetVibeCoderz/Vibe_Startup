# Arsitektur Ngibrid Logistics

Dokumen ini menjelaskan struktur teknis aplikasi: lapisan, alur data, dan keputusan desain penting.

---

## 1. Gambaran Umum

Ngibrid adalah **satu proyek ASP.NET Core (.NET 10)** yang menyatukan:

| Lapisan | Teknologi | Lokasi |
|---|---|---|
| UI | Blazor Server (Interactive Server) | `Components/` |
| REST API | Minimal API + Swagger | `Api/` |
| Real-time | SignalR (4 hub) | `Hubs/` |
| Business logic | Service class | `Services/` |
| Data | EF Core multi-provider | `Data/`, `Models/` |
| Latar belakang | 3 `BackgroundService` | `Services/SimulatorServices.cs` |

```
Browser
  │
  ├── Blazor circuit (SignalR) ──► Components ──► Services ──► DbContext ──► SQLite/SQLServer/MySQL/Postgre
  │
  ├── REST /api/v1/*  ──────────► Minimal API ──► Services ──┘
  │
  └── Hub /hubs/*     ──────────► SignalR Hubs ─► Services ──┘

Background threads: GpsSimulator · IotSimulator · SmartLockerSimulator
```

---

## 2. Struktur Folder

```
Ngibrid/
├── Api/
│   ├── ApiEndpoints.cs       # /api/v1/* — order, tracking, pricing, analytics, dll.
│   └── AuthEndpoints.cs      # /api/auth/* — login, register, reset password
├── Components/
│   ├── App.razor             # Root HTML, urutan pemuatan script
│   ├── Routes.razor          # Router + AuthorizeRouteView
│   ├── Layout/
│   │   ├── MainLayout.razor  # Sidebar, notifikasi, info pengguna
│   │   └── BlankLayout.razor # Layout tanpa chrome untuk halaman auth
│   └── Pages/                # Satu folder per domain
├── Data/
│   ├── NgibridDbContext.cs   # DbSet + konfigurasi relasi
│   └── DbProviderFactory.cs  # Pemilihan provider dari appsettings
├── Hubs/AppHubs.cs           # TrackingHub, ChatHub, NotificationHub, CourierHub
├── Models/                   # Entitas EF Core
├── Services/                 # Business logic
└── wwwroot/
    ├── css/                  # ngibrid.css (design system), chat.css, components.css
    └── js/                   # d3.min.js, leaflet.js, ngibrid-charts.js, ngibrid.js
```

---

## 3. Keputusan Desain Penting

### 3.1 Autentikasi lewat endpoint HTTP, bukan dari komponen Blazor

Komponen Blazor Server interaktif berjalan di atas **circuit SignalR** yang sudah terbentuk —
header respons HTTP sudah tertutup, sehingga `SignInManager.PasswordSignInAsync` **tidak dapat
menulis cookie autentikasi**.

Karena itu halaman login/register mem-POST ke `/api/auth/*` lewat `fetch` (helper
`ngibrid.postAuth`), lalu melakukan *full reload* agar cookie terbaca.

```
LoginPage.razor ──fetch──► POST /api/auth/login ──► SignInManager ──► Set-Cookie
                                                                          │
                             window.location = "/" ◄────────────────────┘
```

### 3.2 Kernel function berlaku untuk semua model AI

`ChatBotService` membangun satu `Kernel` berisi 6 plugin. Rutenya berbeda per provider:

| Provider | Jalur | Function calling |
|---|---|---|
| OpenAI | Konektor SK native | Otomatis (`FunctionChoiceBehavior.Auto`) |
| Ollama | Konektor SK (endpoint `/v1` kompatibel OpenAI) | Otomatis |
| Anthropic | `AnthropicChatClient` (HTTP native) | Loop tool-call manual |
| Gemini | `GeminiChatClient` (HTTP native) | Loop tool-call manual |

Semantic Kernel belum menyediakan konektor resmi untuk Anthropic dan Gemini, sehingga kedua
client tersebut menerjemahkan metadata `KernelFunction` menjadi skema tool masing-masing API,
lalu menjalankan siklus *tool → hasil → lanjut* sampai model menjawab.

### 3.3 Plugin memakai `IServiceScopeFactory`, bukan `DbContext` langsung

Satu giliran chat bisa memicu beberapa function call paralel dan berumur lebih panjang daripada
scope halaman. `DbContext` tidak aman dipakai bersamaan, jadi tiap plugin membuka scope sendiri.

### 3.4 Simulator: singleton + hosted service

`GpsSimulatorService`, `IotSimulatorService`, dan `SmartLockerSimulatorService` didaftarkan **dua kali**
— sebagai singleton (agar halaman bisa memanggil `StartSimulation`) dan sebagai hosted service
(agar berdetak di thread sendiri). Di dalam loop, tiap tick membuat scope DI baru.

### 3.5 Skema database dibuat via `EnsureCreated`

Tidak ada migration. Konsekuensinya: **mengubah entitas berarti menghapus `Data/ngibrid.db`**
(atau database target) agar skema baru terbentuk. `DataSeeder` berhenti bila sudah ada baris user.

### 3.6 Satu master data kota untuk semua perhitungan geografis

Tabel `Cities` (514 kota/kabupaten, 38 propinsi) adalah satu-satunya sumber koordinat. Tracking,
tarif, emisi, peta, dan optimasi rute semuanya memanggil `CityCoordinates`, jadi satu kota berarti
satu titik di seluruh aplikasi — dulu tiap tempat punya daftar kota hardcoded sendiri yang isinya
cuma 20 kota besar.

Koordinat yang disimpan adalah **ibu kota daerah**, bukan centroid geometris: paket dikirim ke
kotanya, bukan ke tengah pegunungan. Kolom `Type` (KOTA/KABUPATEN) wajib ikut dalam kunci indeks
karena 26 nama dipakai kota sekaligus kabupaten dan ibu kotanya bisa berjarak puluhan kilometer.

Pengisian awal dari `Data/IndonesiaCities.cs` dijalankan `CityService.InitializeAsync()` **sebelum**
`DataSeeder`, dan syaratnya "tabel `Cities` kosong" — bukan "belum ada user" — supaya database lama
tetap kebagian master datanya.

---

## 4. Alur Data Utama

### 4.1 Pembuatan pesanan

```
OrdersPage / POST /api/v1/orders
        │
        ▼
OrderService.CreateOrderAsync
        ├── nomor order harian (NGB-yyyyMMdd-0001)
        ├── berat volumetrik = P×L×T / 6000
        ├── DynamicPricingService.CalculatePriceAsync  (zona jarak + peak + weekend + demand)
        │     └── CityCoordinates.Resolve(propinsi, kota) → koordinat ibu kota dari master data
        ├── GreenLogisticsService.EstimateEmissionAsync (Haversine antar kota)
        ├── simpan Order → simpan OrderStatusHistory (CREATED)
        └── AuditService.LogAsync
```

### 4.2 Perubahan status

`OrderService.UpdateStatusAsync` menulis status, riwayat, dan titik GPS, lalu menjalankan
*post-status hooks* yang **masing-masing terisolasi** — kegagalan notifikasi atau timeout
marketplace tidak membatalkan perubahan status yang sudah terlihat pelanggan:

```
UpdateStatusAsync
   ├── NotificationService.NotifyOrderStatusChangeAsync
   ├── (DELIVERED) LoyaltyService.EarnForOrderAsync      — idempoten per order
   ├── (DELIVERED) ComplianceService.RecordTaxAsync      — idempoten per order+jenis pajak
   └── (ada ExternalOrderId) MarketplaceService.PushStatusAsync
```

### 4.3 Prediksi permintaan

`ForecastService` memakai **Holt linear exponential smoothing** (level + tren) atas volume
harian, dikali **indeks musiman per hari dalam minggu**. Interval kepercayaan melebar mengikuti
`√horizon` karena galat menumpuk seperti random walk.

---

## 5. Multi-Provider

### Database
`Data/DbProviderFactory.ConfigureProvider` memilih provider dari `Database:Provider`
(`SQLite`, `SQLServer`, `MySQL`, `Postgre`), masing-masing dengan connection string sendiri.

### Storage
`StorageService` mendukung `FileSystem`, `AzureBlob`, `S3`, dan `MinIO` untuk Upload/Delete.
Validasi ekstensi dan ukuran dijalankan sebelum byte dibaca.

---

## 6. Keamanan

| Aspek | Implementasi |
|---|---|
| Autentikasi | ASP.NET Core Identity, cookie HttpOnly, sliding expiration 7 hari |
| Otorisasi | Policy `AdminOnly`, `AdminOrManager`, `StaffArea`, `CourierArea` |
| Lockout | 5 percobaan gagal → kunci 15 menit |
| Reset password | Wajib token dari email; halaman menolak render form tanpa token |
| Enumerasi akun | Login dan forgot-password memberi pesan seragam |
| Audit trail | `AuditService` mencatat aksi, entitas, nilai lama/baru, IP, user agent |
| Markdown chat | `DisableHtml()` — output model tidak boleh menyuntik HTML |
| SSRF | `scrape_url` / `read_file_from_url` menolak loopback dan IP privat |
| Lampiran | Path lampiran lokal divalidasi agar tidak keluar dari `wwwroot` |
| Kebocoran data API | Endpoint publik memproyeksikan DTO, bukan entitas mentah — mis. `/lockers` tidak pernah mengirim PIN kompartemen |
| Siklus serialisasi | `ReferenceHandler.IgnoreCycles` global agar navigasi dua arah EF tidak meledakkan serializer |

---

## 7. Frontend

- **Tanpa framework CSS.** Design system memakai CSS custom property di `ngibrid.css`;
  tema gelap lewat `.dark-theme` / `[data-theme="dark"]`.
- **Chart memakai D3.js** (`ngibrid-charts.js`): line/area, donut, bar, forecast band, sparkline.
  Semua chart membaca warna dari CSS variable sehingga ikut berubah saat tema diganti, dan
  digambar ulang otomatis lewat `ResizeObserver`.
- **Peta memakai Leaflet** (di-*vendor* lokal, tanpa CDN). Tersedia di `/tracking` (rute + marker
  live yang bergerak mengikuti simulasi GPS), `/courier` (peta armada + peta rute teroptimasi),
  `/warehouse`, dan `/locker`. Helper generiknya `ngibrid.renderPointsMap`.
  **Semua peta digambar di `OnAfterRenderAsync`, tidak pernah dari `OnInitializedAsync` atau
  event handler** — interop JS dilarang saat prerender, dan elemen `<div>` peta belum ada di DOM
  sebelum Blazor merender ulang, sehingga `L.map()` tidak menemukan kontainernya.
- Semua aset JS/CSS berada di `wwwroot` — tidak ada dependensi CDN eksternal.
