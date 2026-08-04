# Screenshot

Semua gambar di folder ini adalah **tangkapan layar aplikasi yang benar-benar berjalan**,
bukan mockup. Angka yang terlihat berasal dari data contoh ditambah lalu lintas yang
dihasilkan simulator.

| Berkas | Isi |
|--------|-----|
| `admin-signin.png` | Layar masuk konsol |
| `admin-dashboard.png` | Ringkasan operasi (tema gelap) |
| `admin-dashboard-light.png` | Ringkasan operasi (tema terang) |
| `admin-orders.png` | Daftar order + filter |
| `admin-drivers.png` | Direktori driver, termasuk umur sinyal GPS |
| `admin-payments.png` | Daftar pembayaran |
| `admin-reports.png` | Laporan keuangan |
| `admin-fares.png` | Tabel tarif |
| `admin-promos.png` | Manajemen promo |
| `admin-payment-providers.png` | Konfigurasi gateway pembayaran |
| `admin-verification.png` | Antrean verifikasi dokumen driver |
| `rider-signin.png` | Masuk penumpang |
| `rider-home.png` | Beranda penumpang |
| `rider-book.png` | Pemesanan, harga per kategori dari tabel tarif |
| `rider-track.png` | Trip selesai dengan tagihan terbuka |
| `rider-pay-methods.png` | Pemilihan metode pembayaran |
| `rider-pay-qris.png` | QRIS siap dipindai |
| `rider-pay-done.png` | Pembayaran lunas |

---

## Menghasilkan ulang

Diambil pada resolusi 2× (konsol 1440×900 logis) agar tetap tajam di layar retina.

### Konsol admin

Dijalankan lewat Playwright, yang benar-benar masuk memakai akun admin bawaan lalu
menelusuri tiap halaman:

```bash
# 1. API dan konsol harus hidup
dotnet run --project FastRide.Api --no-launch-profile --urls "http://localhost:5000"
ApiSettings__BaseUrl=http://localhost:5000 \
  dotnet run --project FastRide.AdminWeb --no-launch-profile --urls "http://localhost:5003"

# 2. Isi dengan lalu lintas nyata supaya panelnya tidak kosong
dotnet run --project FastRide.Simulator -- --url http://localhost:5000 --riders 8 --drivers 5 --duration 40

# 3. Tangkap
node scripts/screenshots.mjs
```

### Aplikasi penumpang

Aplikasi MAUI dijalankan sebagai *unpackaged* Windows app dan jendelanya ditangkap dengan
`PrintWindow(PW_RENDERFULLCONTENT)` — `BitBlt` biasa tidak bisa membaca isi WebView2.

```bash
dotnet run --project FastRide.RiderApp -f net10.0-windows10.0.19041.0 -p:WindowsPackageType=None
```

`-p:WindowsPackageType=None` hanya override baris perintah; berkas proyek tidak diubah.
API harus berjalan di `https://localhost:5001`, karena itu alamat yang dipakai aplikasi di
luar Android.

---

## Catatan

- Aplikasi **driver** belum difoto. Alurnya sudah tercakup test dan simulator, tetapi
  belum ada tangkapan layarnya.
- Nominal, nama, dan kode order berasal dari data contoh — bukan data nyata siapa pun.
- Kredensial yang terlihat di layar masuk adalah akun demo yang memang didokumentasikan
  (`Password123`), bukan rahasia.
