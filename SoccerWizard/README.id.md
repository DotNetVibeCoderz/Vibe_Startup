# ⚽ SoccerWizard - Platform Prediksi Pertandingan Sepak Bola

> Prediksi Sepak Bola Berbasis AI menggunakan ML.NET, Distribusi Poisson & Large Language Models

---

## 🎯 Gambaran Umum

**SoccerWizard** adalah aplikasi web prediksi pertandingan sepak bola yang dibangun dengan .NET Blazor Server. Menggabungkan model machine learning **ML.NET**, analisis statistik **Distribusi Poisson**, dan **Large Language Models (LLMs)** untuk prediksi pertandingan yang akurat berbasis data.

Platform ini menyediakan update real-time via **SignalR**, visualisasi data yang kaya, asisten chat AI, dan analisis sentimen berita sepak bola.

---

## ✨ Fitur Utama

### 📊 Data & Statistik
- Data live score, jadwal, dan hasil pertandingan
- Statistik head-to-head, performa kandang/tandang
- Model Poisson untuk distribusi skor dan peluang menang
- Dashboard visual dengan grafik tren performa tim

### 🤖 Machine Learning (ML.NET)
- **Binary Classification**: Prediksi hasil (Menang/Seri/Kalah)
- **Regression Model**: Prediksi skor akhir
- **Feature Engineering**: ELO rating, momentum tim, expected goals (xG)
- **19 Fitur Input**: ELO, kekuatan serangan/pertahanan, H2H, cuaca
- **Evaluasi Model**: Accuracy, Precision, Recall, F1 Score, AUC-ROC

### 🧠 Integrasi LLM
- **Multi-Provider**: OpenAI, Gemini, Anthropic, Ollama
- **Analisis Sentimen**: Ekstraksi sentimen dari berita
- **Prediksi Teks**: Analisis pertandingan oleh AI
- **Chat Interaktif**: Asisten AI untuk pertanyaan tentang sepak bola

### 🛠️ Teknologi .NET
- **Blazor Server**: UI interaktif dengan komponen real-time
- **SignalR**: Update skor dan prediksi langsung
- **Entity Framework Core**: Manajemen database SQLite
- **ASP.NET Core Identity**: Autentikasi & otorisasi
- **Cross-Platform**: Windows, Linux, cloud

---

## 🚀 Memulai

### Prasyarat
- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)

### Instalasi

```bash
git clone https://github.com/your-org/SoccerWizard.git
cd SoccerWizard
dotnet restore
dotnet build
dotnet run
```

Buka `https://localhost:5001` di browser.

---

## 👤 Akun Demo

| Role | Email | Password |
|------|-------|----------|
| **Admin** | admin@soccerwizard.com | Admin123! |
| **User** | demo@soccerwizard.com | Demo123! |
| User | john.doe@soccerwizard.com | User123! |
| User | jane.smith@soccerwizard.com | User123! |

---

## 📁 Struktur Proyek

```
SoccerWizard/
├── Components/          # UI Components (Blazor)
├── Data/               # DbContext & Seeder
├── Hubs/               # SignalR Hubs
├── Models/             # Domain & ML Models
├── Services/           # Business Logic
├── wwwroot/            # CSS, JS, Static files
├── docs/               # Dokumentasi
└── Program.cs          # Entry point
```

---

## 🔧 Konfigurasi LLM

Edit `appsettings.json`:

```json
{
  "LLM": {
    "DefaultProvider": "Ollama",
    "OpenAI": { "ApiKey": "sk-..." },
    "Gemini": { "ApiKey": "..." }
  }
}
```

---

**Dibuat dengan ❤️ oleh Jacky the Code Bender @ GraviCode Studios**

*Kalau suka, traktir pulsa dong! https://studios.gravicode.com/products/budax* ☕
