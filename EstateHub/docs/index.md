# Dokumentasi EstateHub

## 📚 Daftar Dokumen

1. [Arsitektur Sistem](architecture.md)
2. [Panduan Pengguna](user-guide.md)
3. [API Reference](api-reference.md)
4. [Panduan Pengembangan](development.md)
5. [Deployment Guide](deployment.md)

---

## Ringkasan

EstateHub adalah platform manajemen properti berbasis web yang dibangun dengan:
- **.NET 10** + **Blazor Server** untuk UI
- **Entity Framework Core** untuk data access
- **MudBlazor** untuk komponen UI modern
- **Semantic Kernel** untuk integrasi AI
- **ML.NET** untuk machine learning
- **SignalR** untuk real-time notifications

### Arsitektur

Aplikasi menggunakan pola **Clean Architecture** sederhana:
- `Models/` - Domain entities
- `Data/` - EF Core DbContext & migrations
- `Services/` - Business logic layer
- `Components/` - Blazor UI components

### Database

Default: SQLite (file-based, no setup required)
Support: SQL Server, MySQL, PostgreSQL

### Keamanan

- Input validation
- Anti-forgery tokens
- CORS policies
- HTTPS enforcement

---

## Tech Stack Detail

| Kategori | Library/Package |
|----------|-----------------|
| UI Framework | MudBlazor 6.x |
| ORM | Entity Framework Core 10 |
| Database | SQLite, SQL Server, MySQL, PostgreSQL |
| AI | Microsoft.SemanticKernel |
| ML | Microsoft.ML |
| Excel Export | ClosedXML |
| CSV | CsvHelper |
| Markdown | Markdig |
| Maps | Leaflet.js (via JS interop) |
| Real-time | ASP.NET Core SignalR |
| API Docs | Swashbuckle / Swagger |
| Cloud Storage | Azure.Storage.Blobs |
