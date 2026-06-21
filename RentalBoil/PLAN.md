# 🚗 RentalBoil - Vehicle Rental Application

## 📋 Development Plan & Progress

### All Phases ✅ 100% Complete

---

### 🆕 Phase 12C: Vehicle Update API Endpoints (Simulator) ✅

| Endpoint | Method | Description |
|----------|--------|-------------|
| `/api/vehicles/{id}/location` | PATCH | Update GPS location, speed, heading, address |
| `/api/vehicles/{id}/iot` | PATCH | Update IoT status (lock, engine, motion) |
| `/api/vehicles/{id}/condition` | PATCH | Update condition (color, availability, desc) |
| `/api/vehicles/{id}/pricing` | PATCH | Update pricing (per hour/day, multiplier, insurance) |
| `/api/vehicles/{id}/simulator-update` | POST | **Full update**: GPS + IoT + Speed + Address (single call) |
| `/api/vehicles/batch/simulator-update` | POST | **Batch update**: multiple vehicles at once |
| `/api/vehicles/active-for-simulator` | GET | List vehicles in active bookings (need tracking) |

### GPS Simulator - Dual Mode ✅
- **DirectDB mode**: Update langsung ke database via DbContext (default)
- **API mode**: Kirim update via REST API endpoint (untuk simulator terpisah)
- **Batch mode**: Update banyak kendaraan sekaligus lewat `/api/vehicles/batch/simulator-update`
- **Auto fallback**: Jika API unreachable, otomatis fallback ke DirectDB
- Named HttpClient `SimulatorClient` dengan timeout 10 detik

### Konfigurasi GPS Simulator
```json
"GPS": {
  "SimulatorEnabled": true,
  "UpdateIntervalSeconds": 3,
  "UpdateMode": "DirectDB",       // "DirectDB" | "Api"
  "UseBatchUpdate": false,        // true = update batch via API
  "ApiBaseUrl": "https://localhost:5001"
}
```

---

## 📊 Progress Tracking
| Module | Status |
|--------|--------|
| Project Foundation | ✅ 100% |
| Database & Models | ✅ 100% |
| Auth & Authorization | ✅ 100% |
| Core UI Framework | ✅ 100% |
| Master Data | ✅ 100% |
| Customer/Partner/Admin | ✅ 100% |
| Chat Bot (4 AI providers) | ✅ 100% |
| REST API (30+ endpoints) | ✅ 100% |
| Kernel Functions (17) | ✅ 100% |
| Simulator Update API | ✅ 100% |
| Documentation | ✅ 100% |

---

*Last Updated: March 2025 | Jacky The Code Bender @ Gravicode Studios*
