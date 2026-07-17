aplikasi manajemen laundry yang bisa mencakup kebutuhan pemilik, admin operasional, kurir, dan pelanggan. fiturnya:

---

 🏢 Fitur untuk Pemilik & Admin
- Dashboard Laundry: Ringkasan order masuk, status pengerjaan, pemasukan, dan pengeluaran.
- Manajemen Order: Input order, tracking status (diterima, dicuci, disetrika, selesai, dikirim).
- Manajemen Pelanggan: Data pelanggan, riwayat transaksi, preferensi layanan.
- Keuangan & Tagihan: Pembuatan invoice otomatis, integrasi e-wallet/bank transfer.
- Laporan Keuangan: Grafik pemasukan, pengeluaran, profit, piutang.
- Inventaris & Stok: Catatan bahan (detergen, pewangi, plastik), status stok, peringatan habis.
- Manajemen Staff: Data pegawai, jadwal kerja, gaji, performa.

---

 👤 Fitur untuk Pelanggan
- Pendaftaran Online: Registrasi akun, input data, preferensi layanan.
- Auth: Login, Logout, Reset Password, User Profile
- Order Online: Pemesanan layanan (cuci kering, setrika, express, kiloan).
- Pembayaran Online: Integrasi dengan OVO, GoPay, Dana, QRIS, VA bank.
- Tracking Order: Status pengerjaan real-time, estimasi selesai.
- Notifikasi Digital: Reminder order selesai, promo, pengumuman.
- Layanan Komplain: Form pengaduan, tracking status komplain.
- Loyalty & Membership: Poin reward, diskon member, paket langganan.

---

 🚚 Fitur Operasional & Kurir
- Manajemen Pickup & Delivery: Jadwal penjemputan/pengantaran, integrasi GPS untuk tracking, ada simulator GPS juga.
- Tracking Kurir: Lokasi kurir real-time, estimasi waktu tiba.
- Integrasi IoT: Sensor mesin cuci, monitoring listrik/air. tambahkan simulator IoT bisa di start/stop di thread berbeda. 
- Autentikasi & Role: Login pemilik, admin, kurir, pelanggan dengan hak akses berbeda.
---

 🌟 Fitur Kompetitif & Tambahan
- Marketplace Laundry: Listing layanan di aplikasi publik (mirip GoLaundry).
- Rating & Review: Pelanggan bisa memberi feedback.
- Analitik Tren: Prediksi permintaan, tren layanan favorit.
- Multi Cabang Management: Satu akun pemilik bisa kelola banyak outlet.
- Integrasi Pajak: Hitung PPh, laporan pajak otomatis.
- UI Dark/Light: Tampilan modern, responsif, mobile-friendly seperti facebook dengan nuansa warna ungu keren  

---

Chat Bot Pelayanan Informasi
- Nama 'Mbok Inem'
- Chat Page dengan tampilan yang keren, multi session (create/delete), reset session, bisa attach gambar (diupload lalu url-nya di jadikan image content) dan dokumen (di upload dan disertakan linknya ke text message).
- System Prompt (persona), temperature, model dan setting lainnya di simpan di appsetting
- Menggunakan Semantic Kernel Library dengan dukungan model: Open AI, Anthropic, Gemini, Ollama (bisa pilih)
- Tambahkan beberapa common functions (kernel functions) yang diperlukan termasuk query ke tavily (search internet), scrap page url, baca file dari url, cek tanggal, Waktu, math calculation, dan beberapa function yang diperlukan lainnya 
- Tambahkan functions untuk query data ke database yang dimiliki untuk mengetahui berbagai informasi
- Bisa render chat thread dengan mark down dengan baik ke html (baik table, media (image, video, audio), code, dan lainnya dengan baik)

---

Lainnya:
- Tambahkan dokumentasi lengkap di folder docs
- Buatkan banyak sample data dan user
- Tambahkan readme.md (English, Indonesia)
- Buat dengan Blazor Server dengan .NET 10
- optimasi kode agar aplikasi cepat dan ringan
- storage support: FileSystem, AzureBlob, S3, MinIO
- REST API: Integrasi dengan marketplace atau aplikasi eksternal dengan Min API dan swagger
- Database Fleksibel: Support SQLite, PostgreSQL, SQL Server.