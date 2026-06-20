# 📚 PDA Documentation Index

Selamat datang di dokumentasi lengkap **PDA (Personal Data Analyst)**!

---

## 📖 Daftar Dokumentasi

| Dokumen | Deskripsi |
|---------|-----------|
| [Getting Started](GETTING_STARTED.md) | Panduan instalasi dan menjalankan aplikasi pertama kali |
| [User Guide](USER_GUIDE.md) | Panduan penggunaan fitur-fitur utama aplikasi |
| [Architecture](ARCHITECTURE.md) | Arsitektur sistem, komponen, dan alur kerja |
| [Configuration](CONFIGURATION.md) | Panduan konfigurasi appsettings.json dan environment |
| [API Reference](API_REFERENCE.md) | Dokumentasi API endpoint, tools, dan kernel functions |
| [Deployment](DEPLOYMENT.md) | Panduan deployment ke production |
| [Database Guide](DATABASE_GUIDE.md) | Skema database, migrasi, dan sample data |
| [LLM Integration](LLM_INTEGRATION.md) | Integrasi LLM providers dan konfigurasi |
| [RAG Guide](RAG_GUIDE.md) | Panduan Knowledge Base indexing (RAG) |
| [FAQ](FAQ.md) | Pertanyaan yang sering diajukan |

---

## 🚀 Mulai Cepat

```bash
# Clone dan jalankan
cd PDA
dotnet run

# Buka browser
https://localhost:5001
```

**Sample Login:**
- Admin: `admin@pda.com` / `Admin@123`
- User: `user@pda.com` / `User@1234`

---

## 🏗️ Tech Stack

| Teknologi | Versi | Keterangan |
|-----------|-------|------------|
| .NET | 10.0 | Runtime & SDK |
| Blazor Server | - | UI Framework |
| Entity Framework Core | 10.0 | ORM |
| SQLite | - | Database default |
| ASP.NET Core Identity | - | Authentication |
| Chart.js | 4.4 | Chart visualizations |
| Markdig | 1.3 | Markdown rendering |

---

## 📝 Changelog

### v1.0.0 (Current)
- ✅ Multi-database connection support (SQLite, SQLServer, PostgreSQL)
- ✅ Chat with Data Agent with LLM tools
- ✅ Multi-LLM provider support (OpenAI, Anthropic, Gemini, Ollama)
- ✅ RAG Knowledge Base indexing
- ✅ Dashboard & Report generation
- ✅ Monitoring dashboard
- ✅ Audit logs
- ✅ Authentication & authorization
- ✅ Neo Brutalism Soft UI (Dark/Light theme)
- ✅ Responsive design
- ✅ Sample data seeder
