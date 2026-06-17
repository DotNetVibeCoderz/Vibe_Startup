# PLAN.md - Aplikasi Pemesanan Tiket Bioskop (Bioskop)

## Checklist Modul

### Fase 1: Setup & Infrastruktur
- [x] Buat project Blazor Server .NET 10
- [x] NuGet packages (EF Core, Identity, Semantic Kernel, QRCoder, Swagger, Markdig, ClosedXML, CsvHelper)
- [x] Struktur folder (Models, Data, Services, Components, Api)
- [x] appsettings.json lengkap
- [x] Program.cs (services, middleware, routing)

### Fase 2: Database & Models
- [x] 15 Models (Movie, Studio, Seat, Showtime, Snack, Order, Ticket, Payment, OrderSnack, Post, Comment, Like, MovieRating, ChatSession, ChatMessage, AuditLog, TrafficLog, ApplicationUser)
- [x] DbContext multi-provider (SQLite, SQL Server, MySQL, PostgreSQL)
- [x] Seed data (8 film, 5 studio, kursi, 12 snack, 6 user, posts, comments, ratings)

### Fase 3: Autentikasi & Otorisasi
- [x] Register, Login, Logout, Access Denied
- [x] Role-based access (Admin, Operator, User)
- [x] Cookie authentication

### Fase 4: Layout & UI Framework
- [x] MainLayout sidebar + topbar
- [x] Light/Dark theme (CSS variables di :root & .dark-theme)
- [x] Animasi (fadeInUp, slideInLeft, pulse, float, typing)
- [x] Komponen shared (Modal, Toast)

### Fase 5: Services
- [x] MovieService (CRUD, rating, search, filter, average rating)
- [x] ShowtimeService, SeatService (real-time seat map)
- [x] SnackService, OrderService, TicketService (QR generation)
- [x] PaymentService (simulasi), PostService (timeline)
- [x] AuditService (log + traffic), StorageService, ChatBotService (Semantic Kernel)

### Fase 6: Pemesanan Tiket
- [x] 3-step booking: Film&Jadwal → Pilih Kursi → Checkout
- [x] Seat map interaktif real-time
- [x] Snack add-on + 4 metode pembayaran
- [x] Generate QR Code tiket (PNG base64)
- [x] My Tickets page

### Fase 7: Curhat Film (Timeline)
- [x] Timeline + sidebar + trending topics
- [x] Post (text, event), Comment, Like
- [x] Show More per 100 postingan
- [x] Delete postingan sendiri

### Fase 8: Chat Bot (Si Bobby Movie Maniac)
- [x] Multi-session chat + reset
- [x] Quick questions
- [x] Semantic Kernel + kernel functions
- [x] Markdown rendering (Markdig)

### Fase 9: REST API
- [x] Minimal API endpoints (Movies, Showtimes, Seats, Snacks, Orders, Tickets, Stats)
- [x] Swagger + ApiKey auth

### Fase 10: Dashboard & Monitoring
- [x] Admin Dashboard (stats, sales chart, snack chart, traffic, recent orders)
- [x] Audit + Traffic logging middleware

### Fase 11: Styling Fix
- [x] **Global CSS** (`wwwroot/app.css`) - semua variabel, reset, layout, komponen umum
- [x] **CSS Variables pindah ke global** - tidak lagi scoped di MainLayout
- [x] **auth.css dihapus** - style digabung ke global app.css
- [x] **Semua component CSS** dibersihkan, pakai variabel global
- [x] Dark theme fully supported via `.dark-theme` class
- [x] Responsive design (mobile breakpoint 768px)
- [x] Build: 0 Errors ✅

---

## Cara Menjalankan
```bash
cd Bioskop
dotnet run
```
Buka: `https://localhost:5001` | Swagger: `/swagger`

### Login:
- Admin: `admin@bioskop.com` / `Admin123!`
- User: `budi@email.com` / `User1234!`
