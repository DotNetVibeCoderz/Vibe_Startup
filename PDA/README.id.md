# 📊 PDA - Personal Data Analyst

> **Asisten analisis data bertenaga AI** - Ngobrol dengan data Anda menggunakan bahasa alami, didukung oleh LLM.

---

## ✨ Fitur Utama

### 🔗 Dukungan Multi-Database
Koneksi ke **SQLite, SQL Server, PostgreSQL** dan lainnya. Simpan koneksi, uji, dan ganti database dengan mudah.

### 💬 Chat dengan Data Agent
Tanyakan apa saja tentang data Anda dalam bahasa alami. AI agent dapat:
- Membuat dan menjalankan **query SQL** (read-only)
- Membuat **dashboard profesional** dan laporan
- Mencari di **Knowledge Base** (RAG)
- Mengambil **data eksternal** dari URL
- Format respons dengan **Markdown, tabel, grafik**

### 🤖 Dukungan Multi-LLM
Konfigurasi dan ganti antara:
- **OpenAI** (GPT-4o, GPT-4o-mini, GPT-4-turbo)
- **Anthropic** (Claude 3.5 Sonnet, Opus, Haiku)
- **Google Gemini** (2.0 Flash, 1.5 Pro)
- **Ollama** (model lokal)

### 📚 RAG - Index Knowledge Base
- Scan folder `KnowledgeBase/` secara periodik
- Index file **PDF, DOCX, XLSX, TXT, CSV, PPTX, MD**
- Pencarian vektor via **In-Memory** store

### 📈 Dashboard Monitoring
Metrik real-time: traffic web, penggunaan token, statistik query & chat, user aktif.

### 📋 Audit Logs
Pencatatan aktivitas lengkap dengan filter dan sorting.

### 🎨 UI Neo Brutalism Soft
- **Tema Dark & Light**
- Desain responsif
- Tampilan profesional dan modern

---

## 🚀 Memulai

### Prasyarat
- [.NET 10.0 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)

### Menjalankan
```bash
cd PDA
dotnet run
```
Buka `https://localhost:5001` di browser.

### Sample User
| Email | Password | Role |
|-------|----------|------|
| admin@pda.com | Admin@123 | Admin |
| user@pda.com | User@1234 | User |
| analyst@pda.com | Analyst@123 | Analyst |

### Konfigurasi LLM
Edit `appsettings.json` dan tambahkan API key:
```json
"LLM": {
  "Providers": {
    "OpenAI": { "ApiKey": "sk-api-key-anda" }
  }
}
```

---

## 📝 Lisensi
MIT © GraviCode Studios

---

*Dibuat dengan ❤️ oleh Jacky the Code Bender di [GraviCode Studios](https://studios.gravicode.com)*

> Kalau berkenan, traktir pulsa di https://studios.gravicode.com/products/budax 😄🙏
