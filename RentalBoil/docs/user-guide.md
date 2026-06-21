# 👤 Panduan Pengguna

## Customer

### Pendaftaran & Login

1. Buka **Daftar** di sidebar atau halaman utama
2. Isi nama lengkap, email, nomor HP, password
3. Pilih role: Customer atau Partner
4. Klik **Daftar**
5. Login dengan email & password yang sudah didaftarkan

**Demo account**: `customer1@rentalboil.com` / `Customer123!`

### Mencari Kendaraan

1. Buka **Cari Kendaraan** di sidebar
2. Gunakan filter:
   - **Jenis**: Mobil atau Motor
   - **Merek**: Toyota, Honda, Yamaha, dll
   - **Transmisi**: Automatic atau Manual
   - **Kapasitas**: Minimal penumpang
   - **Harga**: Range harga per hari
3. Klik kendaraan untuk melihat detail

### Booking Kendaraan

1. Di halaman detail kendaraan, klik **Sewa Sekarang**
2. Pilih **tanggal mulai** dan **tanggal selesai**
3. (Opsional) Masukkan **kode kupon**
4. Lihat estimasi biaya
5. Klik **Konfirmasi Booking**

### Pembayaran

1. Buka **Pesanan Saya** → klik **Bayar** pada booking
2. Pilih metode pembayaran:
   - 🏦 Transfer Bank (BCA, Mandiri, BNI, BRI)
   - 📱 E-Wallet (GoPay, OVO, Dana)
   - 📲 QRIS
   - 💳 Kartu Kredit
3. Masukkan **ID transaksi** dari bank/e-wallet
4. Klik **Konfirmasi Pembayaran**

> ⚠️ Pembayaran ini simulasi. Akan langsung dikonfirmasi.

### Mengambil Kendaraan

1. Setelah bayar & partner konfirmasi, status jadi **Confirmed**
2. Klik **Ambil Kendaraan** di halaman Pesanan Saya
3. GPS tracking otomatis dimulai
4. Kunci kendaraan otomatis terbuka

### Mengembalikan Kendaraan

1. Buka **Pesanan Saya** → klik **Kembalikan**
2. GPS tracking berhenti
3. Kunci kendaraan otomatis terkunci
4. Poin loyalty ditambahkan

### Memberi Ulasan

1. Setelah mengembalikan, klik **⭐ Ulasan**
2. Pilih rating (1-5 bintang)
3. Tulis komentar (opsional)
4. Klik **Kirim Ulasan**

### GPS Tracking

1. Buka **GPS Tracking** di sidebar
2. Lihat posisi real-time kendaraan yang sedang disewa
3. Auto-refresh setiap 5 detik
4. Kontrol IoT: start/stop tracking, toggle mesin, toggle kunci

### Chat Bot (Bang Tony Brewok)

1. Buka **Bang Tony Brewok** di sidebar
2. Tanyakan apa saja:
   - "Cari mobil murah di Jakarta"
   - "Booking kendaraan nomor 3 untuk 2 hari"
   - "Ada promo apa hari ini?"
   - "Cek posisi mobil saya"

---

## Partner (Pemilik Kendaraan)

**Demo account**: `partner1@rentalboil.com` / `Partner123!`

### Dashboard

Buka **Dashboard** untuk melihat:
- Total booking & pendapatan
- Booking aktif & pending
- Grafik pendapatan 7 hari
- Performa kendaraan

### Manajemen Kendaraan

1. Buka **Kendaraan Saya**
2. **Tambah**: Klik ➕, isi form, simpan
3. **Edit**: Klik ✏️ pada kendaraan
4. **Toggle**: Klik 🔒/🔓 untuk ubah ketersediaan
5. **Hapus**: Klik 🗑️ (konfirmasi dialog)
6. **Search**: Filter nama, merek, tipe, status

### Pesanan Masuk

Di dashboard, bagian **Pesanan Masuk**:
- Lihat daftar booking pending
- Klik ✅ **Terima** untuk konfirmasi
- Customer akan dapat notifikasi

### Laporan

Buka **Laporan**:
- Filter per hari/minggu/bulan/tahun
- Sort by tanggal atau jumlah
- Export CSV / Excel
- Lihat summary: total sewa, revenue, rata-rata, total hari

---

## Admin

**Demo account**: `admin@rentalboil.com` / `Admin123!`

### Dashboard

- 8 KPI cards (total booking, revenue, user, vehicle, dll)
- Grafik pendapatan 30 hari
- Top 8 kendaraan terpopuler
- Revenue per metode pembayaran
- Verifikasi pending

### Verifikasi Kendaraan

Di dashboard admin, bagian **Verifikasi Pending**:
1. Lihat kendaraan yang belum diverifikasi
2. Klik ✅ untuk verifikasi
3. Kendaraan akan muncul di pencarian customer

### Peta Semua Kendaraan

Buka **Peta Kendaraan** untuk melihat:
- Semua kendaraan yang sedang disewa dalam satu peta
- Warna hijau = bergerak, kuning = berhenti
- Popup: nama, plat, kecepatan, status IoT, customer
- Tabel lengkap di bawah peta
- Link ke detail per kendaraan

### Laporan

Sama seperti Partner, tapi bisa lihat semua data.

---

## Notifikasi

### Melihat Notifikasi

1. Klik ikon 🔔 di topbar (dengan badge merah jumlah unread)
2. Atau buka `/notifications`

### Fitur Notifikasi

| Fitur | Deskripsi |
|-------|-----------|
| 🔵 **Dot biru** | Notifikasi belum dibaca |
| 📄 **Klik** | Tandai sebagai dibaca |
| 🔗 **Buka →** | Link ke halaman terkait |
| ✅ **Tandai Semua** | Tandai semua sebagai dibaca |
| 📑 **Filter** | Semua / Belum Dibaca |

### Kapan Notifikasi Muncul?

- Booking baru → notifikasi ke partner
- Booking dikonfirmasi → notifikasi ke customer
- Booking selesai → notifikasi ke customer (dengan link review)
- Pembayaran → notifikasi ke customer
