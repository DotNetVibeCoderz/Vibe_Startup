# Dokumentasi Teknis SmartDrive Academy

## 📋 Arsitektur Aplikasi

### Pola Arsitektur
Aplikasi menggunakan arsitektur **Blazor Server** dengan pola **Clean Architecture** sederhana:

```
Presentation Layer (Blazor Components)
        ↓
Service Layer (Business Logic)
        ↓
Data Access Layer (EF Core + DbContext)
        ↓
Database (SQLite/SQLServer/MySQL/PostgreSQL)
```

### Component Lifecycle
Blazor Server menggunakan SignalR untuk komunikasi real-time antara server dan client. Setiap interaksi user dikirim ke server, diproses, dan hasilnya dikirim kembali ke client.

## 🗄️ Database Schema

### Tabel Utama

#### Users & Profiles
- `AspNetUsers` - Standard Identity + custom fields
- `InstructorProfiles` - Data khusus instruktur
- `StudentProfiles` - Data khusus siswa

#### Master Data
- `Vehicles` - Kendaraan latihan
- `TrainingLocations` - Lokasi latihan
- `TheoryModules` - Modul teori
- `ExamQuestions` - Soal ujian
- `MarketplaceProducts` - Produk marketplace

#### Operational
- `Bookings` - Jadwal latihan
- `InstructorSchedules` - Ketersediaan instruktur
- `Payments` - Pembayaran
- `StudentFeedbacks` - Evaluasi siswa
- `GpsTrackingData` - Data GPS

#### Communication
- `ChatMessages` - Chat instruktur-siswa
- `ChatBotSessions` - Sesi chat AI
- `ChatBotMessages` - Pesan chat AI
- `Notifications` - Notifikasi user

#### Others
- `SystemConfigs` - Konfigurasi sistem
- `InsurancePolicies` - Polis asuransi
- `InsuranceClaims` - Klaim asuransi
- `MarketplaceOrders` / `MarketplaceOrderItems` - Order marketplace
- `StudentBadges` - Badge gamifikasi
- `ExamAttempts` - Riwayat ujian

### Relationships Diagram (ERD)

```
AspNetUsers 1---1 InstructorProfile
AspNetUsers 1---1 StudentProfile
AspNetUsers 1---* Booking
AspNetUsers 1---* Payment
AspNetUsers 1---* ChatMessage (Sender)
AspNetUsers 1---* ChatMessage (Receiver)
AspNetUsers 1---* Notification
AspNetUsers 1---* MarketplaceOrder

InstructorProfile 1---* Booking
InstructorProfile 1---* InstructorSchedule
InstructorProfile 1---* StudentFeedback

StudentProfile 1---* Booking
StudentProfile 1---* StudentBadge
StudentProfile 1---* ExamAttempt
StudentProfile *---1 InstructorProfile (AssignedInstructor)

Vehicle 1---* Booking
Vehicle 1---* VehicleServiceRecord

TrainingLocation 1---* Booking

TheoryModule 1---* ExamQuestion

MarketplaceProduct 1---* MarketplaceOrderItem
MarketplaceOrder 1---* MarketplaceOrderItem
MarketplaceOrder 1---* Payment

Booking 1---* Payment
Booking 1---* GpsTrackingData
Booking 1---* StudentFeedback

InsurancePolicy 1---* InsuranceClaim
Vehicle 1---* InsurancePolicy
```

## 🔐 Authentication & Authorization

### Flow
1. User login → ASP.NET Identity validasi → Cookie/Session
2. Authorize attribute cek role → allow/deny akses
3. Role-Based Access Control (RBAC):
   - `Admin` - Akses penuh ke semua fitur admin
   - `Instructor` - Akses fitur instruktur
   - `Student` - Akses fitur siswa

### Role Mapping
| Role | Halaman |
|------|---------|
| Admin | /admin/* |
| Instructor | /instructor/* |
| Student | /student/* |
| All Auth | /chat-bot, /marketplace, /locations |

## 🤖 Chat Bot "Om Bambang"

### Arsitektur
```
User Input → ChatBotService → Semantic Kernel → AI Model (OpenAI/Anthropic/Gemini/Ollama)
                     ↓
              Database Context
              (User info, bookings, stats)
```

### Kernel Functions
- `DateTimePlugin`: GetCurrentDateTime, GetCurrentDate, DaysBetween
- `UtilityPlugin`: Calculate, SearchInternet, ScrapeWebPage, ReadFileFromUrl

### Session Management
- Multi-session support: user bisa punya banyak chat session
- Create/delete/reset session
- History disimpan di database
- Context-aware: bot tahu profil user dan data terkait

## 📡 GPS Tracking Simulator

### Cara Kerja
1. Saat booking status "InProgress", instruktur bisa start simulator
2. `GpsSimulatorService` berjalan di background thread
3. Setiap 5 detik, generate koordinat GPS random di sekitar Jakarta
4. Data disimpan ke `GpsTrackingData` table
5. Bisa di-stop kapan saja

### API Endpoint
- `POST /api/gps/push` - Push data GPS dari perangkat nyata
- `GET /api/gps/{bookingId}` - Ambil data GPS (dengan filter waktu)

## 🔌 REST API

### Design
- Minimal API pattern
- Swagger documentation di `/swagger`
- Standard HTTP response (200 OK, 201 Created, 404 Not Found)

### Endpoints Lengkap
| Method | Path | Auth | Description |
|--------|------|------|-------------|
| GET | /api/health | No | Health check |
| GET | /api/vehicles | No | List vehicles (paginated) |
| GET | /api/vehicles/{id} | No | Vehicle detail |
| GET | /api/locations | No | List locations (search, filter) |
| GET | /api/bookings | Yes | List bookings (user, status filter) |
| POST | /api/gps/push | Yes | Push GPS data |
| GET | /api/gps/{bookingId} | Yes | Get GPS tracking |
| GET | /api/schedules/{instructorId} | No | Instructor schedules |
| GET | /api/payments | Yes | Payment history |
| GET | /api/theory-modules | No | Theory modules |
| GET | /api/exam-questions | No | Exam questions |
| GET | /api/stats | No | Dashboard statistics |
| GET | /api/marketplace/products | No | Marketplace products |

## 🎨 UI/UX

### Design System
- Facebook-inspired layout
- CSS Variables untuk theme
- Responsive design (mobile, tablet, desktop)
- Dark/Light mode toggle

### Component Library
Semua komponen custom-built tanpa library UI pihak ketiga:
- Cards, Buttons, Forms, Tables, Modals
- Pagination, Badges, Alerts
- Grid system dengan CSS Grid

### CSS Structure
```
app.css
├── Theme Variables (light/dark)
├── Reset & Base
├── Layout (header, sidebar, content, footer)
├── Components (cards, buttons, forms, tables)
├── Pages (auth, dashboard, chat)
└── Utilities & Responsive
```

## 🚀 Performance Optimization

1. **EF Core**: No-tracking queries untuk read-only data
2. **Pagination**: Semua list data menggunakan server-side pagination
3. **Lazy Loading**: Data di-fetch sesuai kebutuhan
4. **Caching**: Identity menggunakan cookie caching
5. **Minimal API**: Lightweight endpoints tanpa MVC overhead

## 🔒 Security

1. **ASP.NET Identity**: Standard security best practices
2. **CSRF Protection**: Antiforgery tokens di semua form
3. **XSS Prevention**: Blazor auto-encoding + Markdig sanitization
4. **Role-Based Access**: Authorize attribute di semua halaman
5. **Password Policy**: Minimum 6 karakter, require digit & uppercase

## 📦 Deployment

### Requirements
- .NET 10 Runtime
- Database (SQLite recommended untuk development)
- Reverse proxy (Nginx/IIS) untuk production

### Production Checklist
- [ ] Update connection string
- [ ] Set database provider
- [ ] Configure HTTPS
- [ ] Set environment to Production
- [ ] Disable Swagger UI
- [ ] Configure proper logging
- [ ] Set SeedData to false
- [ ] Update admin password
