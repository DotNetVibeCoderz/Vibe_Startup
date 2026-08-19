aplikasi manajemen Travel Haji/Umroh, fitur yang lengkap biasanya mencakup modul untuk jamaah, agen travel, operasional, serta integrasi eksternal. Berikut daftar fiturnya:

 🕌 Fitur untuk Jamaah
- Pendaftaran Online: Formulir digital dengan upload dokumen (KTP, paspor, KK, sertifikat vaksin).
- Pembayaran dan Cicilan: Integrasi payment gateway, opsi cicilan, notifikasi jatuh tempo.
- Informasi Paket: Detail paket (harga, fasilitas, hotel, maskapai, jadwal) bisa upload gambar brosur.
- Tracking Proses: Status dokumen, visa, keberangkatan, dan update real-time.
- Komunikasi Jamaah: Chat dengan admin/travel, forum jamaah, pengumuman, bisa attach gambar/file.
- Manajemen Perjalanan: Itinerary digital, lokasi hotel (ada opsi buka di map), jadwal ziarah, transportasi.
- Edukasi & Bimbingan: Materi manasik online, video tutorial, kuis interaktif.

 🛫 Fitur untuk Agen Travel
- Manajemen Paket: Buat, edit, delete dan publikasi paket Haji/Umroh.
- Manajemen Jamaah: Manage Data jamaah, status dokumen, riwayat pembayaran.
- Dokumentasi & Arsip: Upload dokumen jamaah, arsip digital, validasi.
- Integrasi Maskapai & Hotel: Booking tiket, reservasi hotel, update ketersediaan. (sementara input manual)
- Dashboard Operasional: Monitoring keberangkatan, status visa, laporan keuangan.
- Notifikasi & Reminder: Pengingat pembayaran, jadwal manasik, update keberangkatan.

 📊 Fitur untuk Admin/Operator
- Manajemen User: Master Data - Manage Hak akses untuk jamaah, agen, dan staf. CRUD, Export csv/excel, Filtering, Sorting, Paging.
- Laporan & Analitik: Statistik jamaah, keuangan, performa paket.
- Keamanan Data: backup, compliance regulasi.
- Integrasi Pemerintah: Sinkronisasi dengan SISKOHAT Kemenag untuk validasi data.

 🌐 Fitur Tambahan
- GPS Tracking: Tabel dan Map Lokasi jamaah saat perjalanan, bisa search, dan jika di klik, map menunjukan lokasi jamaah tersebut. Ada simulator jamaah juga yang bergerak di sekitar masjidil haram.
- Virtual Manasik: Link url ke simulasi manasik dengan VR/AR.
- Layanan Darurat: Tombol SOS (buat jamaah: bentuknya tombol SOS besar yang mudah di tekan bisa mengisi pesan juga - ada default messagenya, menyimpan longitude/latitude jamaah saat itu juga; buat admin: bentuknya panel berisi tabel notifikasi berisi informasi jamaah yang menekan tombol, waktu, lokasi dan pesannya, terlihat di map juga posisinya), kontak darurat (bisa di update), asuransi perjalanan (informasi asuransi).
- Marketplace Produk: Penjualan perlengkapan Haji/Umroh (koper, mukena, sajadah). Untuk jamaah: bisa memilih barang, masukan cart, dan bayar. untuk admin: melihat daftar order jamaah, menerima pembayaran, memberikan barang yang dibeli.
- Multi Bahasa: Bahasa Indonesia, Inggris.

---

Chat Bot Pelayanan Travel Haji/Umroh
- Nama 'Syeikh Jenggot'
- Chat Page dengan tampilan yang keren, multi session, reset session, bisa attach gambar (diupload lalu url-nya di jadikan image content) dan dokumen (di upload dan disertakan linknya ke text message).
- System Prompt (persona), temperature, model dan setting lainnya di simpan di appsetting
- Menggunakan Semantic Kernel Library dengan dukungan model: Open AI, Anthropic, Gemini, Ollama (bisa pilih)
- Tambahkan beberapa common functions (kernel functions) yang diperlukan termasuk query ke tavily (search internet), scrap page url, baca file dari url, cek Waktu dan tanggal, math calculation dan lainnya 
- Tambahkan functions untuk query data ke database yang dimiliki, untuk tahu berbagai informasi dari mulai persiapan, pembelian, perjalanan, kepulangan
- Bisa render chat thread dengan mark down dengan baik ke html (baik table, media (image, video, audio), code, dan lainnya dengan baik)
- Bisa attach gambar/dokumen nanti di upload ke storage, untuk gambar urlnya di input sebagai ImageContent dan selain gambar url-nya dimasukan ke text message 

---

Teknologi:
- Dibuat dengan Blazor Server .NET 10
- LLM dengan library semantic kernel support model Open AI, Anthropic, Gemini, Ollama
- Dokumentasi lengkap di folder docs
- Readme.md (English dan Indonesia)
- Optimasi kode agar aplikasi cepat dan ringan
- Buatkan sample data yang cukup banyak dan user sample
- Master Data bisa CRUD, Grid Filter, Sorting, Paging, Export CSV, Excel
- Storage support: FileSystem, AzureBlob, S3, MinIO
- Database support: SQLite, Sqlserver, MySQL, Postgre
- Konfigurasi di appsetting dan UI
- Desain UI dengan clean design yang indah, elegan dan modern, responsive mobile friendly, theme dark/light
---
