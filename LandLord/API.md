# 📡 API.md - API Endpoints Reference

## CRUD Endpoints

### Tanah (Land)

| Method | Endpoint | Description |
|--------|----------|-------------|
| GET | `/api/tanah` | Get all land records |
| GET | `/api/tanah/{id}` | Get land by ID |
| POST | `/api/tanah` | Create new land record |
| PUT | `/api/tanah/{id}` | Update land record |
| DELETE | `/api/tanah/{id}` | Delete land record |
| GET | `/api/tanah/search?keyword=` | Search land records |
| GET | `/api/tanah/filter?jenisHak=&statusPajak=&kota=` | Filter land records |

### Bangunan (Building)

| Method | Endpoint | Description |
|--------|----------|-------------|
| GET | `/api/bangunan` | Get all building records |
| GET | `/api/bangunan/{id}` | Get building by ID |
| POST | `/api/bangunan` | Create new building record |
| PUT | `/api/bangunan/{id}` | Update building record |
| DELETE | `/api/bangunan/{id}` | Delete building record |
| GET | `/api/bangunan/search?keyword=` | Search building records |
| GET | `/api/bangunan/filter?jenis=&fungsi=&status=` | Filter building records |

### Auth

| Method | Endpoint | Description |
|--------|----------|-------------|
| POST | `/api/auth/login` | User login |
| POST | `/api/auth/register` | User registration |
| POST | `/api/auth/reset-password` | Request password reset |
| GET | `/api/auth/profile` | Get user profile |

### Chat

| Method | Endpoint | Description |
|--------|----------|-------------|
| GET | `/api/chat/sessions` | Get chat sessions |
| POST | `/api/chat/sessions` | Create new session |
| GET | `/api/chat/sessions/{id}/messages` | Get messages |
| POST | `/api/chat/sessions/{id}/messages` | Send message |

### Documents

| Method | Endpoint | Description |
|--------|----------|-------------|
| POST | `/api/documents/upload` | Upload document |
| GET | `/api/documents/{id}/download` | Download document |
| DELETE | `/api/documents/{id}` | Delete document |

---

## Export

| Method | Endpoint | Description |
|--------|----------|-------------|
| GET | `/api/export/tanah/csv` | Export land data to CSV |
| GET | `/api/export/bangunan/csv` | Export building data to CSV |
| GET | `/api/export/tanah/excel` | Export land data to Excel |
| GET | `/api/export/bangunan/excel` | Export building data to Excel |
