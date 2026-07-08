# 🎾 PadelHub - Padel Management System

**PadelHub** is a comprehensive padel management application for clubs, tournaments, and individual players. Built with **Blazor Server .NET 10** and **MudBlazor** (Material Design).

---

## ✨ Features

### 🎾 Core Features
- **Club Management**: Club profiles, courts, facilities, operating hours
- **Court Reservation**: Online booking, interactive calendar, auto payment
- **Tournament & League**: Registration, auto bracket, match scheduling, real-time results
- **Player Profiles**: Statistics, ranking, match history, achievements
- **Coach & Courses**: Training schedules, session booking, course materials

### 💳 Financial
- **Online Payment**: E-wallet, credit card, bank transfer integration
- **Membership Packages**: Monthly/yearly plans, discounts, loyalty points
- **Financial Reports**: Revenue, expenses, transaction analytics

### 📊 Analytics & Monitoring
- **Match Statistics**: Scores, player performance, shot heatmaps
- **Club Dashboard**: Reservation trends, court popularity, member activity
- **Ranking & Rating**: Auto point system, weekly/monthly leaderboard

### 📱 Social & Community
- **Chat & Forum**: Player discussions, community groups
- **Social Events**: Gatherings, fun matches, charity events
- **Timeline**: Share match results, highlights, comments, likes, emojis

### 🔒 Security & Admin
- **User Authentication**: Login, register, reset password, profile edit
- **Master Data Management**: CRUD, Export CSV/Excel, Filter, Sort, Paging
- **Member Card**: Print with QR Code
- **Check-in System**: QR scan or member number input
- **Audit Log**: Activity records with filter & search

### 🚀 Competitive Features
- **AI Match Analysis**: AI-powered video analysis
- **Smart Scheduling**: Optimal tournament scheduling algorithm
- **IoT Integration**: Court sensors, ball tracking, smart lighting simulator
- **Gamification**: Badges, achievements, community leaderboard
- **REST API**: Minimal API with Swagger documentation

### 🤖 Chat Bot - Coach Sherly
- Multi-session chat with reset
- Image & document attachment support
- Multiple AI model support (OpenAI, Anthropic, Gemini, Ollama)
- Semantic Kernel integration
- Markdown rendering with full HTML output

---

## 🛠️ Tech Stack

| Technology | Purpose |
|------------|---------|
| .NET 10 | Runtime |
| Blazor Server | UI Framework |
| MudBlazor 9 | Material Design Components |
| Entity Framework Core | ORM |
| ASP.NET Identity | Authentication |
| Semantic Kernel | AI Integration |
| SQLite/SQLServer/MySQL/PostgreSQL | Database |
| Markdig | Markdown Rendering |
| QRCoder | QR Code Generation |
| ClosedXML | Excel Export |
| CsvHelper | CSV Export |

---

## 🚀 Quick Start

### Prerequisites
- .NET 10 SDK
- (Optional) SQL Server, MySQL, or PostgreSQL

### Run
```bash
cd PadelHub
dotnet run
```

Open `https://localhost:5001` in your browser.

### Default Accounts
| Role | Email | Password |
|------|-------|----------|
| Admin | admin@padelhub.com | Admin@123 |
| Operator | operator@padelhub.com | Operator@123 |
| Coach | coach.andi@padelhub.com | Coach@123 |
| Member | rina@padelhub.com | Member@123 |

---

## 📁 Project Structure

```
PadelHub/
├── Components/
│   ├── Layout/         # Main layout, navigation
│   ├── Pages/          # All application pages
│   │   ├── Admin/      # Admin pages
│   │   ├── Chat/       # Coach Sherly chatbot
│   │   ├── Club/       # Club & coach management
│   │   ├── Court/      # Court & reservation
│   │   ├── Dashboard/  # Analytics dashboard
│   │   ├── Finance/    # Payment & membership
│   │   ├── Player/     # Player management
│   │   ├── Social/     # Timeline & social
│   │   └── Tournament/ # Tournament management
│   └── Shared/         # Shared components
├── Data/               # DbContext
├── Models/             # Entity models
├── Services/           # Business logic services
├── wwwroot/            # Static files
├── docs/               # Documentation
├── appsettings.json    # Configuration
└── Program.cs          # Application entry
```

---

## ⚙️ Configuration

Edit `appsettings.json` to change:
- **Database**: SQLite (default), SQLServer, MySQL, PostgreSQL
- **Storage**: FileSystem (default), Azure Blob, S3, MinIO
- **AI Model**: OpenAI, Anthropic, Gemini, Ollama
- **Chat Bot**: System prompt, temperature, max tokens

---

## 📚 Documentation

See [docs/](docs/) for detailed documentation:
- [Architecture](docs/architecture.md)
- [API Reference](docs/api.md)
- [User Guide](docs/user-guide.md)

---

## 🙏 Credits

Built with ❤️ by **GraviCode Studios**  
Lead: Kang Fadhil  
AI Assistant: Jacky the Code Bender

If you find this helpful, treat me some credit! 🎾  
https://studios.gravicode.com/products/budax

---

**PadelHub** - Your Complete Padel Management Solution! 🎾
