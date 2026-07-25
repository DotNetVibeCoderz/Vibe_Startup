# REST API Ngibrid

Base URL: `/api/v1` · Swagger UI: `/api/docs` (Development)
Autentikasi: cookie ASP.NET Core Identity (`/api/auth/login`).

Kolom **Akses**: `publik` = tanpa login · `login` = perlu autentikasi ·
`staff` = Admin/Manager/WarehouseStaff · `kurir` = Admin/Manager/Courier · `admin` = Admin.

---

## Autentikasi — `/api/auth`

| Method | Path | Akses | Keterangan |
|---|---|---|---|
| POST | `/login` | publik | Body `{email, password, rememberMe}`. Menetapkan cookie sesi. |
| POST | `/register` | publik | Membuat akun Customer lalu langsung sign-in. |
| GET/POST | `/logout` | publik | Menghapus cookie sesi. |
| POST | `/forgot-password` | publik | Selalu membalas sukses (mencegah enumerasi email). |
| POST | `/reset-password` | publik | Wajib `token` dari email. |
| POST | `/change-password` | login | Menyegarkan cookie setelah ganti password. |

```bash
curl -c cookie.txt -X POST http://localhost:5182/api/auth/login \
  -H "Content-Type: application/json" \
  -d '{"email":"admin@ngibrid.com","password":"Admin123!"}'
```

---

## Master Data Kota — `/provinces`, `/cities`

Referensi kota/kabupaten se-Indonesia (514 daerah, 38 propinsi). Publik tanpa login karena formulir
pesanan, importer marketplace, dan chat bot butuh datanya sebelum ada sesi.

| Method | Path | Akses | Keterangan |
|---|---|---|---|
| GET | `/provinces` | **publik** | Daftar nama propinsi, urut abjad. |
| GET | `/cities?province=&q=` | **publik** | Kota/kabupaten; tanpa filter mengembalikan seluruh 514 baris. Kota didahulukan sebelum kabupaten. |
| GET | `/cities/{id}` | **publik** | Satu daerah. |
| GET | `/cities/distance?from=&to=&fromProvince=&toProvince=` | **publik** | Jarak garis lurus (km) antar dua daerah; 400 bila nama tidak ada di master data. |

`from`/`to` menerima penulisan longgar — `Kota Bandung`, `kab. bandung`, atau nama ibu kota
`Soreang`. Nama telanjang `Bandung` diartikan **Kota** Bandung; sertakan propinsi (dan awalan
`Kota`/`Kabupaten`) kalau butuh presisi.

```bash
curl "http://localhost:5182/api/v1/cities?province=Papua%20Tengah"
curl "http://localhost:5182/api/v1/cities/distance?from=Kabupaten%20Bandung&to=Kota%20Jakarta%20Pusat"
```

---

## Pesanan — `/orders`

| Method | Path | Akses | Keterangan |
|---|---|---|---|
| GET | `/orders?search=&status=&page=&size=` | login | Staff melihat semua; pelanggan hanya miliknya. |
| GET | `/orders/{id}` | login | 403 bila bukan pemilik dan bukan staff. |
| GET | `/orders/track/{trackingNumber}` | **publik** | Tampilan publik: tanpa harga & alamat lengkap. |
| POST | `/orders` | login | Membuat pesanan; harga dihitung server. `senderProvince`/`recipientProvince` opsional — bila kosong diisi otomatis dari master data berdasarkan nama kotanya. |
| PUT | `/orders/{id}/status` | kurir | Body `{status, notes, latitude, longitude}`. |
| POST | `/orders/{id}/cancel` | login | Body `{reason}`. |

```bash
curl "http://localhost:5182/api/v1/orders/track/NGB2607250001ABCD"
```

---

## Tracking & Tarif

| Method | Path | Akses | Keterangan |
|---|---|---|---|
| GET | `/tracking/{orderId}` | publik | Riwayat titik GPS. |
| GET | `/tracking/{orderId}/latest` | publik | Posisi terakhir. |
| POST | `/tracking/{orderId}` | kurir | Kirim posisi dari aplikasi kurir. |
| GET | `/pricing/calculate?origin=&dest=&weight=&service=&originProvince=&destProvince=` | publik | Tarif satu layanan. |
| GET | `/pricing/compare?origin=&dest=&weight=&originProvince=&destProvince=` | publik | Tarif ECO/REG/EXP/SAMEDAY sekaligus. |

Parameter propinsi opsional tapi disarankan: tanpa itu `Bandung` tidak bisa dibedakan dari
`Kabupaten Bandung`, dan tarifnya berbeda.

```bash
curl "http://localhost:5182/api/v1/pricing/compare?origin=Kota%20Bandung&originProvince=Jawa%20Barat&dest=Kota%20Surabaya&destProvince=Jawa%20Timur&weight=3"
```

---

## Keuangan

| Method | Path | Akses |
|---|---|---|
| GET | `/payments?page=&size=` | login |
| POST | `/payments/{orderId}` | login |
| POST | `/payments/{paymentId}/confirm` | login |
| GET | `/invoices` · `/invoices/{orderId}` | login |
| GET | `/invoices/{invoiceId}/html` | login |
| GET/POST | `/insurance/claims` | login |
| POST | `/insurance/claims/{id}/review` | staff (Admin/Manager) |

---

## Operasional

| Method | Path | Akses | Keterangan |
|---|---|---|---|
| GET | `/warehouses` · `/warehouses/{id}` | publik | |
| GET | `/inventory?query=&warehouseId=` | publik | Cari SKU/nama/RFID/barcode. |
| POST | `/inventory/{itemId}/movement` | staff | Body `{type, quantity, notes}`. |
| GET | `/packaging/recommend?length=&width=&height=` | publik | Rekomendasi box + berat volumetrik. |
| GET | `/couriers` · `/couriers/available` | login/publik | |
| POST | `/couriers/{id}/location` | kurir | |
| GET | `/couriers/{id}/route?date=` | kurir | Rute teroptimasi + penghematan. |
| POST | `/pickup` · GET `/pickup/pending` | login | |
| GET/POST | `/support/tickets` | login | |
| GET | `/lockers?city=` | publik | Diproyeksikan — PIN kompartemen tidak pernah ikut dikirim. |
| POST | `/lockers/{id}/assign` | kurir | Menitipkan paket, mengirim PIN. |
| POST | `/lockers/compartments/{id}/collect` | publik | Body `{pin}`. |

---

## Analytics

| Method | Path | Akses |
|---|---|---|
| GET | `/dashboard/revenue?days=` | login |
| GET | `/dashboard/delivery-volume?days=` | publik |
| GET | `/dashboard/status-breakdown?days=` | publik |
| GET | `/dashboard/sla?days=` | publik |
| GET | `/dashboard/snapshot` · `/dashboard/couriers` | login |
| GET | `/analytics/forecast?days=&city=` | login |
| GET | `/analytics/trend?months=` | login |
| GET | `/analytics/cost-insights` | login |
| GET | `/analytics/emissions?days=` | login |

---

## Integrasi & Mitra

| Method | Path | Akses |
|---|---|---|
| GET | `/integrations` | staff (Admin/Manager) |
| POST | `/integrations/{id}/sync` | staff (Admin/Manager) |
| GET | `/integrations/logs?integrationId=` | staff (Admin/Manager) |
| GET | `/partners` | publik |
| GET | `/partners/quotes?destination=&weight=&crossBorder=` | publik |
| POST | `/partners/handover` | staff |

---

## Kepatuhan & Loyalty

| Method | Path | Akses |
|---|---|---|
| GET | `/compliance/tax?period=` · `/compliance/tax/summary?months=` | staff (Admin/Manager) |
| GET/POST | `/compliance/customs` | staff |
| POST | `/compliance/customs/{id}/status` | staff |
| GET | `/loyalty/balance` | login |
| POST | `/loyalty/redeem` | login |

---

## Chat Bot

| Method | Path | Akses |
|---|---|---|
| POST/GET | `/chat/sessions` | login |
| GET/POST | `/chat/sessions/{id}/messages` | login |
| DELETE | `/chat/sessions/{id}` | login |
| GET | `/chat/functions` | publik |

```bash
curl -b cookie.txt -X POST http://localhost:5182/api/v1/chat/sessions/1/messages \
  -H "Content-Type: application/json" \
  -d '{"message":"Berapa ongkir Jakarta ke Bandung 2 kg?"}'
```

---

## Label & Sistem

| Method | Path | Akses | Keterangan |
|---|---|---|---|
| GET | `/labels/{trackingNumber}/qr` | publik | PNG QR code. |
| GET | `/labels/{trackingNumber}/barcode` | publik | SVG Code128. |
| GET | `/config` | staff (Admin/Manager) | Konfigurasi dari database. |
| PUT | `/config/{key}` | admin | |
| GET | `/health` | publik | Status database, storage, model AI. |

---

## Kode Status

| Kode | Arti |
|---|---|
| 200 / 201 | Berhasil |
| 400 | Input tidak valid |
| 401 | Belum login |
| 403 | Login tetapi tidak berhak |
| 404 | Data tidak ditemukan |
