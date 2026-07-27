# Roadmap — Joka OTA

Rencana pengembangan. Status aktual per fitur ada di [Progress.md](Progress.md);
file ini hanya soal **apa berikutnya dan kenapa urutannya begitu**.

Prioritas: `P0` memblokir fitur lain · `P1` melengkapi requirement · `P2` penyempurnaan

---

## ~~Fase A — Menutup pondasi transaksi~~ ✅ SELESAI

Diverifikasi 24/24 lewat harness terhadap database nyata.

- ✅ **A-1** `CheckoutService.PayAsync` menulis `PaymentTransaction` lengkap dengan diskon, asuransi, dan status
- ✅ **A-2** Voucher divalidasi ke DB: aktif, jendela waktu, kuota, min. transaksi, kesesuaian produk, plafon diskon
- ✅ **A-3** Asuransi dipilih di checkout dan masuk ke total
- ✅ **A-4** PayLater tenor 3/6/12 bulan, bunga dari `appsettings`, dibebankan ke total
- ✅ **A-5** Poin loyalitas 1/Rp10.000 + kenaikan tier otomatis + `LoyaltyTransaction`

## ~~Fase B — Relasi merchant ke produk~~ ✅ SELESAI

- ✅ **B-1** `MerchantId` di Hotel, Flight, BusService, Activity
- ✅ **B-2** Portal merchant memfilter berdasarkan kepemilikan (isolasi antar partner diverifikasi)
- ✅ **B-3** Approval yang disetujui menerapkan `PayloadJson` ke tabel produk

---

## Fase C — CRUD Merchant `P1`

Sekarang tidak lagi terhalang: kepemilikan produk sudah eksplisit lewat `MerchantId`.

- ✅ **C-1** Form kamar & aktivitas baru lewat approval; Create/Update/Delete lengkap
- ✅ **C-2** Ubah stok kamar, sisa kursi, kuota tiket — tervalidasi terhadap kapasitas dan tiket terjual
- ✅ **C-3** Input harga bebas dengan validasi, masuk antrean approval
- ✅ **C-4** Tambah keberangkatan bus lewat approval, diterapkan admin ke katalog
- ✅ **C-5** Unggah foto lewat `IStorageService`, batas 5 MB
- ✅ **C-6** Merchant menyusun paket bundling sendiri

## Fase D — Melengkapi Admin & Operator `P1`

- ✅ **D-1** Form buat/ubah voucher dengan validasi lengkap
- ✅ **D-2** 5 aturan fraud berjalan otomatis saat transaksi dibuat
- ✅ **D-3** Health probe nyata: DB, storage round-trip, chatbot, gateway, proses
- ✅ **D-4** Operator menerapkan voucher ke transaksi yang sudah ada (kompensasi / kode terlewat saat checkout)
- ✅ **D-5** Verifikasi pembayaran manual oleh operator
- ✅ **D-6** Halaman `/admin/settings` — override `appsettings.json` lewat tabel `AppConfiguration`, berlaku tanpa restart

## Fase E — Fitur pelanggan yang belum lengkap `P1`

- ✅ **E-1** Tombol wishlist di Hotel, Aktivitas, Rental Mobil, Paket (`WishlistButton`)
- ✅ **E-2** Notifikasi real-time: `NotificationBroadcaster` + badge live di topbar, `MapHub` untuk klien non-Blazor. Terkirim otomatis saat pembayaran sukses dan refund diputuskan
- ✅ **E-3** Multi-bahasa untuk **seluruh halaman pelanggan**. 355 kunci per bahasa (shell, Home, Flights, Trains, Buses, Hotels, Cars, Activities, Packages, Insurance, Promos, Transport, Wishlist, MyBookings, Notifications, Profile, Support, Login, Register, ResetPassword, Chat, ulasan hotel, seat map, state error). Back-office sengaja dikecualikan — alat internal, satu bahasa. Selector masih disembunyikan lewat `AppSettings:ShowLanguageSwitcher`; nyalakan setelah review terjemahan
- ✅ **E-4** Konversi mata uang nyata di semua halaman pelanggan (`CurrencyService` + komponen `Money` + cookie preferensi). Back-office sengaja tetap Rupiah untuk rekonsiliasi
- ✅ **E-5** Halaman `/my-points`: poin, tier, progress ke tier berikutnya, riwayat poin
- ✅ **E-6** Trip Planner menyusun timeline dari booking nyata milik user, plus saran aktivitas di kota tujuan
- ✅ **E-7** Seat selection untuk kereta dan bus: peta kursi dari layout armada, kursi terisi dari booking lain, cek bentrok saat konfirmasi

## ~~Fase F — Integrasi eksternal~~ ✅ SELESAI

- ✅ **F-1** Payment gateway sungguhan: Midtrans (Snap) dan Xendit (Invoice) di belakang `IPaymentGateway`, dipilih lewat `PaymentGatewayFactory` dengan fallback ke `StubGateway` bila kredensial kosong. Uang hanya ditandai lunas dari stub atau dari webhook yang tanda tangannya diverifikasi — browser tidak pernah boleh mengabari kita bahwa pembayaran berhasil. Voucher dan poin dikonsumsi saat settlement, dan `SettleAsync` idempoten karena gateway suka mengirim ulang notifikasi
- ✅ **F-2** Integrasi KAI untuk jadwal kereta: `ITrainScheduleProvider` dengan `KaiTrainScheduleProvider` (aktif bila `Integrations:KAI:ApiKey` diisi) dan `LocalTrainScheduleProvider` sebagai cadangan. KAI tidak punya API publik, jadi ini sengaja dibuat sebagai *seam* integrasi: provider tidak pernah melempar, jadwal dari KAI ditandai tidak bisa dipesan, dan fallback diumumkan di UI alih-alih diam-diam menampilkan data lama
- ✅ **F-3** Storage provider Azure Blob, S3, dan MinIO diimplementasi; dipilih dari Settings, fallback aman ke FileSystem bila kredensial kosong
- ✅ **F-4** Konektor native Anthropic & Gemini untuk Mas Bolang, menggantikan `AddOpenAIChatCompletion` untuk semua provider. Tiap konektor punya kelas execution settings sendiri, jadi `BuildSettings()` memilih yang sesuai per provider
- ✅ **F-5** Transportasi lokal: ojek online dan airport transfer (`/transport`, `GET /api/transport`, plus function `cari_transportasi_lokal` di Mas Bolang). Tarif dihitung di satu tempat — `TransportService.FareFor` — supaya halaman cari, catatan booking, dan chatbot tidak mungkin berbeda angka
- ✅ **F-6** Live agent untuk customer support: `SupportTicket`/`SupportMessage`, antrean agen di konsol operator, thread live di `/support`. Real-time lewat `SupportBroadcaster` singleton di atas sirkuit SignalR milik Blazor sendiri, bukan koneksi klien kedua

## Fase G — Penyempurnaan `P2`

- ✅ **G-1** Chart D3.js di dashboard admin dan merchant lewat komponen `<Chart>` + `wwwroot/js/charts.js`. Warna dibaca dari CSS custom property saat menggambar dan digambar ulang pada event `joka:theme`, jadi chart ikut toggle tema. Angkanya dari `AnalyticsService`; seri merchant di-join lewat `MerchantId` supaya partner tidak bisa melihat angka partner lain
- ✅ **G-2** Seluruh halaman pelanggan ditata ulang dengan design system: Activities, Cars, Packages, Insurance, Promos, Wishlist, MyBookings, Notifications, Profile, Support, Register, ResetPassword, Chat. Tidak ada lagi warna hardcoded di halaman-halaman ini — semuanya lewat token `var(--…)` sehingga toggle tema bekerja
- ✅ **G-3** `PasswordHasher` (PBKDF2, salt per user) menggantikan SHA256 bersalt statis; hash lama dimigrasi otomatis saat login berhasil
- **G-4** Pindahkan API key ke user-secrets
- ✅ **G-5** Moderasi ulasan hotel oleh admin: ulasan masuk berstatus `Pending`, hanya yang `Approved` tampil di halaman hotel **dan** dihitung ke rating. Rating hotel diturunkan dari ulasan yang disetujui, bukan disimpan manual — jadi menolak ulasan benar-benar menarik kembali bintangnya
- **G-6** Naikkan EF Core ke 10 begitu Pomelo merilis versi yang kompatibel

---

## Utang teknis yang diketahui

| Item | Kenapa dibiarkan |
|---|---|
| EF Core 9 di .NET 10 | Pomelo mengunci Relational di `[9.0.0, 9.0.999]`; naik ke EF 10 berarti kehilangan MySQL |
| Tanpa EF migrations | `EnsureCreated()` + seeder. Setiap perubahan model menuntut hapus `Data/joka.db` |
| Gambar hotlink ke Unsplash | Cepat untuk demo; produksi sebaiknya unggah sendiri lewat `IStorageService` |
| `SQLitePCLRaw` 2.1.10 | Kerentanan high, transitif dari EF Core 9 Sqlite — ikut selesai di G-6 |
