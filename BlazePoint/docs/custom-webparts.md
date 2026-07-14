# BlazePoint — Custom WebParts & Masterpages

## Built-in webparts (21)

| Key | Nama | Deskripsi |
|---|---|---|
| `Text` | Teks / Markdown | Konten markdown → HTML |
| `Image` | Gambar | Gambar tunggal + caption |
| `Clock` | Jam | Jam real-time per timezone |
| `Calculator` | Kalkulator | Kalkulator fungsional |
| `Weather` | Cuaca | Cuaca real-time (Open-Meteo, tanpa API key) |
| `Map` | Peta (Leaflet) | Peta OpenStreetMap + marker alamat |
| `Documents` | Dokumen Terbaru | Daftar dokumen terbaru |
| `Events` | Event Mendatang | Daftar event kalender |
| `Hero` | Hero Banner | Banner besar + judul + CTA (ala SharePoint) |
| `QuickLinks` | Quick Links | Grid shortcut tiles |
| `News` | Berita | Kartu halaman CMS terbaru |
| `People` | Orang / Direktori | Kartu profil pengguna |
| `Video` | Video | Embed YouTube atau MP4 |
| `Gallery` | Galeri Gambar | Grid gambar + zoom |
| `Countdown` | Countdown Timer | Hitung mundur ke tanggal target |
| `Html` | Embed HTML | Snippet HTML mentah |
| `Button` | Tombol / CTA | Tombol aksi dengan warna kustom |
| `Divider` | Garis Pemisah | Pemisah visual |
| `Spacer` | Spasi Kosong | Ruang vertikal |
| `ListView` | Tampilan List | Render item custom list di halaman |
| `Rss` | RSS Feed | Pembaca feed RSS/Atom |
| `Activity` | Aktivitas Site | Audit log terbaru |

## Membuat custom webpart (contoh lengkap)

Webpart = komponen Razor biasa dengan satu parameter `Settings`. Contoh: webpart penghitung karakter.

**1. Buat komponen** — `Components/WebParts/CharCounterWebPart.razor`:

```razor
@* Custom webpart: hitung karakter secara live. *@

<textarea class="bp-textarea" rows="3" @bind="_text" @bind:event="oninput"
          placeholder="@Settings.GetValueOrDefault("placeholder", "Ketik sesuatu…")"></textarea>
<div class="bp-muted">@_text.Length karakter</div>

@code {
    [Parameter] public Dictionary<string, string> Settings { get; set; } = [];
    private string _text = "";
}
```

**2. Daftarkan** di `Components/WebParts/WebPartRegistry.cs` — tambahkan ke list `All`:

```csharp
new("CharCounter", "Penghitung Karakter", "🔡", typeof(CharCounterWebPart),
    [new("placeholder", "Placeholder")],
    new() { ["placeholder"] = "Ketik sesuatu…" }),
```

**3. Selesai.** Webpart muncul otomatis di palette editor halaman (`/pages/edit/{id}`), lengkap dengan panel properti yang dihasilkan dari daftar `Settings` (kind: `text`, `textarea`, `number`).

Tips:
- Injeksi service apa pun berfungsi normal (`@inject DocumentService Docs` dll).
- Simpan semua konfigurasi di dictionary `Settings` agar ikut terserialisasi ke JSON halaman dan ter-versioning saat publish.
- Untuk JS interop (seperti `MapWebPart`), gunakan id elemen unik dan bersihkan resource di `DisposeAsync`.

## Custom masterpage (layout halaman)

Masterpage menentukan bingkai visual halaman CMS. Bawaan: `Default`, `News` (header merah koran), `Intranet` (header biru korporat), `Web` (hero gelap lebar) — contoh render ada di `/p/news`, `/p/intranet-home`, `/p/it-kb`.

Menambah masterpage baru, misal `Event`:

**1. Tambahkan case** di `Components/Pages/PageView.razor`:

```razor
case "Event":
    <div class="mp-event-header">
        <h1>🎪 @_page.Title</h1>
    </div>
    break;
```

**2. Tambahkan CSS** di `wwwroot/app.css`:

```css
.mp-event-header {
    background: linear-gradient(120deg, #7b2ff7, #f107a3);
    color: #fff; padding: 40px 24px; border-radius: 10px; margin-bottom: 20px;
}
```

**3. Tambahkan opsi** pada dropdown layout di `PagesIndex.razor` dan `PageEditor.razor`:

```razor
<option value="Event">Event — header ungu festival</option>
```

Semua halaman yang memilih layout `Event` langsung memakai bingkai baru tersebut.
