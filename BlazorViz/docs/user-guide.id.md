# Panduan Pengguna

> 🇬🇧 English version: [user-guide.md](user-guide.md)

## 1. Masuk

Jalankan aplikasi (`dotnet run --project src/BlazorViz`) lalu login dengan akun bawaan
(`admin@blazorviz.local` / `Admin123!`) atau daftar akun baru. Role:

- **Viewer** — melihat dashboard, chat dengan Data Wizard.
- **Analyst** — semua di atas + membuat koneksi, dataset, dan dashboard.
- **Admin** — semua + manajemen pengguna, audit log, usage, performa, pengaturan.

Gunakan tombol 🌙/☀️ di bawah sidebar untuk berganti tema gelap/terang.

## 2. Koneksi

*Connection* menyimpan kredensial/lokasi sumber data. Buka **Connections → New connection**,
pilih jenis dan isi konfigurasi JSON (petunjuk tiap jenis tampil di bawah editor). Klik **Test**
untuk memverifikasi. Jenis yang didukung: `sqlite`, `sqlserver`, `postgresql`, `mysql`, `oracle`,
`excel`, `csv`, `rest`, `graphql`.

## 3. Dataset

*Dataset* = sumber + langkah ETL opsional + script opsional.

1. **Datasets → New dataset**, beri nama.
2. Pilih **connection** dan tulis query (SQL untuk database, dokumen GraphQL, path REST, atau nama
   sheet Excel), atau pilih **Uploaded file** dan unggah CSV/Excel/JSON.
3. Tambahkan **langkah ETL** (opsional) — setiap langkah berupa `op` + parameter seperti
   `field=Region; op==; value=North`. Lihat [etl-and-scripting.md](etl-and-scripting.md).
4. Tambahkan **script** (opsional; C#, JavaScript, atau Python). Pilih template dari dropdown lalu modifikasi.
5. Klik **Preview** untuk melihat hasil, tipe kolom, dan saran visualisasi. **Save**.

Atur **Auto refresh** (detik) agar panel dashboard mengambil ulang data secara berkala.

## 4. Dashboard

- **Dashboards → Create**, Anda langsung masuk ke **designer**.
- **＋ Panel** menambah panel; seret lewat judulnya, ubah ukuran dari pojok kanan-bawah.
- **✏️ pada panel** membuka editor: pilih dataset, tipe chart (24 tipe termasuk `custom`),
  field X/Y/series, agregasi, sort/limit. Peta butuh field lat/lng; bubble butuh field ukuran.
- Panel **custom**: pilih ECharts / Chart.js / D3 dan tulis body fungsi yang menerima
  `(el, rows, columns, lib, palette)` — render apa pun ke `el`.
- **Tab**: ＋ Tab untuk menambah, ganti nama langsung; tiap tab punya grid panel sendiri.
- **Filter**: tambahkan slicer / dropdown / multi-select / rentang tanggal yang terikat ke field dataset.
  Filter berlaku ke semua panel yang memakai dataset itu.
- **💡 Suggest** menambahkan chart yang direkomendasikan berdasarkan tipe kolom data Anda.
- **Save** membuat versi baru. **🕘 Versions** menampilkan riwayat — **Rollback** memulihkan versi mana pun.
- **Link publik**: centang *Public link*, lalu **🔗 Copy link** (`/share/{token}`, tanpa login)
  atau **📋 Embed code** (snippet iframe untuk aplikasi lain).

Header tiap panel punya export ⬇️ CSV dan 🖼️ PNG; REST API bisa export CSV/JSON/Excel/PDF.

## 5. Data Wizard (chat AI)

Buka **Data Wizard**. Asisten dapat:

- meng-query dataset & dashboard Anda (skema, preview, agregasi, filter) lewat tool bawaan,
- menghitung matematika, mengecek tanggal, mencari di internet, scraping halaman web,
- otomatis memakai kutipan dari dokumen RAG yang sudah diindeks,
- menerima **lampiran**: gambar dikirim sebagai image content; dokumen ditautkan dan isinya disertakan.

Jawaban tampil secara streaming dan dirender sebagai markdown (tabel, blok kode, gambar). Sesi tersimpan
per pengguna di kolom kiri. Provider/model/persona/temperature diambil dari bagian `Ai` di `appsettings.json`.

## 6. Dokumen (RAG)

**Documents (RAG)** → unggah file PDF / Word / Excel / teks. File dipotong (chunk), di-embed, dan diindeks
ke vector store yang dikonfigurasi. Gunakan kotak *Search test* untuk mengecek retrieval. Dokumen terindeks
otomatis dipakai Data Wizard sebagai konteks.

## 7. Predictive analytics

**Predictive** → pilih tugas:

- **Forecasting** — pilih kolom waktu, kolom numerik, dan horizon; menampilkan histori + prediksi.
- **Regression** — pilih kolom target dan fitur; menampilkan scatter aktual-vs-prediksi dengan R²/RMSE.
- **Clustering** — pilih kolom fitur dan k; menambahkan kolom `Cluster` pada tabel hasil.

## 8. Admin

- **Users** — buat pengguna, atur role per pengguna, hapus.
- **Audit Logs** — filter per user/kategori/tanggal, urutkan per kolom.
- **Usage** — jumlah query, chat, estimasi token, panggilan API; grafik harian; pengguna teratas.
- **Performance** — waktu respons langsung, trafik per path, memori/CPU/uptime (refresh tiap 5 detik).
- **Settings** — ringkasan konfigurasi AI/storage, status plugin + reload, dan **manajemen API key**.

## 9. API eksternal

Buat key di **Admin → Settings**, lalu panggil `/api/v1/...` dengan header `X-Api-Key`.
Dokumentasi interaktif: **`/swagger`**. Lihat [api.md](api.md).
