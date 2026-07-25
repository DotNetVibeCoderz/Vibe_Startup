using System.ComponentModel;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.SemanticKernel;
using Ngibrid.Data;
using Ngibrid.Models;

namespace Ngibrid.Services;

// ═══════════════════════════════════════════════
//  SEMANTIC KERNEL PLUGINS (Kernel Functions)
//
//  Every plugin here is registered on the kernel for all four providers. Plugins take a
//  scope factory rather than a DbContext because a chat turn can outlive the page's scope
//  and function calls may run concurrently — sharing one DbContext across them is not safe.
// ═══════════════════════════════════════════════

/// <summary>
/// Logistics data — orders, tracking, warehouses, couriers, service catalogue.
/// </summary>
public class LogisticsPlugin
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IConfiguration _config;

    /// <summary>Set when the chat session belongs to a signed-in user, so "pesanan saya" resolves.</summary>
    public long? CurrentUserId { get; init; }

    public LogisticsPlugin(IServiceScopeFactory scopeFactory, IConfiguration config, long? currentUserId = null)
    { _scopeFactory = scopeFactory; _config = config; CurrentUserId = currentUserId; }

    private NgibridDbContext Db(IServiceScope scope) => scope.ServiceProvider.GetRequiredService<NgibridDbContext>();

    [KernelFunction("track_order")]
    [Description("Lacak status dan posisi terakhir pesanan berdasarkan nomor tracking/resi atau nomor order")]
    public async Task<string> TrackOrder([Description("Nomor tracking/resi atau nomor order")] string trackingNumber)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = Db(scope);

        var key = trackingNumber.Trim();
        var order = await db.Orders
            .Include(o => o.StatusHistory)
            .FirstOrDefaultAsync(o => o.TrackingNumber == key || o.OrderNumber == key);

        if (order == null) return $"❌ Pesanan dengan nomor {key} tidak ditemukan.";

        var history = order.StatusHistory?.OrderByDescending(h => h.CreatedAt).Take(5).ToList()
            ?? new List<OrderStatusHistory>();
        var lastPoint = await db.ShipmentTrackings
            .Where(t => t.OrderId == order.Id)
            .OrderByDescending(t => t.Timestamp)
            .FirstOrDefaultAsync();

        var sb = new StringBuilder();
        sb.AppendLine($"📦 **Pesanan {order.OrderNumber}**");
        sb.AppendLine();
        sb.AppendLine("| Field | Nilai |");
        sb.AppendLine("|---|---|");
        sb.AppendLine($"| Resi | {order.TrackingNumber} |");
        sb.AppendLine($"| Status | **{order.Status}** |");
        sb.AppendLine($"| Layanan | {order.ServiceType} |");
        sb.AppendLine($"| Pengirim | {order.SenderName} ({order.SenderCity}) |");
        sb.AppendLine($"| Penerima | {order.RecipientName} ({order.RecipientCity}) |");
        sb.AppendLine($"| Berat | {order.WeightKg} kg |");
        sb.AppendLine($"| Total | Rp {order.TotalAmount:N0} |");
        sb.AppendLine($"| Estimasi tiba | {order.EstimatedDeliveryDate:dd MMM yyyy} |");
        if (lastPoint != null)
            sb.AppendLine($"| Posisi terakhir | {lastPoint.Latitude:F4}, {lastPoint.Longitude:F4} ({lastPoint.Timestamp:dd MMM HH:mm}) |");

        if (history.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("**Riwayat terbaru:**");
            foreach (var h in history)
                sb.AppendLine($"- `{h.CreatedAt:dd MMM HH:mm}` **{h.Status}** — {h.Notes}");
        }

        return sb.ToString();
    }

    [KernelFunction("list_my_orders")]
    [Description("Tampilkan daftar pesanan milik pengguna yang sedang login")]
    public async Task<string> ListMyOrders([Description("Jumlah maksimum pesanan, default 5")] int limit = 5)
    {
        if (CurrentUserId is null)
            return "Saya belum bisa mengenali akun Anda. Silakan login dulu untuk melihat daftar pesanan.";

        using var scope = _scopeFactory.CreateScope();
        var db = Db(scope);

        var orders = await db.Orders
            .Where(o => o.CustomerId == CurrentUserId && !o.IsDeleted)
            .OrderByDescending(o => o.CreatedAt)
            .Take(Math.Clamp(limit, 1, 20))
            .ToListAsync();

        if (orders.Count == 0) return "Anda belum punya pesanan.";

        var sb = new StringBuilder("📋 **Pesanan Anda**\n\n| Order | Resi | Tujuan | Status | Total |\n|---|---|---|---|---|\n");
        foreach (var o in orders)
            sb.AppendLine($"| {o.OrderNumber} | `{o.TrackingNumber}` | {o.RecipientCity} | {o.Status} | Rp {o.TotalAmount:N0} |");
        return sb.ToString();
    }

    [KernelFunction("check_shipping_cost")]
    [Description("Hitung estimasi ongkos kirim antar kota untuk semua tipe layanan")]
    public async Task<string> CheckShippingCost(
        [Description("Kota asal, mis. 'Kota Bandung' atau 'Bandung'")] string origin,
        [Description("Kota tujuan")] string destination,
        [Description("Berat dalam kg")] double weight,
        [Description("Propinsi asal, opsional tapi membuat perhitungan lebih presisi")] string originProvince = "",
        [Description("Propinsi tujuan, opsional")] string destinationProvince = "")
    {
        using var scope = _scopeFactory.CreateScope();
        var pricing = scope.ServiceProvider.GetRequiredService<DynamicPricingService>();

        var sb = new StringBuilder($"💰 **Ongkos kirim {origin} → {destination}** ({weight} kg)\n\n");
        sb.AppendLine("| Layanan | Estimasi | Tarif |");
        sb.AppendLine("|---|---|---|");

        PriceResult? last = null;
        foreach (var (service, label) in new[]
                 {
                     ("ECO", "Eco 5-7 hari"), ("REG", "Regular 2-4 hari"),
                     ("EXP", "Express 1-2 hari"), ("SAMEDAY", "Sameday")
                 })
        {
            last = await pricing.CalculatePriceAsync(origin, destination, weight, service,
                originProvince, destinationProvince);
            sb.AppendLine($"| {service} | {label} | Rp {last.TotalPrice:N0} |");
        }

        if (last is not null)
            sb.AppendLine($"\nJarak tempuh diperkirakan **{last.EstimatedDistanceKm:N0} km**.");

        // Say so rather than quietly quoting a made-up distance for a city we don't have.
        foreach (var (label, name, province) in new[]
                 { ("asal", origin, originProvince), ("tujuan", destination, destinationProvince) })
        {
            if (!CityCoordinates.IsKnown(province, name))
                sb.AppendLine($"\n⚠️ Kota {label} \"{name}\" tidak ada di master data, tarif di atas hanya perkiraan kasar.");
        }

        return sb.ToString();
    }

    [KernelFunction("find_city")]
    [Description("Cari kota/kabupaten di master data Indonesia, opsional difilter per propinsi. " +
                 "Berguna untuk memastikan penulisan nama kota sebelum menghitung ongkir.")]
    public async Task<string> FindCity(
        [Description("Sebagian nama kota, kosongkan untuk melihat semua kota di propinsi")] string query = "",
        [Description("Nama propinsi, kosongkan untuk mencari se-Indonesia")] string province = "")
    {
        using var scope = _scopeFactory.CreateScope();
        var db = Db(scope);

        var q = db.Cities.Where(c => c.IsActive);
        if (!string.IsNullOrWhiteSpace(province)) q = q.Where(c => c.Province.Contains(province));
        if (!string.IsNullOrWhiteSpace(query)) q = q.Where(c => c.Name.Contains(query));

        var cities = await q.OrderBy(c => c.Province).ThenBy(c => c.Name).Take(40).ToListAsync();
        if (cities.Count == 0) return $"Tidak ada kota yang cocok dengan \"{query}\" {province}".Trim() + ".";

        var sb = new StringBuilder("🏙️ **Master data kota**\n\n| Kota/Kabupaten | Propinsi | Ibu kota | Koordinat |\n|---|---|---|---|\n");
        foreach (var c in cities)
            sb.AppendLine($"| {c.FullName} | {c.Province} | {c.SeatName ?? c.Name} | {c.Latitude:F4}, {c.Longitude:F4} |");
        return sb.ToString();
    }

    [KernelFunction("get_warehouse_info")]
    [Description("Informasi gudang Ngibrid beserta kapasitasnya, opsional difilter per kota")]
    public async Task<string> GetWarehouseInfo([Description("Nama kota, kosongkan untuk semua")] string city = "")
    {
        using var scope = _scopeFactory.CreateScope();
        var db = Db(scope);

        var query = db.Warehouses.Where(w => !w.IsDeleted);
        if (!string.IsNullOrWhiteSpace(city)) query = query.Where(w => w.City.Contains(city));

        var warehouses = await query.OrderBy(w => w.Code).Take(10).ToListAsync();
        if (warehouses.Count == 0)
            return string.IsNullOrWhiteSpace(city) ? "Belum ada data gudang." : $"Tidak ada gudang di {city}.";

        var sb = new StringBuilder("🏭 **Gudang Ngibrid**\n\n| Nama | Kota | Kapasitas | Terpakai | Status |\n|---|---|---|---|---|\n");
        foreach (var w in warehouses)
        {
            var pct = w.TotalCapacityM3 > 0 ? w.UsedCapacityM3 / w.TotalCapacityM3 * 100 : 0;
            sb.AppendLine($"| {w.Name} | {w.City} | {w.TotalCapacityM3:N0} m³ | {pct:F0}% | {w.Status} |");
        }
        return sb.ToString();
    }

    [KernelFunction("get_courier_availability")]
    [Description("Jumlah dan status kurir yang tersedia saat ini")]
    public async Task<string> GetCourierAvailability()
    {
        using var scope = _scopeFactory.CreateScope();
        var db = Db(scope);

        var byStatus = await db.CourierProfiles
            .Where(c => !c.IsDeleted)
            .GroupBy(c => c.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToListAsync();

        if (byStatus.Count == 0) return "Belum ada data kurir.";

        var total = byStatus.Sum(s => s.Count);
        var sb = new StringBuilder($"🛵 **Kurir** (total {total})\n\n");
        foreach (var s in byStatus.OrderByDescending(s => s.Count))
            sb.AppendLine($"- {s.Status}: **{s.Count}**");
        return sb.ToString();
    }

    [KernelFunction("get_services_info")]
    [Description("Daftar layanan dan fitur yang ditawarkan Ngibrid")]
    public string GetServicesInfo() => """
        🚚 **Layanan Ngibrid Logistics**

        | Kode | Layanan | Estimasi |
        |---|---|---|
        | REG | Regular | 2-4 hari kerja |
        | EXP | Express | 1-2 hari kerja |
        | SAMEDAY | Same day delivery | Hari yang sama (area tertentu) |
        | ECO | Green delivery | 5-7 hari, carbon offset |

        **Fitur tambahan:** pickup gratis dari lokasi, asuransi barang, COD,
        smart locker, tracking GPS real-time, dan pengiriman lintas negara.
        """;

    [KernelFunction("get_order_statistics")]
    [Description("Statistik operasional: jumlah order, pendapatan, dan kepatuhan SLA dalam periode tertentu")]
    public async Task<string> GetOrderStatistics([Description("Jumlah hari ke belakang, default 30")] int days = 30)
    {
        using var scope = _scopeFactory.CreateScope();
        var analytics = scope.ServiceProvider.GetRequiredService<AnalyticsService>();

        var revenue = await analytics.GetRevenueSummaryAsync(days);
        var sla = await analytics.GetSlaComplianceAsync(days);
        var snapshot = await analytics.GetOperationalSnapshotAsync();

        return $"""
            📊 **Statistik {days} hari terakhir**

            | Metrik | Nilai |
            |---|---|
            | Total order | {revenue.TotalOrders:N0} |
            | Pendapatan | Rp {revenue.TotalRevenue:N0} |
            | Rata-rata nilai order | Rp {revenue.AvgOrderValue:N0} |
            | Terkirim | {revenue.DeliveredCount:N0} |
            | SLA compliance | {sla}% |
            | Sedang transit | {snapshot.InTransit} |
            | Pickup menunggu | {snapshot.PendingPickups} |
            | Tiket terbuka | {snapshot.OpenTickets} |
            """;
    }

    [KernelFunction("get_demand_forecast")]
    [Description("Prediksi volume pengiriman beberapa hari ke depan dan deteksi peak season")]
    public async Task<string> GetDemandForecast([Description("Jumlah hari yang diprediksi, default 7")] int days = 7)
    {
        using var scope = _scopeFactory.CreateScope();
        var forecast = scope.ServiceProvider.GetRequiredService<ForecastService>();

        var results = await forecast.ForecastDemandAsync(Math.Clamp(days, 1, 30));
        if (results.Count == 0) return "Data historis belum cukup untuk membuat prediksi.";

        var sb = new StringBuilder("📈 **Prediksi volume pengiriman**\n\n| Tanggal | Prediksi | Rentang | Peak? |\n|---|---|---|---|\n");
        foreach (var f in results)
            sb.AppendLine($"| {f.ForecastDate:ddd, dd MMM} | {f.PredictedOrders:F0} order | {f.LowerBound:F0}–{f.UpperBound:F0} | {(f.IsPeakSeason ? "🔥 ya" : "-")} |");
        return sb.ToString();
    }
}

/// <summary>
/// Date and time helpers, including Indonesian timezones and delivery ETA maths.
/// </summary>
public class DateTimePlugin
{
    [KernelFunction("get_current_time")]
    [Description("Waktu dan tanggal saat ini")]
    public string GetCurrentTime([Description("Timezone: UTC, WIB, WITA, WIT")] string timezone = "WIB")
    {
        var now = DateTime.UtcNow;
        var offset = timezone.ToUpperInvariant() switch { "WIB" => 7, "WITA" => 8, "WIT" => 9, _ => 0 };
        var local = now.AddHours(offset);
        return $"🕐 {local:dddd, dd MMMM yyyy HH:mm:ss} {timezone.ToUpperInvariant()} (UTC {now:HH:mm})";
    }

    [KernelFunction("calculate_estimated_arrival")]
    [Description("Estimasi tanggal tiba berdasarkan tipe layanan, melewati akhir pekan")]
    public string CalculateEta([Description("REG, EXP, SAMEDAY, atau ECO")] string serviceType)
    {
        var days = serviceType.ToUpperInvariant() switch { "SAMEDAY" => 0, "EXP" => 1, "ECO" => 5, _ => 3 };
        var eta = DateTime.UtcNow.AddHours(7).AddDays(days);
        while (eta.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday) eta = eta.AddDays(1);
        return $"📅 Estimasi tiba layanan {serviceType.ToUpperInvariant()}: **{eta:dddd, dd MMM yyyy}**";
    }

    [KernelFunction("days_between")]
    [Description("Hitung selisih hari antara dua tanggal (format yyyy-MM-dd)")]
    public string DaysBetween(
        [Description("Tanggal awal, format yyyy-MM-dd")] string startDate,
        [Description("Tanggal akhir, format yyyy-MM-dd")] string endDate)
    {
        if (!DateTime.TryParse(startDate, out var start) || !DateTime.TryParse(endDate, out var end))
            return "❌ Format tanggal tidak valid. Gunakan yyyy-MM-dd.";
        var days = (end.Date - start.Date).Days;
        return $"📆 {start:dd MMM yyyy} → {end:dd MMM yyyy} = **{days} hari**";
    }
}

/// <summary>
/// Arithmetic and unit conversion. The expression evaluator is a hand-written
/// recursive-descent parser: DataTable.Compute would accept arbitrary expressions
/// and is not something to point a language model at.
/// </summary>
public class MathPlugin
{
    [KernelFunction("calculate")]
    [Description("Hitung ekspresi matematika, contoh: (5*10)/2 + 3^2")]
    public string Calculate([Description("Ekspresi matematika")] string expression)
    {
        try
        {
            var result = ExpressionEvaluator.Evaluate(expression);
            return $"🧮 `{expression}` = **{result:0.##########}**";
        }
        catch (Exception ex)
        {
            return $"❌ Gagal menghitung: {ex.Message}";
        }
    }

    [KernelFunction("convert_weight")]
    [Description("Konversi satuan berat antara kg, g, lbs, dan oz")]
    public string ConvertWeight(
        [Description("Nilai yang dikonversi")] double value,
        [Description("Satuan asal: kg, g, lbs, oz")] string from,
        [Description("Satuan tujuan: kg, g, lbs, oz")] string to)
    {
        var toKg = from.ToLowerInvariant() switch
        {
            "kg" => value, "g" => value / 1000.0, "lbs" => value * 0.453592, "oz" => value * 0.0283495,
            _ => double.NaN
        };
        if (double.IsNaN(toKg)) return $"❌ Satuan asal '{from}' tidak dikenal.";

        var result = to.ToLowerInvariant() switch
        {
            "kg" => toKg, "g" => toKg * 1000, "lbs" => toKg / 0.453592, "oz" => toKg / 0.0283495,
            _ => double.NaN
        };
        if (double.IsNaN(result)) return $"❌ Satuan tujuan '{to}' tidak dikenal.";

        return $"⚖️ {value} {from} = **{Math.Round(result, 3)} {to}**";
    }

    /// <summary>
    /// Minimal safe arithmetic parser: + - * / % ^, parentheses, unary minus.
    /// </summary>
    private static class ExpressionEvaluator
    {
        public static double Evaluate(string expression)
        {
            var pos = 0;
            var text = expression.Replace(" ", "");
            if (text.Length == 0) throw new FormatException("Ekspresi kosong");
            var value = ParseExpression(text, ref pos);
            if (pos < text.Length) throw new FormatException($"Karakter tidak terduga '{text[pos]}'");
            return value;
        }

        private static double ParseExpression(string s, ref int pos)
        {
            var left = ParseTerm(s, ref pos);
            while (pos < s.Length && (s[pos] == '+' || s[pos] == '-'))
            {
                var op = s[pos++];
                var right = ParseTerm(s, ref pos);
                left = op == '+' ? left + right : left - right;
            }
            return left;
        }

        private static double ParseTerm(string s, ref int pos)
        {
            var left = ParseFactor(s, ref pos);
            while (pos < s.Length && (s[pos] == '*' || s[pos] == '/' || s[pos] == '%'))
            {
                var op = s[pos++];
                var right = ParseFactor(s, ref pos);
                left = op switch
                {
                    '*' => left * right,
                    '/' => right == 0 ? throw new DivideByZeroException("Pembagian dengan nol") : left / right,
                    _ => right == 0 ? throw new DivideByZeroException("Modulo dengan nol") : left % right
                };
            }
            return left;
        }

        private static double ParseFactor(string s, ref int pos)
        {
            var value = ParseUnary(s, ref pos);
            if (pos < s.Length && s[pos] == '^')
            {
                pos++;
                var exponent = ParseFactor(s, ref pos); // right-associative
                return Math.Pow(value, exponent);
            }
            return value;
        }

        private static double ParseUnary(string s, ref int pos)
        {
            if (pos < s.Length && s[pos] == '-') { pos++; return -ParseUnary(s, ref pos); }
            if (pos < s.Length && s[pos] == '+') { pos++; return ParseUnary(s, ref pos); }
            return ParsePrimary(s, ref pos);
        }

        private static double ParsePrimary(string s, ref int pos)
        {
            if (pos >= s.Length) throw new FormatException("Ekspresi tidak lengkap");

            if (s[pos] == '(')
            {
                pos++;
                var value = ParseExpression(s, ref pos);
                if (pos >= s.Length || s[pos] != ')') throw new FormatException("Kurung tidak seimbang");
                pos++;
                return value;
            }

            var start = pos;
            while (pos < s.Length && (char.IsDigit(s[pos]) || s[pos] == '.')) pos++;
            if (start == pos) throw new FormatException($"Angka tidak valid pada posisi {pos}");

            return double.Parse(s[start..pos], System.Globalization.CultureInfo.InvariantCulture);
        }
    }
}

/// <summary>
/// Internet access: Tavily search, page scraping, and reading a file from a URL.
/// </summary>
public class InternetPlugin
{
    private readonly IHttpClientFactory _http;
    private readonly IConfiguration _config;

    public InternetPlugin(IHttpClientFactory http, IConfiguration config)
    { _http = http; _config = config; }

    [KernelFunction("search_internet")]
    [Description("Cari informasi terkini di internet menggunakan Tavily Search")]
    public async Task<string> SearchInternet([Description("Kata kunci pencarian")] string query)
    {
        var apiKey = _config["ChatBot:Tavily:ApiKey"];
        if (string.IsNullOrWhiteSpace(apiKey))
            return "🔧 API Key Tavily belum dikonfigurasi. Atur di Settings → Chat Bot AI.";

        try
        {
            var client = _http.CreateClient("Tavily");
            var response = await client.PostAsJsonAsync("search", new
            {
                api_key = apiKey,
                query,
                search_depth = "basic",
                max_results = 5,
                include_answer = true
            });

            if (!response.IsSuccessStatusCode)
                return $"❌ Pencarian gagal (HTTP {(int)response.StatusCode}).";

            var result = await response.Content.ReadFromJsonAsync<JsonElement>();
            var sb = new StringBuilder();

            if (result.TryGetProperty("answer", out var answer) && answer.ValueKind == JsonValueKind.String)
                sb.AppendLine($"**Ringkasan:** {answer.GetString()}\n");

            if (result.TryGetProperty("results", out var items) && items.ValueKind == JsonValueKind.Array)
            {
                sb.AppendLine("**Sumber:**");
                foreach (var r in items.EnumerateArray().Take(5))
                {
                    var title = r.TryGetProperty("title", out var t) ? t.GetString() : "(tanpa judul)";
                    var url = r.TryGetProperty("url", out var u) ? u.GetString() : "";
                    var content = r.TryGetProperty("content", out var c) ? c.GetString() : "";
                    sb.AppendLine($"- [{title}]({url})");
                    if (!string.IsNullOrEmpty(content))
                        sb.AppendLine($"  > {Truncate(content, 200)}");
                }
            }

            return sb.Length > 0 ? sb.ToString() : "Tidak ada hasil yang relevan.";
        }
        catch (Exception ex)
        {
            return $"❌ Gagal mencari di internet: {ex.Message}";
        }
    }

    [KernelFunction("scrape_url")]
    [Description("Ambil dan bersihkan isi teks dari sebuah halaman web")]
    public async Task<string> ScrapeUrl([Description("URL halaman web lengkap")] string url)
    {
        if (!IsPublicHttpUrl(url, out var error)) return error!;

        try
        {
            var client = _http.CreateClient("Default");
            var html = await client.GetStringAsync(url);

            // Drop script/style bodies before stripping tags, or their contents leak into the text.
            var text = System.Text.RegularExpressions.Regex.Replace(html,
                "<(script|style)[^>]*>.*?</\\1>", " ",
                System.Text.RegularExpressions.RegexOptions.Singleline |
                System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            text = System.Text.RegularExpressions.Regex.Replace(text, "<[^>]+>", " ");
            text = System.Net.WebUtility.HtmlDecode(text);
            text = System.Text.RegularExpressions.Regex.Replace(text, @"\s+", " ").Trim();

            return text.Length == 0 ? "Halaman tidak berisi teks yang bisa dibaca." : Truncate(text, 4000);
        }
        catch (Exception ex)
        {
            return $"❌ Gagal membaca halaman: {ex.Message}";
        }
    }

    [KernelFunction("read_file_from_url")]
    [Description("Baca isi berkas dari URL (teks, markdown, CSV, JSON, atau XML) termasuk lampiran yang diunggah pengguna")]
    public async Task<string> ReadFileFromUrl([Description("URL berkas lengkap")] string url)
    {
        if (!IsPublicHttpUrl(url, out var error)) return error!;

        try
        {
            var client = _http.CreateClient("Default");
            using var response = await client.GetAsync(url);
            if (!response.IsSuccessStatusCode)
                return $"❌ Gagal mengunduh berkas (HTTP {(int)response.StatusCode}).";

            var contentType = response.Content.Headers.ContentType?.MediaType ?? "";
            var extension = Path.GetExtension(new Uri(url).AbsolutePath).ToLowerInvariant();

            if (contentType.StartsWith("image/"))
                return $"Berkas ini adalah gambar ({contentType}). Lampirkan langsung ke chat agar saya bisa melihatnya: {url}";

            var isTextual = contentType.StartsWith("text/")
                || contentType.Contains("json") || contentType.Contains("xml") || contentType.Contains("csv")
                || extension is ".txt" or ".md" or ".csv" or ".json" or ".xml" or ".log";

            if (!isTextual)
                return $"Berkas bertipe `{(string.IsNullOrEmpty(contentType) ? extension : contentType)}` " +
                       $"tidak bisa dibaca sebagai teks. Saya hanya bisa membaca txt, md, csv, json, dan xml.";

            var content = await response.Content.ReadAsStringAsync();
            if (string.IsNullOrWhiteSpace(content)) return "Berkas kosong.";

            return $"📄 **Isi berkas** ({contentType}, {content.Length:N0} karakter):\n\n```\n{Truncate(content, 6000)}\n```";
        }
        catch (Exception ex)
        {
            return $"❌ Gagal membaca berkas: {ex.Message}";
        }
    }

    /// <summary>
    /// Only absolute http(s) URLs are fetched. Loopback and private ranges are refused so a
    /// prompt-injected URL can't turn the server into a proxy for its own internal network.
    /// </summary>
    private static bool IsPublicHttpUrl(string url, out string? error)
    {
        error = null;

        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            error = "❌ URL harus lengkap dan diawali http:// atau https://.";
            return false;
        }

        if (uri.IsLoopback)
        {
            error = "❌ Alamat lokal tidak dapat diakses dari sini.";
            return false;
        }

        if (System.Net.IPAddress.TryParse(uri.Host, out var ip) && IsPrivate(ip))
        {
            error = "❌ Alamat jaringan internal tidak dapat diakses dari sini.";
            return false;
        }

        return true;
    }

    private static bool IsPrivate(System.Net.IPAddress ip)
    {
        if (System.Net.IPAddress.IsLoopback(ip)) return true;
        var bytes = ip.GetAddressBytes();
        if (bytes.Length != 4) return ip.IsIPv6LinkLocal || ip.IsIPv6SiteLocal;
        return bytes[0] switch
        {
            10 => true,
            127 => true,
            169 when bytes[1] == 254 => true,
            172 when bytes[1] >= 16 && bytes[1] <= 31 => true,
            192 when bytes[1] == 168 => true,
            _ => false
        };
    }

    private static string Truncate(string text, int max) =>
        text.Length <= max ? text : text[..max] + $"\n… (dipotong, total {text.Length:N0} karakter)";
}

/// <summary>
/// Pricing, packaging, and green-logistics calculations.
/// </summary>
public class PricingPlugin
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IConfiguration _config;

    public PricingPlugin(IServiceScopeFactory scopeFactory, IConfiguration config)
    { _scopeFactory = scopeFactory; _config = config; }

    [KernelFunction("calculate_volume")]
    [Description("Hitung berat volumetrik dan rekomendasi ukuran box untuk sebuah paket")]
    public string CalculateVolume(
        [Description("Panjang dalam cm")] double length,
        [Description("Lebar dalam cm")] double width,
        [Description("Tinggi dalam cm")] double height,
        [Description("Berat aktual dalam kg")] double weight)
    {
        using var scope = _scopeFactory.CreateScope();
        var warehouse = scope.ServiceProvider.GetRequiredService<WarehouseService>();

        var volume = length * width * height;
        var volWeight = volume / 6000.0;
        var chargeable = Math.Max(weight, volWeight);
        var (box, wasted) = warehouse.RecommendBox(length, width, height);

        return $"""
            📦 **Kalkulasi paket** ({length}×{width}×{height} cm, {weight} kg)

            | Metrik | Nilai |
            |---|---|
            | Volume | {volume:N0} cm³ |
            | Berat volumetrik | {volWeight:F2} kg |
            | Berat aktual | {weight:F2} kg |
            | **Berat ditagih** | **{chargeable:F2} kg** |
            | Rekomendasi box | {box} |
            | Ruang terbuang | {wasted}% |
            """;
    }

    [KernelFunction("estimate_carbon_emission")]
    [Description("Perkirakan emisi karbon dan biaya carbon offset untuk sebuah pengiriman")]
    public string EstimateCarbon(
        [Description("Jarak tempuh dalam km")] double distanceKm,
        [Description("Berat paket dalam kg")] double weightKg)
    {
        var factor = _config.GetValue<double>("GreenLogistics:EmissionFactorGramCo2PerKm", 150);
        var offsetPrice = _config.GetValue<double>("GreenLogistics:CarbonOffsetPricePerKg", 500);
        var discount = _config.GetValue<double>("GreenLogistics:EcoVehicleDiscount", 0.1);

        var emission = distanceKm * factor * (weightKg / 10.0);
        var ecoEmission = emission * (1 - discount);
        var offsetCost = emission / 1000.0 * offsetPrice;

        return $"""
            🌱 **Estimasi emisi karbon** ({distanceKm:N0} km, {weightKg} kg)

            | Metrik | Nilai |
            |---|---|
            | Emisi standar | {emission:N0} gram CO₂ |
            | Emisi eco-delivery | {ecoEmission:N0} gram CO₂ (−{discount * 100:F0}%) |
            | Biaya carbon offset | Rp {offsetCost:N0} |

            Pilih layanan **ECO** untuk menekan emisi sekaligus ongkos kirim.
            """;
    }

    [KernelFunction("check_partner_options")]
    [Description("Bandingkan opsi mitra logistik pihak ketiga, termasuk pengiriman lintas negara")]
    public async Task<string> CheckPartnerOptions(
        [Description("Kota tujuan, atau kode negara ISO 2 huruf untuk lintas negara")] string destination,
        [Description("Berat dalam kg")] double weightKg,
        [Description("true untuk pengiriman lintas negara")] bool crossBorder = false)
    {
        using var scope = _scopeFactory.CreateScope();
        var partners = scope.ServiceProvider.GetRequiredService<PartnerLogisticsService>();

        var quotes = await partners.GetQuotesAsync(destination, weightKg, crossBorder);
        if (quotes.Count == 0)
            return $"Belum ada mitra logistik yang melayani {destination}.";

        var sb = new StringBuilder($"🤝 **Mitra logistik untuk {destination}** ({weightKg} kg)\n\n");
        sb.AppendLine("| Mitra | Biaya | Estimasi | COD | Rating |");
        sb.AppendLine("|---|---|---|---|---|");
        foreach (var q in quotes)
            sb.AppendLine($"| {q.PartnerName} ({q.PartnerCode}) | Rp {q.Cost:N0} | {q.EstimatedDaysMin}-{q.EstimatedDaysMax} hari | {(q.SupportsCod ? "✅" : "—")} | ⭐ {q.Rating:F1} |");
        return sb.ToString();
    }
}

/// <summary>
/// Customer support: FAQ, tickets, loyalty, notifications, and smart lockers.
/// </summary>
public class SupportPlugin
{
    private readonly IServiceScopeFactory _scopeFactory;

    public long? CurrentUserId { get; init; }

    public SupportPlugin(IServiceScopeFactory scopeFactory, long? currentUserId = null)
    { _scopeFactory = scopeFactory; CurrentUserId = currentUserId; }

    [KernelFunction("get_faq")]
    [Description("Jawaban pertanyaan umum seputar layanan Ngibrid")]
    public string GetFaq([Description("Topik: pengiriman, pembayaran, retur, pickup, asuransi, loker, umum")] string topic = "umum")
    {
        var faqs = new Dictionary<string, string>
        {
            ["pengiriman"] = "📦 REG 2-4 hari, EXP 1-2 hari, SAMEDAY untuk area tertentu. Tracking GPS real-time tersedia di halaman Tracking.",
            ["pembayaran"] = "💳 Kami menerima e-wallet (GoPay, OVO, DANA, ShopeePay), transfer bank (BCA, Mandiri, BNI, BRI), kartu kredit, dan COD. Invoice otomatis terbit setelah order dibuat.",
            ["retur"] = "🔄 Retur gratis dalam 7 hari setelah barang diterima jika barang rusak atau tidak sesuai. Buat tiket dengan kategori REFUND.",
            ["pickup"] = "📥 Pickup gratis. Ajukan lewat halaman Pickup, pilih tanggal dan slot waktu (pagi/siang/sore), kurir akan menjemput sesuai jadwal.",
            ["asuransi"] = "🛡️ Premi 2% dari nilai barang yang dideklarasikan. Klaim diajukan lewat halaman Payment → Insurance, diproses maksimal 3 hari kerja.",
            ["loker"] = "🔐 Smart locker tersedia 24 jam. Anda menerima PIN 6 digit lewat notifikasi, paket disimpan hingga 72 jam.",
            ["umum"] = "🚚 Ngibrid melayani pengiriman domestik ke 500+ kota di Indonesia dan lintas negara lewat mitra. Kontak: support@ngibrid.com / 021-5555-1234."
        };

        return faqs.GetValueOrDefault(topic.ToLowerInvariant(), faqs["umum"]);
    }

    [KernelFunction("create_support_ticket")]
    [Description("Buatkan tiket dukungan pelanggan atas nama pengguna yang sedang login")]
    public async Task<string> CreateSupportTicket(
        [Description("Subjek atau ringkasan masalah")] string subject,
        [Description("Kategori: GENERAL, COMPLAINT, LOST_PACKAGE, DAMAGE, REFUND")] string category = "GENERAL",
        [Description("Prioritas: LOW, NORMAL, HIGH, URGENT")] string priority = "NORMAL")
    {
        if (CurrentUserId is null)
            return "Saya belum bisa mengenali akun Anda. Silakan login dulu agar tiket tercatat atas nama Anda.";

        using var scope = _scopeFactory.CreateScope();
        var support = scope.ServiceProvider.GetRequiredService<SupportTicketService>();

        var ticket = await support.CreateTicketAsync(new SupportTicket
        {
            UserId = CurrentUserId.Value,
            Subject = subject,
            Category = category.ToUpperInvariant(),
            Priority = priority.ToUpperInvariant(),
            Status = "OPEN"
        });

        return $"✅ Tiket **{ticket.TicketNumber}** berhasil dibuat dengan prioritas {ticket.Priority}. " +
               "Tim support akan merespons dalam 1×24 jam. Pantau di halaman Support.";
    }

    [KernelFunction("get_my_loyalty_points")]
    [Description("Cek saldo poin loyalty dan tier pengguna yang sedang login")]
    public async Task<string> GetLoyaltyPoints()
    {
        if (CurrentUserId is null)
            return "Silakan login dulu untuk melihat saldo poin Anda.";

        using var scope = _scopeFactory.CreateScope();
        var loyalty = scope.ServiceProvider.GetRequiredService<LoyaltyService>();

        var balance = await loyalty.GetBalanceAsync(CurrentUserId.Value);
        var tier = LoyaltyService.GetTier(balance);
        var next = LoyaltyService.GetNextTier(balance);

        var sb = new StringBuilder($"{tier.Icon} **Tier {tier.Name}** — saldo **{balance:N0} poin** (pengali {tier.Multiplier}×)");
        if (next is { } n)
            sb.Append($"\n\nKurang **{n.MinPoints - balance:N0} poin** lagi menuju tier {n.Name}.");
        return sb.ToString();
    }

    [KernelFunction("find_smart_locker")]
    [Description("Cari smart locker terdekat beserta ketersediaan pintu loker")]
    public async Task<string> FindSmartLocker([Description("Nama kota")] string city = "")
    {
        using var scope = _scopeFactory.CreateScope();
        var lockers = scope.ServiceProvider.GetRequiredService<SmartLockerService>();

        var results = await lockers.GetLockersAsync(string.IsNullOrWhiteSpace(city) ? null : city);
        if (results.Count == 0)
            return string.IsNullOrWhiteSpace(city) ? "Belum ada smart locker terdaftar." : $"Belum ada smart locker di {city}.";

        var sb = new StringBuilder("🔐 **Smart Locker**\n\n| Lokasi | Kota | Status | Pintu kosong |\n|---|---|---|---|\n");
        foreach (var l in results.Take(10))
        {
            var free = l.Compartments?.Count(c => c.Status == "EMPTY") ?? 0;
            var total = l.Compartments?.Count ?? 0;
            sb.AppendLine($"| {l.Name} | {l.City} | {l.Status} | {free}/{total} |");
        }
        return sb.ToString();
    }

    [KernelFunction("get_my_notifications")]
    [Description("Tampilkan notifikasi terbaru untuk pengguna yang sedang login")]
    public async Task<string> GetNotifications([Description("Jumlah maksimum, default 5")] int limit = 5)
    {
        if (CurrentUserId is null) return "Silakan login dulu untuk melihat notifikasi Anda.";

        using var scope = _scopeFactory.CreateScope();
        var notifications = scope.ServiceProvider.GetRequiredService<NotificationService>();

        var items = await notifications.GetUserNotificationsAsync(CurrentUserId.Value);
        if (items.Count == 0) return "Tidak ada notifikasi.";

        var sb = new StringBuilder("🔔 **Notifikasi terbaru**\n\n");
        foreach (var n in items.Take(Math.Clamp(limit, 1, 20)))
            sb.AppendLine($"- {(n.IsRead ? "" : "**[baru]** ")}`{n.CreatedAt:dd MMM HH:mm}` **{n.Title}** — {n.Message}");
        return sb.ToString();
    }
}
