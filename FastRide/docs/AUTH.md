# 🔐 Autentikasi & Otorisasi — FastRide

---

## Ringkas

FastRide memakai **JWT bearer token** dengan kata sandi di-hash BCrypt (work factor 12).
Token berlaku 24 jam dan **bisa dibatalkan sebelum kedaluwarsa**.

> **Catatan sejarah.** Sampai v1.x, JWT diterbitkan tetapi tidak pernah diperiksa: tidak
> satu pun endpoint memanggil `RequireAuthorization()` dan tidak ada kebijakan bawaan,
> sehingga seluruh API bisa diakses anonim. Sejak v2.0 otorisasi benar-benar ditegakkan.

---

## Alur

```
register / login  ──►  JWT (24 jam)  ──►  Authorization: Bearer <token>
                                              │
                          SecurityStampMiddleware memeriksa
                          apakah token masih sah untuk sesi ini
```

### Klaim di dalam token

| Klaim | Isi |
|-------|-----|
| `sub` / `nameidentifier` | Id pengguna |
| `name` | Nama lengkap |
| `email` | Email |
| `role` | `Rider`, `Driver`, atau `Admin` |
| `sstamp` | Security stamp saat token diterbitkan |
| `jti` | Id token |

---

## Membatalkan token

JWT tidak bisa "ditarik kembali" — begitu terbit, ia sah sampai kedaluwarsa. Karena itu
setiap pengguna punya kolom `SecurityStamp`.

`SecurityStampMiddleware` membandingkan klaim `sstamp` di token dengan nilai terkini milik
pengguna. Kalau berbeda, permintaan ditolak `401`.

Stamp dinaikkan saat:

| Kejadian | Efek |
|----------|------|
| `POST /api/auth/logout` | Semua token pengguna itu langsung tidak berlaku |
| `POST /api/auth/change-password` | Sama — perangkat lain ikut keluar |
| `POST /api/auth/reset-password` | Sama |
| Admin menonaktifkan akun | Sesi berjalan langsung terputus |

Nilai stamp di-cache 5 menit (`ICacheService`), jadi jalur normal tidak menambah query ke
database per permintaan.

---

## Kebijakan otorisasi

| Kebijakan | Peran yang diterima |
|-----------|---------------------|
| `AdminOnly` | `Admin` |
| `DriverOnly` | `Driver`, `Admin` |
| `RiderOnly` | `Rider`, `Admin` |
| `StaffOrDriver` | `Driver`, `Admin` |

### Kepemilikan data

Peran saja tidak cukup. Rute yang memuat `{userId}` juga memeriksa kepemilikan lewat
`ClaimsPrincipal.CanAccess(userId)`:

```csharp
public static bool CanAccess(this ClaimsPrincipal principal, Guid targetUserId) =>
    principal.IsAdmin() || principal.UserId() == targetUserId;
```

Tanpa ini, setiap rider yang sudah masuk bisa membaca riwayat perjalanan rider lain hanya
dengan mengganti id pada URL.

Detail order memakai aturan yang sedikit berbeda: yang boleh membaca adalah **peserta
perjalanan** (rider atau driver pada order itu) atau admin.

---

## Endpoint anonim

Hanya tiga kelompok:

- `GET /api/health`
- `POST /api/auth/register`, `login`, `forgot-password`, `reset-password`
- `GET /api/reviews/user/{userId}` — rating publik seorang driver

---

## Batas laju

Endpoint autentikasi adalah yang paling layak diserang, jadi dibatasi lebih ketat:

| Grup | Batas bawaan | Pengaturan |
|------|--------------|------------|
| `/api/auth/*` | 30 / menit | `RateLimiting:AuthPermitPerMinute` |
| Global | 600 / menit | `RateLimiting:GlobalPermitPerMinute` |

Partisi dihitung per pengguna bila sudah masuk, atau per alamat IP bila belum. Melebihi
batas menghasilkan `429` dengan pesan berbahasa Indonesia.

---

## Reset kata sandi

```
POST /api/auth/forgot-password   { "email": "..." }
   → kode 6 digit disimpan di cache selama 15 menit

POST /api/auth/reset-password    { "email": "...", "resetCode": "123456", "newPassword": "..." }
   → kata sandi diganti, security stamp naik, kode dihapus
```

Kode dibuat dengan `RandomNumberGenerator`, bukan `Random`.

**Yang masih perlu dikerjakan:** belum ada pengirim email. Di lingkungan Development kode
dikembalikan langsung di respons supaya alur bisa diuji; di lingkungan lain field `resetCode`
bernilai `null` dan kode hanya tercatat di log. Sambungkan SMTP di
`AuthEndpoints.ForgotPassword` sebelum produksi.

---

## Kata sandi

- Hash: **BCrypt**, work factor **12**, dipakai konsisten di API dan seeder.
- Panjang minimum 8 karakter (`[MinLength(8)]` pada DTO).
- Verifikasi menangkap `SaltParseException`, jadi hash rusak menghasilkan "gagal masuk",
  bukan `500`.

---

## Mencegah enumerasi akun

Login membalas pesan yang sama untuk "email tidak ada" dan "kata sandi salah".
`forgot-password` membalas pesan yang sama untuk email terdaftar maupun tidak. Keduanya
mencegah endpoint publik dipakai memetakan siapa saja yang punya akun.

---

## Verifikasi driver

Autentikasi tidak cukup untuk driver. Sebelum bisa online atau menerima order, driver
harus punya tiga dokumen berstatus `Approved`:

| Dokumen | Enum |
|---------|------|
| SIM | `DocumentType.DriverLicense` |
| STNK | `DocumentType.VehicleRegistration` |
| KTP | `DocumentType.IdentityCard` |

Ditegakkan di dua tempat: `PUT /status` menolak `Online` untuk driver yang belum
terverifikasi, dan `OrderService.AcceptAsync` menolak pengambilan order.

---

## Konfigurasi

```json
"Jwt": {
  "Secret": "FastRide-Development-Only-Secret-Key-Change-Me-32+",
  "Issuer": "FastRide",
  "Audience": "FastRide",
  "AccessTokenExpirationMinutes": 1440
}
```

API **menolak start** bila `Jwt:Secret` kosong atau kurang dari 32 karakter — kegagalan
saat start jauh lebih baik daripada diam-diam memakai kunci lemah.

Untuk produksi, pasang lewat variabel lingkungan atau secret store, bukan file:

```bash
export Jwt__Secret="$(openssl rand -base64 48)"
```

`ClockSkew` diturunkan menjadi 30 detik (bawaan .NET 5 menit) agar token berumur pendek
berperilaku sesuai perkiraan saat pengujian.

---

## Klien

| Klien | Penyimpanan token |
|-------|-------------------|
| AdminWeb | `ProtectedSessionStorage`, dilingkupi per circuit Blazor |
| RiderApp / DriverApp | `SecureStorage` milik MAUI (keystore/keychain) |
| Simulator | Di memori, satu klien HTTP per aktor |

Ketiganya memperlakukan `401` sebagai "sesi berakhir": sesi lokal dibersihkan dan pengguna
dikembalikan ke layar masuk.

---

## Yang belum ada

| Item | Catatan |
|------|---------|
| Refresh token | Sekarang satu token 24 jam; perpanjangan berarti login ulang |
| Autentikasi dua faktor | Belum |
| OAuth / login sosial | Belum |
| Pengiriman email | Kode reset belum benar-benar dikirim |

Lihat [`../PLAN.md`](../PLAN.md) untuk urutan pengerjaannya.
