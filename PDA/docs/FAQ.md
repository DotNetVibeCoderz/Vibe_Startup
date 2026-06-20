# ❓ FAQ - Frequently Asked Questions

Pertanyaan yang sering diajukan tentang PDA.

---

## Umum

### Q: Apa itu PDA?
**A:** PDA (Personal Data Analyst) adalah aplikasi analisis data berbasis AI yang memungkinkan Anda "ngobrol" dengan database menggunakan bahasa alami. Cukup tanya seperti ngobrol, AI akan menggenerate SQL query dan memberikan analisis.

### Q: Apakah PDA gratis?
**A:** PDA sendiri gratis (open source). Biaya timbul dari LLM API (OpenAI, Anthropic, dll). Anda bisa menggunakan **Ollama** (lokal, gratis) untuk menghindari biaya API.

### Q: Data saya aman?
**A:** 
- Database Anda tetap di server Anda (tidak dikirim ke cloud kecuali via LLM API)
- Query SQL bersifat read-only (SELECT only)
- Gunakan Ollama lokal untuk privacy 100%
- Semua komunikasi via HTTPS

---

## Instalasi & Setup

### Q: .NET 10 belum rilis, bagaimana?
**A:** Gunakan .NET 9 atau 8:
```xml
<!-- Di PDA.csproj -->
<TargetFramework>net9.0</TargetFramework>
```

### Q: Error "API Key not configured"
**A:** Isi API key di `appsettings.json`:
```json
"LLM": {
  "Providers": {
    "OpenAI": { "ApiKey": "sk-your-key" }
  }
}
```
Atau gunakan Ollama (gratis, tanpa API key).

### Q: Bagaimana cara ganti database aplikasi?
**A:** Ubah connection string di `appsettings.json`:
```json
"ConnectionStrings": {
  "DefaultConnection": "Server=localhost;Database=PDA;Trusted_Connection=true"
}
```

---

## Database

### Q: Database apa saja yang didukung?
**A:** SQLite (default), SQL Server, PostgreSQL. MySQL bisa dikonfigurasi. Excel, CSV, dan MS Access via file import.

### Q: Bagaimana cara konek ke database perusahaan?
**A:** 
1. Buka `/connections`
2. Klik **➕ Tambah Koneksi**
3. Pilih tipe database
4. Masukkan connection string
5. Klik **🔍 Test** untuk verifikasi

### Q: Apakah AI bisa mengubah data saya?
**A:** **TIDAK.** Semua query SQL bersifat **read-only**. Hanya SELECT dan WITH yang diizinkan. INSERT, UPDATE, DELETE otomatis ditolak.

### Q: Kenapa schema database saya tidak terbaca?
**A:** 
- Pastikan user database punya akses baca metadata
- Coba test connection dulu
- Cek log untuk error detail
- Beberapa database perlu permission khusus untuk `GetSchema()`

---

## LLM & Chat

### Q: Kenapa respons AI lambat?
**A:** 
- Cloud API (OpenAI/Anthropic): ~1-5 detik normal
- Ollama lokal: tergantung hardware (CPU: lambat, GPU: cepat)
- Query database besar: tambah waktu
- Gunakan model lebih kecil (gpt-4o-mini, phi3)

### Q: Bisa pakai model lokal?
**A:** Ya! Gunakan **Ollama**:
```bash
ollama pull llama3.1
```
Set `DefaultProvider: "Ollama"` di config.

### Q: AI tidak mengerti pertanyaan saya
**A:** Tips:
1. Sebutkan nama tabel/kolom yang spesifik
2. Gunakan bahasa yang jelas
3. Lihat sample prompts untuk inspirasi
4. Coba naikkan temperature (0.5-0.7)
5. Pastikan schema database terbaca

### Q: Bagaimana cara ganti model di tengah chat?
**A:** Di halaman chat, ada dropdown **Model** dan **Provider**. Ganti kapan saja, config tersimpan otomatis per sesi.

---

## RAG & Knowledge Base

### Q: Apa bedanya RAG dengan chat biasa?
**A:** 
- **Chat biasa**: AI hanya mengandalkan training data + schema database
- **Dengan RAG**: AI juga bisa mencari di dokumen Anda (PDF, Word, Excel, dll)

### Q: Kenapa dokumen saya tidak ke-index?
**A:** 
1. Cek format file didukung (.pdf, .docx, .xlsx, .txt, .csv, .pptx)
2. Cek ukuran file < 50 MB (default)
3. Letakkan di folder `KnowledgeBase/`
4. Tunggu scan interval (30 menit)
5. Cek `/rag-index` untuk status

### Q: Berapa banyak dokumen yang bisa di-index?
**A:** Tergantung vector store:
- **In-Memory**: Terbatas RAM (~100-500 dokumen)
- **Qdrant/Chroma**: Ribuan hingga jutaan

---

## Troubleshooting

### Q: Aplikasi crash saat start
**A:** 
```bash
# Hapus database dan coba lagi
rm PDA.db
dotnet run
```

### Q: Error "dotnet not found"
**A:** Install .NET SDK dari [dotnet.microsoft.com](https://dotnet.microsoft.com)

### Q: Port sudah digunakan
**A:** 
```bash
dotnet run --urls "https://localhost:7001"
```

### Q: Style/CSS tidak muncul
**A:** 
- Clear browser cache (Ctrl+Shift+R)
- Cek file `wwwroot/app.css` exists
- Cek browser console untuk error

### Q: Blazor circuit disconnected
**A:** Normal untuk Blazor Server. Aplikasi akan auto-reconnect. Jika sering terjadi:
- Cek koneksi internet
- Tambah SignalR timeout di config
- Pertimbangkan Blazor WebAssembly untuk production

---

## Security

### Q: Apakah API key aman di appsettings.json?
**A:** Untuk development, ya. Untuk production:
- Gunakan **Environment Variables**
- Gunakan **Azure Key Vault** / **AWS Secrets Manager**
- Gunakan **User Secrets** (`dotnet user-secrets set`)

### Q: Apakah perlu HTTPS?
**A:** **YA!** HTTPS wajib untuk production. Development environment sudah include self-signed certificate.

### Q: Bagaimana cara menambah user?
**A:** 
1. Via halaman `/register`
2. Via database langsung (untuk admin)
3. Via UserManager API

---

## Performance

### Q: Aplikasi terasa lambat
**A:** 
1. Gunakan SQL Server/PostgreSQL (bukan SQLite) untuk production
2. Kurangi ChunkOverlap di RAG
3. Batasi history chat (default 20 messages)
4. Gunakan model LLM lebih kecil
5. Enable response compression

### Q: Berapa user yang bisa di-handle?
**A:** 
- **Blazor Server**: ~100-500 concurrent users per server
- **Dengan scaling**: Bisa ribuan
- **Pertimbangan**: SignalR circuit per user (~250KB memory)

---

## Kontribusi

### Q: Bagaimana cara berkontribusi?
**A:** 
1. Fork repository
2. Buat branch fitur
3. Commit changes
4. Buat Pull Request

### Q: Roadmap fitur?
**A:** Lihat [PLAN.md](../PLAN.md) untuk development plan.

---

> **Punya pertanyaan lain?** Hubungi kami di [GraviCode Studios](https://studios.gravicode.com)
