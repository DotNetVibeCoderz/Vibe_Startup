# 🔷 BlazePoint

**Platform kolaborasi ala SharePoint yang dibangun ulang dengan .NET 10 Blazor Server.**

> 🇬🇧 Read in [English](README.md)

BlazePoint menyatukan manajemen dokumen, custom list, halaman CMS dengan webpart, otomasi workflow, team sites, dan asisten AI ("Clippy") dalam satu aplikasi Blazor Server yang cepat dan ringan.

## ✨ Fitur

| Area | Sorotan |
|---|---|
| 📁 **Manajemen Dokumen** | Upload, versioning otomatis + rollback, metadata/tag, preview (gambar/PDF/video/audio/teks), recycle bin |
| 📋 **List & Library** | Custom list dengan kolom dinamis (Text/Number/Date/Choice/Boolean/Url), filter, grouping, sorting |
| 🔍 **Mesin Pencari** | Full-text + pencarian semantik berbasis vektor (Local dalam-DB / Qdrant / Chroma) dengan embedding pluggable (Local / OpenAI / Ollama) |
| 🔐 **Autentikasi** | Login/logout, registrasi, reset password, role-based access (Admin / Editor / Viewer) via ASP.NET Core Identity |
| ⚙️ **Otomasi Workflow** | Designer workflow drag-and-drop (node Start/Approval/Condition/Notify/End), tugas persetujuan, notifikasi |
| 📄 **CMS** | Editor halaman dengan webpart drag-and-drop (Teks/Markdown, Gambar, Jam, Kalkulator, Cuaca, Peta Leaflet, Dokumen, Event), masterpage kustom (News / Intranet / Web), versioning saat publish + rollback |
| 🧭 **Manajemen Navigasi** | Kelola top navigation & quick launch (menu samping) lewat UI |
| 🏢 **Team Sites** | Site collection per departemen dengan dokumen, list, event, dan diskusi terpisah |
| 💬 **Discussion Board** | Balasan berjenjang (threaded), @mention dengan notifikasi, markdown |
| 📅 **Kalender** | Tampilan bulan bersama, pengingat, feed ICS untuk subscribe dari Outlook/Google Calendar, tombol "Tambah ke Google Calendar" |
| 🔗 **Berbagi File** | Link publik/privat, tanggal kedaluwarsa, penghitung unduhan |
| 📝 **Form Designer** | Form builder drag-and-drop, export/import skema JSON, pustaka template, daftar jawaban |
| 📊 **Dashboard** | Kartu KPI, grafik ChartJs.Blazor (tren upload, storage per jenis, aktivitas), feed aktivitas terbaru |
| 🤖 **Chatbot AI Clippy** | Semantic Kernel dengan OpenAI / Anthropic / Gemini / Ollama, chat multi-sesi, lampiran gambar & dokumen, jawaban streaming ber-markdown, tool calling (pencarian internet Tavily, scrape URL, baca file, tanggal/waktu, kalkulasi, query data internal) |
| 🛠️ **Panel Admin** | Manajemen pengguna & role, pengaturan site, monitoring/audit log, reindex pencarian |
| 🌐 **API** | REST di `/api/*` dan GraphQL di `/graphql` |
| 🎨 **UI/UX** | Desain responsif gaya Facebook dengan tema light/dark |

## 🚀 Mulai Cepat

```bash
# prasyarat: .NET 10 SDK
cd src/BlazePoint
dotnet run
# buka http://localhost:5112
```

Saat pertama dijalankan aplikasi otomatis membuat database SQLite (`App_Data/blazepoint.db`), mengisi sample data, dan membangun index pencarian. **Tidak perlu API key** untuk fitur inti — pencarian semantik memakai embedder lokal sebagai fallback.

### Akun demo

| Role | Email | Password |
|---|---|---|
| 👑 Admin | `admin@blazepoint.local` | `Blaze123!` |
| ✏️ Editor | `editor@blazepoint.local` | `Blaze123!` |
| 👁️ Viewer | `viewer@blazepoint.local` | `Blaze123!` |

## ⚙️ Konfigurasi (`appsettings.json`)

Semua berbasis provider dan diatur di `src/BlazePoint/appsettings.json`:

| Bagian | Pilihan |
|---|---|
| `Database:Provider` | `Sqlite` (default) · `SqlServer` · `PostgreSql` · `MySql` — connection string di `ConnectionStrings` |
| `Storage:Provider` | `FileSystem` (default) · `AzureBlob` · `S3` · `MinIO` |
| `Search:VectorStore` | `Local` (default, cosine dalam database) · `Qdrant` · `Chroma` |
| `Search:Embeddings:Provider` | `Local` (default, tanpa key) · `OpenAI` · `Ollama` |
| `Clippy:Provider` | `OpenAI` · `Anthropic` · `Gemini` · `Ollama` — plus `SystemPrompt`, `Temperature`, `MaxTokens` |
| `Clippy:Tavily:ApiKey` | mengaktifkan tool pencarian internet Clippy |

Untuk mengaktifkan Clippy, isi API key provider pilihan Anda, contoh dengan Ollama (tanpa API key, cocok untuk uji lokal):

```json
"Clippy": {
  "Provider": "Ollama",
  "Ollama": { "Endpoint": "http://localhost:11434", "Model": "llama3.2" }
}
```

## 📚 Dokumentasi

- [Arsitektur](docs/architecture.md) — lapisan, model data, alur request
- [Referensi API](docs/api-reference.md) — REST & GraphQL
- [Panduan Konfigurasi](docs/configuration.md) — penjelasan tiap provider
- [WebPart & Masterpage Kustom](docs/custom-webparts.md) — memperluas CMS dengan komponen Razor sendiri
- [Sample Data & Pengguna](docs/sample-data.md) — apa saja yang di-seed

## 🧱 Teknologi

.NET 10 · Blazor Server · EF Core 10 (SQLite/SQL Server/PostgreSQL/MySQL) · ASP.NET Core Identity · Semantic Kernel · Microsoft.Extensions.AI · HotChocolate GraphQL · ChartJs.Blazor.Fork · Markdig · Leaflet.js · SDK Azure Blob / AWS S3 / MinIO

## 📦 Deployment

```bash
dotnet publish src/BlazePoint -c Release -o publish
```

Set `ASPNETCORE_ENVIRONMENT=Production` dan gunakan reverse proxy (IIS/Nginx/Caddy) dengan WebSockets aktif (wajib untuk Blazor Server).

## Lisensi

MIT
