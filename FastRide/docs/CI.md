# ⚙️ CI — FastRide

GitHub Actions, di `.github/workflows/ci.yml`. Berjalan pada push dan pull request ke
`main`, `master`, dan `develop`.

---

## Job

| Job | Runner | Kapan | Isi |
|-----|--------|-------|-----|
| **Build & test** | ubuntu | Selalu | Build 6 proyek non-MAUI, jalankan 318 test, kumpulkan cakupan |
| **End-to-end smoke** | ubuntu | Setelah test lulus | Nyalakan API, jalankan simulator 45 detik, periksa log |
| **MAUI apps** | windows | PR dan branch utama | Build aplikasi rider & driver untuk Android |
| **Inspection** | ubuntu | Selalu | Laporan paket rentan/usang, pindai kredensial |

Job `mobile` hanya berjalan di PR dan branch utama karena memasang workload MAUI memakan
waktu beberapa menit; menjalankannya pada tiap push ke branch fitur tidak sepadan.

---

## Kenapa tidak `dotnet build FastRide.sln`

Solusi memuat kedua aplikasi MAUI, yang butuh workload yang tidak dipasang di job Linux.
Build per proyek membuat run tetap cepat dan kegagalannya bisa dibaca — bukan tenggelam di
error workload yang tidak relevan.

```yaml
for project in FastRide.Shared FastRide.Data FastRide.Api FastRide.AdminWeb FastRide.Simulator FastRide.Tests; do
  dotnet build "$project/$project.csproj" --no-restore --configuration Release
done
```

---

## Smoke test

Unit dan integration test menjalankan API di memori. Smoke job menjalankannya **sebagai
proses sungguhan**, lalu melepas simulator ke arahnya:

```bash
dotnet run --project FastRide.Simulator -- --url http://localhost:5000 --riders 6 --drivers 3 --duration 45
```

Simulator menjalani siklus penuh: daftar, verifikasi dokumen, GPS, quote, booking, jemput,
antar, **bayar**, ulas. Batangnya: **0 request gagal**.

Setelahnya job memeriksa log API untuk `Unhandled exception`. Exception yang lolos dari test
tapi muncul di sini adalah cacat nyata, jadi diperlakukan sebagai build gagal.

Log diunggah sebagai artefak, jadi kegagalan bisa didiagnosis tanpa mengulang run.

---

## Pemindai kredensial

Kunci gateway pembayaran adalah satu hal yang tidak boleh masuk repositori. Job `inspect`
menolak commit yang mengandung:

| Pola | Milik |
|------|-------|
| `"ServerKey": "Mid-server-...` | Midtrans produksi |
| `"ClientKey": "Mid-client-...` | Midtrans produksi |
| `xnd_production_` | Xendit produksi |

Placeholder sandbox di `appsettings.json` sengaja dibiarkan lolos — yang dicari adalah kunci
yang tampak hidup. Lihat [`PAYMENTS.md`](PAYMENTS.md) untuk cara menyimpan kredensial dengan
benar.

---

## Laporan

- **Hasil test** tampil sebagai check pada PR lewat `dorny/test-reporter`, termasuk saat
  build merah — justru saat itulah laporannya paling berguna.
- **Ringkasan cakupan** ditulis ke `$GITHUB_STEP_SUMMARY`.
- **Artefak** (`.trx`, cakupan Cobertura + HTML, log API) disimpan 14 hari.

---

## Menjalankan lokal

Persis seperti yang dilakukan CI:

```bash
# Build & test
for p in FastRide.Shared FastRide.Data FastRide.Api FastRide.AdminWeb FastRide.Simulator FastRide.Tests; do
  dotnet build "$p/$p.csproj" --configuration Release
done

dotnet test FastRide.Tests/FastRide.Tests.csproj --configuration Release

# Smoke
dotnet run --project FastRide.Api &
dotnet run --project FastRide.Simulator -- --url http://localhost:5000 --duration 45
```

---

## Concurrency

Push yang lebih baru membatalkan run yang masih berjalan pada ref yang sama — kecuali di
`main`/`master`, yang riwayatnya dibiarkan utuh.

---

## Yang belum ada

| Item | Catatan |
|------|---------|
| Test terhadap Postgres/SQL Server | Suite berjalan di SQLite; provider lain diuji manual |
| Analisis statis (SonarQube/CodeQL) | Belum dipasang |
| Publikasi image Docker | Dockerfile ada di `DEPLOYMENT.md`, belum di-build otomatis |
| Deployment otomatis | Belum ada environment target |

Semuanya ada di [`../PLAN.md`](../PLAN.md).
