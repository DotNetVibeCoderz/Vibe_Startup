# 📚 RAG (Retrieval Augmented Generation) Guide

Panduan lengkap Knowledge Base indexing dan pencarian di PDA.

---

## Apa itu RAG?

RAG (Retrieval Augmented Generation) adalah teknik yang memungkinkan AI mencari informasi dari dokumen Anda sebelum menjawab. Ini memberikan konteks tambahan yang tidak ada dalam training data LLM.

```
User: "Apa kesimpulan laporan Q4?"
  │
  ▼
searchKnowledgeBase("laporan Q4")
  │
  ▼
Vector Search → Dokumen relevan ditemukan
  │
  ▼
LLM: "Berdasarkan laporan Q4, penjualan meningkat 15%..."
```

---

## Cara Kerja di PDA

### 1. Document Ingestion Flow
```
KnowledgeBase/
├── report_q4.pdf         ──→ Extract text ──→ Chunking ──→ Index
├── sales_data.xlsx       ──→ Extract text ──→ Chunking ──→ Index
├── product_catalog.docx  ──→ Extract text ──→ Chunking ──→ Index
└── notes.txt             ──→ Extract text ──→ Chunking ──→ Index
```

### 2. Search Flow
```
Query: "laporan keuangan Q4"
  │
  ▼
Tokenize: ["laporan", "keuangan", "Q4"]
  │
  ▼
BM25-like scoring against all chunks
  │
  ▼
Return top-K results (default: 5)
  │
  ▼
LLM receives results as context
```

---

## Setup RAG

### 1. Siapkan Folder

```bash
mkdir KnowledgeBase
```

### 2. Tambahkan Dokumen

Letakkan dokumen di folder `KnowledgeBase/`:
```bash
KnowledgeBase/
├── Annual_Report_2024.pdf
├── Sales_Data_Q4.xlsx
├── Product_Catalog.docx
├── Meeting_Notes.txt
└── Pricing_Strategy.pptx
```

### 3. Konfigurasi

```json
"RAG": {
  "Enabled": true,
  "KnowledgeBasePath": "KnowledgeBase",
  "ScanIntervalMinutes": 30,
  "VectorProvider": "InMemory",
  "ChunkSize": 1000,
  "ChunkOverlap": 200,
  "MaxFileSizeMb": 50
}
```

### 4. Verifikasi

1. Buka `/rag-index`
2. Dokumen yang sudah di-index akan muncul di tabel
3. Tunggu hingga scan selesai (otomatis setiap 30 menit)

---

## Format Dokumen yang Didukung

| Format | Ekstensi | Ekstraksi | Kualitas |
|--------|---------|-----------|----------|
| **Text** | .txt, .md | Full | ⭐⭐⭐⭐⭐ |
| **PDF** | .pdf | Basic regex | ⭐⭐ |
| **Word** | .docx, .doc | Basic regex | ⭐⭐ |
| **Excel** | .xlsx, .xls | Basic regex | ⭐⭐ |
| **PowerPoint** | .pptx | Basic regex | ⭐ |
| **CSV** | .csv | Full | ⭐⭐⭐⭐⭐ |
| **HTML** | .html, .htm | Full | ⭐⭐⭐⭐ |
| **JSON** | .json | Full | ⭐⭐⭐⭐ |

> **⚠️ Note:** Ekstraksi PDF, DOCX, XLSX, PPTX saat ini menggunakan basic text extraction. Untuk production, tambahkan library khusus:
> - PDF: `PdfPig` atau `iTextSharp`
> - DOCX: `DocumentFormat.OpenXml`
> - XLSX: `EPPlus` (sudah terinstall)

---

## Chunking Configuration

### ChunkSize
Ukuran setiap potongan teks (dalam karakter).

| Setting | Use Case |
|---------|----------|
| 500 | Presisi tinggi, dokumen pendek |
| **1000** | **(Default)** Seimbang |
| 2000 | Dokumen panjang, konteks luas |

### ChunkOverlap
Overlap antar chunk untuk menjaga konteks.

| Setting | Use Case |
|---------|----------|
| 100 | Presisi, sedikit overlap |
| **200** | **(Default)** Seimbang |
| 500 | Konteks maksimal, lebih banyak chunk |

### Contoh Chunking

**ChunkSize: 100, ChunkOverlap: 20**
```
"The quick brown fox jumps over the lazy dog. The dog was sleeping
 peacefully under the tree."

Chunk 1: "The quick brown fox jumps over the lazy dog. The dog was sleeping"
Chunk 2: "The dog was sleeping peacefully under the tree."
         ^^^^^^^^^^^^^^^^ overlap
```

---

## Vector Store Providers

### In-Memory (Default)
- ✅ Zero setup
- ✅ Cepat untuk development
- ❌ Hilang saat restart
- ❌ Tidak scalable

### FileSystem
- ✅ Persistent
- ✅ Simple
- ❌ Tidak untuk banyak dokumen

### Qdrant (Production)
```json
"Qdrant": {
  "Endpoint": "http://localhost:6333",
  "CollectionName": "pda-knowledge"
}
```
- ✅ Production-ready
- ✅ Scalable
- ✅ Fast vector search
- ❌ Butuh setup terpisah

### Azure AI Search
```json
"AzureAISearch": {
  "Endpoint": "https://your-search.search.windows.net",
  "ApiKey": "...",
  "IndexName": "pda-knowledge"
}
```
- ✅ Cloud-managed
- ✅ Enterprise features
- ❌ Berbayar

### Chroma
```json
"Chroma": {
  "Endpoint": "http://localhost:8000",
  "CollectionName": "pda-knowledge"
}
```
- ✅ Open source
- ✅ Easy setup
- ✅ Good performance

---

## Mencari di Knowledge Base

### Via Chat (Otomatis)
AI akan otomatis menggunakan `searchKnowledgeBase` tool saat relevan.

### Via Chat (Eksplisit)
```
"Cari di knowledge base tentang laporan tahunan"
"Apa yang tertulis di dokumen sales Q4?"
"Ringkas informasi dari knowledge base tentang produk A"
```

### Response Format
```markdown
**Knowledge Base Search Results for:** "laporan Q4"

- 📄 **Q4_Sales_Report.pdf** (pdf) - Indexed: 2024-12-15 - Chunks: 45
  Keywords: Q4, sales, report, revenue, profit, customer, product

- 📄 **Q4_Summary.xlsx** (xlsx) - Indexed: 2024-12-10 - Chunks: 12
  Keywords: summary, quarterly, financial, metrics
```

---

## Best Practices

### 📁 Organisasi Folder
```
KnowledgeBase/
├── finance/
│   ├── Q1_report.pdf
│   └── Q2_report.pdf
├── products/
│   ├── catalog.xlsx
│   └── specs.docx
└── meetings/
    └── notes_2024.txt
```

### 📝 Naming Convention
```
✅ Annual_Report_2024_Q4.pdf
✅ Sales_Data_January_2024.xlsx
✅ Product_Specification_v2.docx

❌ doc1.pdf
❌ final_final_v3.xlsx
❌ Untitled.docx
```

### ⚡ Performance Tips
1. **Ukuran file**: Maks 50 MB (configurable)
2. **Jumlah file**: Monitor memory untuk In-Memory store
3. **Scan interval**: 30 menit untuk development, 60+ untuk production
4. **Chunk size**: Sesuaikan dengan tipe dokumen

### 🔒 Security
- RAG hanya membaca file, tidak menulis
- Content hash (SHA256) mencegah re-index file yang sama
- Status tracking (Indexed, Failed, Processing)

---

## Troubleshooting

### Dokumen tidak muncul di index
1. Cek folder `KnowledgeBase/` exists
2. Cek format file didukung
3. Cek ukuran file < MaxFileSizeMb
4. Tunggu scan interval (default 30 menit)
5. Cek log untuk error

### Hasil pencarian tidak relevan
1. Gunakan keywords yang lebih spesifik
2. Cek chunking configuration
3. Tambahkan dokumen yang lebih relevan
4. Pertimbangkan upgrade ke Qdrant/Chroma

### Memory usage tinggi
1. Kurangi jumlah dokumen
2. Gunakan FileSystem atau Qdrant provider
3. Kurangi ChunkOverlap
