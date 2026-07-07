# 🏋️ FitnessCenter Management Application

A modular and scalable fitness center management application built with **Blazor Server .NET 10**, featuring a modern neo-brutalism futuristic design with dark/light theme support.

## 🚀 Features

### 🏋️ Core Features
- **Member Registration** — Online/offline registration with KTP/email/phone integration
- **Membership Plans** — Daily, weekly, monthly, yearly plans with auto-renewal
- **Attendance Tracking** — QR/Barcode scan for check-in/out
- **Class Scheduling** — Yoga, Zumba, HIIT, Pilates with online booking
- **Trainer Management** — Trainer profiles, schedules, ratings, member assignment

### 💳 Payment & Finance
- **Payment Gateway** — Integration with e-wallet, credit card, bank transfer (Midtrans/Stripe/Xendit)
- **Billing & Invoicing** — Auto invoice, payment reminders, financial reports
- **Discounts & Promotions** — Coupons, referral bonuses, seasonal promos

### 📊 Analytics & Dashboard
- **Member Analytics** — Attendance stats, workout progress, retention rate
- **Revenue Dashboard** — Monthly revenue charts, per package, per class
- **Trainer Performance** — KPI based on class count, rating, feedback

### 📱 Member Engagement
- **Workout Tracking** — Exercise log with Fitbit/Apple Watch integration
- **Nutrition Plans** — Diet recommendations, meal plans
- **Push Notifications** — Class reminders, promos, daily motivation
- **Community Forum** — Member discussions, tips, weekly challenges with images, emoji, likes

### 🔒 Security & Access
- **Role-Based Access** — Admin, Trainer, Member, Staff with different permissions
- **Emergency Alerts** — Panic button, staff notification

### 🚀 Advanced Features
- **AI ChatBot "Coach Tommy"** — Powered by OpenAI/Anthropic/Gemini/Ollama
- **Virtual Classes** — Streaming via Zoom/Teams
- **Gamification** — Points, badges, leaderboard
- **Integration API** — Minimal API with Swagger docs
- **Event Management** — Competitions, workshops, seminars with blog timeline

## 🛠️ Tech Stack

- **Framework:** .NET 10 Blazor Server
- **Database:** SQLite (default), SQL Server, MySQL, PostgreSQL
- **Storage:** File System (default), Azure Blob, S3, MinIO
- **AI:** OpenAI, Anthropic Claude, Google Gemini, Ollama
- **Libraries:** Entity Framework Core, ClosedXML, CsvHelper, QRCoder, Markdig, Semantic Kernel

## 📁 Project Structure

```
FitnessCenter/
├── Models/              # Domain models & enums
├── Data/                # EF Core DbContext
├── Services/            # Business logic services
├── Api/                 # Minimal API endpoints
├── Components/
│   ├── Layout/          # MainLayout, MinimalLayout
│   ├── Pages/           # Main pages (Home, Login, Error)
│   ├── Shared/          # Shared components
│   └── Features/        # Feature modules
│       ├── Members/
│       ├── Membership/
│       ├── Attendance/
│       ├── Classes/
│       ├── Trainers/
│       ├── Payments/
│       ├── Forum/
│       ├── Events/
│       ├── Workout/
│       ├── Nutrition/
│       ├── Feedback/
│       ├── Gamification/
│       ├── Discounts/
│       ├── ChatBot/
│       └── Analytics/
├── wwwroot/             # Static files & CSS
└── docs/                # Documentation
```

## 🚀 Getting Started

### Prerequisites
- .NET 10 SDK
- SQLite (default, no installation needed)

### Run
```bash
cd FitnessCenter
dotnet run
```

### Default Accounts
| Role    | Email                        | Password    |
|---------|------------------------------|-------------|
| Admin   | admin@fitnesscenter.com      | Admin123!   |
| Trainer | trainer1@fitnesscenter.com   | Trainer123! |
| Member  | member1@email.com            | Member123!  |

### Configuration
Edit `appsettings.json` to change database provider, storage, payment gateway, AI provider, etc.

## 📡 API Documentation
Access Swagger UI at: `/api/docs`

## 🎨 Theme
Neo-brutalism futuristic design with dark/light theme toggle. Theme preference is saved in localStorage.

## 📄 License
MIT License

---

**Built with ❤️ by Gravicode Studios**
