# 🔌 REST API

## Overview

JuraganKost menyediakan REST API menggunakan **ASP.NET Core Minimal API**. Semua endpoint berada di route prefix `/api/v1/`.

**Swagger UI:** `http://localhost:5085/swagger`

---

## Endpoints

### 🚪 Kamar

| Method | URL | Deskripsi |
|---|---|---|
| `GET` | `/api/v1/kamar` | List semua kamar (opsional filter: `?kostId=1`) |
| `GET` | `/api/v1/kamar/{id}` | Detail kamar by ID |
| `POST` | `/api/v1/kamar` | Tambah kamar baru |
| `PUT` | `/api/v1/kamar/{id}` | Update kamar |
| `DELETE` | `/api/v1/kamar/{id}` | Hapus kamar |

**Contoh Request:**
```json
POST /api/v1/kamar
{
    "nomorKamar": "KM-013",
    "kostId": 1,
    "hargaSewa": 1500000,
    "deposit": 1500000,
    "jenis": "Premium",
    "status": "Kosong",
    "luas": 20,
    "fasilitas": "[\"AC\",\"WiFi\",\"Kamar Mandi Dalam\"]"
}
```

### 👥 Penghuni

| Method | URL | Deskripsi |
|---|---|---|
| `GET` | `/api/v1/penghuni` | List semua penghuni |
| `GET` | `/api/v1/penghuni/{id}` | Detail penghuni |
| `POST` | `/api/v1/penghuni` | Tambah penghuni baru |

### 📋 Tagihan

| Method | URL | Deskripsi |
|---|---|---|
| `GET` | `/api/v1/tagihan` | List semua tagihan |
| `GET` | `/api/v1/tagihan/{id}` | Detail tagihan |

### 💵 Pembayaran

| Method | URL | Deskripsi |
|---|---|---|
| `GET` | `/api/v1/pembayaran` | List semua pembayaran |
| `POST` | `/api/v1/pembayaran` | Buat pembayaran baru |
| `POST` | `/api/v1/pembayaran/{id}/verifikasi?diterima=true` | Verifikasi pembayaran |

### 🔧 Komplain

| Method | URL | Deskripsi |
|---|---|---|
| `GET` | `/api/v1/komplain` | List semua komplain |
| `POST` | `/api/v1/komplain` | Buat komplain baru |

### 📡 IoT

| Method | URL | Deskripsi |
|---|---|---|
| `GET` | `/api/v1/iot/latest?kamarId=1` | Data sensor terbaru |
| `GET` | `/api/v1/iot/history/{deviceId}?hours=24` | Riwayat sensor |
| `POST` | `/api/v1/iot/record` | Rekam data sensor |
| `POST` | `/api/v1/iot/simulate/{kostId}` | Simulasi data sensor |

### 📊 Dashboard

| Method | URL | Deskripsi |
|---|---|---|
| `GET` | `/api/v1/dashboard/{kostId}` | Ringkasan dashboard |

**Contoh Response:**
```json
{
    "totalKamar": 8,
    "kamarTerisi": 6,
    "kamarKosong": 1,
    "kamarBooking": 1,
    "okupansi": 75.0,
    "pemasukanBulanIni": 12000000,
    "piutang": 4500000,
    "totalPenghuni": 6,
    "komplainPending": 1
}
```

### 🏪 Marketplace

| Method | URL | Deskripsi |
|---|---|---|
| `GET` | `/api/v1/marketplace` | Listing kost publik |

### 🗄️ Storage

| Method | URL | Deskripsi |
|---|---|---|
| `POST` | `/api/v1/storage/upload` | Upload file (multipart form) |
| `GET` | `/api/v1/storage/list?prefix=kamar` | List file |
| `DELETE` | `/api/v1/storage/{fileKey}` | Hapus file |

### 💬 Chat

| Method | URL | Deskripsi |
|---|---|---|
| `POST` | `/api/v1/chat/send` | Kirim pesan ke Mpok Inem |
| `POST` | `/api/v1/chat/reset?sessionId=abc` | Reset session chat |

### 📥 Export

| Method | URL | Deskripsi |
|---|---|---|
| `GET` | `/api/v1/export/kamar/csv` | Export kamar CSV |
| `GET` | `/api/v1/export/kamar/excel` | Export kamar Excel |
| `GET` | `/api/v1/export/penghuni/csv` | Export penghuni CSV |
| `GET` | `/api/v1/export/penghuni/excel` | Export penghuni Excel |
| `GET` | `/api/v1/export/tagihan/csv` | Export tagihan CSV |
| `GET` | `/api/v1/export/tagihan/excel` | Export tagihan Excel |

---

## Chat API Example

```json
POST /api/v1/chat/send
{
    "sessionId": "a1b2c3d4",
    "message": "Ada kamar kosong apa saja?",
    "imageUrl": null,
    "documentUrl": null
}

// Response:
{
    "response": "🏠 **Kamar Kosong:**\n- **KM-010** (Standar) - Rp800.000/bln...",
    "sessionId": "a1b2c3d4"
}
```
