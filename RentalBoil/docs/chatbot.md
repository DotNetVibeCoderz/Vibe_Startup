# 🤖 Chat Bot AI — Bang Tony Brewok

## Overview

RentalBoil mengintegrasikan AI chat bot menggunakan **Microsoft Semantic Kernel** yang mendukung 4 provider: OpenAI, Anthropic Claude, Google Gemini, dan Ollama (lokal).

Nama bot: **Bang Tony Brewok** — asisten virtual yang ramah, humoris, dan siap membantu 24/7.

---

## Arsitektur

```
┌──────────┐     ┌─────────────────┐     ┌──────────────────┐
│  Chat UI │────→│   BotService    │────→│  Semantic Kernel │
│ (Blazor) │     │                 │     │  (AI Provider)   │
└──────────┘     │ Provider Router │     └──────────────────┘
                 │ OpenAI/Claude/  │              │
                 │ Gemini/Ollama   │              ↓
                 └─────────────────┘     ┌──────────────────┐
                         │               │  Kernel Functions │
                         │               │  (17 functions)  │
                         ↓               └──────────────────┘
                 ┌─────────────────┐              │
                 │ Chat History DB │              ↓
                 │ (SQLite/etc)    │     ┌──────────────────┐
                 └─────────────────┘     │   AppDbContext   │
                                         │   Tavily API     │
                                         │   HttpClient     │
                                         └──────────────────┘
```

---

## Konfigurasi

Semua pengaturan di `appsettings.json`:

```json
{
  "AI": {
    "Provider": "OpenAI",
    "ProviderOptions": ["OpenAI", "Anthropic", "Gemini", "Ollama"],
    "OpenAI": { "ApiKey": "", "Model": "gpt-4o-mini" },
    "Anthropic": { "ApiKey": "", "Model": "claude-3-haiku-20240307" },
    "Gemini": { "ApiKey": "", "Model": "gemini-2.0-flash" },
    "Ollama": { "Endpoint": "http://localhost:11434", "Model": "llama3.2" }
  },
  "ChatBot": {
    "Name": "Bang Tony Brewok",
    "SystemPrompt": "...",
    "Temperature": 0.7,
    "MaxTokens": 2000,
    "TopP": 0.9
  }
}
```

---

## Kernel Functions (17)

Bang Tony Brewok dapat melakukan aksi nyata melalui **kernel functions**:

| # | Function | Deskripsi | Akses Data |
|---|----------|-----------|------------|
| 1 | `get_current_datetime` | Waktu WIB (UTC+7) | System |
| 2 | `get_day_of_week` | Hari dari tanggal | System |
| 3 | `math_calculate` | Kalkulasi matematika | System |
| 4 | `convert_currency_simulation` | Konversi mata uang (simulasi) | System |
| 5 | `search_internet` | Cari di internet via Tavily | Tavily API |
| 6 | `scrap_web_page` | Ambil konten halaman web | HTTP |
| 7 | `read_file_from_url` | Baca file teks/JSON/CSV | HTTP |
| 8 | `search_vehicles_db` | Cari kendaraan (case-insensitive) | DB |
| 9 | `get_vehicle_detail_db` | Detail kendaraan | DB |
| 10 | `create_booking_via_chat` | Booking via chat | DB |
| 11 | `check_booking_status` | Cek status order | DB |
| 12 | `get_vehicle_position` | Posisi GPS + IoT | DB |
| 13 | `get_active_promotions` | Promo & kupon aktif | DB |
| 14 | `get_faqs` | FAQ search (case-insensitive) | DB |
| 15 | `calculate_rental_price` | Estimasi biaya | DB |
| 16 | `get_weather_info` | Simulasi cuaca | Local |
| 17 | `get_platform_stats` | Statistik platform | DB |

---

## Cara Kerja Auto-Invoke

Dengan `ToolCallBehavior.AutoInvokeKernelFunctions`, AI otomatis memutuskan kapan harus memanggil kernel function:

```
User: "Cari mobil Avanza murah di Jakarta"

AI: [Deteksi intent = search]
    [Auto-panggil search_vehicles_db("Avanza", maxBudget=300000, location="Jakarta")]
    [Terima hasil dari database]
    
Bot: "🔍 Saya temukan 3 kendaraan:
      #1 Toyota Avanza 2024 - Rp 400.000/hari
      #2 Toyota Avanza 2023 - Rp 350.000/hari
      ..."
```

```
User: "Booking kendaraan #1 untuk 3 hari mulai 20 April"

AI: [Auto-panggil create_booking_via_chat(1, userId, "2025-04-20", 3)]
    [Booking tersimpan di DB]

Bot: "✅ Booking Berhasil!
      🎫 No: RB-20250420-0001
      🚗 Toyota Avanza 2024
      📅 20 Apr → 23 Apr (3 hari)
      💰 Rp 1.200.000"
```

---

## Provider Detail

### OpenAI (via Semantic Kernel)

- Menggunakan `Microsoft.SemanticKernel.Connectors.OpenAI`
- Chat completion dengan `gpt-4o-mini` (default)
- Support tool calling (kernel functions)

### Ollama (via OpenAI-compatible endpoint)

- Menggunakan endpoint `/v1` dari Ollama
- Model: `llama3.2` (default)
- Support tool calling via OpenAI compatibility layer
- Fallback message jika server tidak tersedia

### Anthropic Claude (HTTP REST)

- Direct HTTP call ke `https://api.anthropic.com/v1/messages`
- Header: `x-api-key`, `anthropic-version: 2023-06-01`
- Model: `claude-3-haiku-20240307`
- ⚠️ Tidak support kernel functions (Claude Messages API belum support native tool use di SK)

### Google Gemini (HTTP REST)

- Direct HTTP call ke `https://generativelanguage.googleapis.com/v1beta/models/{model}:generateContent`
- Query parameter: `?key={api_key}`
- Model: `gemini-2.0-flash`
- Support inline image data
- ⚠️ Tidak support kernel functions

---

## System Prompt

System prompt dikonfigurasi di `appsettings.json → ChatBot → SystemPrompt`. Default:

```
Kamu adalah Bang Tony Brewok, asisten virtual yang ramah dan humoris 
untuk aplikasi RentalBoil. Kamu membantu pelanggan mencari kendaraan, 
melakukan booking, dan menjawab pertanyaan seputar rental. Gunakan 
bahasa yang santai dan friendly, campurkan sedikit bahasa gaul Indonesia.
```

---

## Database Chat History

Semua percakapan disimpan di `ChatSessions` dan `ChatHistories`:

```sql
ChatSessions
├── Id, UserId, Title, Model, CreatedAt, IsActive

ChatHistories
├── Id, SessionId, Role (user/assistant), Content
├── ImageUrls, DocumentUrls, TokenCount, CreatedAt
```

### Fitur Multi-Session:
- User bisa punya banyak sesi chat
- Switch sesi via dropdown
- Reset sesi (soft delete)
- History dipertahankan antar login
