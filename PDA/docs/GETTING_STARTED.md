# 🚀 Getting Started

Panduan lengkap untuk menginstall dan menjalankan PDA pertama kali.

---

## 1. Prasyarat

### Software yang Dibutuhkan

| Software | Versi Minimum | Download |
|----------|-------------|----------|
| .NET SDK | 10.0 | [dotnet.microsoft.com](https://dotnet.microsoft.com/download/dotnet/10.0) |
| Git | 2.x | [git-scm.com](https://git-scm.com/) |
| Browser Modern | Chrome/Firefox/Edge | - |

### Opsional (untuk production)

| Software | Keterangan |
|----------|------------|
| SQL Server | Untuk production database |
| PostgreSQL | Untuk production database |
| MySQL | Untuk production database |
| Docker | Untuk containerized deployment |
| Nginx/Apache | Reverse proxy |

---

## 2. Instalasi

### Clone Project

```bash
git clone <repository-url>
cd PDA
```

### Restore Packages

```bash
dotnet restore
```

Proses ini akan mendownload semua NuGet packages yang dibutuhkan.

### Build Project

```bash
dotnet build
```

Pastikan build sukses dengan `0 Error(s)`.

---

## 3. Konfigurasi Awal

### API Key LLM

Edit `appsettings.json` dan tambahkan API key untuk LLM provider yang ingin digunakan:

```json
{
  "LLM": {
    "DefaultProvider": "OpenAI",
    "DefaultModel": "gpt-4o",
    "Providers": {
      "OpenAI": {
        "ApiKey": "sk-your-openai-api-key",
        "Endpoint": "https://api.openai.com/v1"
      }
    }
  }
}
```

> **💡 Tip:** Untuk development, Anda bisa menggunakan Ollama (gratis, lokal). Install Ollama lalu set `DefaultProvider` ke `Ollama`.

### Ollama Setup (Gratis)

```bash
# Install Ollama dari https://ollama.com
# Pull model
ollama pull llama3.1

# Di appsettings.json:
"DefaultProvider": "Ollama",
"Providers": {
  "Ollama": {
    "Endpoint": "http://localhost:11434",
    "Models": ["llama3.1", "mistral"]
  }
}
```

---

## 4. Menjalankan Aplikasi

### Development Mode

```bash
dotnet run
```

Aplikasi akan berjalan di:
- `https://localhost:5001`
- `http://localhost:5000`

### Watch Mode (Hot Reload)

```bash
dotnet watch run
```

---

## 5. Pertama Kali Login

Setelah aplikasi berjalan, database SQLite akan otomatis dibuat dan di-seed dengan data sample.

### Sample Users

| Email | Password | Role | Deskripsi |
|-------|----------|------|-----------|
| `admin@pda.com` | `Admin@123` | Admin | Akses penuh, monitoring, audit logs |
| `user@pda.com` | `User@1234` | User | Akses standar |
| `analyst@pda.com` | `Analyst@123` | Analyst | Akses analyst |

### Halaman yang Tersedia

| Halaman | URL | Akses |
|---------|-----|-------|
| Home | `/` | Semua user (login) |
| Chat | `/chat` | Semua user |
| Connections | `/connections` | Semua user |
| RAG Index | `/rag-index` | Semua user |
| Profile | `/profile` | Semua user |
| Monitoring | `/monitoring` | Admin only |
| Audit Logs | `/audit-logs` | Admin only |
| Login | `/login` | Public |
| Register | `/register` | Public |

---

## 6. Struktur Folder Penting

```
PDA/
├── appsettings.json          # Konfigurasi utama
├── KnowledgeBase/             # Folder untuk dokumen RAG
├── wwwroot/uploads/           # Upload file storage
├── PDA.db                    # Database SQLite (auto-generated)
└── docs/                     # Dokumentasi
```

---

## 7. Troubleshooting

### Port sudah digunakan
```bash
# Ubah port di Properties/launchSettings.json
# Atau gunakan:
dotnet run --urls "https://localhost:7001"
```

### Error SQLite
```bash
# Hapus database dan rebuild
rm PDA.db
dotnet run
```

### Error "API Key not configured"
- Pastikan API key LLM sudah diisi di `appsettings.json`
- Atau gunakan Ollama (gratis) untuk development

### Error "dotnet not found"
- Install .NET SDK 10.0 dari [dotnet.microsoft.com](https://dotnet.microsoft.com)
- Verifikasi: `dotnet --version`

---

## 8. Next Steps

1. ✅ **Connect database** - Tambahkan koneksi database di halaman Connections
2. ✅ **Start chatting** - Buat sesi chat dan mulai bertanya tentang data
3. ✅ **Add knowledge** - Letakkan dokumen di folder `KnowledgeBase/`
4. ✅ **Configure LLM** - Sesuaikan model dan parameter di UI atau config
5. ✅ **Explore features** - Coba dashboard, monitoring, dan audit logs

---

> *Butuh bantuan? Hubungi kami di [GraviCode Studios](https://studios.gravicode.com)*
