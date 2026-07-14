nama: BlazePoint

deskripsi:
aplikasi SharePoint yang di-rebuild dengan .NET Blazor Server, berikut adalah fiturnya:

---

 🔑 Core Features
- Document Management  
  Upload, versioning, metadata tagging, preview, dan recycle bin.
- List & Library  
  Custom lists, libraries dengan kolom dinamis, filter, dan grouping.
- Search Engine  
  Full-text search, semantic search dengan vector DB (SQLite, Qdrant, Chroma).
- User Authentication  
  Login/logout, register, reset password, role-based access (admin, editor, viewer).
- Workflow Automation  
  Drag-and-drop workflow designer, approval flows, notifications.
- Content Management Features
  Create/Edit Page, Add Webpart, Can add custom webpart with razor page (give an examples: calculator, time, weather, Address in Maps (leafletjs)), Can add custom masterpage with razor layout page (give examples: news, intranet, web layout)
- Navigation Management
  Customize navigation with UI (top navigation, side menu - quick launch)
---

 🎨 UI/UX Features
- Responsive Design  
  facebook UI/UX style, dukungan light/dark theme.
- Dashboard  
  Analytic charts (ChartJs.Blazor.Fork), KPI cards, trend analysis.
- Form Designer  
  Drag-and-drop form builder, JSON export/import, template library.
- Customizable Layout  
  Modular page sections, widget-based components.

---

 📊 Collaboration Features
- Team Sites  
  Site collections per department/project.
- Discussion Boards  
  Threaded comments, mentions, notifications.
- Calendar Integration  
  Shared events, reminders, sync with Outlook/Google Calendar.
- File Sharing  
  Public/private links, expiration dates, access control.

---

 ⚙️ Technical Features
- Database Support  
  SQLite, SQL Server, PostgreSQL, MySQL.
- Storage Options  
  FileSystem, Azure Blob, S3, MinIO.
- API Integration  
  REST/GraphQL endpoints, Microsoft.Extensions.AI untuk semantic search.
- Version Control  
  Auto-versioning saat publish, rollback ke versi sebelumnya.
- Extensibility  
  Plugin system, modular components, appsettings-based configuration.

---

 📂 Documentation & Support
- Readme.md  
  Setup, deployment, usage guide.
- Docs Folder  
  API reference, architecture diagrams, sample data & users.
- Admin Panel  
  User management, site settings, monitoring hooks.

---
Smart Features

Chat Bot Pelayanan Informasi
- Nama 'Clippy'
- Chat Page dengan tampilan yang keren, multi session (create/delete), reset session, bisa attach gambar (diupload lalu url-nya di jadikan image content) dan dokumen (di upload dan disertakan linknya ke text message).
- System Prompt (persona), temperature, model dan setting lainnya di simpan di appsetting
- Menggunakan Semantic Kernel Library dengan dukungan model: Open AI, Anthropic, Gemini, Ollama (bisa pilih)
- Tambahkan beberapa common functions (kernel functions) yang diperlukan termasuk query ke tavily (search internet), scrap page url, baca file dari url, cek tanggal, Waktu, math calculation, dan beberapa function yang diperlukan lainnya 
- Tambahkan functions untuk query data ke database yang dimiliki untuk mengetahui berbagai informasi
- Bisa render chat thread dengan mark down dengan baik ke html (baik table, media (image, video, audio), code, dan lainnya dengan baik)

Lainnya:
- Tambahkan dokumentasi lengkap di folder docs
- Tambahkan readme.md (English, Indonesia)
- Buat dengan Blazor Server dengan .NET 10
- optimasi kode agar aplikasi cepat dan ringan