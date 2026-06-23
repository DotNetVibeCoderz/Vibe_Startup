# 🎉 EventSphere - Event & Wedding Organizer Platform

> **Your All-in-One Event Planning Solution**  
> Built with Blazor Server .NET 10 • AI-Powered • Modern Neomorphism UI

---

## 🚀 Quick Start

### Prerequisites
- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
- SQLite (default) or SQL Server / MySQL / PostgreSQL

### Run Locally
```bash
# Clone & navigate
cd EventSphere

# Restore & run
dotnet restore
dotnet run
```

Open `https://localhost:5001` in your browser.

---

## 🔑 Demo Accounts

| Role | Email | Password |
|------|-------|----------|
| **Admin** | admin@eventsphere.com | Admin123! |
| **Organizer** | organizer@eventsphere.com | Organizer123! |
| **Client** | rini@email.com | Client123! |
| **Vendor** | catering@berkah.com | Vendor123! |
| **Guest** | sari@email.com | Guest123! |
| **Moderator** | moderator@eventsphere.com | Moderator123! |

---

## ✨ Features

### 🔑 Core
- ✅ Event Creation & Management (CRUD, status tracking)
- ✅ Guest Management (RSVP, seating arrangement, dietary)
- ✅ Vendor Directory (database, contracts, invoices)
- ✅ Budget Tracking (estimation, allocation, monitoring)
- ✅ Task & Checklist (deadlines, reminders, progress)

### 💬 Communication
- ✅ Chat & Messaging (real-time, per-event)
- ✅ Document Sharing (contracts, proposals, photos)
- ✅ Notification System (deadlines, reminders)

### 🎨 Design
- ✅ Theme & Style Selector
- ✅ Visual Seating Planner
- ✅ Photo & Media Gallery

### 🤖 AI (Tante Sherly)
- ✅ Multi-model ChatBot (OpenAI, Anthropic, Gemini, Ollama)
- ✅ Semantic Kernel Integration
- ✅ Internet Search (Tavily API)
- ✅ Web Scraping
- ✅ Markdown Rendering with Code Highlighting
- ✅ Image Attachment Support
- ✅ Multi-session Management

### 📊 Analytics
- ✅ Dashboard with Key Metrics
- ✅ Monthly Statistics
- ✅ Event Type & Vendor Breakdown
- ✅ Export to CSV & Excel

### 🛡️ Security
- ✅ Role-Based Access Control (6 roles)
- ✅ Authentication & Authorization
- ✅ Secure File Storage

---

## 🏗️ Architecture

```
EventSphere/
├── Components/
│   ├── Layout/        # MainLayout, NavMenu
│   ├── Pages/         # All Razor Pages
│   └── Shared/        # Reusable Components
├── Data/
│   ├── Models/        # EF Core Entity Models
│   └── Context/       # AppDbContext
├── Services/          # Business Logic Layer
│   ├── EventService
│   ├── VendorService
│   ├── BudgetService
│   ├── TaskService
│   ├── GuestService
│   ├── ChatService
│   ├── NotificationService
│   ├── MediaService
│   ├── StorageService
│   ├── ExportService
│   ├── DashboardService
│   ├── AiChatService      # Semantic Kernel + Multi-Model
│   └── DataSeeder         # Sample Data
├── wwwroot/
│   ├── app.css        # Neomorphism Theme
│   └── uploads/       # File Storage
└── docs/              # Documentation
```

---

## 🔧 Configuration

### Database
Edit `appsettings.json`:
```json
{
  "Database": {
    "Provider": "SQLite"  // or: SqlServer, MySQL, PostgreSQL
  },
  "ConnectionStrings": {
    "DefaultConnection": "Data Source=EventSphere.db"
  }
}
```

### AI / ChatBot
```json
{
  "AI": {
    "DefaultProvider": "OpenAI",
    "Providers": {
      "OpenAI": { "ApiKey": "sk-...", "Model": "gpt-4o" },
      "Anthropic": { "ApiKey": "...", "Model": "claude-3-5-sonnet" },
      "Gemini": { "ApiKey": "...", "Model": "gemini-2.0-flash" },
      "Ollama": { "Model": "llama3", "Endpoint": "http://localhost:11434" }
    }
  }
}
```

### Storage
```json
{
  "Storage": {
    "Provider": "FileSystem"  // or: AzureBlob, S3, MinIO
  }
}
```

---

## 📚 Tech Stack

| Layer | Technology |
|-------|-----------|
| **Framework** | .NET 10 Blazor Server |
| **Database** | SQLite / SQL Server / MySQL / PostgreSQL |
| **ORM** | Entity Framework Core |
| **Auth** | ASP.NET Core Identity |
| **AI/LLM** | Semantic Kernel (OpenAI, Anthropic, Gemini, Ollama) |
| **UI Theme** | Custom Neomorphism CSS |
| **Markdown** | Markdig |
| **Export** | CsvHelper + ClosedXML |
| **Storage** | FileSystem / AzureBlob / S3 / MinIO |

---

## 👥 Role Matrix

| Role | Access Level |
|------|-------------|
| **Admin** | Full access, manage users & vendors, monitor budget |
| **Organizer** | Manage events, checklist, vendors, guests, communication |
| **Client** | View progress, approve vendor & budget, feedback |
| **Vendor** | View event details, upload docs, update status, invoice |
| **Guest** | Digital invitation, RSVP, seating plan, upload photos |
| **Moderator** | Manage forum, filter content, community engagement |

---

## 📄 License
Proprietary - GraviCode Studios © 2025

---

**Built with ❤️ by GraviCode Studios**  
[Jacky the Code Bender](https://studios.gravicode.com)
