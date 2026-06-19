# 🤖 AI & Si Mak Comblang — Dokumentasi

## Overview

Si Mak Comblang adalah chatbot AI perjodohan yang dibangun menggunakan **Microsoft Semantic Kernel**. Dia bisa membantu pengguna mencari jodoh, memberi tips kencan, menganalisis kecocokan, dan banyak lagi.

---

## Arsitektur AI

```
┌──────────────────────────────────────────────┐
│              SiMakComblang.razor              │
│         (Blazor Chat UI Component)            │
│   ┌──────────────────────────────────────┐   │
│   │  ChatSession → SimpleMessage List    │   │
│   │  _kernelHistory → (role,content)[]   │   │
│   └──────────────┬───────────────────────┘   │
└──────────────────┼───────────────────────────┘
                   │ ChatSimpleAsync()
┌──────────────────┴───────────────────────────┐
│              KernelService                     │
│   ┌──────────────────────────────────────┐   │
│   │  BuildKernel(model) → Kernel         │   │
│   │  GetChatService() → IChatCompletion  │   │
│   └──────────────┬───────────────────────┘   │
└──────────────────┼───────────────────────────┘
                   │
┌──────────────────┴───────────────────────────┐
│           Semantic Kernel + Plugins            │
│   ┌──────────────────────────────────────┐   │
│   │  ComblangFunctions Plugin             │   │
│   │  ├── SearchInternet (Tavily API)      │   │
│   │  ├── ScrapPage (Web scraper)          │   │
│   │  ├── ReadFileFromUrl                  │   │
│   │  ├── QueryUserProfiles (Database)     │   │
│   │  ├── CalculateCompatibility           │   │
│   │  └── GetCurrentDateTime (WIB)         │   │
│   └──────────────────────────────────────┘   │
└──────────────────────────────────────────────┘
```

---

## Model AI yang Didukung

### 1. OpenAI
```json
{
  "AI": {
    "Models": {
      "OpenAI": {
        "ApiKey": "sk-...",
        "ModelId": "gpt-4o",
        "Endpoint": ""
      }
    }
  }
}
```
- **Model rekomendasi**: `gpt-4o`, `gpt-4o-mini`, `gpt-3.5-turbo`
- Gunakan endpoint kosong untuk official API, atau isi untuk Azure OpenAI

### 2. Anthropic Claude
```json
{
  "AI": {
    "Models": {
      "Anthropic": {
        "ApiKey": "sk-ant-...",
        "ModelId": "claude-3-5-sonnet-20241022",
        "Endpoint": "https://api.anthropic.com/v1"
      }
    }
  }
}
```
- **Model rekomendasi**: `claude-3-5-sonnet-20241022`, `claude-3-opus-20240229`

### 3. Google Gemini
```json
{
  "AI": {
    "Models": {
      "Gemini": {
        "ApiKey": "AIza...",
        "ModelId": "gemini-2.0-flash",
        "Endpoint": "https://generativelanguage.googleapis.com/v1beta/openai/"
      }
    }
  }
}
```
- **Model rekomendasi**: `gemini-2.0-flash`, `gemini-1.5-pro`

### 4. Ollama (Lokal)
```bash
# Install Ollama: https://ollama.com
ollama pull llama3.2
```
```json
{
  "AI": {
    "Models": {
      "Ollama": {
        "ModelId": "llama3.2",
        "Endpoint": "http://localhost:11434"
      }
    }
  }
}
```
- **Model rekomendasi**: `llama3.2`, `mistral`, `phi3`, `gemma2`
- GRATIS, berjalan lokal, tidak perlu API key!

---

## Kernel Functions

Fungsi-fungsi yang bisa dipanggil Si Mak Comblang:

| Function | Deskripsi | Trigger |
|----------|-----------|---------|
| `SearchInternet` | Mencari di internet via Tavily API | Otomatis oleh AI |
| `ScrapPage` | Membaca konten halaman web | Otomatis oleh AI |
| `ReadFileFromUrl` | Membaca file dari URL | Otomatis oleh AI |
| `QueryUserProfiles` | Mencari profil user di database | Otomatis oleh AI |
| `CalculateCompatibility` | Menghitung skor kecocokan 2 user | Otomatis oleh AI |
| `GetCurrentDateTime` | Mendapatkan waktu WIB | Otomatis oleh AI |

> Fungsi-fungsi ini diinvoke otomatis oleh Semantic Kernel melalui **Auto Function Calling** ketika AI menilai perlu.

---

## Konfigurasi Si Mak Comblang

```json
{
  "SiMakComblang": {
    "Model": "OpenAI",
    "SystemPrompt": "Kamu adalah Si Mak Comblang...",
    "Temperature": 0.7,
    "MaxTokens": 2048,
    "WelcomeMessage": "Halo! Aku Si Mak Comblang...",
    "Functions": {
      "SearchInternet": true,
      "ScrapPage": true,
      "ReadFileFromUrl": true,
      "QueryDatabase": true,
      "MatchmakingAlgorithm": true
    }
  }
}
```

| Setting | Default | Deskripsi |
|---------|---------|-----------|
| `Model` | `OpenAI` | Model AI default |
| `SystemPrompt` | (persona Mak Comblang) | Prompt sistem / persona AI |
| `Temperature` | `0.7` | Kreativitas respons (0.0-2.0) |
| `MaxTokens` | `2048` | Maksimum token per respons |
| `WelcomeMessage` | (pesan sambutan) | Pesan awal saat chat baru |
| `Functions.*` | `true` | Enable/disable kernel functions |

---

## Tavily Search API

Untuk mengaktifkan pencarian internet, daftar di [tavily.com](https://tavily.com) dan dapatkan API key gratis:

```json
{
  "Tavily": {
    "ApiKey": "tvly-xxxxxxxxxxxxxxxxxxxx"
  }
}
```

---

## Chat Flow

```
User: "Cari jodoh buat aku dong Mak!"
  │
  ▼
SiMakComblang.razor → KernelService.ChatSimpleAsync()
  │
  ▼
Semantic Kernel memproses:
  1. System prompt ditambahkan
  2. Chat history ditambahkan
  3. AI memutuskan apakah perlu memanggil function
     ├── Ya → Panggil function → hasil ke AI → respons final
     └── Tidak → Respons langsung
  │
  ▼
Respons dirender dengan MarkdownRenderer
```

---

## Tips Performa

1. **Gunakan Ollama lokal** untuk development — gratis & cepat
2. **Temperature rendah (0.3-0.5)** untuk respons faktual
3. **Temperature tinggi (0.8-1.0)** untuk respons kreatif
4. **MaxTokens 1024** cukup untuk sebagian besar use case
5. Matikan functions yang tidak diperlukan untuk mengurangi latency
