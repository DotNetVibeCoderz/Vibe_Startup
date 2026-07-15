nama: AppBender

deskripsi:
aplikasi kombinasi *Low-Code Platform* ala Power Apps + Power Automate, berbasis .NET 10 dan Blazor Server, dengan tambahan Data Hub (fungsi mirip DataVerse) serta AI-assisted development:

---

 📝 Core Features
- Visual Form Designer – Drag & drop komponen UI dengan preview real-time. tambahkan komponen UI yang lengkap 
- Workflow Automation – Designer berbasis blok (trigger → action → condition → output), buatkan actions yang lengkap seperti power automate, termasuk action-action berbasis AI (call LLM, computer vision, image/audio/video/code generator, OCR, search internet, dsb)  
- Dual Mode Editing – Mode low-code (designer) + mode advanced (C#, Python, JS).  
- export/import Form, Workflow, Schema/Dataset ke dalam file json
---

 ⚙️ Integration & Extensibility
- Connector Library – Built-in konektor ke berbagai DB, API REST/GraphQL, cloud storage, cloud/3rd party service yang popular.  
- Custom Connector Builder – Tambah konektor baru via JSON + kode backend.  Berikan beberapa contohnya.
- Event Triggers – Trigger berbasis waktu, webhook, database, atau user action.  

---

 🗄️ Data Hub (Mirip DataVerse)
- Unified Data Hub – Pusat data internal untuk entity, relasi, metadata aplikasi.  
- Entity & Relationship Designer – Visual schema builder dengan tipe data kompleks.  
- Data API Layer – Otomatis expose entity ke REST/GraphQL API dengan swagger   
- Cross-Connector Sync – Sinkronisasi dengan DB eksternal & cloud storage.  
- Security & Governance – Role-based access, audit logs, versioning schema/data.  

---

 🔄 Data & Persistence
- Versioning & Rollback – Auto-version setiap publish workflow/app.  
- Data Storage Options – SQLite, SQL Server, PostgreSQL, MySQL, plus MinIO/S3/Azure Blob.  

---

 🌐 Publishing & Sharing
- App Publishing – Generate URL unik untuk aplikasi.  
- Role-Based Access – Kontrol granular: admin, developer, end-user.  
- Multi-Tenant Support – Isolasi data antar organisasi.  

 📈 Monitoring & Admin
- Usage analytics: Monitor jumlah query, token, aktivitas user.  
- Performance dashboard: Traffic web, response time, resource usage.  
- Storage integration: FileSystem, AzureBlob, S3, MinIO.  

---

 🤖 AI-Assisted Development
- AI Schema Generator – AI membuat entity & relasi dari deskripsi kebutuhan.  
- Prompt-to-App – Prompt → auto generate form, workflow, schema.  
- AI Workflow Assistant – AI menyarankan langkah workflow sesuai deskripsi.  
- Auto-Dashboard Generator – AI membuat dashboard interaktif dari data.  
- Model Builder Integration – AI bantu buat ML model langsung dari dataset.  
- Conversational Builder – Chat dengan AI untuk membangun aplikasi/workflow.  
- Knowledge RAG Integration – AI menjawab pertanyaan berbasis dokumen (PDF, Word, Excel, CSV).  
- Support LLM (OpenAI, Anthropic, Gemini, Ollama) dengan Semantic Kernel Library. Bisa attach gambar (diupload lalu url-nya di jadikan image content) dan dokumen (di upload dan disertakan linknya ke text message). Nama Assistant: App Guru. Bisa render chat thread dengan mark down dengan baik ke html (baik table, media (image, video, audio), code, dan lainnya dengan baik)
- Kernel Functions Common: Math Calculation, Check Date and Time, Search Internet (tavily), Scrape Url Page, Query ke dataset.
- System Prompt (persona), temperature, model dan setting lainnya di simpan di appsetting
- Support multi session (add/delete), reset session
- kasih beberapa contoh prompt untuk bikin form, workflow, dataset, dsb
---

 🎨 UI & UX
- Modern Responsive Design – modern UI seperti facebook dengan light/dark theme.  
- Interactive Dashboards – Chart interaktif, filter, tabular data.  
- Customizable Themes – Tambah tema via CSS/Blazor styling.  

---

 🚀 Developer-Oriented Features
- Extensible Architecture – Modular, mudah ditambah komponen baru.  
- Code Snippet Library – Snippet untuk API calls, validation, looping, dan berbagai common code snippet untuk mempercepat development.  
- Monitoring & Logging – Real-time monitoring workflow execution & error logging.  

---

 🔄 End-to-End AI + Low-Code
- AI-Assisted Testing – Generate test cases otomatis.  
- AI Deployment Advisor – Rekomendasi deployment (scaling, DB, storage).  

---

📌 Dengan fitur ini, platform menjadi Low-Code + AI-driven Development Environment:  
- Data Hub sebagai pusat data mirip DataVerse.  
- AI sebagai co-pilot untuk schema, model, aplikasi, workflow, dashboard.  
- Blazor Server + .NET 10 sebagai fondasi cross-platform yang scalable.  

---

Lainnya:
- Tambahkan dokumentasi lengkap di folder docs
- Buatkan beberapa contoh form, workflow dan dataset di Data Hub
- Buatkan beberapa sample user
- Tambahkan readme.md (English, Indonesia)
- Buat dengan Blazor Server dengan .NET 10
- optimasi kode agar aplikasi cepat dan ringan
