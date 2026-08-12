# 🏋️ FitnessCenter Management Application

A modular and scalable fitness center management application built with **Blazor Server .NET 10**, in a design system called **"Plate Load"** — an accent palette taken from the IWF competition barbell-plate color code, with light and dark themes.

![Admin dashboard](docs/screenshots/03-dashboard-admin-dark.png)

## 📸 Screenshots

The dashboard adapts to the signed-in role. Admins and staff get a **floor panel** — today's check-ins as the headline number, with a load meter comparing it against the busiest day of the last 30 days. Members get their check-in streak, days left on their membership, and unpaid invoices.

| Admin dashboard | Member dashboard |
|---|---|
| ![Admin dashboard, light](docs/screenshots/03-dashboard-admin-light.png) | ![Member dashboard](docs/screenshots/04-dashboard-member-light.png) |

| Classes | Payments |
|---|---|
| ![Classes](docs/screenshots/06-classes-light.png) | ![Payments](docs/screenshots/07-payments-admin-light.png) |

Members choose a payment method per invoice. Providers without API keys never appear, so the list always reflects what actually works:

| Sign in | Payment methods |
|---|---|
| ![Sign in](docs/screenshots/01-login-dark.png) | ![Payment methods](docs/screenshots/25-payment-methods-all-providers-light.png) |

**→ [Full gallery: all 30 screens, light and dark](docs/SCREENSHOTS.md)**

## 🚀 Features

### 🏋️ Core Features
- **Member Registration** — Online/offline registration with KTP/email/phone integration
- **Membership Plans** — Daily, weekly, monthly, yearly plans with auto-renewal
- **Attendance Tracking** — QR/Barcode scan for check-in/out
- **Class Scheduling** — Yoga, Zumba, HIIT, Pilates with online booking
- **Trainer Management** — Trainer profiles, schedules, ratings, member assignment

### 💳 Payment & Finance
- **Payment Gateway** — Manual transfer, Midtrans Snap, Xendit Invoice, and Stripe Checkout. Members pick a method per invoice; providers without API keys are skipped automatically and payment falls back to manual transfer.
- **Webhooks** — Each provider's callback is signature-verified (`POST /api/v1/payments/webhook/{provider}`); status can also be polled on demand.
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
├── Services/Payments/   # Payment gateway providers
├── wwwroot/
│   ├── app.css          # "Plate Load" design system
│   ├── app.js           # Micro-interaction runtime
│   └── images/          # Class, event & forum cover art (SVG)
└── docs/                # Documentation & screenshots
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
| Staff   | staff1@fitnesscenter.com     | Staff123!   |
| Trainer | trainer1@fitnesscenter.com   | Trainer123! |
| Member  | member1@email.com … member40@email.com | Member123! |

### Sample data

The first run creates the schema and seeds a gym that already has history, so every
screen has something real to show:

| | | | |
|---|---|---|---|
| 40 members | 6 trainers | 12 classes | 30 weekly schedules |
| 6 membership plans | 45 days of check-ins | ~180 class bookings | ~120 invoices |
| 10 forum threads + comments | 6 events + registrations | workout logs & meal plans | badges, feedback, notifications |

Invoices are deliberately spread across every status — pending, awaiting verification,
paid, failed, cancelled, refunded — so the payment flows can be exercised without
touching a real gateway.

**Seeding only runs when the database is empty.** To pick up changes to
`DataSeedService`, delete the database and start again:

```bash
rm fitness_center.db fitness_center.db-shm fitness_center.db-wal
dotnet run
```

### Configuration
Edit `appsettings.json` to change database provider, storage, payment gateway, AI provider, etc.

## 📡 API Documentation
Access Swagger UI at: `/api/docs`

## 💳 Configuring a payment provider

1. Put your keys in `appsettings.json` under `PaymentGateway:<Provider>` and set `PaymentGateway:BaseUrl` to the app's public address.
2. Set `PaymentGateway:DefaultProvider` to the method offered first.
3. Open `/admin/config` — each provider shows whether it is active and gives you the callback URL to paste into the provider's dashboard.

| Provider | Required keys | Callback |
|---|---|---|
| Manual | `BankName`, `AccountNumber`, `AccountHolder` | — (admin verifies) |
| Midtrans | `ServerKey` | `/api/v1/payments/webhook/midtrans` |
| Xendit | `ApiKey`, `CallbackToken` | `/api/v1/payments/webhook/xendit` |
| Stripe | `SecretKey`, `WebhookSecret` | `/api/v1/payments/webhook/stripe` |

A provider with a missing secret rejects incoming callbacks rather than trusting them.

## 🎨 Theme

**"Plate Load"** — the accent palette is the IWF competition barbell-plate color code (25 kg red, 20 kg blue, 15 kg yellow, 10 kg green), used as a status language throughout: every card carries a colored *plate edge* naming its category. Typography pairs Barlow Condensed (scoreboard headings and numerals) with Barlow and JetBrains Mono. Dark and light themes, toggled from the topbar and saved in localStorage. Micro-interactions — counting numerals, lane-stripe navigation, plate meters, staggered reveals — live in `wwwroot/app.js` and respect `prefers-reduced-motion`.

Cover art in `wwwroot/images/` is hand-built SVG in the same palette — one per class type,
plus event and forum covers. It's vector and self-hosted, so it stays crisp at any size,
costs ~70 KB in total, and needs no network.

## 📄 License
MIT License

---

**Built with ❤️ by Gravicode Studios**
