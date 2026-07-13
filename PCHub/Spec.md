nama: PCHub

deskripsi:
aplikasi Rental PC/Game Center dengan arsitektur server berbasis web (Blazor) untuk admin dan client app (WPF) di PC pengguna, berikut adalah fiturnya:

---

 🔹 Fitur Admin Web (Blazor Server)
- Dashboard Analitik: Statistik penggunaan PC, pendapatan harian/bulanan, tren game populer.  
- Manajemen PC: Tambah/edit status PC (aktif, rusak, maintenance).  
- Manajemen User: Registrasi, login, membership, paket langganan.  
- Billing & Pembayaran: Hitung otomatis biaya berdasarkan waktu, integrasi e-wallet/bank transfer.  
- Reservasi Online: Booking PC sebelum datang, pilih durasi dan game.  
- Manajemen Game: Daftar game, lisensi, update otomatis.  
- Promo & Membership: Diskon, paket VIP, loyalty points.  
- Laporan Keuangan: Export ke Excel/PDF, integrasi akuntansi.  
- Notifikasi: Email/WhatsApp untuk booking, promo, reminder.  
- Multi Database Support: SQLite, SQL Server, PostgreSQL, MySQL.  
- Storage Support: FileSystem, Azure Blob, S3, MinIO untuk data log & screenshot.
- Konfigurasi di appsettings dan UI
- Dengan tampilan yang modern, responsive dengan style seperti facebook + neobrutalism dengan dukungan theme dark/light  
- Auth: register user, login, logout, reset password, user profile
- Api dokumentasi dengan swagger
- Tambahkan banyak sample data dan user 
- Buatkan Master data yang diperlukan di lengkapi fitur CRUD, Export CSV & Excel, Column Filter, Column Sort, Paging

Chat Bot Pelayanan Informasi
- Nama 'Koh Dedi'
- Chat Page dengan tampilan yang keren, multi session (create/delete), reset session, bisa attach gambar (diupload lalu url-nya di jadikan image content) dan dokumen (di upload dan disertakan linknya ke text message).
- System Prompt (persona), temperature, model dan setting lainnya di simpan di appsetting
- Menggunakan Semantic Kernel Library dengan dukungan model: Open AI, Anthropic, Gemini, Ollama (bisa pilih)
- Tambahkan beberapa common functions (kernel functions) yang diperlukan termasuk query ke tavily (search internet), scrap page url, baca file dari url, cek tanggal, Waktu, math calculation, dan beberapa function yang diperlukan lainnya 
- Tambahkan functions untuk query data ke database yang dimiliki untuk mengetahui berbagai informasi
- Bisa render chat thread dengan mark down dengan baik ke html (baik table, media (image, video, audio), code, dan lainnya dengan baik)

---

 🔹 Fitur Client (WPF)
- Login & Auth: User login dengan akun/member ID.  
- Timer Otomatis: Hitung durasi pemakaian, auto lock saat habis.  
- Game Launcher: UI modern untuk memilih dan menjalankan game.  
- Chat & Support: Chat dengan admin, request bantuan.  
- Notifikasi Pop-up: Reminder waktu habis, promo, event.  
- Integrasi Billing: Sinkron dengan server untuk biaya real-time.  
- Screen Lock: Lock otomatis saat waktu habis atau user logout.  
- UI Modern Responsive: Desain ala neo-brutalism soft, support light/dark mode.  
- Update Client Otomatis: Auto-update aplikasi dari server.  
- Monitoring Resource: CPU, GPU, RAM usage per PC.  
- Multi-Language Support: Bahasa Indonesia & Inggris.
- Konfigurasi di simpan di UI dan App.config
---

 🔹 Integrasi & Ekstra
- API Minimal: REST/GRPC untuk komunikasi server-client.  
- Security: enkripsi komunikasi, role-based access.  
- Cloud Sync: Backup otomatis ke cloud.  
- Event & Tournament: Modul untuk kompetisi internal.  
- IoT Integration: Smart lamp/AC otomatis saat PC aktif.  

---

Lainnya:
- Tambahkan dokumentasi lengkap di folder docs
- Tambahkan readme.md (English, Indonesia)
- Buat dengan Blazor Server, WPF dengan .NET 10
- optimasi kode agar aplikasi cepat dan ringan