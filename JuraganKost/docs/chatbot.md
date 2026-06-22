# 🤖 Chat Bot — Mpok Inem

## Overview

Mpok Inem adalah AI chat bot untuk pelayanan kost yang dibangun dengan **Microsoft Semantic Kernel**. Mendukung 4 provider AI dan 25 kernel functions.

## Arsitektur Chat

```
┌──────────────────────────────────────────────────┐
│                  Chat.razor                       │
│  ┌────────────┐  ┌─────────────┐  ┌───────────┐ │
│  │ Session List│  │ Chat Messages│  │ Quick Btns│ │
│  └────────────┘  └─────────────┘  └───────────┘ │
└──────────────────────┬───────────────────────────┘
                       │
┌──────────────────────▼───────────────────────────┐
│               ChatService (Singleton)            │
│  ┌──────────────────────────────────────────┐   │
│  │  Session Management  (memory + DB)       │   │
│  │  PersistSessionAsync / LoadUserSessions  │   │
│  └──────────────────────────────────────────┘   │
│  ┌──────────────────────────────────────────┐   │
│  │  AI Provider Selection                    │   │
│  │  OpenAI / Ollama (SK)                     │   │
│  │  Anthropic / Gemini (HTTP)               │   │
│  └──────────────────────────────────────────┘   │
│  ┌──────────────────────────────────────────┐   │
│  │  Kernel Functions (25 functions)         │   │
│  │  FunctionChoiceBehavior.Auto()           │   │
│  └──────────────────────────────────────────┘   │
└──────────────────────────────────────────────────┘
```

## AI Providers

| Provider | Library | Konfigurasi |
|---|---|---|
| 🧠 **OpenAI** | Semantic Kernel Connector | `ApiKey` + `ModelId` |
| 🦙 **Ollama** | SK OpenAI Connector via `/v1` | `Endpoint` + `ModelId` |
| 🔮 **Anthropic** | Custom HTTP Client | `ApiKey` + Messages API |
| 🌟 **Gemini** | Custom HTTP Client | `ApiKey` + Generative Language API |

**Konfigurasi di `appsettings.json`:**
```json
"ChatBot": {
  "ModelProvider": "OpenAI",
  "Providers": {
    "OpenAI": { "ModelId": "gpt-4o", "ApiKey": "sk-..." },
    "Ollama": { "ModelId": "llama3.1:latest", "Endpoint": "http://localhost:11434" },
    "Anthropic": { "ModelId": "claude-3-5-sonnet-20241022", "ApiKey": "..." },
    "Gemini": { "ModelId": "gemini-2.0-flash", "ApiKey": "..." }
  }
}
```

## 25 Kernel Functions

### 📅 Date & Time (5)
| Function | Deskripsi |
|---|---|
| `tanggal_sekarang` | Hari ini (format Indonesia) |
| `jam_sekarang` | Waktu WIB/WITA/WIT |
| `hitung_tanggal` | Selisih/tambah/kurang hari |
| `hari_apa` | Hari dari tanggal |
| `kalender_bulan` | Kalender bulanan |

### 🧮 Math (2)
| Function | Deskripsi |
|---|---|
| `kalkulator` | 17 operasi: +, -, ×, ÷, ^, %, √, sin, cos, tan, log, ln, abs, round, floor, ceil |
| `konversi_satuan` | Panjang, berat, suhu, luas, volume |

### 🎲 Random (3)
`angka_acak`, `lempar_koin`, `lempar_dadu`

### 📝 Text (4)
`hitung_karakter`, `ubah_kapitalisasi`, `balik_teks`, `enkripsi_sederhana`

### 💵 Finance (3)
`format_mata_uang`, `persentase`, `diskon_harga`

### ℹ️ System (1)
`info_sistem`

### 🌐 Web (2)
`search_internet` (Tavily), `scrap_webpage`

### 🗄️ Database (3)
`cari_kamar_kosong`, `info_kost`, `cek_tagihan`

### 🏠 Kost (2)
`bandingkan_harga`, `fasilitas_kost`

### 📁 File (1)
`baca_file_dari_url`

---

## Auto Function Calling

```csharp
// ChatService.cs
private OpenAIPromptExecutionSettings CreateSettings() => new()
{
    Temperature = 0.7,
    MaxTokens = 2000,
    FunctionChoiceBehavior = FunctionChoiceBehavior.Auto()  // 🔑 KEY!
};
```

Dengan `FunctionChoiceBehavior.Auto()`, AI otomatis mendeteksi kapan harus memanggil kernel function.

**Contoh:** User: "Tanggal sekarang" → AI auto invoke `tanggal_sekarang()` → return "📅 **Senin, 21 Juli 2025**"

## Session Persistence

- **Memory:** `Dictionary<string, ChatSession>` — akses cepat
- **Database:** `ChatThreads` + `ChatMessages` tables — survive restart

Setiap kirim pesan → `PersistSessionAsync()` menyimpan ke DB.
Saat login → `LoadUserSessionsAsync()` memuat semua sesi user dari DB.

## Fallback Mode

Jika API key tidak dikonfigurasi:
1. Cek `HasValidApiKey()` → false → skip AI call
2. Tampilkan pesan info + `GetLocalFallback()` — jalankan CommonFunctions langsung
3. Keyword matching: "tanggal sekarang", "kalkulator tambah", dll
