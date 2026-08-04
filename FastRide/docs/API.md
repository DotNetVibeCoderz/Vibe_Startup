# 📡 API Reference — FastRide

Base URL pengembangan: **`https://localhost:5001`** (HTTP: `http://localhost:5000`).
Semua rute berada di bawah prefiks `/api`.

Dokumen OpenAPI tersedia di `/openapi/v1.json` saat berjalan di lingkungan Development.

---

## Aturan umum

### Autentikasi

Setiap endpoint **membutuhkan token**, kecuali yang ditandai `anon`. Kirim sebagai bearer:

```http
Authorization: Bearer <token>
```

Token didapat dari `POST /api/auth/login` atau `/register`, berlaku 24 jam
(`Jwt:AccessTokenExpirationMinutes`).

> Token dapat dibatalkan sebelum kedaluwarsa. Logout, ganti kata sandi, reset kata sandi,
> dan penonaktifan akun menaikkan *security stamp* pengguna sehingga token lama ditolak
> dengan `401`.

### Peran & kepemilikan

| Tanda | Arti |
|-------|------|
| `anon` | Tidak perlu token |
| `auth` | Perlu token peran apa pun |
| `admin` | Hanya `UserRole.Admin` |
| `self` | Hanya pemilik data, atau admin |

Rute yang memuat `{userId}` bersifat `self`: mengganti id di URL dengan milik orang lain
menghasilkan `403`, bukan data orang tersebut.

### Enum

Enum dikirim dan diterima sebagai **string** (`"Completed"`, `"EWallet"`). Angka masih
diterima pada request untuk kompatibilitas, tetapi respons selalu berupa string.

### Bentuk daftar

Semua endpoint daftar mengembalikan amplop yang sama:

```json
{
  "total": 417,
  "page": 1,
  "limit": 25,
  "data": [ ... ],
  "totalPages": 17,
  "hasPrevious": false,
  "hasNext": true
}
```

`limit` dibatasi maksimum 200.

### Bentuk error

```json
{ "error": "Conflict", "detail": "Pesanan sudah diambil driver lain atau dibatalkan." }
```

| Kode | Kapan |
|------|-------|
| `400` | Input tidak valid |
| `401` | Token hilang, kedaluwarsa, atau sudah dibatalkan |
| `403` | Peran salah, atau mengakses data milik orang lain |
| `404` | Tidak ditemukan |
| `409` | Bentrok aturan bisnis (transisi status tidak sah, order sudah diambil, ulasan ganda) |
| `429` | Melebihi batas laju |

### Batas laju

| Grup | Batas |
|------|-------|
| `/api/auth/*` | 30 permintaan / menit / klien |
| Selain itu | 600 permintaan / menit / klien |

Diatur di bagian `RateLimiting` pada `appsettings.json`.

---

## Health

| Metode | Rute | Akses | Keterangan |
|--------|------|-------|------------|
| GET | `/api/health` | `anon` | Status plus provider database, storage, dan cache yang dipakai |

Mengembalikan `503` bila database tidak bisa dihubungi.

```json
{ "status": "healthy", "version": "2.0.0", "database": "SQLite", "storageProvider": "FileSystem", "cache": "Memory" }
```

---

## Auth

| Metode | Rute | Akses | Keterangan |
|--------|------|-------|------------|
| POST | `/api/auth/register` | `anon` | Buat akun rider atau driver. Peran `Admin` ditolak |
| POST | `/api/auth/login` | `anon` | Tukar kredensial dengan token |
| POST | `/api/auth/forgot-password` | `anon` | Terbitkan kode reset (berlaku 15 menit) |
| POST | `/api/auth/reset-password` | `anon` | Pasang kata sandi baru dengan kode |
| POST | `/api/auth/logout` | `auth` | Akhiri sesi dan batalkan semua token yang sudah terbit |
| POST | `/api/auth/change-password` | `auth` | Ganti kata sandi (semua sesi lama berakhir) |
| GET | `/api/auth/me` | `auth` | Profil pengguna yang sedang masuk |

**Register**

```json
POST /api/auth/register
{
  "fullName": "Budi Santoso",
  "email": "budi@email.com",
  "phoneNumber": "08123456789",
  "password": "Password123",
  "role": "Rider",

  "licenseNumber": "SIM-123456",
  "vehicleType": "Toyota Avanza",
  "vehiclePlate": "B 1234 XYZ",
  "vehicleCategory": "Economy"
}
```

Empat field terakhir hanya dipakai bila `role` = `Driver`.

**Lupa kata sandi.** Balasan selalu sama, terdaftar atau tidak, supaya endpoint ini tidak
bisa dipakai memetakan email mana yang ada. Di lingkungan **Development** kode reset ikut
dikembalikan (`resetCode`) agar alur bisa diuji tanpa SMTP; di lingkungan lain bernilai
`null` — sambungkan pengirim email sebelum produksi.

---

## Profil

| Metode | Rute | Akses | Keterangan |
|--------|------|-------|------------|
| GET | `/api/profile/{userId}` | `self` | Profil, termasuk data driver atau statistik rider |
| PUT | `/api/profile/{userId}` | `self` | Ubah nama/telepon/foto. Menerima JSON **atau** multipart |
| DELETE | `/api/profile/{userId}/photo` | `self` | Hapus foto, kembali ke avatar inisial |
| PUT | `/api/profile/{userId}/driver` | `self` | Ubah SIM, kendaraan, nomor polisi, kategori |

Unggah foto: maksimum **2 MB**, tipe `image/jpeg`, `image/png`, atau `image/webp`.
Multipart memakai field `photo`, `fullName`, `phoneNumber`; JSON memakai
`profilePhotoBase64` + `profilePhotoMimeType`.

---

## Dokumen driver

| Metode | Rute | Akses | Keterangan |
|--------|------|-------|------------|
| GET | `/api/drivers/{userId}/documents` | `self` | Daftar dokumen dan status peninjauan |
| POST | `/api/drivers/{userId}/documents` | `self` | Unggah/ganti satu dokumen (maks 5 MB, base64) |
| PUT | `/api/drivers/{userId}/documents/{documentId}/review` | `admin` | Setujui atau tolak |

Driver **tidak bisa online atau menerima order** sebelum `DriverLicense`,
`VehicleRegistration`, dan `IdentityCard` disetujui. Mengunggah ulang dokumen apa pun
mengembalikan status verifikasi ke menunggu.

---

## Order

| Metode | Rute | Akses | Keterangan |
|--------|------|-------|------------|
| GET | `/api/orders` | `admin` | Daftar berpaginasi + filter |
| GET | `/api/orders/export.csv` | `admin` | Unduh hasil filter (maks 10.000 baris) |
| GET | `/api/orders/{id}` | peserta / `admin` | Detail lengkap termasuk singgah dan pembayaran |
| GET | `/api/orders/{id}/tracking` | peserta / `admin` | Posisi driver, jarak, dan ETA |
| POST | `/api/orders/quote` | `auth` | Hitung tarif sebelum memesan |
| POST | `/api/orders` | `self` (rider) | Buat pesanan |
| POST | `/api/orders/{id}/cancel` | peserta / `admin` | Batalkan |

**Filter `GET /api/orders`:** `page`, `limit`, `status`, `vehicleCategory`, `paymentMethod`,
`riderId`, `driverId`, `from`, `to`, `search` (kode order, nama rider, alamat).

**Quote**

```json
POST /api/orders/quote
{
  "pickupLatitude": -6.2088, "pickupLongitude": 106.8456,
  "dropoffLatitude": -6.1751, "dropoffLongitude": 106.8650,
  "vehicleCategory": "Comfort",
  "promoCode": "WEEKEND20",
  "stops": [{ "latitude": -6.19, "longitude": 106.85, "address": "Halte Benhil" }]
}
```

```json
{
  "vehicleCategory": "Comfort", "distanceKm": 4.3, "estimatedDurationMinutes": 15,
  "baseFare": 7000, "surgeMultiplier": 1, "estimatedFare": 34780,
  "discount": 20000, "finalFare": 14780,
  "promoApplied": "WEEKEND20", "promoMessage": "Hemat Rp 20.000 dengan kode WEEKEND20."
}
```

Quote **tidak** memakai kuota promo. Kuota baru berkurang saat order benar-benar dibuat.

**Aturan pembuatan order**

- Rider hanya boleh punya satu perjalanan berjalan; jika tidak → `409`.
- Jarak dihitung lewat haversine termasuk semua titik singgah.
- Tarif = `(BaseFare + km × CostPerKm + menit × CostPerMinute) × Surge`, dengan
  `MinimumFare` sebagai lantai.
- Kuota promo diambil dengan `UPDATE` bersyarat, jadi dua rider tidak bisa merebut slot
  terakhir yang sama.

**Siklus status**

```
Requested ─┬─► Accepted ─┬─► DriverArrived ─► Started ─► Completed
           │             └─► Cancelled
           ├─► Cancelled
           └─► Expired
```

Transisi di luar itu ditolak dengan `409`.

---

## Rider (mobile)

| Metode | Rute | Akses | Keterangan |
|--------|------|-------|------------|
| GET | `/api/riders` | `admin` | Direktori penumpang |
| GET | `/api/mobile/rider/{userId}/home` | `self` | Ringkasan + perjalanan aktif + riwayat singkat |
| GET | `/api/mobile/rider/{userId}/trips` | `self` | Riwayat berpaginasi (opsional `status`) |

---

## Driver (mobile)

| Metode | Rute | Akses | Keterangan |
|--------|------|-------|------------|
| GET | `/api/drivers` | `admin` | Direktori driver + filter `status`, `verified`, `search` |
| GET | `/api/drivers/nearby` | `auth` | Driver online di sekitar titik (`lat`, `lng`, `radiusKm`, `category`) |
| GET | `/api/mobile/driver/{userId}/home` | `self` | Status, pendapatan hari ini, trip aktif, tawaran order |
| GET | `/api/mobile/driver/{userId}/earnings` | `self` | Hari/minggu/bulan + rincian harian |
| GET | `/api/mobile/driver/{userId}/orders/available` | `self` | Order terbuka terdekat |
| PUT | `/api/mobile/driver/{userId}/location` | `self` | Kirim posisi GPS |
| PUT | `/api/mobile/driver/{userId}/status` | `self` | `Online`, `Offline`, `Break` |
| PUT | `/api/mobile/driver/{userId}/toggle-online` | `self` | Balik status online/offline |
| PUT | `/api/mobile/driver/{userId}/accept-order` | `self` | Ambil order |
| PUT | `/api/mobile/driver/{userId}/arrive-order` | `self` | Tandai tiba di titik jemput |
| PUT | `/api/mobile/driver/{userId}/start-order` | `self` | Mulai perjalanan |
| PUT | `/api/mobile/driver/{userId}/complete-order` | `self` | Selesaikan dan catat pembayaran |

Catatan penting:

- Driver dengan posisi GPS **lebih tua dari 10 menit** tidak ikut dipertimbangkan dalam
  pencocokan.
- `accept-order` memakai `UPDATE` bersyarat: dari dua driver yang berebut order yang sama,
  satu menang dan yang lain menerima `409`.
- Driver tidak bisa berpindah dari `OnTrip` ke `Offline` selagi ada perjalanan berjalan.

---

## Pembayaran

| Metode | Rute | Akses | Keterangan |
|--------|------|-------|------------|
| GET | `/api/payments` | `admin` | Daftar + filter `status`, `method`, `from`, `to` |
| GET | `/api/payments/{id}` | peserta / `admin` | Satu transaksi |
| POST | `/api/payments` | peserta / `admin` | Selesaikan pembayaran order |

`POST /api/payments` bersifat **idempoten**. Kalau order sudah dibayar — biasanya karena
`complete-order` lebih dulu sampai — endpoint mengembalikan pembayaran yang sudah ada,
bukan membuat yang kedua. Kolom `Payment.OrderId` punya unique index sebagai penjaga
terakhir.

---

## Ulasan

| Metode | Rute | Akses | Keterangan |
|--------|------|-------|------------|
| POST | `/api/reviews` | `self` | Beri rating 1–5 setelah perjalanan selesai |
| GET | `/api/reviews/user/{userId}` | `anon` | Ulasan yang diterima seorang pengguna |

Satu ulasan per orang per order. Rating driver dihitung ulang dari seluruh ulasan yang
tercatat, jadi angka di profil selalu cocok dengan daftar ulasannya.

---

## Promo

| Metode | Rute | Akses | Keterangan |
|--------|------|-------|------------|
| GET | `/api/promos` | `auth` | Daftar (opsional `activeOnly=true`) |
| POST | `/api/promos/validate` | `auth` | Uji kode terhadap suatu nominal |
| POST | `/api/promos` | `admin` | Buat |
| PUT | `/api/promos/{id}` | `admin` | Ubah |
| DELETE | `/api/promos/{id}` | `admin` | Hapus, atau nonaktifkan bila sudah pernah dipakai |

Promo yang sudah pernah ditukar **tidak dihapus** agar riwayat order tetap utuh; promo itu
dinonaktifkan. Kuota juga tidak bisa diturunkan di bawah jumlah yang sudah terpakai.

---

## Tarif

| Metode | Rute | Akses | Keterangan |
|--------|------|-------|------------|
| GET | `/api/fares` | `auth` | Tabel tarif semua kategori |
| PUT | `/api/fares/{category}` | `admin` | Ubah tarif satu kategori |

Menyimpan tarif membatalkan cache tarif, jadi harga berikutnya langsung memakai angka baru.
`surgeMultiplier` dibatasi 1,0–5,0.

---

## Notifikasi

| Metode | Rute | Akses | Keterangan |
|--------|------|-------|------------|
| GET | `/api/notifications/{userId}` | `self` | Daftar (opsional `unreadOnly=true`) |
| GET | `/api/notifications/{userId}/unread-count` | `self` | Jumlah belum dibaca |
| PUT | `/api/notifications/{userId}/read-all` | `self` | Tandai semua dibaca |
| PUT | `/api/notifications/{id}/read` | `self` | Tandai satu dibaca |

---

## Dashboard

Semua `admin`. Hasil di-cache 10 detik.

| Metode | Rute | Keterangan |
|--------|------|------------|
| GET | `/api/dashboard/overview` | Semua data dashboard dalam satu panggilan |
| GET | `/api/dashboard/stats` | Kartu ringkasan |
| GET | `/api/dashboard/orders-by-status` | Sebaran status (termasuk yang bernilai nol) |
| GET | `/api/dashboard/orders-by-hour` | 24 keranjang jam (opsional `date`) |
| GET | `/api/dashboard/revenue-series` | Seri harian (opsional `days`, maks 365) |
| GET | `/api/dashboard/top-drivers` | Peringkat 30 hari |
| GET | `/api/dashboard/financial-summary` | Kotor, diskon, bersih, komisi (`from`, `to`) |
| GET | `/api/dashboard/financial-summary/export.csv` | Unduh laporan |

Gunakan `/overview` untuk dashboard: satu panggilan menggantikan enam, dan hasilnya
konsisten satu sama lain.

---

## Admin

| Metode | Rute | Akses | Keterangan |
|--------|------|-------|------------|
| GET | `/api/admin/users` | `admin` | Semua akun + filter `role`, `active`, `search` |
| PUT | `/api/admin/users/{userId}/active` | `admin` | Aktifkan/nonaktifkan |
| PUT | `/api/admin/users/{userId}/verify` | `admin` | Tandai terverifikasi |
| GET | `/api/admin/drivers/pending-verification` | `admin` | Antrean peninjauan dokumen |

Menonaktifkan akun langsung memutus sesinya, dan mematikan status online driver. Admin
tidak bisa menonaktifkan akunnya sendiri.

---

## Contoh alur lengkap

```bash
API=http://localhost:5000/api

# 1. Rider masuk
RIDER=$(curl -s -X POST $API/auth/login -H "Content-Type: application/json" \
  -d '{"email":"budi.santoso@email.com","password":"Password123"}')
RT=$(echo "$RIDER" | jq -r .token)
RID=$(echo "$RIDER" | jq -r .userId)

# 2. Cek harga
curl -s -X POST $API/orders/quote -H "Authorization: Bearer $RT" \
  -H "Content-Type: application/json" \
  -d '{"pickupLatitude":-6.2088,"pickupLongitude":106.8456,
       "dropoffLatitude":-6.1751,"dropoffLongitude":106.8650,
       "vehicleCategory":"Economy"}'

# 3. Pesan
ORDER=$(curl -s -X POST $API/orders -H "Authorization: Bearer $RT" \
  -H "Content-Type: application/json" \
  -d "{\"riderId\":\"$RID\",
       \"pickupLatitude\":-6.2088,\"pickupLongitude\":106.8456,\"pickupAddress\":\"Jl. Sudirman 1\",
       \"dropoffLatitude\":-6.1751,\"dropoffLongitude\":106.8650,\"dropoffAddress\":\"Jl. Thamrin 9\",
       \"vehicleCategory\":\"Economy\",\"paymentMethod\":\"Cash\"}")
OID=$(echo "$ORDER" | jq -r .id)

# 4. Pantau
curl -s $API/orders/$OID/tracking -H "Authorization: Bearer $RT"
```

Untuk sisi driver, lihat [`SIMULATOR.md`](SIMULATOR.md) — simulator menjalankan persis
rangkaian panggilan ini.
