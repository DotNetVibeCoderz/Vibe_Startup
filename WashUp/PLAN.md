# 🧺 WashUp - Laundry Management System

## 📋 Development Plan & Checklist

### Status Legend:
- ⬜ Not Started  
- 🔄 In Progress  
- ✅ Completed  
- ❌ Blocked/Failed

---

## 🏗️ Phase 1: Project Foundation

| # | Task | Status |
|---|------|--------|
| 1.1 | Create Blazor Server Project (.NET 10) | ✅ |
| 1.2 | Setup project structure (folders) | ✅ |
| 1.3 | Add NuGet packages (EF Core, Identity, Semantic Kernel, etc.) | ✅ |
| 1.4 | Configure appsettings.json (DB, Storage, AI, Payment) | ✅ |
| 1.5 | Setup database context & models (EF Core) | ✅ |
| 1.6 | Configure Identity with roles (Pemilik, Admin, Kurir, Pelanggan) | ✅ |
| 1.7 | Setup Dark/Light theme with purple color scheme | ✅ |
| 1.8 | Create MainLayout with responsive sidebar | ✅ |

---

## 🗄️ Phase 2: Database & Models

| # | Task | Status |
|---|------|--------|
| 2.1 | User & Role models (extended Identity) | ✅ |
| 2.2 | Customer model (profile, preferences) | ✅ |
| 2.3 | Order model (status tracking, service type) | ✅ |
| 2.4 | Payment & Invoice models | ✅ |
| 2.5 | Inventory/Stock model | ✅ |
| 2.6 | Staff model (schedule, salary, performance) | ✅ |
| 2.7 | Branch (multi-cabang) model | ✅ |
| 2.8 | Loyalty & Membership models | ✅ |
| 2.9 | Review & Rating models | ✅ |
| 2.10 | Complaint model | ✅ |
| 2.11 | IoT sensor data model | ✅ |
| 2.12 | Pickup & Delivery model (GPS tracking) | ✅ |
| 2.13 | Chat session & message model | ✅ |
| 2.14 | Database seed data (sample users & data) | ✅ |

---

## 🔐 Phase 3: Authentication & Authorization

| # | Task | Status |
|---|------|--------|
| 3.1 | Login page (Blazor component) | ✅ |
| 3.2 | Registration page (customer self-registration) | ✅ |
| 3.3 | Reset password flow | ✅ |
| 3.4 | User profile management (+ ganti password & preferensi) | ✅ |
| 3.5 | Role-based access control (RBAC) | ✅ |
| 3.6 | Role management UI (admin) — /admin/users | ✅ |

---

## 📊 Phase 4: Dashboard & Analytics

| # | Task | Status |
|---|------|--------|
| 4.1 | Owner/Admin dashboard (orders, revenue, expenses) | ✅ |
| 4.2 | Revenue charts & graphs | ✅ |
| 4.3 | Financial reports (income, expense, profit, receivable) | ✅ |
| 4.4 | Trend analytics (demand prediction, popular services) | ✅ |
| 4.5 | Tax calculation (PPh) & reports | ✅ |
| 4.6 | Export reports (CSV/Excel + cetak PDF via print) | ✅ |

---

## 📦 Phase 5: Order Management

| # | Task | Status |
|---|------|--------|
| 5.1 | Order input form (service type, weight, price) | ✅ |
| 5.2 | Order status tracking (received→wash→iron→done→delivered) | ✅ |
| 5.3 | Order list with filtering & search | ✅ |
| 5.4 | Order detail view | ✅ |
| 5.5 | Customer order history | ✅ |
| 5.6 | Online ordering (customer-facing) | ✅ |
| 5.7 | Express & kiloan service types | ✅ |

---

## 👥 Phase 6: Customer Management

| # | Task | Status |
|---|------|--------|
| 6.1 | Customer list with search | ✅ |
| 6.2 | Customer detail (profile, history, preferences) | ✅ |
| 6.3 | Customer registration approval | ✅ |
| 6.4 | Loyalty points & rewards system | ✅ |
| 6.5 | Membership & subscription packages | ✅ |

---

## 💰 Phase 7: Payment & Billing

| # | Task | Status |
|---|------|--------|
| 7.1 | Invoice generation (auto) | ✅ |
| 7.2 | Payment gateway integration config | ✅ |
| 7.3 | Payment status tracking | ✅ |
| 7.4 | Receivables management | ✅ |
| 7.5 | Financial dashboard | ✅ |

---

## 📦 Phase 8: Inventory & Stock

| # | Task | Status |
|---|------|--------|
| 8.1 | Inventory list (detergent, fragrance, plastic, etc.) | ✅ |
| 8.2 | Stock in/out tracking | ✅ |
| 8.3 | Low stock alerts | ✅ |
| 8.4 | Usage reporting | ✅ |

---

## 👷 Phase 9: Staff Management

| # | Task | Status |
|---|------|--------|
| 9.1 | Staff list & profiles | ✅ |
| 9.2 | Work schedule management | ✅ |
| 9.3 | Salary management | ✅ |
| 9.4 | Performance tracking | ✅ |

---

## 🚚 Phase 10: Pickup, Delivery & Courier

| # | Task | Status |
|---|------|--------|
| 10.1 | Pickup scheduling | ✅ |
| 10.2 | Delivery scheduling | ✅ |
| 10.3 | Courier real-time GPS tracking (simulator) | ✅ |
| 10.4 | GPS simulator (start/stop on separate thread) | ✅ |
| 10.5 | ETA calculation | ✅ |

---

## 🔧 Phase 11: IoT Integration

| # | Task | Status |
|---|------|--------|
| 11.1 | IoT sensor data model | ✅ |
| 11.2 | Washing machine monitoring simulator | ✅ |
| 11.3 | Electricity/water monitoring simulator | ✅ |
| 11.4 | IoT simulator start/stop (separate thread) | ✅ |
| 11.5 | IoT dashboard | ✅ |

---

## 🌟 Phase 12: Marketplace & Social

| # | Task | Status |
|---|------|--------|
| 12.1 | Service listing (public marketplace) | ✅ |
| 12.2 | Rating & review system | ✅ |
| 12.3 | Customer feedback form | ✅ |

---

## 🏢 Phase 13: Multi-Branch Management

| # | Task | Status |
|---|------|--------|
| 13.1 | Branch CRUD | ✅ |
| 13.2 | Branch dashboard (owner view) | ✅ |
| 13.3 | Cross-branch reporting | ✅ |

---

## 💬 Phase 14: Chat Bot "Mbok Inem"

| # | Task | Status |
|---|------|--------|
| 14.1 | Semantic Kernel integration | ✅ |
| 14.2 | Multi-model support (OpenAI, Anthropic, Gemini, Ollama) | ✅ |
| 14.3 | Chat page UI (multi-session, create/delete/reset) | ✅ |
| 14.4 | Image attachment (upload → URL → image content) | ✅ |
| 14.5 | Document attachment (upload → link in text) | ✅ |
| 14.6 | System prompt & settings from appsettings.json | ✅ |
| 14.7 | Kernel functions: Tavily search, URL scraper, file reader | ✅ |
| 14.8 | Kernel functions: date/time, math calculation | ✅ |
| 14.9 | Kernel functions: database query | ✅ |
| 14.10 | Markdown to HTML rendering (tables, media, code) | ✅ |

---

## 🔌 Phase 15: REST API

| # | Task | Status |
|---|------|--------|
| 15.1 | Minimal API endpoints | ✅ |
| 15.2 | Swagger/OpenAPI configuration | ✅ |
| 15.3 | API authentication (JWT) | ✅ |
| 15.4 | Marketplace integration endpoints | ✅ |

---

## 🗄️ Phase 16: Storage & Database

| # | Task | Status |
|---|------|--------|
| 16.1 | FileSystem storage provider | ✅ |
| 16.2 | Azure Blob storage provider | ✅ |
| 16.3 | S3/MinIO storage provider | ✅ |
| 16.4 | SQLite support | ✅ |
| 16.5 | PostgreSQL support | ✅ |
| 16.6 | SQL Server support | ✅ |

---

## 📝 Phase 17: Documentation & Sample Data

| # | Task | Status |
|---|------|--------|
| 17.1 | README.md (English) | ✅ |
| 17.2 | README.md (Indonesia) | ✅ |
| 17.3 | API documentation | ✅ |
| 17.4 | User guide (docs/) | ✅ |
| 17.5 | Sample data & users (15+ customers, 50 orders, etc.) | ✅ |
| 17.6 | Database seed method | ✅ |

---

## 🎨 Phase 18: UI/UX Polish

| # | Task | Status |
|---|------|--------|
| 18.1 | Dark/Light theme toggle | ✅ |
| 18.2 | Purple color scheme (modern Facebook-like) | ✅ |
| 18.3 | Responsive mobile-friendly layout | ✅ |
| 18.4 | Loading states & animations | ✅ |
| 18.5 | Notification system | ✅ |
| 18.6 | Error handling pages | ✅ |

---

## 🧪 Phase 19: Testing & Optimization

| # | Task | Status |
|---|------|--------|
| 19.1 | Code optimization (fast & lightweight) | ✅ |
| 19.2 | Compilation verification | ✅ |
| 19.3 | Zero build errors | ✅ |

---

## 📦 Phase 20: Final Delivery

| # | Task | Status |
|---|------|--------|
| 20.1 | Final review | ✅ |
| 20.2 | Send project to user | ✅ |

---

## 🔁 Revisi Penyelesaian (Juli 2026)

Perbaikan besar setelah audit menyeluruh:

- **Interaktivitas diperbaiki**: `App.razor` kini memakai `@rendermode InteractiveServer` global (sebelumnya seluruh app dirender statis sehingga semua tombol mati). Halaman auth di-opt-out via `[ExcludeFromInteractiveRouting]` + form SSR agar cookie Identity bisa di-set; logout lewat endpoint `/auth/logout`.
- **Fitur baru**: reset password (demo mode, link tampil langsung), role management (/admin/users), export CSV + cetak PDF, form order asli (hitung diskon member + pajak, invoice & notifikasi otomatis), CRUD cabang + laporan lintas cabang, stok masuk/keluar + laporan pemakaian, tindak lanjut komplain oleh admin, piutang & pajak di Keuangan, tugaskan kurir dari detail order, review pelanggan (+20 poin), poin loyalty otomatis saat order selesai.
- **Chart asli**: ApexCharts di Laporan & Dashboard (palet tervalidasi CVD-safe), sparkline SVG di IoT, peta posisi kurir SVG dengan auto-refresh.
- **Bug fix**: ChatBot (DI KernelFunctions, endpoint Ollama/Anthropic/Gemini, function calling aktif, duplikasi history, pilihan provider terpakai), GPS simulator (status Arrived tertimpa), pruning data sensor/GPS, notifikasi topbar berfungsi, pencarian order/pelanggan berfungsi, klaim FullName di cookie, JWT bearer untuk REST API.
- **UI/UX**: tema "Ungu Bersih" dark/light dirombak, Plus Jakarta Sans, drawer mobile, modal/empty-state/timeline, focus states, print stylesheet.

**✨ BUILD STATUS: SUCCESS (0 errors) — diverifikasi end-to-end: login/logout, reset password, JWT API, export CSV, simulator IoT & GPS menulis data.**
