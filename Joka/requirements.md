aplikasi Online Travel Agent (OTA) terinspirasi dari Tiket.com dan Traveloka. Fiturnya:

---

 🛫 Core Travel Features
- Tiket Pesawat: Pencarian, pemesanan, multi-maskapai, filter harga/jadwal.  
- Tiket Kereta Api: Integrasi langsung dengan KAI, seat selection.  
- Tiket Bus & Shuttle: Rute antar kota, jadwal real-time.  
- Hotel & Akomodasi: Booking hotel, villa, apartemen, dengan review & rating.  
- Rental Mobil: Pilihan mobil, driver optional, durasi fleksibel.  
- Aktivitas & Event: Tiket konser, atraksi wisata, paket tour.  

---

 💳 Payment & Financial Features
- Multi Payment: Transfer bank, e-wallet, kartu kredit, QRIS.  
- PayLater: Cicilan tanpa kartu kredit, integrasi fintech.  
- Promo & Cashback: Diskon musiman, kode voucher, loyalty points.  
- Asuransi Perjalanan: Proteksi keterlambatan, pembatalan, kesehatan.  

---

 📊 User Experience & Utility
- Multi-language: Bahasa Indonesia, Inggris, dll.  
- Multi-currency: Konversi otomatis sesuai lokasi pengguna.  
- Wishlist & Favorit: Simpan hotel/tiket untuk dibeli nanti.  
- Notifikasi Real-time: Reminder jadwal, gate info, promo.  
- E-ticket & QR Code: Tiket digital langsung di aplikasi.  

---

 🔧 Advanced & Lifestyle Features
- Travel Package: Bundling tiket + hotel + aktivitas.  
- Membership & Loyalty: My Points, Tiket Elite.  
- Trip Planner: Itinerary otomatis berdasarkan booking.  
- Customer Support 24/7: Chatbot (lihat di Bawah) + live agent.  
- Integrasi Transportasi Lokal: Ojek online, airport transfer.  

---

Chat Bot Pelayanan Informasi
  - Nama 'Mas Bolang'
  - Chat Page dengan tampilan yang keren, multi session (create/delete), reset session, bisa attach gambar (diupload lalu url-nya di jadikan image content) dan dokumen (di upload dan disertakan linknya ke text message).
  - System Prompt (persona), temperature, model dan setting lainnya di simpan di appsetting
  - Menggunakan Semantic Kernel Library dengan dukungan model: Open AI, Anthropic, Gemini, Ollama (bisa pilih)
  - Tambahkan beberapa common functions (kernel functions) yang diperlukan termasuk query ke tavily (search internet), scrap page url, baca file dari url, cek tanggal, Waktu, math calculation, dan beberapa function yang diperlukan lainnya
  - Tambahkan functions untuk query data ke database untuk mengetahui berbagai informasi dan fungsi-fungsi yang dimiliki aplikasi
  - Bisa render chat thread dengan mark down dengan baik ke html (baik table, media (image, video, audio), code, dan lainnya dengan baik)

---

Notes:

- Dibuat dengan blazor server, .NET 10, D3JS, dengan desain fresh, fun dan modern kombinasi (Minimalism, Neo Brutalism Soft, Flat Design) dengan dukungan dark theme/light
- Semua konfigurasi disimpan di appsetting dan bisa diubah dari aplikasi
- Tambahkan readme.md (English dan Bahasa Indonesia)
- Database support SQLite, SQLServer, MySQL, Postgre
- Storage Support: FileSystem, AzureBlob, S3, MinIO
- Tambahkan dokumentasi lengkap di folder docs
- Buatkan banyak sample data, termasuk gambar-gambar aset dan user
- optimasi kode agar aplikasi cepat dan ringan
- REST API: Integrasi dengan aplikasi eksternal dengan Min API dan swagger

 🚀 Hasil akhir
aplikasi OTA yang komprehensif: mulai dari tiket pesawat, kereta, hotel, hingga aktivitas hiburan, ditambah fitur keuangan seperti PayLater dan promo. 

---
claude --dangerously-skip-permissions