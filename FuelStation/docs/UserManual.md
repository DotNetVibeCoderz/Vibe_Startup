# 📖 FuelStation - Panduan Pengguna (User Manual)

## Daftar Isi
1. [Memulai](#1-memulai)
2. [Dashboard](#2-dashboard)
3. [POS Kasir](#3-pos-kasir)
4. [Master Data](#4-master-data)
5. [Transaksi](#5-transaksi)
6. [Laporan](#6-laporan)
7. [Marketplace Non-BBM](#7-marketplace-non-bbm)
8. [Manajemen Pelanggan](#8-manajemen-pelanggan)
9. [Shift & Attendance](#9-shift--attendance)
10. [IoT Monitor](#10-iot-monitor)
11. [Simulator](#11-simulator)
12. [Chat AI - Bang Jenggo](#12-chat-ai---bang-jenggo)
13. [Notifikasi](#13-notifikasi)

---

## 1. Memulai

### Login
Buka browser dan akses `https://localhost:5001`. Gunakan akun demo:

| Role | Email | Password | Akses |
|------|-------|----------|-------|
| Admin | admin@fuelstation.com | Admin123! | Semua fitur |
| Supervisor | supervisor@fuelstation.com | Super123! | Laporan, shift, operator |
| Operator | operator1@fuelstation.com | Oper123! | POS, transaksi |

### Navigasi
- **Sidebar kiri**: Menu utama (klik logo untuk collapse)
- **Top bar**: Judul halaman + jam real-time
- **Toggle tema**: ☀️ Light / 🌙 Dark di sidebar bawah

---

## 2. Dashboard

Halaman utama menampilkan ringkasan operasional:

| Widget | Keterangan |
|--------|-----------|
| **Stat Cards** | Transaksi hari ini, pendapatan, liter terjual, peringatan aktif |
| **Status Tangki** | Progress bar per tangki (hijau >40%, kuning 20-40%, merah <20%) |
| **Transaksi Terbaru** | 10 transaksi terakhir |
| **Grafik 7 Hari** | Bar chart penjualan mingguan |
| **Peringatan** | Emergency alerts aktif |

---

## 3. POS Kasir

Dioptimalkan untuk **layar sentuh**:

### Langkah Transaksi:
1. **Pilih Stasiun** dari dropdown
2. **Pilih Produk BBM** (tap kartu produk)
3. **Masukkan Liter** via keypad numerik atau preset (1L, 5L, 10L, 20L, 30L)
4. **Pilih Pembayaran**: Cash, QRIS, E-Wallet, Debit, Kredit, Transfer
5. **Tap BAYAR** ✅
6. **Cetak Struk** 🖨️ (muncul preview, lalu print)

### Tombol Keypad:
- `⌫` Hapus 1 digit
- `C` Clear semua
- `5L` / `10L` Quick preset

---

## 4. Master Data

Akses: **📋 Master Data** di sidebar

### Tab:
| Tab | Isi |
|-----|-----|
| ⛽ Produk BBM | Nama, kode, jenis, oktan, harga/liter |
| 🏪 Stasiun | Daftar SPBU dengan alamat & kontak |
| 🛢️ Tangki | Kapasitas, sisa volume, status |
| 🛍️ Non-BBM | Produk marketplace (oli, minuman, dll) |
| 👤 Karyawan | Daftar operator & supervisor |

### Fitur:
- 🔍 **Search** real-time
- 📊 **Column sort** (klik header)
- 📄 **Pagination** (← →)
- 📥 **Export CSV**

---

## 5. Transaksi

Akses: **🧾 Transaksi** di sidebar

Melihat riwayat semua transaksi dengan filter:
- **Tanggal** (dari - sampai)
- **Metode pembayaran**
- **Stasiun**
- **Nomor transaksi**

Fitur:
- Summary stat cards
- Export CSV
- Detail per transaksi

---

## 6. Laporan

Akses: **📈 Laporan** di sidebar

### Periode:
- 📅 **Harian** - 1 hari
- 📆 **Mingguan** - 7 hari
- 🗓️ **Bulanan** - 1 bulan

### Tampilan:
- **Summary**: Total transaksi, pendapatan, biaya, profit
- **Grafik Revenue**: Bar chart
- **Payment Breakdown**: Pie-style dengan progress bar
- **Tabel Detail**: 50 transaksi teratas

### Export:
- 📥 CSV export

---

## 7. Marketplace Non-BBM

Akses: **🛒 Marketplace** di sidebar

### Katalog:
- Cari produk dengan search bar
- Tap kartu produk untuk tambah ke keranjang

### Keranjang:
- +/- untuk atur quantity
- ✕ untuk hapus item
- Pilih metode pembayaran
- **Checkout** untuk proses order

---

## 8. Manajemen Pelanggan

Akses: **👥 Pelanggan** di sidebar

### Fitur:
- Statistik: Total pelanggan, Platinum, Gold, poin loyalty
- Search by nama, member code, phone
- Membership tiers: Regular, Silver, Gold, Platinum
- Feedback & Rating (bintang 1-5)

---

## 9. Shift & Attendance

### Shift Management (/shifts)
- Kalender mingguan operator × 7 hari
- Tiga shift: ☀️ Morning (06-14), 🌤️ Afternoon (14-22), 🌙 Night (22-06)
- Tambah/edit/hapus shift
- Filter per stasiun

### Attendance (/attendance)
- Check-in / Check-out per operator
- Status otomatis: Present ✅, Late ⚠️, Absent ❌, Overtime 🔥
- Log absensi dengan filter tanggal
- Export CSV

---

## 10. IoT Monitor

Akses: **📡 IoT Monitor** di sidebar

### Visualisasi:
- **Tank level** dengan animasi
- Threshold line (batas minimum)
- Info suhu & tekanan
- Status leak detection

### Emergency Alerts:
- Daftar alert (Fire, Leak, Warning, Critical)
- Tombol ✅ Resolve untuk menutup alert
- Riwayat pembacaan sensor (chart)

---

## 11. Simulator

Akses: **🚗 Simulator** di sidebar

### Kontrol:
- ▶️ Start / ⏹️ Stop simulator
- 🔥 Stress Test (atur concurrent + total orders)
- 🗑️ Clear log / Clear vehicles

### Monitor:
- Kendaraan aktif (plat, BBM, liter, status)
- Simulation log real-time
- Benchmark hasil stress test

---

## 12. Chat AI - Bang Jenggo

Akses: **💬 Chat AI** di sidebar

### Fitur:
- Chat multi-session dengan streaming response
- Pilih model AI (GPT-4o, Claude, Gemini, Llama)
- Upload gambar & dokumen
- Markdown rendering (tabel, code block, dll)
- Reset session untuk chat baru
- Quick-ask buttons untuk pertanyaan umum

### Contoh pertanyaan:
- "Harga BBM hari ini?"
- "Cek poin loyalitas 0812-3456-7890"
- "Penjualan hari ini gimana?"
- "Stok tangki aman?"

---

## 13. Notifikasi

Akses: **🔔 Notifikasi** di sidebar

### Tipe:
- 🎉 **Promo** - Info diskon & promo
- 🧾 **Transaction** - Notifikasi transaksi
- ⚙️ **System** - Info sistem
- 🚨 **Alert** - Peringatan darurat

### Fitur:
- Filter per tipe
- Mark as read / Mark all read
- Real-time toast notification
- Badge counter di sidebar

---

*FuelStation User Manual v1.0 — Gravicode Studios*
