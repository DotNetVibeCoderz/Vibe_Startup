# PCHub - Rental PC/Game Center Application

## 🎯 Project Overview
Aplikasi manajemen rental PC/Game Center dengan arsitektur Blazor Server (Admin Web) + WPF (Client App).

## 🏗️ Architecture
```
PCHub/
├── src/
│   ├── PCHub.Shared/          # Shared Library ✅
│   ├── PCHub.Admin/           # Blazor Server Admin Web ✅
│   └── PCHub.Client/          # WPF Client Application ✅
├── tests/
│   └── PCHub.Tests/           # xUnit Test Project ✅
├── charts/pchub/              # Helm Chart ✅
├── .github/workflows/         # CI/CD Pipeline ✅
├── docs/                      # Documentation ✅
├── Dockerfile                 # Docker Build ✅
├── docker-compose.yml         # Docker Compose ✅
├── PLAN.md                    # This file
└── README.md / README.id.md   # Readme
```

## ✅ Development Checklist

### Phase 1-2: Foundation + Admin ✅
### Phase 3: WPF Client ✅
### Phase 4: Polish (Config, Docker, Docs) ✅

### Phase 5: Future Enhancements ✅
- [x] **Full Semantic Kernel Integration** - OpenAI, Anthropic, Gemini, Ollama + Kernel Functions (get_time, calc, prices, hours, format_rp)
- [x] **Email Notification Sender** - MailKit SMTP/SendGrid, booking confirmation, payment receipt
- [x] **Azure Blob / S3 Storage Provider** - Multi-provider: FileSystem, Azure Blob, AWS S3
- [x] **Payment Gateway** - Midtrans & Xendit integration + simulated fallback
- [x] **SignalR Real-time Notifications** - NotificationHub + SignalRNotificationService
- [x] **Redis Caching** - StackExchange.Redis + In-Memory fallback
- [x] **Tournament Bracket System** - Single Elimination, Round Robin generation
- [x] **IoT Integration** - Smart lamp/AC/lock controller, MQTT support
- [x] **Unit Tests (xUnit)** - Auth, Billing, Helper, ChatBot tests (10+ tests)
- [x] **CI/CD Pipeline** - GitHub Actions: Build → Test → Docker → Deploy
- [x] **Kubernetes Helm Chart** - Full Helm chart with values.yaml
- [x] **Docker Multi-stage Build** - Optimized production image
- [ ] Mobile companion app (.NET MAUI) - SKIPPED per request

---

## 🚀 Quick Start

### Admin Web
```bash
cd src/PCHub.Admin && dotnet run
```

### WPF Client
```bash
cd src/PCHub.Client && dotnet run
```

### Unit Tests
```bash
dotnet test tests/PCHub.Tests
```

### Docker
```bash
docker compose up -d
```

### Kubernetes
```bash
helm install pchub ./charts/pchub
```

**Demo Login:** admin / Admin123!

---

## 📊 Phase 5 Services Added

| Service | Library | Description |
|---|---|---|
| **ChatBotService** | Microsoft.SemanticKernel | AI Chat: OpenAI, Anthropic, Gemini, Ollama + Fallback |
| **EmailService** | MailKit | SMTP email sender + HTML templates |
| **PaymentService** | Custom | Midtrans + Xendit integration |
| **StorageMultiProviderService** | Azure.Storage.Blobs, AWSSDK.S3 | Azure Blob + AWS S3 storage |
| **CacheService** | StackExchange.Redis | Redis + In-Memory fallback |
| **NotificationHub** | SignalR | Real-time notification broadcast |
| **IoTService** | MQTTnet | Smart device controller |
| **TournamentBracketService** | Custom | Tournament bracket generator |

---

*Last Updated: 2025*
