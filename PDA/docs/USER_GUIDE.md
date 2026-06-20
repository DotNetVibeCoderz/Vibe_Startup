# 👤 User Guide

Panduan lengkap penggunaan aplikasi PDA untuk analisis data.

---

## Daftar Isi
1. [Login & Register](#1-login--register)
2. [Dashboard Home](#2-dashboard-home)
3. [Database Connections](#3-database-connections)
4. [Chat with Data](#4-chat-with-data)
5. [Dashboard & Report](#5-dashboard--report)
6. [Knowledge Base (RAG)](#6-knowledge-base-rag)
7. [Monitoring Dashboard](#7-monitoring-dashboard)
8. [Audit Logs](#8-audit-logs)
9. [User Profile](#9-user-profile)
10. [Tips & Best Practices](#10-tips--best-practices)

---

## 1. Login & Register

### Login
1. Buka halaman `/login`
2. Masukkan email dan password
3. Klik **🔑 Login**
4. Centang "Remember me" untuk tetap login

### Register
1. Buka halaman `/register`
2. Isi nama lengkap, email, password
3. Password minimal 8 karakter dengan huruf besar, kecil, dan angka
4. Klik **📝 Register**

### Lupa Password
1. Klik "Forgot password?" di halaman login
2. Masukkan email terdaftar
3. Cek email untuk link reset password

---

## 2. Dashboard Home

Halaman utama menampilkan:
- **Quick Stats**: Active users, queries, chats, tokens
- **Feature Cards**: Akses cepat ke fitur utama
- **Quick Start Guide**: 3 langkah memulai

---

## 3. Database Connections

### Menambah Koneksi
1. Buka `/connections`
2. Klik **➕ Tambah Koneksi**
3. Isi form:
   - **Nama Koneksi**: Nama untuk identifikasi
   - **Deskripsi**: Keterangan opsional
   - **Tipe Database**: Pilih SQLite, SQLServer, PostgreSQL, dll
   - **Connection String**: String koneksi database

### Tipe Database yang Didukung

| Tipe | Format Connection String |
|------|-------------------------|
| **SQLite** | `Data Source=mydb.db` |
| **SQL Server** | `Server=localhost;Database=mydb;Trusted_Connection=true;TrustServerCertificate=true` |
| **PostgreSQL** | `Host=localhost;Database=mydb;Username=postgres;Password=mypass` |

### Testing Koneksi
1. Klik **🔍 Test** pada koneksi
2. Lihat hasil: ✅ Success atau ❌ Failed
3. Jika sukses, koneksi siap digunakan

### Mengelola Koneksi
- **Edit**: Klik ✏️ Edit untuk mengubah konfigurasi
- **Delete**: Klik 🗑️ Delete untuk menghapus permanen

---

## 4. Chat with Data

Ini adalah fitur **utama** PDA!

### Memulai Chat
1. Buka `/chat`
2. Klik **✨ Buat Sesi Baru**
3. Pilih database dari dropdown **Database**
4. Mulai bertanya!

### Menulis Pertanyaan Efektif

#### Contoh pertanyaan bagus:
```
✅ "Tampilkan 10 pelanggan dengan total pembelian tertinggi"
✅ "Berapa total penjualan per bulan dalam 6 bulan terakhir?"
✅ "Bandingkan performa penjualan antara kategori A dan B"
✅ "Buatkan dashboard ringkasan penjualan"
```

#### Contoh pertanyaan kurang efektif:
```
❌ "Data" (terlalu umum)
❌ "Tampilkan semua" (bisa terlalu besar)
❌ "Apa aja" (tidak spesifik)
```

### Fitur Chat

| Fitur | Keterangan |
|-------|------------|
| **Multi-Session** | Banyak sesi chat paralel |
| **Sample Prompts** | Klik prompt contoh untuk mengisi otomatis |
| **Model Selector** | Pilih LLM provider dan model |
| **Temperature** | Atur kreativitas respons (0=presisi, 1=kreatif) |
| **Reset Session** | 🔄 Hapus semua pesan |
| **New Chat** | ✨ Buat sesi baru |
| **Upload File** | 📎 Upload gambar/dokumen |
| **Voice Chat** | 🎤 Mockup (coming soon) |

### Memahami Respons

Respons AI berisi:
- **Markdown text**: Penjelasan analisis
- **Tabel data**: Hasil query dalam format tabel
- **Dashboard HTML**: Visualisasi interaktif
- **Statistik**: Token usage dan response time

---

## 5. Dashboard & Report

Ketika AI membuat dashboard, akan muncul panel khusus:

### Dashboard Panel
- **Expand/Collapse**: Klik header untuk buka/tutup
- **📸 Export Image**: Export dashboard sebagai gambar
- **📄 Export CSV**: Export data sebagai CSV

### Jenis Visualisasi yang Didukung
- Tabel data (basic to advanced grid)
- Bar charts, line charts, pie charts
- Multi-tab reports
- Filter interaktif

> **💡 Tip:** Minta "Buatkan dashboard..." untuk mendapatkan visualisasi

---

## 6. Knowledge Base (RAG)

### Cara Kerja
1. Letakkan dokumen di folder `KnowledgeBase/`
2. System otomatis scan setiap 30 menit
3. Dokumen di-index ke vector store
4. AI bisa mencari informasi dari dokumen

### Format yang Didukung
- PDF (.pdf)
- Word (.docx, .doc)
- Excel (.xlsx, .xls)
- PowerPoint (.pptx)
- Text (.txt, .md)
- CSV (.csv)
- HTML (.html, .htm)

### Melihat Status Index
1. Buka `/rag-index`
2. Lihat daftar dokumen yang sudah di-index
3. Informasi: nama file, ukuran, chunks, tanggal index, keyword

### Mencari di Knowledge Base
AI otomatis menggunakan `searchKnowledgeBase` tool saat relevan. Atau minta secara eksplisit:
```
"Cari informasi tentang laporan Q4 di knowledge base"
```

---

## 7. Monitoring Dashboard

**(Admin Only)**

Halaman `/monitoring` menampilkan:

### Key Metrics
- Active users (15 menit terakhir)
- Chats per hour
- Queries per hour
- Token usage per hour

### Detail Metrics
- Total requests & traffic
- Total tokens & penggunaan
- Average tokens per chat

### Active Users
- List user yang aktif dalam 15 menit terakhir
- System info (timestamp, version, environment)

---

## 8. Audit Logs

**(Admin Only)**

Halaman `/audit-logs` menampilkan:

### Data yang Tercatat
- **Timestamp**: Waktu aktivitas
- **User**: Pengguna yang melakukan
- **Category**: Auth, Chat, Query, Database, RAG, dll
- **Action**: Login, MessageProcessed, SQLExecuted, dll
- **Description**: Detail aktivitas
- **Duration**: Waktu eksekusi (ms)
- **Status**: ✅ OK atau ❌ Error

### Fitur Table
- **Sort**: Klik header kolom (Timestamp, Category, Duration)
- **Pagination**: Prev/Next untuk navigasi

---

## 9. User Profile

Halaman `/profile` untuk mengelola akun:

### Informasi Profil
- Nama lengkap
- Email (read-only)
- Tema (Dark/Light)

### Ganti Password
- Password saat ini
- Password baru (min. 8 karakter)
- Konfirmasi password

### Statistik Akun
- Member sejak
- Last login
- Role

---

## 10. Tips & Best Practices

### 💡 Tips Analisis Data
1. **Mulai dengan pertanyaan sederhana** lalu bertahap ke kompleks
2. **Spesifik** - sebutkan tabel, kolom, atau metrik yang dimaksud
3. **Gunakan sample prompts** sebagai inspirasi
4. **Cek schema** - AI diberi context schema database
5. **Minta dashboard** untuk visualisasi yang lebih baik

### ⚡ Performa
- Gunakan temperature rendah (0.1-0.3) untuk query akurat
- Gunakan temperature tinggi (0.7-1.0) untuk analisis kreatif
- Batasi hasil query dengan LIMIT jika data besar
- Index dokumen yang relevan saja di KnowledgeBase

### 🔒 Keamanan
- Semua query SQL bersifat **read-only** (SELECT only)
- User harus login untuk semua fitur
- Admin role untuk akses monitoring & audit
- API keys disimpan di server (tidak expose ke client)

---

> *Selamat menganalisis data! 🚀*
