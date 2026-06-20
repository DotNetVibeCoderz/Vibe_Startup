# 📡 API Reference

Dokumentasi API endpoint dan Kernel Functions (Tools) yang tersedia.

---

## REST API Endpoints

### Base URL
```
https://localhost:5001
```

---

### POST /api/chat/send

Mengirim pesan ke chat agent.

**Auth**: Required (Cookie)

**Request Body**:
```json
{
  "sessionId": 1,
  "message": "Tampilkan 10 pelanggan teratas",
  "attachments": [
    {
      "fileName": "report.pdf",
      "fileUrl": "/uploads/report.pdf",
      "contentType": "application/pdf",
      "fileSize": 1024000,
      "isImage": false
    }
  ]
}
```

**Response (200)**:
```json
{
  "id": 42,
  "chatSessionId": 1,
  "role": "assistant",
  "content": "Berikut adalah 10 pelanggan teratas...",
  "dashboardHtml": "<div class='dashboard'>...</div>",
  "promptTokens": 150,
  "completionTokens": 200,
  "totalTokens": 350,
  "responseTimeMs": 1250.5,
  "timestamp": "2024-01-15T10:30:00Z"
}
```

**Response (Error)**:
```json
{
  "status": 500,
  "detail": "Error message"
}
```

---

### POST /api/chat/voice

Voice chat endpoint (mockup).

**Auth**: Required

**Response**:
```json
{
  "message": "Fitur belum di rilis"
}
```

---

### GET /api/health

Health check endpoint.

**Auth**: None

**Response**:
```json
{
  "status": "healthy",
  "timestamp": "2024-01-15T10:30:00Z"
}
```

---

## Kernel Functions (LLM Tools)

Tools yang tersedia untuk LLM agent. Semua tools hanya bisa dipanggil oleh AI (bukan user langsung).

---

### queryToDatabase

Eksekusi SQL query (read-only).

**Parameter**:
```json
{
  "sql": "SELECT * FROM Customers LIMIT 10"
}
```

**Security**:
- Hanya SELECT dan WITH queries yang diizinkan
- INSERT, UPDATE, DELETE, DROP, ALTER akan ditolak
- Maksimal 100 baris hasil

**Response Format**:
```markdown
**Query Results:** (10 rows returned)

| `Id` | `Name` | `Email` | `City` |
| --- | --- | --- | --- |
| 1 | John Doe | john@email.com | Jakarta |
| 2 | Jane Smith | jane@email.com | Surabaya |
```

---

### getQueryStat

Statistik query terakhir.

**Parameter**: None

**Response**:
```
Last query returned 25 rows. Results were not truncated.
```

---

### createDashboard

Generate dashboard HTML.

**Parameter**:
```json
{
  "title": "Sales Dashboard Q4 2024",
  "description": "Ringkasan penjualan kuartal 4",
  "htmlContent": "<div class='dashboard'>...</div>"
}
```

**Output**: Dashboard panel yang muncul di chat, bisa di-expand/collapse dan di-export.

---

### searchKnowledgeBase

Mencari di Knowledge Base (RAG).

**Parameter**:
```json
{
  "query": "laporan keuangan tahunan",
  "topK": 5
}
```

**Response**:
```markdown
**Knowledge Base Search Results for:** "laporan keuangan tahunan"

- 📄 **Annual_Report_2024.pdf** (pdf) - Indexed: 2024-12-15 - Chunks: 45
  Keywords: annual, report, financial, revenue, profit
- 📄 **Q4_Summary.xlsx** (xlsx) - Indexed: 2024-12-10 - Chunks: 12
  Keywords: Q4, summary, sales, customers
```

---

### readDataFromUrl

Fetch data dari URL eksternal.

**Parameter**:
```json
{
  "url": "https://api.example.com/data.json"
}
```

**Response**: Konten dari URL (maksimal 5000 karakter).

---

### getCurrentDateTime

Waktu saat ini.

**Parameter**: None

**Response**:
```
Current date/time: 2024-01-15 10:30:45 UTC
```

---

### formatDateFriendly

Format tanggal ke format friendly.

**Parameter**:
```json
{
  "date": "2024-01-15"
}
```

**Response**:
```
Monday, 15 January 2024 00:00 UTC
```

---

## Tool Call Workflow

```
User: "Tampilkan customer dari Jakarta"
  │
  ▼
LLM decides: queryToDatabase
  │
  ▼
Tool executes: SELECT * FROM Customers WHERE City = 'Jakarta'
  │
  ▼
Result: 15 rows returned
  │
  ▼
LLM formats: table + analysis text
  │
  ▼
User sees: formatted response
```

### Multi-Tool Workflow

```
User: "Analisis sales dan buat dashboard"
  │
  ▼
LLM: queryToDatabase("SELECT ...")
  │
  ▼
Result: sales data
  │
  ▼
LLM: getQueryStat()
  │
  ▼
Result: 50 rows, 200ms
  │
  ▼
LLM: createDashboard(html)
  │
  ▼
User sees: analysis + dashboard panel
```

**Max Tool Calls**: 5 iterasi (prevent infinite loop)

---

## Error Codes

| Code | Description |
|------|-------------|
| 400 | Bad request / invalid parameters |
| 401 | Unauthorized (not logged in) |
| 403 | Forbidden (insufficient role) |
| 404 | Session/Connection not found |
| 500 | Internal server error |

### Common Errors

```json
// Invalid SQL
{
  "error": "Only SELECT queries are allowed for security reasons."
}

// No database
{
  "error": "No database connected. Please connect to a database first."
}

// Session not found
{
  "error": "Chat session not found."
}
```

---

## Query Examples

### Basic
```
"SELECT * FROM Customers"
"SELECT Name, Email FROM Customers WHERE City = 'Jakarta'"
```

### Aggregation
```
"SELECT Category, COUNT(*) as Total, SUM(Price) as Revenue 
 FROM Products 
 GROUP BY Category 
 ORDER BY Revenue DESC"
```

### Joins
```
"SELECT c.Name, o.OrderDate, o.TotalAmount
 FROM Customers c
 JOIN Orders o ON c.Id = o.CustomerId
 WHERE o.OrderDate >= '2024-01-01'"
```

### WITH (CTE)
```
"WITH TopCustomers AS (
   SELECT CustomerId, SUM(TotalAmount) as Total
   FROM Orders GROUP BY CustomerId
   ORDER BY Total DESC LIMIT 10
 )
 SELECT c.Name, tc.Total
 FROM Customers c JOIN TopCustomers tc ON c.Id = tc.CustomerId"
```

### Date Functions
```
"SELECT strftime('%Y-%m', OrderDate) as Month, 
        COUNT(*) as Orders, 
        SUM(TotalAmount) as Revenue
 FROM Orders
 GROUP BY Month
 ORDER BY Month"
```

---

## Rate Limiting

Untuk production, tambahkan rate limiting pada API endpoints.
