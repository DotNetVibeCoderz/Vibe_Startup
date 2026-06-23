# 🔌 REST API Documentation

## Authentication

Semua endpoint REST API menggunakan **API Key Authentication**.

### Header
```
X-Api-Key: eventsphere-api-key-2025-secure
```

### API Keys (default)
| Key | Usage |
|-----|-------|
| `eventsphere-api-key-2025-secure` | Standard access |
| `eventsphere-admin-key-2025` | Admin access |

Konfigurasi di `appsettings.json`:
```json
{
  "ApiKey": {
    "HeaderName": "X-Api-Key",
    "Keys": [
      "eventsphere-api-key-2025-secure",
      "eventsphere-admin-key-2025"
    ]
  }
}
```

---

## Swagger UI

```
http://localhost:5001/swagger
```

---

## Base URL

```
http://localhost:5001/api/v1
```

---

## Endpoints

### 📅 Events

| Method | URL | Description |
|--------|-----|-------------|
| `GET` | `/api/v1/events` | List events (pagination + filter status) |
| `GET` | `/api/v1/events/{id}` | Get event detail (full includes) |
| `POST` | `/api/v1/events` | Create new event |
| `PUT` | `/api/v1/events/{id}` | Update event |
| `DELETE` | `/api/v1/events/{id}` | Delete event |

**Query Params (GET list):**
- `status` — Draft, Planned, Confirmed, InProgress, Completed, Cancelled
- `page` — default 1
- `size` — default 20

**Example Request:**
```bash
curl -H "X-Api-Key: eventsphere-api-key-2025-secure" \
  "http://localhost:5001/api/v1/events?status=Confirmed&page=1&size=10"
```

### 🏢 Vendors

| Method | URL | Description |
|--------|-----|-------------|
| `GET` | `/api/v1/vendors` | List vendors (filter + search) |
| `GET` | `/api/v1/vendors/{id}` | Get vendor detail |
| `POST` | `/api/v1/vendors` | Create vendor |
| `PUT` | `/api/v1/vendors/{id}` | Update vendor |
| `DELETE` | `/api/v1/vendors/{id}` | Delete vendor |

**Query Params:** `category`, `search`, `page`, `size`

### 👥 Guests

| Method | URL | Description |
|--------|-----|-------------|
| `GET` | `/api/v1/guests/{eventId}` | List guests for event |

### 💰 Budget

| Method | URL | Description |
|--------|-----|-------------|
| `GET` | `/api/v1/budget/{eventId}` | Budget items with totals |

### ✅ Tasks

| Method | URL | Description |
|--------|-----|-------------|
| `GET` | `/api/v1/tasks` | All tasks (filter by userId, status) |
| `GET` | `/api/v1/tasks/{eventId}` | Tasks per event |

### 📁 Documents

| Method | URL | Description |
|--------|-----|-------------|
| `GET` | `/api/v1/documents/{eventId}` | Documents for event |

### 🖼️ Gallery

| Method | URL | Description |
|--------|-----|-------------|
| `GET` | `/api/v1/gallery/{eventId}` | Media gallery (filter by category) |

### 🪑 Seating

| Method | URL | Description |
|--------|-----|-------------|
| `GET` | `/api/v1/seating/{eventId}` | Table arrangement with guests |

### 💬 Chat

| Method | URL | Description |
|--------|-----|-------------|
| `GET` | `/api/v1/chat/{sessionId}` | Messages in chat session |

### 💡 Forum

| Method | URL | Description |
|--------|-----|-------------|
| `GET` | `/api/v1/forum` | Forum posts (filter by category) |
| `GET` | `/api/v1/forum/{id}` | Post detail with comments |

### 👤 Users

| Method | URL | Description |
|--------|-----|-------------|
| `GET` | `/api/v1/users` | List users (filter by role, search, active) |
| `GET` | `/api/v1/users/{id}` | User detail |

### 📊 Dashboard

| Method | URL | Description |
|--------|-----|-------------|
| `GET` | `/api/v1/dashboard/stats` | Overall statistics |
| `GET` | `/api/v1/dashboard/events-by-type` | Event type breakdown |

### 🔔 Notifications

| Method | URL | Description |
|--------|-----|-------------|
| `GET` | `/api/v1/notifications/{userId}` | User notifications (filter unreadOnly) |

### ⭐ Feedback

| Method | URL | Description |
|--------|-----|-------------|
| `GET` | `/api/v1/feedback/{eventId}` | Event feedback with avg rating |

### 📊 Contracts & Invoices

| Method | URL | Description |
|--------|-----|-------------|
| `GET` | `/api/v1/contracts/{eventId}` | Vendor contracts for event |
| `GET` | `/api/v1/invoices/{contractId}` | Invoices for contract |

---

## Response Format

Semua response dalam format JSON:

```json
{
  "total": 42,
  "page": 1,
  "size": 20,
  "data": [...]
}
```

Error response:
```json
{
  "error": "Unauthorized",
  "message": "Missing X-Api-Key header."
}
```

---

## Pagination

Endpoint dengan list support pagination:
- `page` — halaman (default: 1)
- `size` — item per halaman (default: 20, max: 100)
