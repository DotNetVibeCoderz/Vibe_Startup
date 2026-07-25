# Konfigurasi Ngibrid

Seluruh konfigurasi berada di `appsettings.json` dan **dapat diubah dari dalam aplikasi**
melalui halaman **Pengaturan** (`/settings`, akses Admin/Manager).

Perubahan ditulis kembali ke `appsettings.json` lalu `IConfiguration` di-*reload*, sehingga
berlaku tanpa restart — kecuali provider database yang memerlukan restart.

---

## Cara Kerja Penyimpanan

`AppConfigService` menulis file dengan pola *write-temp-then-replace* dan satu writer lock,
sehingga penyimpanan bersamaan tidak merusak file. Tipe nilai dipertahankan: angka tetap angka,
boolean tetap boolean (bukan string), agar `GetValue<int>` dan `GetValue<bool>` tetap bekerja.

---

## AppInfo

| Key | Default | Keterangan |
|---|---|---|
| `AppInfo:Name` | Ngibrid | Nama aplikasi |
| `AppInfo:ContactEmail` | support@ngibrid.com | Email support |

## Database

| Key | Nilai | Keterangan |
|---|---|---|
| `Database:Provider` | `SQLite` \| `SQLServer` \| `MySQL` \| `Postgre` | **Perlu restart** |
| `Database:ConnectionStrings:<Provider>` | string | Satu entri per provider |

## Storage

| Key | Default | Keterangan |
|---|---|---|
| `Storage:Provider` | `FileSystem` | `FileSystem` \| `AzureBlob` \| `S3` \| `MinIO` |
| `Storage:BasePath` | wwwroot/uploads | Untuk FileSystem |
| `Storage:MaxFileSizeMb` | 25 | Batas ukuran unggahan |
| `Storage:AllowedExtensions` | array | Ekstensi yang diizinkan |
| `Storage:AzureBlob:*` | — | ConnectionString, ContainerName |
| `Storage:S3:*` | — | AccessKey, SecretKey, BucketName, ServiceUrl, Region |
| `Storage:MinIO:*` | — | Endpoint, AccessKey, SecretKey, BucketName, UseSSL |

## Auth

| Key | Default |
|---|---|
| `Auth:Password:RequiredLength` | 8 |
| `Auth:Password:RequireDigit` / `RequireUppercase` / `RequireLowercase` / `RequireNonAlphanumeric` | true |
| `Auth:Password:MaxFailedAttempts` | 5 |
| `Auth:Password:LockoutMinutes` | 15 |

## Shipment

| Key | Default | Keterangan |
|---|---|---|
| `Shipment:DefaultCurrency` | IDR | |
| `Shipment:TaxRate` | 0.11 | PPN 11% |
| `Shipment:InsuranceRate` | 0.02 | Premi 2% dari nilai barang |
| `Shipment:MaxPackageWeight` | 50 | kg |

## AI

| Key | Default | Keterangan |
|---|---|---|
| `AI:DynamicPricing:BaseFare` | 9000 | Tarif dasar kg pertama (IDR) |
| `AI:DynamicPricing:RatePerKm` | 22 | Komponen jarak per km (IDR) |
| `AI:DynamicPricing:PeakHourMultiplier` | 1.3 | 07–09 & 17–19 |
| `AI:DynamicPricing:WeekendMultiplier` | 1.2 | Sabtu–Minggu |
| `AI:RouteOptimization:MaxIterations` | 1000 | Batas iterasi 2-opt |
| `AI:Forecast:PeakSeasonThreshold` | 1.25 | Indeks musiman ≥ nilai ini = peak |

> **Model tarif.** Kg pertama = `BaseFare + jarak × RatePerKm`; kg berikutnya 60% dari itu.
> Jarak dihitung Haversine antar koordinat kota × 1.3 (faktor jalan).

## Simulator

| Key | Default |
|---|---|
| `GPS:Simulator:Enabled` | true |
| `GPS:Simulator:UpdateIntervalMs` | 5000 |
| `GPS:Simulator:SpeedKmh` | 40 |
| `IoT:Simulator:Enabled` | true |
| `IoT:Simulator:SensorUpdateIntervalMs` | 10000 |
| `IoT:LockerSimulator:Enabled` | true |
| `IoT:LockerSimulator:UpdateIntervalMs` | 30000 |

## Chat Bot

Lihat [CHATBOT.md](CHATBOT.md) untuk penjelasan lengkap.

## Loyalty

| Key | Default | Keterangan |
|---|---|---|
| `Loyalty:PointsPerRupiah` | 0.0001 | 1 poin per Rp10.000 |
| `Loyalty:RupiahPerPoint` | 100 | Nilai tukar 1 poin |

Tier: Bronze (0) → Silver (1.000) → Gold (5.000) → Platinum (15.000),
dengan pengali perolehan 1× / 1.25× / 1.5× / 2×.

## Compliance

| Key | Default | Keterangan |
|---|---|---|
| `Compliance:Customs:DutyRate` | 0.075 | Bea masuk 7,5% |
| `Compliance:Customs:ImportVatRate` | 0.11 | PPN impor |
| `Compliance:Customs:DeMinimisUsd` | 3 | Di bawah nilai ini bea masuk nol |

## Green Logistics

| Key | Default |
|---|---|
| `GreenLogistics:EmissionFactorGramCo2PerKm` | 150 |
| `GreenLogistics:EcoVehicleDiscount` | 0.1 |
| `GreenLogistics:CarbonOffsetPricePerKg` | 500 |

## Notification

| Key | Keterangan |
|---|---|
| `Notification:Email:*` | SMTP host, port, kredensial, pengirim |
| `Notification:SMS:*` | Twilio AccountSid, AuthToken, FromNumber |
| `Notification:PushNotification:FirebaseServerKey` | FCM |

Kanal yang belum dikonfigurasi dilewati dengan aman — notifikasi in-app tetap tersimpan.

## Integration

| Key | Keterangan |
|---|---|
| `Integration:Marketplace:Tokopedia:ApiKey` / `Endpoint` | |
| `Integration:Marketplace:Shopee:ApiKey` / `Endpoint` | |

Tanpa kredensial, sinkronisasi memakai generator data contoh yang deterministik sehingga alur
impor tetap dapat diuji.

---

## Catatan Produksi

Sebelum deploy:

1. Ganti `Auth:Jwt:Secret` dengan nilai acak ≥ 32 karakter.
2. Pindahkan seluruh API key ke *user secrets*, variabel lingkungan, atau vault —
   jangan biarkan di `appsettings.json` yang ter-commit.
3. Nonaktifkan simulator (`GPS`, `IoT`, `IoT:LockerSimulator`) bila memakai perangkat nyata.
4. Setel `ASPNETCORE_ENVIRONMENT=Production` (Swagger otomatis nonaktif, HSTS aktif).
5. Ganti provider database dari SQLite ke SQL Server/MySQL/PostgreSQL.
