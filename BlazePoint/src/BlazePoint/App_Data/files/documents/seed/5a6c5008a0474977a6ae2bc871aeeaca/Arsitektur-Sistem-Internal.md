# Arsitektur Sistem Internal

Sistem internal berjalan di atas .NET 10 dengan arsitektur modular:
- **BlazePoint** — portal kolaborasi (Blazor Server, SQLite/PostgreSQL)
- **API Gateway** — REST + GraphQL untuk integrasi
- **Storage** — MinIO on-premise dengan replikasi ke S3

Diagram lengkap tersedia di folder /IT/diagrams.