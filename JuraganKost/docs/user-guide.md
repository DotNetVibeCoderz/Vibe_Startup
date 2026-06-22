# 📖 Panduan Pengguna

Dokumen ini menjelaskan cara menggunakan aplikasi JuraganKost untuk setiap role pengguna.

---

## Akun Demo

| Role | Email | Password |
|---|---|---|
| Super Admin | `superadmin@juragankost.com` | `Admin123!` |
| Pemilik | `pemilik@juragankost.com` | `Pemilik123!` |
| Admin | `admin@juragankost.com` | `Admin123!` |
| Penghuni 1 | `penghuni1@juragankost.com` | `Penghuni123!` |
| Penghuni 2-9 | `penghuni2@juragankost.com` ... | `Penghuni123!` |

---

## 👑 Pemilik / Admin

### Dashboard
1. Login sebagai pemilik/admin
2. Halaman utama menampilkan **Dashboard** dengan ringkasan:
   - Total kamar, okupansi, pemasukan, piutang
   - Progress bar okupansi
   - Aksi cepat ke menu utama

### Manajemen Kamar (`/kamar`)
- **Lihat** semua kamar dengan filter (kost, status, search)
- **Tambah** kamar baru: nomor, kost, jenis, harga, deposit, fasilitas
- **Edit** kamar: klik ✏️
- **Hapus** kamar: klik 🗑️
- **Export** CSV/Excel

### Manajemen Penghuni (`/penghuni`)
- Lihat daftar penghuni dengan kamar
- Klik 👁️ untuk detail (NIK, kontak darurat, dll)
- Tambah/edit/hapus penghuni

### Tagihan & Pembayaran
- `/tagihan` — Generate tagihan bulanan otomatis
- `/pembayaran` — Verifikasi pembayaran (✅/❌)

### Komplain (`/komplain`)
- Lihat semua komplain dari penghuni
- Update status: Menunggu → Diproses → Selesai
- Tambah respon untuk penghuni

### Inventaris & Staff
- `/inventaris` — Catat barang (kasur, AC, meja) + status
- `/staff` — Data penjaga, cleaning service, gaji

### IoT Monitor (`/iot`)
- Pilih kost → lihat data sensor simulasi
- Klik "Simulasi Sensor" untuk generate data

### Laporan (`/laporan`)
- Ringkasan pemasukan, profit, piutang per kost

---

## 👤 Penghuni

### Dashboard
- Lihat ringkasan tagihan & status

### Tagihan Saya (`/tagihan`)
- Lihat tagihan aktif & riwayat

### Komplain (`/komplain`)
- **Buat komplain baru:** pilih kategori, isi judul & deskripsi
- **Tracking status:** lihat status & respon admin

### Marketplace (`/marketplace`)
- **Cari kost:** lihat listing kost publik
- **Detail:** klik 👁️ — lihat kamar tersedia & review
- **Booking:** klik 📝 Booking (redirect ke login)
- **Review:** ⭐ 1-5 + emoji + komentar (setelah login sebagai penghuni)

### Mpok Inem (`/chat`)
- Tanya apa saja ke AI chat bot
- Klik quick buttons atau ketik pertanyaan
- **Upload file:** klik 📎 untuk lampirkan gambar/dokumen
- **Multi-session:** buat sesi baru, switch antar sesi, hapus sesi

---

## 🔧 Tips

- **Dark/Light mode:** klik ☀️/🌙 di sidebar footer
- **Collapse sidebar:** klik ☰ di topbar
- **Currency:** semua nominal dalam Rupiah (`Rp1.500.000`)
- **Responsive:** tampilan mobile-friendly (sidebar auto-collapse)
