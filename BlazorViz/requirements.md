nama: BlazorViz

deskripsi:
aplikasi analisa dan visualisasi data berbasis web dengan Blazor Server .NET 10, dirancang agar setara dengan pengalaman seperti Power BI namun dengan fleksibilitas penuh, dengan fitur:

---

 🔑 Core Features
- Multi-database support: Koneksi ke SQLite, SQLServer, PostgreSQL, MySQL, Oracle, Excel, CSV, Rest API, GraphQL.  
- Data import/export: Upload file (Excel, CSV, JSON) dan export hasil analisa ke berbagai format (CSV, PDF, Image).  
- ETL pipeline: Transformasi data sederhana (filter, join, aggregate) langsung dari UI.
- Mendukung script untuk membantu membuat ETL Pipeline dan Manipulasi dataset (python, js, csharp), buatkan banyak template untuk use case tertentu yang sudah terintegrasi di aplikasi tinggal di modifikasi oleh user
- Real-time refresh: Auto-refresh data dengan interval yang bisa dikonfigurasi.  

---

 📊 Visualization Features
- Rich chart library: Line, bar, pie, scatter, bubble, radar, waterfall, treemap, heatmap, gauge, funnel, tambahkan lagi yang banyak dibutuhkan.
- Advanced dashboards: Multi-tab, multi-panel, drag-and-drop layout.  
- Interactive filters: Slicer, dropdown, date range, multi-select.  
- Custom visuals: Mendukung integrasi dengan ChartJs, D3.js, ECharts.  
- Geo-maps: Visualisasi data berbasis peta dengan integrasi Leaflet/Mapbox.  
- Responsive design: Neo-brutalism soft style dengan dark/light theme.  

---

 🤖 AI & Automation
- Natural language query: Chat dengan data menggunakan LLM (OpenAI, Anthropic, Gemini, Ollama) dengan Semantic Kernel Library. Bisa attach gambar (diupload lalu url-nya di jadikan image content) dan dokumen (di upload dan disertakan linknya ke text message). Nama Assistant: Data Wizard. Bisa render chat thread dengan mark down dengan baik ke html (baik table, media (image, video, audio), code, dan lainnya dengan baik)
- Kernel Functions Common: Math Calculation, Check Date and Time, Search Internet, Scrape Url Page, Query ke dashboard dan dataset yang aktif.
- System Prompt (persona), temperature, model dan setting lainnya di simpan di appsetting
- Smart recommendations: Saran visualisasi otomatis berdasarkan tipe data.  
- RAG integration: Index dokumen eksternal (PDF, Word, Excel) ke vector DB (Qdrant, Chroma, Azure AI Search) dengan Microsoft.Extensions.VectorData.  
- Predictive analytics: Forecasting, regression, clustering dengan ML.NET.  
---

 🔐 Security & Collaboration
- User authentication: Login/logout, register, reset password, role-based access, user profile.  
- Audit logs: Semua aktivitas tercatat dengan filter & sort.  
- Collaboration: Share dashboard/report via link, embed ke aplikasi lain.  
- Version control: Simpan versi dashboard & rollback.  

---

 📈 Monitoring & Admin
- Usage analytics: Monitor jumlah query, token, aktivitas user.  
- Performance dashboard: Traffic web, response time, resource usage.  
- Storage integration: FileSystem, AzureBlob, S3, MinIO.  

---

 📂 Documentation & Extensibility
- Docs & Readme: Dokumentasi lengkap (English & Bahasa Indonesia).  
- Plugin system: Tambah custom kernel functions (cek tanggal, math, format).  
- API access: Minimal APIs untuk integrasi eksternal dengan swagger.  

Lainnya:
- Tambahkan dokumentasi lengkap di folder docs
- Buatkan beberapa contoh dataset dan dashboard
- Buatkan beberapa sample user
- Tambahkan readme.md (English, Indonesia)
- Buat dengan Blazor Server dengan .NET 10
- optimasi kode agar aplikasi cepat dan ringan