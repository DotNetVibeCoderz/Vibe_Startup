# 🚗 RentalBoil - Aplikasi Rental Kendaraan

**Rental Kendaraan Anti Ngambek!**

Platform rental kendaraan full-featured dengan Blazor Server .NET 10, REST API, Swagger, AI Chat Bot multi-model, GPS tracking, dan UI claymorphism.

---

## ✨ Fitur Utama

### 🔌 REST API (Minimal API + Swagger)
- **25+ endpoint** untuk vehicles, bookings, users, reviews, payments, GPS/IoT, chat
- **ApiKey Auth** via header `X-Api-Key`
- **Swagger UI** di `/swagger` untuk dokumentasi interaktif

### 🤖 AI Chat Bot - Bang Tony Brewok
- **4 Provider**: OpenAI, Anthropic Claude, Google Gemini, Ollama
- **17 Kernel Functions**: cari kendaraan, booking, cek order, GPS, internet search, web scraping, kalkulasi, dll.
- Bisa booking kendaraan langsung via chat!

---

## 🚀 Quick Start

```bash
cd RentalBoil
dotnet run
```

- **App**: `https://localhost:5001`
- **Swagger**: `https://localhost:5001/swagger`

### Akun Demo
| Role | Email | Password |
|------|-------|----------|
| Admin | admin@rentalboil.com | Admin123! |
| Partner | partner1@rentalboil.com | Partner123! |
| Customer | customer1@rentalboil.com | Customer123! |
| API Key | `X-Api-Key` header | `rntl-2025-secure-api-key-change-in-production` |

### 🤖 Konfigurasi AI
```json
"AI": {
  "Provider": "OpenAI",  // OpenAI | Anthropic | Gemini | Ollama
  "OpenAI": { "ApiKey": "sk-...", "Model": "gpt-4o-mini" }
}
```

---

Dibuat dengan ❤️ oleh **Gravicode Studios** | Kang Fadhil & Jacky The Code Bender
