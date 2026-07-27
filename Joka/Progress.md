# Progress — Joka OTA

Status pengembangan aktual, diverifikasi dengan menjalankan aplikasi.
Roadmap dan urutan pengerjaan ada di [PLAN.md](PLAN.md).

**Legenda:** `✅` selesai & teruji · `🟡` sebagian · `⬜` belum ada · `🚫` butuh sistem eksternal

Terakhir diperbarui: 27 Juli 2026 · .NET 10 · EF Core 9 · Semantic Kernel 1.78

---

## Ringkasan cepat

| Area | Status |
|---|---|
| Core travel (pesawat, kereta, bus, hotel, mobil, aktivitas, paket, transportasi lokal) | ✅ 8/8 pencarian & booking |
| Pembayaran | ✅ Transaksi, voucher, asuransi, PayLater, poin, gateway Midtrans/Xendit |
| UX (bahasa, mata uang, wishlist, notifikasi, e-ticket) | ✅ 5/5 |
| Design system di halaman pelanggan | ✅ 21/21 halaman, tanpa warna hardcoded |
| Chatbot Mas Bolang | ✅ terverifikasi dengan model asli, 4 provider native |
| Backend Admin | ✅ 10 selesai · 1 sebagian (API Integration) |
| Backend Operator | ✅ 7/7 area |
| Backend Merchant | ✅ 8/8 area |
| REST API + Swagger | ✅ |

---

## Akun demo

Password semua akun: **`Joka123!`**

| Email | Role | Keterangan |
|---|---|---|
| `admin@joka.id` | Admin | Akses penuh Admin Console |
| `operator@joka.id` | Operator | Operator Desk |
| `merchant@joka.id` | Merchant | Terhubung ke Padma Hotels Group |
| `merchant2@joka.id` | Merchant | Terhubung ke DayTrans Shuttle |
| `demo@joka.id` | User | Pelanggan biasa |
| `budi@example.com` | User | Tier Gold |
| `siti@example.com` | User | Tier Platinum |
| `blocked@example.com` | User | Diblokir — untuk menguji blacklist |

Login diverifikasi dua jalur: form `/login` (cookie auth) dan `POST /api/auth/login`. Akun terblokir ditolak dengan alasannya, password salah ditolak.

---

## 👩‍💻 Admin Console — `/admin`

| Fitur | Status | Catatan |
|---|---|---|
| User & Role Management | ✅ | Ubah role antar 4 role, blokir/buka blokir. Blacklist benar-benar mencegah login (diuji). |
| Audit Logs | ✅ | Setiap mutasi admin, operator, dan merchant menulis `AuditLog` otomatis. |
| Promo Management | ✅ | Form buat/ubah voucher dengan validasi (kode unik, persen ≤100, rentang tanggal, kuota), aktif/nonaktif, kuota terpakai otomatis. |
| Settlement & Reconciliation | ✅ | Bruto/komisi/netto, selisih ditandai, tandai dibayar. |
| Fraud Detection | ✅ | 5 aturan berjalan otomatis saat transaksi dibuat: velocity, anomali nominal, nilai besar, akun diblokir, penyalahgunaan voucher. Skor 0–100 + severity, review aman/fraud. |
| Approval Workflow | ✅ | Setujui/tolak, dan payload benar-benar diterapkan: Update harga, Create jadwal bus & paket travel, Delete (soft) aktivitas/jadwal/kamar. |
| Transaction Monitoring | ✅ | Transaksi nyata dari checkout, volume dihitung dari nilai yang dibayar. |
| System Health Monitoring | ✅ | Probe nyata dengan tombol periksa ulang: koneksi DB, round-trip tulis+hapus storage, konfigurasi chatbot & payment gateway, uptime dan memori proses. |
| API Integration | 🟡 | Daftar integrasi, status, latensi, aktif/nonaktif. **Belum benar-benar memanggil API partner.** |
| Moderasi Ulasan Hotel | ✅ | Tab Ulasan: saring Pending/Approved/Rejected, setujui atau tolak dengan catatan (tolak wajib beralasan), tercatat di audit log. Rating hotel dihitung ulang dari ulasan yang disetujui — menolak ulasan menarik kembali bintangnya. |
| Chart analitik | ✅ | 4 chart D3 di ringkasan: pendapatan 14 hari, transaksi per metode, transaksi per status, booking per produk. Ikut toggle tema. |

## 🛠️ Operator Desk — `/operator`

| Fitur | Status | Catatan |
|---|---|---|
| Order Management | ✅ | Antrean gabungan 4 jenis booking (pesawat/kereta/bus/hotel). |
| Booking Verification | ✅ | Tab Verifikasi Pembayaran: konfirmasi atau tolak transaksi, alasan penolakan tersimpan, tercatat di audit log. |
| Refund & Reschedule | ✅ | Setujui/tolak, tercatat di audit log. |
| Incident Reporting | ✅ | Form buat insiden + tandai selesai. |
| Customer Support Tools | ✅ | Live agent: antrean tiket (Open di atas Assigned), ambil tiket, balas dalam thread, selesaikan dengan ringkasan wajib, tutup. Balasan agen langsung sampai ke pelanggan lewat `SupportBroadcaster` dan memicu notifikasi. Agen lain tidak bisa merebut tiket yang sudah diambil. |
| Promo Application | ✅ | Operator menerapkan voucher ke transaksi yang sudah ada; total dihitung ulang, kuota voucher terpakai, tercatat di audit log. |
| Real-time Notification | ✅ | Keputusan refund operator langsung mengirim notifikasi ke pelanggan. |

## 🏨 Merchant Portal — `/merchant`

| Fitur | Status | Catatan |
|---|---|---|
| Partner Dashboard | ✅ | Produk aktif, rating, komisi, saldo belum dibayar, plus 4 chart D3 (pendapatan harian, booking per produk, tren settlement, okupansi kamar) — semuanya di-join lewat `MerchantId` sehingga partner tidak melihat angka partner lain. |
| Settlement Report | ✅ | Laporan per periode dengan status pembayaran. |
| Pricing & Dynamic Fare | ✅ | Input harga bebas dengan validasi, masuk antrean approval admin. Pengajuan ganda untuk item yang sama ditolak. |
| Inventory Management | ✅ | Ubah stok kamar, sisa kursi, dan kuota tiket. Langsung berlaku, tervalidasi terhadap kapasitas armada dan tiket terjual. |
| Schedule Management | ✅ | Tambah keberangkatan baru (armada, rute, waktu, durasi, harga) lewat approval; admin menerapkannya ke katalog. |
| Content Management | ✅ | Unggah foto kamar & aktivitas lewat `IStorageService`, batas 5 MB per berkas. |
| Promo & Paket Bundling | ✅ | Merchant menyusun paket bundling sendiri, masuk katalog setelah disetujui admin. |
| CRUD Data Produk | ✅ | Form kamar & aktivitas baru, jadwal, dan paket — semua lewat approval. Update harga/stok/konten dan Delete sudah ada. |

> **Pembagian wewenang, disengaja:** harga dan record baru masuk antrean approval admin (mereka menyentuh katalog publik); stok dan konten langsung berlaku (operasional harian). Kepemilikan produk eksplisit lewat kolom `MerchantId`, dan isolasi antar partner sudah diuji.

---

## Fitur pelanggan

| Fitur | Status | Catatan |
|---|---|---|
| Tiket pesawat | ✅ | Cari, filter, urutkan, pesan, e-ticket + QR. |
| Bus & Shuttle | ✅ | Bus antar kota + shuttle door-to-door, seat selection, validasi kursi, e-ticket. |
| Hotel | ✅ | Cari, galeri foto hotel & kamar, pesan. Ulasan tamu tampil di detail hotel (hanya yang disetujui admin); tamu yang login bisa menulis ulasan, ditandai "Terverifikasi" bila benar-benar punya booking di hotel itu. |
| Rental mobil | ✅ | Foto kendaraan dengan fallback ikon. |
| Aktivitas & event | ✅ | Galeri foto per aktivitas. |
| Paket travel | ✅ | Galeri foto per paket. |
| E-ticket & QR Code | ✅ | QR asli via QRCoder, diverifikasi end-to-end. |
| Wishlist | ✅ | Tombol simpan di Hotel, Aktivitas, Rental Mobil, dan Paket. Belum login → diarahkan ke login dengan ReturnUrl. |
| Pembayaran | ✅ | Menulis `PaymentTransaction`, voucher divalidasi ke DB, asuransi & PayLater masuk total. Gateway sungguhan (Midtrans Snap / Xendit Invoice) di belakang `IPaymentGateway`; tanpa kredensial jatuh ke gateway simulasi. Status lunas hanya datang dari webhook yang tanda tangannya diverifikasi. |
| PayLater | ✅ | Tenor 3/6/12 bulan, bunga dari `appsettings`, dibebankan ke total transaksi. |
| Asuransi | ✅ | Bisa dipilih di checkout dan masuk ke total serta tersimpan di transaksi. |
| Tiket kereta | ✅ | Cari, pesan, dan **seat selection**. Jadwal lewat `ITrainScheduleProvider`: KAI dulu bila `Integrations:KAI:ApiKey` diisi, jadwal Joka sebagai cadangan. KAI tidak punya API publik, jadi kelasnya sengaja jadi *seam* — jadwal dari KAI ditandai "Info KAI" dan tidak bisa dipesan, dan saat fallback dipakai UI mengatakannya. |
| Profil & foto profil | ✅ | Ubah nama, telepon, dan preferensi bahasa/mata uang. Unggah foto profil lewat `IStorageService` (JPG/PNG/WebP, maks 2 MB) — ekstensi diambil dari content type yang di-whitelist, bukan dari nama berkas kiriman. Hapus foto hanya melepas referensinya; blob-nya sengaja ditinggal. |
| Membership & loyalty | ✅ | Halaman `/my-points`: poin, tier, progress bar ke tier berikutnya, riwayat transaksi poin. |
| Trip Planner | ✅ | Timeline per hari disusun dari booking nyata (pesawat/kereta/bus/hotel), dikelompokkan per kota, plus saran aktivitas. |
| Multi-bahasa | ✅ | `.resx` id/en, `RequestLocalization`, cookie. **355 kunci per bahasa menutupi seluruh halaman pelanggan** — katalog produk, halaman akun, auth, chat, ulasan hotel, transportasi lokal, dan live agent. Back-office sengaja dikecualikan (alat internal, satu bahasa). Selector masih disembunyikan lewat `AppSettings:ShowLanguageSwitcher`; nyalakan setelah terjemahan direview. |
| Multi-mata uang | ✅ | Konversi nyata di 13 halaman pelanggan via `CurrencyService` (kurs dari appsettings, bisa diubah dari Settings). Pilihan tersimpan di cookie, tooltip menampilkan harga asli. Back-office sengaja tetap Rupiah. |
| Notifikasi real-time | ✅ | Badge live di topbar via `NotificationBroadcaster` (push lewat koneksi SignalR milik Blazor). `MapHub` di `/hubs/notifications` untuk klien eksternal. Terkirim otomatis saat pembayaran sukses dan keputusan refund. |
| Transportasi lokal (ojek/airport transfer) | ✅ | Halaman `/transport`: ojek/mobil per-km dan airport transfer tarif tetap, 3 penyedia & 15 layanan di 4 kota. Tarif dihitung satu tempat (`TransportService.FareFor`, dibulatkan ke Rp500 terdekat dengan tarif minimum) sehingga halaman cari, catatan booking, `GET /api/transport`, dan Mas Bolang tidak mungkin berbeda angka. |
| Live agent customer support | ✅ | `/support` punya thread langsung dengan agen: buka tiket (satu tiket aktif per user), lihat balasan agen tanpa refresh, riwayat tiket lama. Kategori Payment/Refund otomatis naik ke prioritas High. |

## 🤖 Mas Bolang

| Fitur | Status |
|---|---|
| Multi-sesi, reset, lampiran, render markdown | ✅ |
| 18 kernel function (11 query DB + 7 utilitas) | ✅ Termasuk `cari_transportasi_lokal`, memakai `TransportService.FareFor` yang sama dengan halaman `/transport` |
| Auto function calling | ✅ Diuji dengan model asli |
| Contoh prompt di halaman chat | ✅ 13 contoh, 4 kelompok |
| Pencarian internet (Tavily) | ✅ |
| Provider Anthropic & Gemini | ✅ Konektor native. Anthropic lewat `Anthropic.SDK` → `IChatClient` + `UseFunctionInvocation()`, Gemini lewat konektor Google SK. Kunci yang kosong kini gagal cepat dengan pesan jelas, bukan diam-diam mengirim `sk-placeholder` |

---

## Infrastruktur

| Item | Status |
|---|---|
| .NET 10 | ✅ |
| Database 4 provider | ✅ SQLite/SQLServer/MySQL/Postgre |
| REST API + Swagger | ✅ |
| Dokumentasi (README EN/ID, docs/) | ✅ |
| Sample data + gambar | ✅ Gambar dari Unsplash, 44 URL diverifikasi 200 |
| Data Protection persisted | ✅ Kunci di `Data/keys`, token bertahan melewati restart |
| Storage 4 provider | ✅ FileSystem, AzureBlob, S3, MinIO — dipilih dari Settings, fallback ke FileSystem bila kredensial kosong |
| Konfigurasi bisa diubah dari aplikasi | ✅ Halaman `/admin/settings`, 19 setelan, berlaku tanpa restart |
| D3.js chart | ✅ Komponen `<Chart>` + `wwwroot/js/charts.js` (bar, line, donut, hbar). Warna dibaca dari token CSS saat menggambar dan digambar ulang pada event `joka:theme`, jadi chart ikut toggle tema |
| Payment gateway | ✅ Midtrans & Xendit di belakang `IPaymentGateway`, webhook `POST /api/payments/midtrans-notification` dan `/api/payments/xendit-callback`, fallback simulasi bila kredensial kosong |

---

## Keamanan

| Item | Status |
|---|---|
| Hash password | ✅ PBKDF2 via `PasswordHasher`, salt acak per user. Hash SHA256 lama dimigrasi otomatis saat login berhasil |
| Blacklist akun | ✅ Mencegah login, terverifikasi |
| Otorisasi role | ✅ `AuthorizeRouteView` + policy per area |
| Data Protection | ✅ Kunci dipersistensikan ke `Data/keys` |
| Webhook pembayaran | ✅ Midtrans diverifikasi SHA512 `order_id+status_code+gross_amount+ServerKey`, Xendit lewat `x-callback-token`; keduanya dibandingkan dengan `CryptographicOperations.FixedTimeEquals`. Browser tidak pernah bisa menandai transaksi lunas |
| API key di appsettings | ⬜ Masih plaintext — lihat G-4 (user-secrets) |

## Bug yang sudah diperbaiki

| # | Masalah | Dampak |
|---|---|---|
| 1 | Design system di `.razor.css` (scoped) | Seluruh token CSS mati di 23 halaman |
| 2 | Google OAuth kosong tapi didaftarkan | Aplikasi 500 di **semua** halaman |
| 3 | `decimal` di `ORDER BY` SQLite | Home, Flights, Trains, 2 endpoint API crash |
| 4 | Object cycle Hotel↔Room | `/api/hotels` 500 |
| 5 | Route param tanpa `[Parameter]` | `/payment-checkout` 500 |
| 6 | `AddFromType` untuk plugin SK | Kernel function tidak bisa dikonstruksi |
| 7 | `ToolCallBehavior` tidak di-set | Function terdaftar tapi tak pernah dipanggil |
| 8 | Culture id-ID + parse config | `temperature 0.7` → `7`, chatbot mati total |
| 9 | Tavily API key tidak pernah dikirim | Pencarian internet selalu gagal diam-diam |
| 10 | Model mengarang harga saat menghitung | Jawaban salah (10jt vs 5,5jt asli) |
| 11 | `RouteView` bukan `AuthorizeRouteView` | `[Authorize]` diabaikan total |
| 12 | Login memakai `HttpContext` di halaman interaktif | **Login gagal untuk semua akun** — `HttpContext` selalu null di InteractiveServer. Halaman login kini static SSR. |
| 13 | `AccessDeniedPath` menunjuk ke `/login` | User terautentikasi tanpa role masuk loop redirect. Kini ada `/access-denied`. |
| 14 | Kunci Data Protection tidak dipersistensikan | Setiap restart membatalkan semua token antiforgery & cookie auth → *"A valid antiforgery token was not provided"*. Kunci kini di `Data/keys`. |
| 15 | Kegagalan antiforgery menampilkan teks framework mentah | Token basi kini diarahkan balik ke form dengan pesan "Sesi formulir sudah kedaluwarsa". |
| 16 | `[CascadingParameter] HttpContext` di 4 halaman akun | **Wishlist, Booking Saya, Notifikasi, dan Profil selalu menampilkan "silakan login"** meski pengguna sudah masuk — akar masalah yang sama dengan bug login. Kini memakai `AuthenticationState`. |

---

## Verifikasi terakhir

- Build: **0 error** (17 warning lama: CVE ImageSharp, CS0649)
- Route: **28/28 sesuai harapan** — 18 halaman publik `200` (termasuk `/transport`), 10 halaman ber-`[Authorize]` `302` ke login
- Endpoint baru: `GET /api/payments/gateway` melaporkan `Simulasi` saat kredensial kosong; `GET /api/transport` mengembalikan tarif; webhook Midtrans & Xendit **tidak** memproses payload bertanda tangan palsu
- Lokalisasi: HTML yang dirender **tidak memuat satu pun kunci mentah**; `Accept-Language: en` benar-benar mengganti teks
- Fase A & B: **24/24 PASS** lewat harness terhadap database nyata
- Fase F & G (F-1, F-2, F-5, F-6, G-1, G-5): **83/83 PASS** lewat harness terhadap database nyata — pemetaan status gateway, penolakan tanda tangan palsu, poin tidak dobel saat gateway mengirim notifikasi ulang, kuota voucher hanya terpakai saat settle, fallback KAI tanpa exception, tarif transportasi (Rp30.000 untuk ojek 10 km, tarif minimum untuk 0,5 km), dan siklus penuh tiket live agent
- Chatbot: 8 skenario dengan gpt-4o-mini, 0 error di log
- Antiforgery: token bertahan melewati restart; token basi → pesan ramah, bukan error mentah

### Belum terverifikasi end-to-end

Aksi interaktif Blazor tidak bisa dipicu lewat HTTP, jadi hal berikut baru terverifikasi sampai tingkat render dan kompilasi:

- Klik tombol wishlist
- Pemilih berkas foto profil (unggah + hapus terverifikasi 14/14 di harness sampai lapisan storage; dialog berkasnya sendiri perlu diklik manual)
- Modal editor merchant (harga, stok, kursi, kuota, unggah foto)
- Form voucher admin, tombol periksa ulang health, verifikasi pembayaran operator
- Chart D3 (gambar ulang saat toggle tema baru terverifikasi lewat kode, bukan lewat mata)
- Percakapan live agent dua sisi di dua browser sekaligus — logikanya 25/25 PASS di harness, tapi push antar-sirkuit perlu dilihat langsung
- Redirect ke Snap/Invoice milik Midtrans & Xendit: tanpa kredensial sungguhan hanya jalur simulasi yang bisa dijalankan

Logika di baliknya ada di service dengan validasi eksplisit (`MerchantCatalogService`, `HealthProbeService`, `FraudDetectionService`) dan mengikuti pola yang sama dengan fitur yang sudah terbukti, tapi jalur kliknya perlu diuji manual di browser.
