# BlazorViz

> 🇬🇧 [Read in English](README.md)

**BlazorViz** adalah aplikasi web analisa dan visualisasi data self-hosted ala Power BI,
dibangun dengan **Blazor Server .NET 10**. Hubungkan ke hampir semua sumber data, olah data dengan
pipeline ETL berbasis UI atau script (C#, JavaScript, Python), bangun dashboard drag-and-drop dengan
20+ tipe chart, mengobrol dengan data Anda lewat asisten AI **Data Wizard**, dan bagikan semuanya ke tim.

## ✨ Fitur

### Inti
- **Konektor multi-database** — SQLite, SQL Server, PostgreSQL, MySQL, Oracle, Excel, CSV, REST API, GraphQL.
- **Import / export** — upload file Excel/CSV/JSON; export dataset atau panel ke CSV, JSON, Excel, PDF, atau gambar PNG.
- **Pipeline ETL** — langkah filter, select, rename, compute, sort, aggregate, join, distinct, limit, dan pivot yang dirangkai dari UI.
- **Scripting** — transformasi dataset dengan C# (Roslyn), JavaScript (Jint), atau Python (interpreter eksternal), dilengkapi 15+ template siap pakai yang tinggal dimodifikasi.
- **Refresh real-time** — TTL cache per dataset dan interval auto-refresh per dashboard.

### Visualisasi
- **20+ tipe chart** (Apache ECharts): line, area, bar, stacked bar, horizontal bar, pie, donut, rose, scatter, bubble, radar, waterfall, treemap, sunburst, heatmap, gauge, funnel, sankey, boxplot, candlestick, kartu KPI, dan tabel data.
- **Custom visual** — tulis kode panel sendiri dengan **ECharts, Chart.js, atau D3.js**.
- **Geo map** — peta titik/gelembung berbasis Leaflet dari kolom lat/lng.
- **Dashboard canggih** — multi-tab, multi-panel, drag-and-drop + resize (Gridstack), berversi dengan rollback.
- **Filter interaktif** — slicer, dropdown, multi-select, rentang tanggal; berlaku ke semua panel dataset yang sama.
- **Desain neo-brutalism soft** — responsif, dengan toggle tema gelap/terang.

### AI & Otomasi
- **Data Wizard** — chat dengan data via **Semantic Kernel**; provider: OpenAI, Anthropic, Gemini, Ollama (lokal). Jawaban streaming dirender sebagai markdown kaya (tabel, kode, gambar). Lampirkan gambar (dikirim sebagai image content) dan dokumen (ditautkan + disertakan isinya).
- **Kernel functions** — kalkulasi matematika, tanggal/waktu, pencarian internet, scraping URL, plus tool query dataset/dashboard sehingga asisten menjawab dari data *sungguhan*.
- **RAG** — indeks dokumen PDF / Word / Excel / teks ke vector store (**InMemory**, **Qdrant**, **Chroma**, atau **Azure AI Search**) memakai `Microsoft.Extensions.VectorData`; Data Wizard otomatis mengutip kutipan relevan. Langsung jalan offline berkat embedder lokal.
- **Predictive analytics (ML.NET)** — forecasting time-series SSA, regresi SDCA, clustering K-Means.
- **Rekomendasi cerdas** — saran chart berdasarkan tipe kolom dataset Anda.

### Keamanan & Kolaborasi
- **Autentikasi** — ASP.NET Core Identity: register, login, reset password, profil, 2FA, passkey.
- **Role-based access** — Admin / Analyst / Viewer.
- **Audit log** — semua aktivitas penting tercatat, bisa difilter dan diurutkan.
- **Berbagi** — link share publik (`/share/{token}`) dan kode embed `<iframe>` per dashboard.
- **Version control** — setiap penyimpanan dashboard membuat versi baru; rollback kapan saja.

### Monitoring & Admin
- **Analitik penggunaan** — jumlah query, chat, estimasi token LLM, panggilan API; grafik harian dan pengguna teratas.
- **Dashboard performa** — timing request langsung, trafik per path, memori/CPU/uptime.
- **Backend storage** — FileSystem (default), Azure Blob, S3 / MinIO.
- **REST API + Swagger** — `/api/v1` (dataset, query, export, dashboard, chat) diamankan API key; dokumentasi interaktif di `/swagger`.
- **Sistem plugin** — letakkan file `.csx` di `plugins/` untuk menambah fungsi kustom Data Wizard.

## 🚀 Mulai cepat

### Prasyarat
- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
- (opsional) Python 3 di PATH — hanya untuk script dataset Python
- (opsional) [Ollama](https://ollama.com) atau API key OpenAI/Anthropic/Gemini — untuk fitur AI

### Jalankan

```bash
git clone <repo-ini>
cd BlazorViz
dotnet run --project src/BlazorViz
```

Buka URL yang tercetak. Database dimigrasi dan di-seed otomatis dengan data contoh:

| Pengguna | Password | Role |
|----------|----------|------|
| `admin@blazorviz.local` | `Admin123!` | Admin |
| `analyst@blazorviz.local` | `Analyst123!` | Analyst |
| `viewer@blazorviz.local` | `Viewer123!` | Viewer |

Dua dashboard contoh (**Sales Overview**, **World & Web Analytics**), empat dataset contoh, dan satu
API key demo dibuat saat pertama dijalankan.

### Konfigurasi AI

Ubah bagian `Ai` di `src/BlazorViz/appsettings.json` (provider, model, persona, temperature) — lihat
[docs/configuration.md](docs/configuration.md). Simpan rahasia di user-secrets atau environment variable:

```bash
dotnet user-secrets set "Ai:Providers:OpenAI:ApiKey" "sk-…" --project src/BlazorViz
```

Provider default adalah **Ollama** (`http://localhost:11434`, model `llama3.1`) sehingga semuanya jalan
tanpa API key cloud. RAG juga langsung berfungsi offline dengan embedder lokal dan indeks vektor in-memory.

## 📚 Dokumentasi

Lihat folder [docs/](docs/) — arsitektur, panduan pengguna (EN & ID), konfigurasi, referensi API,
ETL & scripting, serta panduan plugin.

## 📄 Lisensi

Proyek contoh — silakan gunakan secara bebas.
