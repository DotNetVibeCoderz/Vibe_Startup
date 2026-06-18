# 🏘️ LandLord - Land & Building Mapping Application

A modern Blazor Server application for land and building mapping management, designed with Indonesian metadata standards.

---

## 🇮🇩 Bahasa Indonesia

### 📋 Deskripsi
LandLord adalah aplikasi pemetaan tanah dan bangunan berbasis Blazor Server dengan fitur lengkap:
- 🗺️ **Peta Interaktif** - Browsing tanah & bangunan dengan peta
- 📊 **Master Data** - CRUD data tanah & bangunan standar Indonesia
- 📈 **Dashboard** - Statistik, chart, dan AI insights
- 🤖 **Frengky Ganteng** - Chatbot AI dengan Semantic Kernel
- ⚙️ **Settings** - Konfigurasi multi-provider

### 🚀 Instalasi

1. **Prasyarat:**
   - .NET 9.0+ SDK
   - Browser modern (Chrome/Firefox/Edge)

2. **Clone & Run:**
   ```bash
   cd LandLord
   dotnet restore
   dotnet run
   ```

3. **Akses aplikasi:**
   - Buka `https://localhost:5001`
   - Login dengan akun demo:
     - Admin: `admin` / `Admin123!`
     - User: `budi_santoso` / `User123!`

### 🗄️ Database Support
- SQLite (default)
- SQL Server
- MySQL
- PostgreSQL

Konfigurasi di `appsettings.json`:
```json
{
  "DatabaseProvider": {
    "Provider": "SQLite",
    "ConnectionString": "Data Source=LandLord.db"
  }
}
```

### 🤖 LLM Provider
- OpenAI (GPT-4o, GPT-4 Turbo, GPT-3.5)
- Anthropic (Claude 3)
- Google Gemini
- Ollama (Local)

---

## 🇬🇧 English

### 📋 Description
LandLord is a Blazor Server-based land and building mapping application with comprehensive features following Indonesian property metadata standards.

### 🚀 Quick Start
```bash
cd LandLord
dotnet run
```
Open `https://localhost:5001` and login with demo credentials.

### Features
- 🗺️ Interactive Maps
- 📊 Master Data Management
- 📈 Dashboard with AI Insights
- 🤖 AI Chatbot (Frengky Ganteng)
- ⚙️ Multi-provider Settings

### Notes
- By default, all pages except Home require authentication
- Maps page requires Google Maps API key (optional)
- AI features work with configured LLM provider

---

**Made with ❤️ by GraviCode Studios | [☕ Traktir Pulsa](https://studios.gravicode.com/products/budax)**
