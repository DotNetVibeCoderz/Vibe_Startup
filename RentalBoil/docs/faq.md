# ❓ FAQ — Pengembang & Pengguna

## Umum

### Q: Apa itu RentalBoil?
Platform rental kendaraan yang menghubungkan pemilik kendaraan dengan pelanggan. Mobil & motor disewakan dengan proses booking, pembayaran, GPS tracking, dan review.

### Q: Teknologi apa yang digunakan?
.NET 10 Blazor Server, EF Core, Semantic Kernel AI, SignalR, Leaflet.js, Bootstrap 5.3.

### Q: Apakah bisa ganti database selain SQLite?
Ya. Support SQLite (default), SQL Server, MySQL, PostgreSQL. Edit `Database:Provider` di `appsettings.json`.

---

## Booking & Pembayaran

### Q: Bagaimana flow booking?
1. Customer cari kendaraan → booking
2. Customer bayar (simulasi)
3. Partner konfirmasi → status Confirmed
4. Customer ambil kendaraan → status Active + GPS mulai
5. Customer kembalikan → status Completed + GPS berhenti
6. Customer beri ulasan ⭐

### Q: Apakah pembayaran real?
Tidak. Ini simulasi. Klik "Konfirmasi Pembayaran" langsung mengubah status ke Paid.

### Q: Di mana halaman pembayaran?
`/customer/bookings/{id}/payment` — bisa diakses dari tombol "Bayar" di halaman Pesanan Saya.

---

## GPS & IoT

### Q: Bagaimana GPS tracking bekerja?
`GpsSimulatorHostedService` berjalan di background, setiap 3 detik mengupdate koordinat kendaraan yang statusnya Active. Koordinat berubah random ±100 meter.

### Q: Di mana melihat GPS tracking?
- `/gps` — semua kendaraan yang sedang disewa (untuk customer & admin)
- `/admin/map` — peta seluruh kendaraan aktif (admin only)
- `/vehicles/{id}/map` — peta satu kendaraan

### Q: Bisakah GPS simulator menggunakan API?
Ya. Set `GPS:UpdateMode` ke `Api` di `appsettings.json`. Simulator akan kirim update via REST API.

---

## AI Chat Bot

### Q: Kenapa chat bot tidak merespon?
Pastikan API key AI provider sudah diisi di `appsettings.json`. Cek `AI:Provider` dan `AI:{Provider}:ApiKey`.

### Q: Bisakah pakai AI lokal?
Ya. Install [Ollama](https://ollama.com), pull model (`ollama pull llama3.2`), jalankan (`ollama serve`), lalu set `AI:Provider` ke `Ollama`.

### Q: Apa saja yang bisa dilakukan chat bot?
Cari kendaraan, booking, cek order, cek GPS, cek promo, FAQ, kalkulasi harga, cari internet (Tavily), web scraping, dan banyak lagi.

### Q: Bagaimana menambah kemampuan baru ke chat bot?
Tambah method dengan `[KernelFunction]` attribute di `Services/BotKernelFunctions.cs`.

---

## API

### Q: Di mana dokumentasi API?
`/swagger` — Swagger UI interaktif. Atau lihat [API Reference](api-reference.md).

### Q: Bagaimana authentication API?
Setiap request ke `/api/*` harus menyertakan header `X-Api-Key`. Default key: `rntl-2025-secure-api-key-change-in-production`.

### Q: Apakah API bisa diakses dari aplikasi mobile?
Ya. REST API standar, bisa dipanggil dari aplikasi mobile, Postman, curl, dll.

---

## Troubleshooting

### Q: Error "L is not defined" di peta
Leaflet.js tidak terload. Pastikan `App.razor` memiliki `<script src="...leaflet.js">` di `<head>`.

### Q: Button tidak merespon klik
Pastikan `App.razor` memiliki `@rendermode="InteractiveServer"` di `<Routes />` dan `<HeadOutlet />`.

### Q: Error "ObjectDisposedException" di GPS Simulator
Sudah difix di versi terbaru. Semua akses DB pakai `IServiceScopeFactory`.

### Q: Error "UNIQUE constraint: BookingNumber"
Hapus file `RentalBoil.db` dan restart aplikasi. Atau sudah difix dengan prefix-based booking number.

### Q: Theme dark/light tidak berfungsi
Cookie-based. Browser harus mengizinkan cookie. Theme di-set via JavaScript `eval()` untuk kompatibilitas Blazor Server.

### Q: Upload foto profil/kTP/SIM gagal
Cek `Storage:Provider` di `appsettings.json`. Default: FileSystem (`wwwroot/uploads/`). Pastikan folder writable.

### Q: Export CSV/Excel tidak mendownload
Fitur download menggunakan JavaScript blob. Pastikan browser mendukung `data:` URI. Cek console browser untuk error.
