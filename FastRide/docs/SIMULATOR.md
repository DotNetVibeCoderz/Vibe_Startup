# 🎮 Simulator — FastRide

Aplikasi konsol yang menjalankan **API sungguhan** dengan penumpang dan driver simulasi.
Berguna untuk demo, uji beban ringan, dan mengisi dashboard dengan lalu lintas hidup.

```bash
dotnet run --project FastRide.Simulator
```

---

## Argumen

| Argumen | Bawaan | Keterangan |
|---------|--------|------------|
| `--riders <n>` | 6 | Jumlah penumpang simulasi |
| `--drivers <n>` | 4 | Jumlah driver simulasi |
| `--duration <detik>` | 0 | Berhenti otomatis. `0` = jalan sampai ditekan `S` |
| `--url <base>` | `https://localhost:5001` | Alamat API |

```bash
# uji beban 2 menit
dotnet run --project FastRide.Simulator -- --riders 25 --drivers 10 --duration 120

# terhadap API di port HTTP
dotnet run --project FastRide.Simulator -- --url http://localhost:5000 --duration 30
```

> `Simulation:DurationSeconds` sekarang benar-benar dibaca. Di versi sebelumnya nilai itu
> ada di `appsettings.json` tetapi tidak pernah dipakai — simulasi selalu berjalan
> tanpa batas.

---

## Kontrol

| Tombol | Aksi |
|--------|------|
| `S` | Berhenti dan tampilkan ringkasan |
| `P` | Jeda / lanjut |

Bila stdin di-*redirect* (CI, pipe), pembaca keyboard tidak dijalankan — gunakan
`--duration`. Simulator akan mengingatkan bila dijalankan tanpa keduanya.

---

## Yang dilakukan simulator

### Persiapan

1. Cek `/api/health`; berhenti dengan pesan jelas bila API tidak hidup.
2. Masuk sebagai **admin** (diperlukan untuk menyetujui dokumen driver).
3. Daftarkan penumpang simulasi. Bila email sudah ada dari run sebelumnya, simulator masuk
   memakai akun itu alih-alih gagal.
4. Daftarkan driver simulasi, lalu **jalani alur verifikasi sungguhan**: unggah SIM, STNK,
   dan KTP, lalu setujui ketiganya sebagai admin.
5. Kirim posisi GPS awal dan set driver menjadi `Online`.

Langkah 4 bukan basa-basi: driver yang belum terverifikasi memang tidak bisa online, jadi
tanpa itu simulator tidak akan pernah menghasilkan satu perjalanan pun.

### Perilaku penumpang

```
minta harga (quote) ──► pesan ──► pantau sampai status akhir ──► beri ulasan ──► ulangi
                                      │
                                      └─► sebagian membatalkan sebelum dijemput
```

Penumpang menunggu perjalanannya selesai sebelum memesan lagi — API memang menolak
pemesanan kedua selagi ada perjalanan berjalan.

### Perilaku driver

```
geser posisi & kirim GPS ──► ambil daftar order ──► terima ──► tiba ──► mulai ──► selesai
```

Beberapa driver akan berebut order yang sama. Yang kalah menerima `409` dan lanjut — persis
seperti aplikasi sungguhan.

---

## Tampilan langsung

```
╭────────────┬──────────────────┬──────────────────┬─────────────┬─────────────╮
│ Kode       │ Penumpang        │ Driver           │ Tarif       │ Status      │
├────────────┼──────────────────┼──────────────────┼─────────────┼─────────────┤
│ FR-4W8K5G  │ SimRider 18110   │ SimDriver 1811   │ Rp 110,480  │ Selesai     │
│ FR-CQUJVU  │ SimRider 18110   │ SimDriver 1811   │ Rp 113,140  │ Driver tiba │
│ FR-VGHMQD  │ SimRider 18110   │ —                │ Rp 46,100   │ Menunggu    │
╰────────────┴──────────────────┴──────────────────┴─────────────┴─────────────╯
╭─────────────┬──────────────────╮
│ Metrik      │ Nilai            │
├─────────────┼──────────────────┤
│ Waktu       │ 00:29 / 30s      │
│ Dibuat      │ 17               │
│ Diterima    │ 18               │
│ Selesai     │ 10               │
│ Dibatalkan  │ 2                │
│ Ulasan      │ 7                │
│ Order/menit │ 34.2             │
│ Request     │ 271 (0.0% gagal) │
│ Latensi p50 │ 3 ms             │
│ Latensi p95 │ 72 ms            │
│ Latensi max │ 454 ms           │
╰─────────────┴──────────────────╯
```

**Kenapa "Diterima" bisa melebihi "Dibuat":** driver simulasi juga mengambil order yang
sudah ada di database dari data contoh, bukan hanya yang dibuat pada run ini.

### Metrik

| Metrik | Arti |
|--------|------|
| Order/menit | Laju pembuatan order |
| Request gagal | Persentase respons non-2xx **dan** exception |
| Latensi p50 / p95 / max | Diambil dari 5.000 permintaan terakhir |

Persentil latensi adalah inti dari uji beban: hitungan "berhasil/gagal" saja tidak bisa
membedakan platform yang sehat dari yang sekadar lambat. Versi sebelumnya hanya mencatat
`API ok / API fail`.

---

## Konfigurasi

`FastRide.Simulator/appsettings.json`:

```json
{
  "ApiSettings": { "BaseUrl": "https://localhost:5001" },
  "Simulation": {
    "RiderCount": 6,
    "DriverCount": 4,
    "DurationSeconds": 0,
    "RandomSeed": 42,
    "CancelRate": 0.08,
    "AdminEmail": "admin@fastride.com",
    "AdminPassword": "Password123",
    "Password": "SimPass123!"
  }
}
```

Bisa juga lewat variabel lingkungan (`Simulation__RiderCount=20`). Argumen baris perintah
menimpa keduanya.

`RandomSeed` membuat pola perjalanan dapat diulang antar-run.

---

## Autentikasi

Setiap penumpang dan driver simulasi punya `HttpClient` dan token JWT sendiri.

> Versi sebelumnya memasang satu header `Authorization` — milik penumpang — pada satu
> `HttpClient` bersama, lalu memakainya untuk memanggil endpoint driver. Itu hanya bisa
> berjalan karena saat itu tidak ada endpoint yang benar-benar terlindungi. Sekarang
> simulator menjalani otorisasi yang sama persis dengan aplikasi mobile.

---

## Menjalankan bersama dashboard

Buka tiga terminal untuk demo yang hidup:

```bash
dotnet run --project FastRide.Api                    # 1
dotnet run --project FastRide.AdminWeb               # 2 → https://localhost:5002
dotnet run --project FastRide.Simulator -- --riders 15 --drivers 6   # 3
```

Panel "Ritme hari ini" dan rel *Fleet Pulse* di konsol admin akan bergerak dalam hitungan
detik.

---

## Membersihkan

Akun simulasi tetap tersimpan di database. Untuk mengosongkan:

```bash
rm FastRide.Api/FastRide.db
dotnet run --project FastRide.Api    # data contoh dibuat ulang
```

---

## Pemecahan masalah

| Gejala | Penyebab |
|--------|----------|
| "Tidak bisa menghubungi API" | API belum jalan, atau `--url` salah port |
| "Gagal masuk sebagai admin" | Kata sandi admin berbeda dari `Simulation:AdminPassword` |
| Banyak `409` | Normal — driver berebut order yang sama |
| Order dibuat tapi tak pernah diterima | Semua driver simulasi gagal diverifikasi; cek log API |
| Latensi p95 melonjak | Coba `Cache:Provider: Redis`, atau pindah dari SQLite |
