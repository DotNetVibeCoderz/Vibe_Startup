# HolySafar Documentation

## Architecture Overview

HolySafar is built on **Blazor Server** (.NET 10) with **Interactive Server rendering mode**. All UI interactions are handled via SignalR connection between the browser and server.

### Key Design Decisions

1. **SQLite as Default Database** - Zero-configuration, file-based database perfect for demos and small deployments. Easily switchable to SQL Server, MySQL, or PostgreSQL.

2. **Semantic Kernel for AI** - Microsoft's Semantic Kernel library provides a unified interface for multiple LLM providers. The chatbot "Syeikh Jenggot" can use OpenAI, Anthropic, Gemini, or Ollama.

3. **FileSystem Storage** - Default storage provider writes to `wwwroot/uploads/`. Swappable to Azure Blob, AWS S3, or MinIO.

4. **Session-based Auth** - Simple session-based authentication using `IHttpContextAccessor` and session storage. No ASP.NET Identity dependency.

5. **CSS Custom Properties** - Theme system using CSS variables for light/dark mode without JavaScript overhead.

---

## Database Schema

### Core Tables
- `ApplicationUser` - User accounts with role-based access
- `Jamaah` - Pilgrim/passenger data
- `DokumenJamaah` - Uploaded documents (KTP, Paspor, etc.)

### Package & Payment
- `Paket` - Hajj/Umrah packages
- `Pembayaran` - Payment records
- `Cicilan` - Installment records

### Operations
- `Keberangkatan` - Departure schedules

### Communication
- `ChatMessage` - User-to-user messages
- `Pengumuman` - System announcements
- `Notifikasi` - System notifications

### Education
- `MateriManasik` - Manasik learning materials
- `Kuis` - Quiz questions

### Emergency
- `SOSTrigger` - SOS alerts
- `KontakDarurat` - Emergency contacts

### Marketplace
- `Produk` - Products
- `CartItem` - Shopping cart
- `Order` / `OrderItem` - Orders

### Chatbot
- `ChatSession` - AI chat sessions
- `ChatbotMessage` - Chat history

---

## Adding a New LLM Provider

1. Add API key to `appsettings.json`:
```json
"Chatbot": {
  "Provider": "OpenAI",
  "Providers": {
    "OpenAI": { "ApiKey": "sk-...", "Model": "gpt-4o" }
  }
}
```

2. Update `ChatbotService.CreateKernel()` to handle the new provider.

---

## GPS Simulator

The `GpsSimulatorService` runs a background timer that randomly moves pilgrims around Masjidil Haram (21.4225, 39.8262). Configure interval in `appsettings.json`:
```json
"AppSettings": {
  "SimulatorIntervalMs": 2000
}
```

---

## Data Seeding

`DataSeeder.SeedAsync()` runs automatically on first startup. It creates:
- 10 users (1 admin, 2 agents, 7 pilgrims)
- 3 packages
- 7 pilgrim records
- Payments, installments, departures
- Educational content, quizzes
- Emergency contacts
- Marketplace products
- Announcements

To reset: delete `Data/holysafar.db` and restart.

---

*For more information, contact Gravicode Studios.*
