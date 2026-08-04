# 🧪 Pengujian — FastRide

```bash
dotnet test FastRide.Tests
```

**318 test, ±72 detik.** Tidak perlu menjalankan API atau menyiapkan database lebih dulu.
Dijalankan otomatis oleh CI — lihat [`CI.md`](CI.md).

---

## Bentuknya

| Lapis | Jumlah | Menguji apa |
|-------|--------|-------------|
| Unit | ~140 | Logika murni: tarif, jarak, transisi status, paging, CSV, tampilan, storage, QRIS, verifikasi tanda tangan provider |
| Integrasi | ~178 | API sungguhan lewat HTTP, dari pendaftaran sampai pembayaran lunas |

Test integrasi menjalankan **aplikasi yang sebenarnya** dengan `WebApplicationFactory<Program>`:
autentikasi, otorisasi, batas laju, cache, dan EF Core yang sama seperti di produksi. Tidak
ada yang di-*mock*. Yang berbeda hanya konfigurasinya — berkas SQLite sekali pakai, tanpa
data contoh, dan batas laju yang dilonggarkan.

---

## Susunan berkas

```
FastRide.Tests/
├── Infrastructure/
│   ├── FastRideApiFactory.cs   host API di memori
│   ├── ApiFixture.cs           satu host untuk seluruh suite + pembuat aktor
│   └── Json.cs                 helper HTTP dan JSON
├── Unit/
│   ├── GeoUtilsTests.cs
│   ├── FareConfigTests.cs
│   ├── OrderTransitionTests.cs
│   ├── PagingTests.cs
│   ├── CsvExporterTests.cs
│   ├── DisplayTests.cs
│   └── FileSystemStorageProviderTests.cs
└── Integration/
    ├── AuthTests.cs
    ├── AuthorizationTests.cs
    ├── OrderLifecycleTests.cs
    ├── PaymentTests.cs
    ├── PromoTests.cs
    ├── DriverVerificationTests.cs
    ├── DispatchTests.cs
    ├── ReviewTests.cs
    ├── DashboardTests.cs
    ├── FareConfigEndpointTests.cs
    └── RateLimitTests.cs
```

---

## Yang dijaga

Setiap bug yang pernah diperbaiki punya test yang menahannya agar tidak kembali.

| Invarian | Test |
|----------|------|
| Satu perjalanan = satu pembayaran | `PaymentTests.PostingAPaymentTwice_NeverChargesTwice` |
| Rute terlindungi menolak anonim | `AuthorizationTests.ProtectedRoutes_RejectAnonymousCallers` |
| Ganti id di URL tidak membuka data orang lain | `AuthorizationTests.ARider_CannotReadAnotherRidersTrips` |
| Logout benar-benar mematikan token | `AuthTests.Logout_InvalidatesTheTokenImmediately` |
| Menonaktifkan akun memutus sesi berjalan | `AuthTests.SuspendingAnAccount_CutsItsLiveSession` |
| Login tidak membocorkan email terdaftar | `AuthTests.Login_ReturnsTheSameRefusal_ForAWrongPasswordAndAnUnknownEmail` |
| Tarif minimum jadi lantai harga | `FareConfigTests.Quote_NeverGoesBelowTheMinimumFare` |
| Surge benar-benar dipakai | `FareConfigEndpointTests.Surge_IsAppliedToNewQuotes` |
| Harga di aplikasi = harga yang ditagih | `OrderLifecycleTests.Booking_StartsInRequestedAndMatchesItsQuote` |
| Hanya satu driver menang berebut order | `OrderLifecycleTests.OnlyOneDriver_CanTakeAnOrder` |
| Transisi status tidak bisa dilompati | `OrderLifecycleTests.ATrip_CannotSkipStraightToCompleted` |
| Driver tanpa dokumen tidak bisa jalan | `DriverVerificationTests.AnUnverifiedDriver_CannotAcceptATrip` |
| Melihat harga tidak memakai kuota promo | `PromoTests.Quoting_ShowsTheDiscountWithoutSpendingIt` |
| Membatalkan mengembalikan kuota promo | `PromoTests.Cancelling_HandsTheRedemptionBack` |
| Promo terpakai dinonaktifkan, bukan dihapus | `PromoTests.ARedeemedPromo_IsDeactivatedRatherThanDeleted` |
| Rating driver cocok dengan ulasannya | `ReviewTests.AReviewUpdatesTheDriversHeadlineRating` |
| GPS basi tidak ikut pencocokan | `DispatchTests.NearbyDrivers_IgnoresDriversThatNeverSentAPosition` |
| Nama berkas jahat tidak keluar folder unggahan | `FileSystemStorageProviderTests.Upload_RefusesToEscapeTheUploadDirectory` |
| Avatar `data:` URI tidak dianggap berkas | `FileSystemStorageProviderTests.ResolveFileName_ReturnsNull_ForAGeneratedAvatar` |
| Batas laju benar-benar menahan | `RateLimitTests.RepeatedSignInAttempts_AreEventuallyThrottled` |
| Payload QRIS lolos checksum-nya sendiri | `QrisPayloadTests.Build_ProducesAPayloadThatPassesItsOwnChecksum` |
| QRIS yang diubah ditolak | `QrisPayloadTests.IsValid_RejectsAPayloadWhoseContentWasAltered` |
| Charge ulang mengembalikan QR yang sama | `PaymentFlowTests.ChargingTwiceReturnsTheSameCode` |
| Pembayaran gagal bisa dicoba lagi | `PaymentFlowTests.ADeclinedChargeCanBeRetried` |
| Retry tidak pernah membuat pembayaran kedua | `PaymentFlowTests.RetryingNeverCreatesASecondPayment` |
| Callback bertanda tangan palsu ditolak | `PaymentProviderTests.Simulated_RejectsACallbackWithAForgedSignature` |
| Callback terlambat tidak membatalkan pelunasan | `PaymentFlowTests.ALateFailureCallbackCannotUnpayASettledTrip` |

---

## Isolasi

Satu host API dipakai bersama seluruh suite integrasi — menyalakan host dan meng-hash kata
sandi dengan BCrypt work factor 12 itu mahal. Isolasi datang dari **setiap test membuat
aktornya sendiri**, bukan dari database baru per test:

```csharp
var rider = await fixture.NewRiderAsync();          // akun baru, token siap pakai
var driver = await fixture.NewVerifiedDriverAsync(); // termasuk unggah + setujui 3 dokumen
```

Dua kelas mengubah keadaan global, jadi keduanya memakai host sendiri:

- `FareConfigEndpointTests` — mengubah tabel tarif
- `RateLimitTests` — sengaja memakai batas 5 permintaan/menit

---

## ⚠️ Dua jebakan yang sudah menggigit

Keduanya sempat membuat seluruh suite hijau palsu atau merah total. Kalau Anda menambah
test integrasi, ingat dua hal ini.

**1. `UseSetting`, bukan `ConfigureAppConfiguration`.**

Pada minimal hosting, `WebApplication.CreateBuilder` membaca konfigurasi lebih dulu, dan API
menentukan provider database/cache/storage-nya saat pendaftaran service. Callback
`ConfigureAppConfiguration` berjalan *setelah* itu, jadi nilainya datang terlambat — dan
seluruh test diam-diam berjalan di `FastRide.db` milik pengembang.

`FastRideApiFactory.AssertIsIsolated()` dipanggil sebelum apa pun disemai, khusus agar
kegagalan seperti ini terlihat sebagai error, bukan sebagai test yang lulus di database yang
salah.

**2. HTTPS redirection mencopot header Authorization.**

Test client mengikuti redirect. Saat `UseHttpsRedirection` memindahkan permintaan dari
`http://` ke `https://`, `RedirectHandler` membuang header `Authorization` — setiap
permintaan terautentikasi tiba sebagai anonim dan dibalas `401` dengan
`WWW-Authenticate: Bearer` tanpa `error=`.

Karena itu API punya sakelar `ApiSettings:UseHttpsRedirection` (bawaan `true`), yang juga
berguna di belakang reverse proxy yang sudah menangani TLS.

---

## Menambah test

```csharp
[Collection(ApiCollection.Name)]        // pakai host bersama
public class FiturBaruTests(ApiFixture fixture)
{
    [Fact]
    public async Task NamanyaMenjelaskanPerilaku()
    {
        var rider = await fixture.NewRiderAsync();

        using var response = await rider.Client.PostJsonAsync("/api/...", payload);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
```

Gunakan host sendiri (`IAsyncLifetime` + `new ApiFixture()`) hanya bila test mengubah
keadaan global seperti tabel tarif atau konfigurasi.

---

## Cakupan

```bash
dotnet test FastRide.Tests --collect:"XPlat Code Coverage"
```

Hasilnya berupa berkas Cobertura di `FastRide.Tests/TestResults/`.

---

## Yang belum diuji

| Area | Alasan |
|------|--------|
| Komponen Blazor (AdminWeb) | Perlu bUnit; UI diuji manual |
| Layar MAUI | Perlu perangkat/emulator |
| Provider S3 & Azure Blob | Perlu MinIO/Azurite; tanda tangan diuji manual |
| Cache Redis | Perlu Redis; jalur `ICacheService` diuji lewat implementasi memori |
| SQL Server / PostgreSQL / MySQL | Suite berjalan di SQLite — provider yang paling ketat soal terjemahan query |

Semuanya tercatat di [`../PLAN.md`](../PLAN.md).
