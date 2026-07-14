namespace BlazePoint.Components.WebParts;

public record WebPartSetting(string Key, string Label, string Kind = "text"); // text | textarea | number

public record WebPartDescriptor(
    string Key, string Name, string Icon, Type ComponentType,
    List<WebPartSetting> Settings, Dictionary<string, string> Defaults);

/// <summary>
/// Registry of available webparts. To add a CUSTOM webpart:
/// 1. create a Razor component with a [Parameter] Dictionary&lt;string,string&gt; Settings,
/// 2. register it here — it immediately appears in the page editor palette.
/// See docs/custom-webparts.md for a walkthrough.
/// </summary>
public static class WebPartRegistry
{
    public static readonly List<WebPartDescriptor> All =
    [
        new("Text", "Teks / Markdown", "📝", typeof(TextWebPart),
            [new("content", "Konten (markdown)", "textarea")],
            new() { ["content"] = "## Judul\n\nTulis konten di sini…" }),

        new("Image", "Gambar", "🖼️", typeof(ImageWebPart),
            [new("url", "URL Gambar"), new("caption", "Keterangan")],
            new() { ["url"] = "https://picsum.photos/600/300", ["caption"] = "" }),

        new("Clock", "Jam", "🕐", typeof(ClockWebPart),
            [new("timezone", "Timezone (Windows ID)")],
            new() { ["timezone"] = "SE Asia Standard Time" }),

        new("Calculator", "Kalkulator", "🧮", typeof(CalculatorWebPart), [], []),

        new("Weather", "Cuaca", "🌤️", typeof(WeatherWebPart),
            [new("city", "Nama Kota"), new("latitude", "Latitude", "number"), new("longitude", "Longitude", "number")],
            new() { ["city"] = "Jakarta", ["latitude"] = "-6.2", ["longitude"] = "106.8" }),

        new("Map", "Peta (Leaflet)", "🗺️", typeof(MapWebPart),
            [new("label", "Label Alamat"), new("latitude", "Latitude", "number"),
             new("longitude", "Longitude", "number"), new("zoom", "Zoom", "number")],
            new() { ["label"] = "Jakarta, Indonesia", ["latitude"] = "-6.2088", ["longitude"] = "106.8456", ["zoom"] = "13" }),

        new("Documents", "Dokumen Terbaru", "📁", typeof(DocumentsWebPart),
            [new("count", "Jumlah", "number")],
            new() { ["count"] = "5" }),

        new("Events", "Event Mendatang", "📅", typeof(EventsWebPart),
            [new("count", "Jumlah", "number")],
            new() { ["count"] = "5" }),

        // ---- SharePoint Online-style webparts ----
        new("Hero", "Hero Banner", "🖥️", typeof(HeroWebPart),
            [new("imageUrl", "URL Gambar Latar"), new("title", "Judul"), new("subtitle", "Sub-judul"),
             new("buttonText", "Teks Tombol"), new("buttonUrl", "URL Tombol")],
            new() { ["imageUrl"] = "https://picsum.photos/1200/400", ["title"] = "Selamat Datang",
                    ["subtitle"] = "Portal kolaborasi tim Anda", ["buttonText"] = "Pelajari", ["buttonUrl"] = "/p/welcome" }),

        new("QuickLinks", "Quick Links", "🔗", typeof(QuickLinksWebPart),
            [new("links", "Link (Judul|URL|Emoji per baris)", "textarea")],
            new() { ["links"] = "Dokumen|/documents|📁\nKalender|/calendar|📅\nDiskusi|/discussions|💬\nClippy|/chat|🤖" }),

        new("News", "Berita (halaman terbaru)", "📰", typeof(NewsWebPart),
            [new("count", "Jumlah", "number")],
            new() { ["count"] = "4" }),

        new("People", "Orang / Direktori", "🧑‍🤝‍🧑", typeof(PeopleWebPart),
            [new("count", "Jumlah", "number")],
            new() { ["count"] = "6" }),

        new("Video", "Video (YouTube/MP4)", "🎬", typeof(VideoWebPart),
            [new("url", "URL Video"), new("caption", "Keterangan")],
            new() { ["url"] = "https://www.youtube.com/watch?v=4XU2Bient0U", ["caption"] = "" }),

        new("Gallery", "Galeri Gambar", "🖼️", typeof(ImageGalleryWebPart),
            [new("urls", "URL gambar (satu per baris)", "textarea")],
            new() { ["urls"] = "https://picsum.photos/id/10/400\nhttps://picsum.photos/id/20/400\nhttps://picsum.photos/id/30/400\nhttps://picsum.photos/id/40/400" }),

        new("Countdown", "Countdown Timer", "⏳", typeof(CountdownWebPart),
            [new("label", "Label"), new("targetDate", "Tanggal Target (yyyy-MM-dd HH:mm)")],
            new() { ["label"] = "Menuju acara besar", ["targetDate"] = DateTime.Today.AddDays(30).ToString("yyyy-MM-dd HH:mm") }),

        new("Html", "Embed HTML", "🧬", typeof(HtmlWebPart),
            [new("html", "Kode HTML", "textarea")],
            new() { ["html"] = "<blockquote style='border-left:4px solid #1877f2;padding-left:12px'>Konten HTML kustom.</blockquote>" }),

        new("Button", "Tombol / CTA", "🔘", typeof(ButtonWebPart),
            [new("text", "Teks"), new("url", "URL"), new("color", "Warna (hex)"), new("align", "Perataan (left/center/right)")],
            new() { ["text"] = "Klik di sini", ["url"] = "/", ["color"] = "#1877f2", ["align"] = "center" }),

        new("Divider", "Garis Pemisah", "➖", typeof(DividerWebPart), [], []),

        new("Spacer", "Spasi Kosong", "⬜", typeof(SpacerWebPart),
            [new("height", "Tinggi (px)", "number")],
            new() { ["height"] = "40" }),

        new("ListView", "Tampilan List", "📋", typeof(ListViewWebPart),
            [new("listId", "ID List", "number"), new("count", "Jumlah item", "number")],
            new() { ["listId"] = "1", ["count"] = "5" }),

        new("Rss", "RSS Feed", "📡", typeof(RssWebPart),
            [new("feedUrl", "URL Feed"), new("count", "Jumlah", "number")],
            new() { ["feedUrl"] = "https://devblogs.microsoft.com/dotnet/feed/", ["count"] = "5" }),

        new("Activity", "Aktivitas Site", "🕘", typeof(ActivityWebPart),
            [new("count", "Jumlah", "number")],
            new() { ["count"] = "6" })
    ];

    public static WebPartDescriptor? Find(string key) => All.FirstOrDefault(d => d.Key == key);
}
