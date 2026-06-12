# 🖥️ Admin Dashboard — FastRide

> Complete guide to the Blazor Server Admin Dashboard with real-time analytics, management tools, and data export.

---

## 📖 Overview

The **FastRide Admin Dashboard** is a Blazor Server web application providing administrators with a comprehensive interface to manage the entire ride-hailing platform.

---

## 🎨 Design

### Theme
- **Dark theme** by default (toggleable to light)
- Custom CSS with CSS variables for consistency
- Bootstrap 5.3 + Bootstrap Icons
- Responsive design (mobile-friendly sidebar collapse)

### Color Palette

| Variable | Color | Usage |
|----------|-------|-------|
| `--fastride-primary` | `#FFD700` (Gold) | Accents, active elements |
| `--fastride-accent` | `#FF6B35` (Orange) | Call-to-action |
| `--fastride-dark` | `#1a1a2e` | Background |
| `--fastride-card` | `#16213e` | Card backgrounds |
| `--fastride-success` | `#00C853` | Positive indicators |
| `--fastride-warning` | `#FFD600` | Warnings |
| `--fastride-danger` | `#FF1744` | Errors |

---

## 📱 Pages

### 1. Dashboard (`/`)

The main landing page showing real-time platform overview.

**Features:**
- 📊 4 stat cards (Total Orders, Active Drivers, Revenue, Avg Rating)
- 📈 Orders per Hour bar chart
- 🥧 Orders by Status pie chart
- 📋 Recent Orders table (last 5)
- 🔄 Refresh button

**Mock Data:**
```
Total Orders Today: 156
Active Drivers: 42
Revenue Today: Rp 4,250,000
Avg Rating: 4.7 ⭐
```

### 2. Orders (`/orders`)

Full order management interface.

**Features:**
- 🔍 Advanced filter bar (date, status, vehicle, search)
- 📋 Paginated orders table
- 🏷️ Color-coded status badges
- 📥 Export to CSV/Excel (planned)

**Status Badges:**
| Status | Color |
|--------|-------|
| Requested | `#17a2b8` (Cyan) |
| Accepted | `#6f42c1` (Purple) |
| On Trip | `#fd7e14` (Orange) |
| Completed | `#28a745` (Green) |
| Cancelled | `#dc3545` (Red) |

### 3. Drivers (`/drivers`)

Driver fleet management.

**Features:**
- 📊 Driver stats (Total, Online, On Trip, Avg Rating)
- 📋 Driver table with vehicle info, status, earnings
- ➕ Add Driver button
- 👁️ View details / 🔴 Ban actions

### 4. Riders (`/riders`)

Registered riders management.

**Features:**
- 📋 Riders table (name, email, phone, trips, spending)
- 🟢 Active/Inactive status badges
- 🔍 Search and filter (planned)

### 5. Payments (`/payments`)

Payment transaction monitoring.

**Features:**
- 💰 Revenue summary cards (Total, Pending, Success Rate)
- 📋 Transaction table with method icons
- 🟢 Completed / 🟡 Pending status badges

### 6. Promos (`/promos`)

Promo code and discount management.

**Features:**
- 📋 Promo table (code, type, value, usage, validity)
- ➕ Create Promo button
- 🟢 Active / 🔴 Inactive status

### 7. Analytics (`/analytics`)

Deep analytics and reporting.

**Features:**
- 📈 Revenue trend chart (7-day)
- 📊 Orders by vehicle category chart
- 🗺️ Hourly heatmap (orders per hour per day)

---

## 🛠️ Technical Implementation

### Routing

Uses Blazor `@page` directive routing:

```csharp
@page "/"           // Dashboard
@page "/orders"     // Orders
@page "/drivers"    // Drivers
@page "/riders"     // Riders
@page "/payments"   // Payments
@page "/promos"     // Promos
@page "/analytics"  // Analytics
```

### Layout

- **MainLayout** — Sidebar + Content area
- **Sidebar** — Navigation with icons, brand, theme toggle
- **NavLink** — Active state highlighting with gold left border

### Chart Integration

Uses **Chart.js 4.4** loaded via CDN. Charts rendered through JS interop (planned).

```html
<script src="https://cdn.jsdelivr.net/npm/chart.js@4.4.0/dist/chart.umd.min.js"></script>
```

### Data Flow

```
AdminWeb ──HTTP──▶ FastRide.Api ──EF Core──▶ Database
   │                                              │
   └──────────── JSON Response ◀──────────────────┘
```

---

## 🚀 Running the Dashboard

```bash
dotnet run --project FastRide.AdminWeb
```

Dashboard runs on:
- `https://localhost:5002`
- `http://localhost:5003`

---

## 🔧 Configuration

### `appsettings.json`

```json
{
  "ApiBaseUrl": "https://localhost:5001",
  "Urls": "https://localhost:5002;http://localhost:5003"
}
```

---

## 🚧 Planned Features

- [ ] Real API integration (replace mock data)
- [ ] Chart.js interop for live charts
- [ ] Data export (CSV/Excel)
- [ ] Advanced filtering with date range picker
- [ ] User detail modal/drawer
- [ ] Real-time updates via SignalR
- [ ] Role-based UI (hide admin-only features)
- [ ] Dark/Light theme persistence
