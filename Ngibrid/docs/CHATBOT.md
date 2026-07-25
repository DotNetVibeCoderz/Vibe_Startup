# Mas Supri — AI Chat Bot

Asisten virtual Ngibrid berbasis **Semantic Kernel** dengan dukungan empat model AI dan
akses langsung ke data aplikasi lewat *kernel function*.

---

## 1. Dukungan Model

| Model | Konfigurasi | Function calling | Vision (gambar) |
|---|---|---|---|
| **OpenAI** | `ChatBot:Models:OpenAI:ApiKey` + `Model` | Otomatis via konektor SK | ✅ |
| **Anthropic** | `ChatBot:Models:Anthropic:ApiKey` + `Model` | Loop tool-call native | ✅ |
| **Gemini** | `ChatBot:Models:Gemini:ApiKey` + `Model` | Loop tool-call native | ✅ |
| **Ollama** | `ChatBot:Models:Ollama:Endpoint` + `Model` | Otomatis (endpoint `/v1`) | tergantung model |

Model dipilih per sesi dari dropdown di halaman `/chat`; model default diatur di
`ChatBot:DefaultModel`.

> **Catatan teknis.** Semantic Kernel belum memiliki konektor resmi untuk Anthropic dan Gemini.
> `AnthropicChatClient` dan `GeminiChatClient` menerjemahkan metadata `KernelFunction` menjadi
> skema tool masing-masing API dan menjalankan siklus *tool call → eksekusi → kirim hasil*
> hingga model memberi jawaban akhir. Hasilnya, keempat model memakai kumpulan fungsi yang sama.

---

## 2. Kernel Functions

### LogisticsPlugin — data operasional
| Fungsi | Kegunaan |
|---|---|
| `track_order` | Lacak status, riwayat, dan posisi GPS terakhir |
| `list_my_orders` | Daftar pesanan milik pengguna yang login |
| `check_shipping_cost` | Tarif semua layanan antar dua kota; menerima propinsi asal/tujuan agar kota dan kabupaten bernama sama tidak tertukar, dan memperingatkan bila kotanya tidak ada di master data |
| `find_city` | Cari kota/kabupaten di master data (514 daerah, 38 propinsi), opsional per propinsi |
| `get_warehouse_info` | Gudang dan kapasitasnya |
| `get_courier_availability` | Jumlah kurir per status |
| `get_services_info` | Katalog layanan |
| `get_order_statistics` | Order, revenue, SLA dalam periode |
| `get_demand_forecast` | Prediksi volume + deteksi peak season |

### DateTimePlugin
`get_current_time` (WIB/WITA/WIT) · `calculate_estimated_arrival` · `days_between`

### MathPlugin
`calculate` (parser aritmetika sendiri) · `convert_weight`

### InternetPlugin
`search_internet` (Tavily) · `scrape_url` · `read_file_from_url`

### PricingPlugin
`calculate_volume` (berat volumetrik + rekomendasi box) · `estimate_carbon_emission` ·
`check_partner_options` (mitra 3PL & lintas negara)

### SupportPlugin
`get_faq` · `create_support_ticket` · `get_my_loyalty_points` · `find_smart_locker` ·
`get_my_notifications`

Daftar fungsi aktif dapat dilihat di **Pengaturan → Chat Bot AI** atau lewat `GET /api/v1/chat/functions`.

---

## 3. Fitur Sesi

- **Multi-session** — buat, pilih, hapus (soft delete), dan reset sesi.
- **Judul otomatis** dari pesan pertama.
- **Ganti model per sesi** tanpa kehilangan riwayat.
- **Riwayat** dibatasi `ChatBot:MaxHistoryMessages` pesan terakhir (default 30).

---

## 4. Lampiran Berkas

Tombol 📎 pada composer menerima gambar dan dokumen (maks. 5 berkas per pesan).

| Jenis | Perlakuan |
|---|---|
| Gambar (`image/*`) | Diunggah lewat `StorageService`, lalu **byte-nya dikirim ke model sebagai image content** sehingga model benar-benar "melihat" gambar. |
| Dokumen (pdf, docx, csv, txt, md, json, xml) | Diunggah, tautannya disisipkan ke pesan; model membacanya dengan `read_file_from_url`. |

Berkas disimpan lewat provider `Storage:Provider` yang aktif (FileSystem/AzureBlob/S3/MinIO).
Validasi ekstensi dan ukuran (`Storage:MaxFileSizeMb`) berjalan sebelum byte dibaca.

---

## 5. Rendering Markdown

Balasan dirender dengan Markdig memakai `UseAdvancedExtensions` + `UseMediaLinks`:

| Elemen | Status |
|---|---|
| Tabel | ✅ (dengan scroll horizontal) |
| Blok kode & inline code | ✅ |
| Gambar | ✅ |
| Video/audio (YouTube, mp4, mp3) | ✅ menjadi player tertanam |
| Task list, footnote, auto-link, emoji | ✅ |
| **HTML mentah** | ❌ dinonaktifkan (`DisableHtml`) — output model tidak dipercaya |

---

## 6. Konfigurasi

Semua parameter berada di `appsettings.json` dan dapat diubah dari **Pengaturan → Chat Bot AI**
tanpa restart:

```json
"ChatBot": {
  "Name": "Mas Supri",
  "Persona": "Kamu adalah Mas Supri, asisten virtual ramah dari Ngibrid Logistics…",
  "DefaultModel": "OpenAI",
  "Temperature": 0.7,
  "MaxTokens": 2000,
  "TopP": 0.95,
  "MaxHistoryMessages": 30,
  "Models": {
    "OpenAI":    { "ApiKey": "sk-…",  "Model": "gpt-4o" },
    "Anthropic": { "ApiKey": "sk-ant-…", "Model": "claude-sonnet-4-5" },
    "Gemini":    { "ApiKey": "…",     "Model": "gemini-2.0-flash" },
    "Ollama":    { "Endpoint": "http://localhost:11434", "Model": "llama3.2" }
  },
  "Tavily": { "ApiKey": "tvly-…" }
}
```

Tanpa API key, bot membalas pesan yang mengarahkan pengguna ke halaman Pengaturan —
aplikasi tetap berjalan normal.

---

## 7. Keamanan

- **Prompt tidak boleh menyuntik HTML** — `DisableHtml()` pada pipeline Markdown.
- **Proteksi SSRF** — `scrape_url` dan `read_file_from_url` menolak `localhost`, alamat loopback,
  dan rentang IP privat (10.x, 172.16–31.x, 192.168.x, 169.254.x).
- **Isolasi data** — fungsi seperti `list_my_orders` dan `get_my_loyalty_points` hanya bekerja
  untuk pengguna yang sedang login; id pengguna diambil dari sesi, bukan dari argumen model.
- **Kegagalan fungsi tidak menghentikan percakapan** — error dikembalikan sebagai teks agar model
  bisa menjelaskannya kepada pengguna.
- **Batas langkah** — maksimal 5 putaran tool call per pesan agar tidak terjadi loop tak berujung.

---

## 8. Contoh Percakapan

```
Pengguna : Lacak resi NGB2607250001ABCD
Mas Supri: [memanggil track_order] → tabel status, rute, ETA, posisi GPS terakhir

Pengguna : Kalau kirim 5 kg dari Bandung ke Medan berapa?
Mas Supri: [memanggil check_shipping_cost] → tabel ECO/REG/EXP/SAMEDAY

Pengguna : [lampirkan invoice.pdf] Tolong ringkas dokumen ini
Mas Supri: [memanggil read_file_from_url] → ringkasan isi dokumen

Pengguna : Minggu depan ramai nggak?
Mas Supri: [memanggil get_demand_forecast] → prediksi harian + tanda peak season
```
