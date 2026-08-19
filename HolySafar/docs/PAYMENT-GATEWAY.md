# 💳 Payment Gateway — HolySafar

Modul pembayaran mendukung lima metode: **Manual (transfer + bukti)**, **Xendit**, **Midtrans**,
**Stripe**, dan **QRIS**. Semua diakses lewat satu service: `Services/PaymentGatewayService.cs`.

---

## Arsitektur singkat

```
Jamaah (/pembayaran atau /marketplace)
        │  pilih provider + nominal
        ▼
PaymentGatewayService.CreateTransactionAsync()
        │  simpan PaymentTransaction (status Pending)
        │  panggil API provider
        ▼
   ┌────────────┬──────────────┬──────────────┬───────────┐
   │  Xendit    │  Midtrans    │   Stripe     │   QRIS    │  Manual
   │  invoice   │  Snap token  │  Checkout    │  payload  │  (instruksi
   │  URL       │  redirect    │  session URL │  EMVCo    │   transfer)
   └────────────┴──────────────┴──────────────┴───────────┘
        │  jamaah membayar
        ▼
POST /webhook/payment/{provider}   ← dipanggil server provider
        │  verifikasi token / signature
        ▼
PaymentGatewayService.MarkPaidAsync()   (idempotent)
        │
        ├─ ReferenceType "Cicilan" → tambah Cicilan (Dikonfirmasi=true),
        │                             update Pembayaran.TotalDibayar & Status
        └─ ReferenceType "Order"   → Order.StatusOrder = "Paid", isi PaidAt
                                      + kirim Notifikasi ke jamaah
```

Entitas: `PaymentTransaction` (`KodeTransaksi` unik, `Provider`, `ReferenceType`/`ReferenceId`,
`Jumlah`, `Status`, `ExternalId`, `PaymentUrl`, `QrString`, `RawResponse`).

---

## Konfigurasi

Semua kunci ada di `appsettings.json` bagian `Payment`, dan **bisa diubah dari UI**
di **Admin → Pengaturan → Pembayaran** (tersimpan di tabel `Pengaturan`, menimpa file konfigurasi
tanpa perlu restart).

| Kunci | Keterangan |
|-------|------------|
| `Payment:Provider` | Provider default yang terpilih di UI jamaah |
| `Payment:EnabledProviders` | Daftar provider yang boleh dipakai, dipisah koma |
| `Payment:Sandbox` | `true` = pakai endpoint sandbox (Midtrans), tampilkan penanda uji coba |
| `Payment:ExpiryHours` | Masa berlaku tagihan |
| `Payment:Xendit:SecretKey` / `CallbackToken` | Kredensial Xendit |
| `Payment:Midtrans:ServerKey` / `ClientKey` | Kredensial Midtrans |
| `Payment:Stripe:SecretKey` / `WebhookSecret` / `Currency` | Kredensial Stripe |
| `Payment:QRIS:StaticPayload` … `CallbackToken` | Data merchant QRIS |

Provider hanya muncul untuk jamaah bila **(a)** namanya ada di `EnabledProviders` **dan**
**(b)** kredensialnya sudah terisi (`PaymentGatewayService.IsConfigured`). `Manual` selalu tersedia.

---

## Webhook

| Provider | URL | Verifikasi |
|----------|-----|-----------|
| Xendit | `POST /webhook/payment/xendit` | header `x-callback-token` |
| Midtrans | `POST /webhook/payment/midtrans` | `signature_key` = SHA512(order_id + status_code + gross_amount + ServerKey) |
| Stripe | `POST /webhook/payment/stripe` | header `Stripe-Signature` (HMAC-SHA256 atas `t.payload`) |
| QRIS | `POST /webhook/payment/qris` | header `x-callback-token` |

> **Penting:** bila secret/token untuk sebuah provider belum diisi, webhook-nya **ditolak** (HTTP 400),
> bukan diterima. Tanpa aturan ini siapa pun bisa mengirim kode transaksi dan menandainya lunas.
> Untuk QRIS tanpa penyedia callback, konfirmasi dilakukan admin di **Admin → Transaksi → ✅ Lunas**.

Webhook berada di luar grup `/api`, jadi tidak memerlukan header `X-Api-Key`.
`MarkPaidAsync` bersifat idempotent — callback ganda tidak menggandakan cicilan.

---

## Catatan per provider

**Xendit** — `POST https://api.xendit.co/v2/invoices`, Basic auth `base64(secretKey + ":")`.
`external_id` diisi `KodeTransaksi` sehingga callback bisa dipetakan balik.

**Midtrans** — Snap `https://app.sandbox.midtrans.com/snap/v1/transactions` (atau tanpa `sandbox.`
saat produksi). `order_id` = `KodeTransaksi`. Respons memberi `token` dan `redirect_url`.

**Stripe** — Checkout Session, form-encoded, Bearer secret key. IDR/JPY/KRW/VND diperlakukan
sebagai *zero-decimal* (nominal dikirim apa adanya); mata uang lain dikali 100.
`client_reference_id` = `KodeTransaksi`.

**QRIS** — payload dibuat lokal mengikuti spesifikasi EMVCo:
- Bila admin mengisi **payload QRIS statis** dari penyedia, payload itu diubah menjadi dinamis
  (tag `01` → `12`), disisipi nominal (tag `54`) sebelum tag `58`, lalu **CRC16-CCITT-FALSE**
  (tag `63`) dihitung ulang. Ini cara yang paling akurat karena data merchant tetap asli.
- Bila tidak, payload dibangun dari `MerchantName`/`MerchantCity`/`NMID`/`MerchantId`.

Gambar QR dirender lewat `https://api.qrserver.com` (`PaymentGatewayService.QrImageUrl`).
Ini dependensi eksternal — bila perlu sepenuhnya offline, ganti dengan encoder QR lokal;
payload EMVCo-nya sendiri sudah dihasilkan di server dan tidak dikirim ke pihak mana pun.

**Manual** — tidak memanggil API. Jamaah melihat instruksi transfer dan mengunggah bukti;
bukti tersimpan sebagai `Cicilan` dengan `Dikonfirmasi = false` sampai admin memverifikasinya.

---

## Menguji tanpa akun provider

1. Biarkan `Payment:Provider` = `Manual` — alur pesan/tagihan bisa dicoba penuh.
2. Untuk mencoba QRIS, isi `Payment:QRIS:MerchantName` di menu Pengaturan; QR akan tergenerate.
3. Untuk mensimulasikan pembayaran berhasil, buka **Admin → Transaksi**, lalu tekan **✅ Lunas**.
   Efeknya identik dengan webhook: cicilan tercatat, tagihan berkurang, notifikasi terkirim.
