# Galeri Tampilan

Seluruh tangkapan layar diambil otomatis dengan Chrome headless pada lebar 1440 px,
memakai data hasil seeding bawaan. Tema terang dan gelap memakai token warna yang sama,
hanya nilai permukaannya yang ditukar.

---

## Masuk & daftar

Layar autentikasi memakai tata letak dua panel: panel kiri membawa identitas merek
dengan garis lima warna plate, panel kanan berisi formulir. Akun demo bisa diklik
untuk mengisi kolom secara otomatis.

| Terang | Gelap |
|---|---|
| ![Masuk, tema terang](screenshots/01-login-light.png) | ![Masuk, tema gelap](screenshots/01-login-dark.png) |

![Halaman daftar](screenshots/02-register-dark.png)

---

## Dashboard

Dashboard menyesuaikan peran. Admin dan staf melihat **panel lantai**: jumlah check-in
hari ini sebagai angka utama, meter kepadatan yang membandingkannya dengan hari tersibuk
30 hari terakhir, lalu ringkasan operasional. Member melihat runtutan check-in, sisa hari
membership, dan tagihan yang belum dibayar.

### Admin

| Terang | Gelap |
|---|---|
| ![Dashboard admin, tema terang](screenshots/03-dashboard-admin-light.png) | ![Dashboard admin, tema gelap](screenshots/03-dashboard-admin-dark.png) |

### Member

![Dashboard member](screenshots/04-dashboard-member-light.png)

---

## Pembayaran

Member memilih metode bayar per tagihan. Provider yang belum berisi kunci API tidak
ditampilkan, sehingga daftar pilihan selalu mencerminkan yang benar-benar bisa dipakai.

| Daftar tagihan (admin) | Daftar tagihan (member) |
|---|---|
| ![Pembayaran admin](screenshots/07-payments-admin-light.png) | ![Pembayaran member](screenshots/22-payments-member-light.png) |

![Pembayaran admin tema gelap](screenshots/07-payments-admin-dark.png)

### Pemilih metode pembayaran

Saat hanya transfer manual yang aktif — kondisi bawaan aplikasi, karena belum ada kunci API:

![Pemilih metode, hanya manual](screenshots/23-payment-methods-light.png)

Setelah Midtrans, Xendit, dan Stripe diisi kuncinya:

![Pemilih metode, semua provider](screenshots/25-payment-methods-all-providers-light.png)

---

## Konfigurasi sistem

Halaman admin menampilkan provider database, storage, AI, dan pembayaran yang sedang
dipakai. Tiap payment provider menunjukkan status kesiapannya beserta URL callback
yang perlu didaftarkan di dashboard provider.

| Bawaan (3 provider belum dikonfigurasi) | Setelah semua kunci diisi |
|---|---|
| ![Konfigurasi, bawaan](screenshots/08-admin-config-light.png) | ![Konfigurasi, semua aktif](screenshots/24-admin-config-providers-active-dark.png) |

![Konfigurasi tema gelap](screenshots/08-admin-config-dark.png)

---

## Kelas

Tiap kartu kelas memakai sampul sesuai jenisnya dan rim berwarna plate. Kelas yang belum
punya gambar sendiri otomatis memakai sampul bawaan jenisnya, jadi kartu tidak pernah
tampil dengan gambar rusak.

| Terang | Gelap |
|---|---|
| ![Kelas, tema terang](screenshots/06-classes-light.png) | ![Kelas, tema gelap](screenshots/06-classes-dark.png) |

---

## Operasional

| Members | Trainers |
|---|---|
| ![Members](screenshots/05-members-light.png) | ![Trainers](screenshots/09-trainers-light.png) |

| Memberships | Attendance |
|---|---|
| ![Memberships](screenshots/10-memberships-light.png) | ![Attendance](screenshots/11-attendance-light.png) |

| Discounts | Notifications |
|---|---|
| ![Discounts](screenshots/18-discounts-light.png) | ![Notifications](screenshots/20-notifications-light.png) |

---

## Komunitas

| Forum | Events |
|---|---|
| ![Forum](screenshots/12-forum-light.png) | ![Events](screenshots/13-events-light.png) |

| Leaderboard | Feedback |
|---|---|
| ![Leaderboard](screenshots/14-leaderboard-light.png) | ![Feedback](screenshots/17-feedback-light.png) |

---

## Latihan & akun

| Workout | Nutrition |
|---|---|
| ![Workout](screenshots/15-workout-light.png) | ![Nutrition](screenshots/16-nutrition-light.png) |

| Coach Tommy | Profil |
|---|---|
| ![Coach Tommy](screenshots/19-chatbot-light.png) | ![Profil](screenshots/21-profile-light.png) |
