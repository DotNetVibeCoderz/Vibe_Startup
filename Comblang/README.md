# 💕 Comblang — Aplikasi Pencarian Jodoh

> _Dibantu Si Mak Comblang, asisten AI perjodohan yang siap membantu kamu menemukan cinta sejati!_

![Platform](https://img.shields.io/badge/platform-.NET%2010-blueviolet)
![Blazor](https://img.shields.io/badge/Blazor-Server-ff69b4)
![License](https://img.shields.io/badge/license-MIT-green)
![Status](https://img.shields.io/badge/build-success-brightgreen)

---

## ✨ Fitur Utama

### 🤖 Si Mak Comblang — AI Chatbot Perjodohan
- Chat multi-sesi dengan history
- Dukungan multi-model: **OpenAI GPT-4o**, **Anthropic Claude 3.5**, **Google Gemini 2.0**, **Ollama (Llama)**
- Attach gambar & dokumen
- Markdown rendering (tabel, code, media, emoji)
- Kernel Functions: Tavily search, web scraper, DB query, kalkulator kecocokan
- System prompt & temperature dikonfigurasi via `appsettings.json`

### 💝 Swipe Matching
- Swipe kanan (like) / kiri (skip) / Super Like ⭐
- Algoritma rekomendasi berbasis preferensi & lokasi
- Skor kecocokan (Compatibility Score) 0-100
- Filter radius pencarian (slider 5-200 km)

### 💬 Chat & Messaging
- Real-time chat via **SignalR** WebSocket
- Bubble chat UI untuk pengirim & penerima
- Siap integrasi voice/video call & virtual gifts

### 📊 Dashboard Analytics
- Statistik pengguna, match, pesan
- Insight penggunaan Si Mak Comblang
- Metrik match rate & skor kecocokan

### 🛡️ Keamanan & Privasi
- Autentikasi ganda: **Cookie** + **JWT Bearer Token**
- Role-based access (User, Admin)
- Block & Report system
- Audit logging middleware
- API Key authentication untuk REST API

### 🗄️ Database & Storage Fleksibel
| Komponen | Opsi |
|----------|------|
| **Database** | SQLite, SQL Server, MySQL, PostgreSQL |
| **Storage** | FileSystem, AWS S3, Azure Blob, MinIO |
| **Cache** | In-Memory, Redis (siap) |
| **AI Model** | OpenAI, Anthropic, Gemini, Ollama |

---

## 🚀 Quick Start

### Prasyarat
- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
- (Opsional) Docker untuk Redis / MinIO

### 1. Clone & Run
```bash
git clone https://github.com/your-org/Comblang.git
cd Comblang
dotnet run
```

Buka **https://localhost:5001** di browser kamu!

### 2. Konfigurasi AI Model

Edit `appsettings.json`:

```json
{
  "AI": {
    "Models": {
      "OpenAI": {
        "ApiKey": "sk-xxxxxxxxxxxxxxxxxxxx",
        "ModelId": "gpt-4o"
      }
    }
  },
  "SiMakComblang": {
    "Model": "OpenAI"
  }
}
```

**Menggunakan Ollama (lokal, gratis):**
```bash
# Install Ollama dulu: https://ollama.com
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
  },
  "SiMakComblang": {
    "Model": "Ollama"
  }
}
```

### 3. Pilih Storage Provider

```json
// FileSystem (default)
{ "Storage": { "Provider": "FileSystem" } }

// AWS S3
{ "Storage": { "Provider": "S3", "S3": { "Bucket": "my-bucket", "Region": "ap-southeast-1", "AccessKey": "...", "SecretKey": "..." } } }

// Azure Blob
{ "Storage": { "Provider": "AzureBlob", "AzureBlob": { "ConnectionString": "...", "Container": "comblang" } } }

// MinIO
{ "Storage": { "Provider": "MinIO", "MinIO": { "Endpoint": "localhost:9000", "AccessKey": "minioadmin", "SecretKey": "minioadmin" } } }
```

---

## 📁 Struktur Proyek

```
Comblang/
├── Components/           # Blazor UI Components
│   ├── Layout/           # MainLayout, NavMenu
│   ├── Pages/            # Semua halaman aplikasi
│   │   ├── Auth/         # Login, Register
│   │   ├── ChatBot/      # Si Mak Comblang 🤖
│   │   ├── Dashboard/    # Analytics
│   │   ├── Events/       # Community events
│   │   ├── Matching/     # Swipe page
│   │   ├── Premium/      # Virtual gifts
│   │   └── Social/       # Chat
│   └── Shared/           # Reusable components
├── Models/               # Entity Framework models
├── Data/                 # AppDbContext
├── Services/
│   ├── AI/               # Semantic Kernel + Kernel Functions
│   ├── Analytics/        # Traffic & match insights
│   ├── Auth/             # JWT + Cookie auth
│   ├── Chat/             # Chat service
│   ├── Location/         # Geo distance (Haversine)
│   ├── Matching/         # Matchmaking engine
│   └── Storage/          # FileSystem, S3, Azure, MinIO
├── API/                  # REST API endpoints + Swagger
├── Hubs/                 # SignalR real-time hubs
├── Middleware/           # Audit logging
├── docs/                 # Dokumentasi lengkap
└── appsettings.json      # Konfigurasi utama
```

---

## 📡 REST API

Swagger UI tersedia di **/api/docs** (development mode).

| Endpoint | Method | Deskripsi |
|----------|--------|-----------|
| `/api/auth/register` | POST | Registrasi user baru |
| `/api/auth/login` | POST | Login & dapatkan JWT token |
| `/api/auth/validate` | GET | Validasi token |

Autentikasi API: header `X-Api-Key` (default: `comblang-api-key-2024-secure`)

---

## 🧠 Teknologi

| Teknologi | Penggunaan |
|-----------|-----------|
| **.NET 10** | Runtime & SDK |
| **Blazor Server** | UI framework |
| **Entity Framework Core** | ORM Database |
| **Semantic Kernel** | AI orchestration |
| **SignalR** | Real-time WebSocket |
| **Swashbuckle** | Swagger/OpenAPI |
| **Markdig** | Markdown → HTML |
| **JWT** | Token authentication |
| **AWS SDK** | S3 storage |
| **Azure SDK** | Blob storage |
| **Minio SDK** | MinIO storage |

---

## 📖 Dokumentasi

Lihat folder [`docs/`](docs/) untuk dokumentasi lengkap:

- [📘 Architecture Overview](docs/architecture.md)
- [📘 AI & Si Mak Comblang](docs/ai-makcomblang.md)
- [📘 Storage Configuration](docs/storage.md)
- [📘 Database Setup](docs/database.md)
- [📘 Deployment Guide](docs/deployment.md)

---

## 🤝 Kontribusi

Pull request sangat diterima! Lihat [PLAN.md](PLAN.md) untuk roadmap fitur.

---

## 📄 Lisensi

MIT License — © 2025 Gravicode Studios

Dibuat dengan 💖 oleh **kang Fadhil** & tim Gravicode Studios.

---

> _Kalau proyek ini bermanfaat, traktir pulsa dong buat Si Mak Comblang! 💕_
> 🔗 https://studios.gravicode.com/products/budax
