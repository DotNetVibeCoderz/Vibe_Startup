using System.ComponentModel;
using System.Text.Json;
using Microsoft.SemanticKernel;
using Microsoft.EntityFrameworkCore;
using EstateHub.Data;

namespace EstateHub.Services.ChatBot;

/// <summary>
/// Kernel functions available to Tante Rita chatbot.
/// Registered with Semantic Kernel for AI function calling.
/// </summary>
public class EstateHubKernelFunctions
{
    private readonly AppDbContext _db;
    private readonly IHttpClientFactory _http;
    private readonly IConfiguration _config;

    public EstateHubKernelFunctions(AppDbContext db, IHttpClientFactory http, IConfiguration config)
    {
        _db = db; _http = http; _config = config;
    }

    // ============================================
    // DATABASE QUERY FUNCTIONS
    // ============================================

    [KernelFunction("search_properties")]
    [Description("Mencari properti di database. Filter: keyword, propertyType, listingType, city, minPrice, maxPrice")]
    public async Task<string> SearchProperties(
        [Description("Kata kunci")] string? keyword,
        [Description("Tipe: House, Apartment, ShopHouse, Villa, Land, Office")] string? propertyType,
        [Description("Jenis: Sale atau Rent")] string? listingType,
        [Description("Nama kota")] string? city,
        [Description("Harga minimum")] decimal? minPrice,
        [Description("Harga maksimum")] decimal? maxPrice)
    {
        var query = _db.Properties.Where(p => p.IsVerified && p.Status == "Available").AsQueryable();

        // Case-insensitive keyword search
        if (!string.IsNullOrWhiteSpace(keyword))
        {
            var kw = keyword.ToLower().Trim();
            // Use ToLower() on both sides for case-insensitive Contains
            query = query.Where(p =>
                p.Title.ToLower().Contains(kw) ||
                p.Description.ToLower().Contains(kw) ||
                p.Address.ToLower().Contains(kw) ||
                p.City!.ToLower().Contains(kw) ||
                (p.District != null && p.District.ToLower().Contains(kw)));
        }

        // Case-insensitive property type
        if (!string.IsNullOrWhiteSpace(propertyType))
        {
            var pt = propertyType.Trim();
            query = query.Where(p => p.PropertyType.ToLower() == pt.ToLower());
        }

        // Case-insensitive listing type
        if (!string.IsNullOrWhiteSpace(listingType))
        {
            var lt = listingType.Trim();
            query = query.Where(p => p.ListingType.ToLower() == lt.ToLower());
        }

        // Case-insensitive city search
        if (!string.IsNullOrWhiteSpace(city))
        {
            var c = city.Trim();
            query = query.Where(p => p.City != null && p.City.ToLower().Contains(c.ToLower()));
        }

        if (minPrice.HasValue) query = query.Where(p => p.Price >= minPrice.Value);
        if (maxPrice.HasValue) query = query.Where(p => p.Price <= maxPrice.Value);

        var results = await query.OrderByDescending(p => p.IsPremium).ThenByDescending(p => p.CreatedAt).Take(10).ToListAsync();
        if (!results.Any()) return "Tidak ada properti yang ditemukan dengan filter tersebut.";

        var lines = results.Select(p =>
        {
            var priceStr = p.Price.ToString("N0");
            return $"- [{p.PropertyType}] **{p.Title}** | {p.City} | Rp {priceStr} | {p.Bedrooms}KT/{p.Bathrooms}KM | {p.BuildingArea}m\u00B2 | ID:{p.Id}";
        });
        return string.Join("\n", lines);
    }

    [KernelFunction("get_property_detail")]
    [Description("Mendapatkan detail properti berdasarkan ID")]
    public async Task<string> GetPropertyDetail([Description("ID properti")] int propertyId)
    {
        var p = await _db.Properties.Include(x => x.Owner).FirstOrDefaultAsync(x => x.Id == propertyId);
        if (p == null) return $"Properti ID {propertyId} tidak ditemukan.";

        var priceStr = p.Price.ToString("N0");
        var verifiedStr = p.IsVerified ? "Ya" : "Tidak";
        var listingStr = p.ListingType == "Sale" ? "Dijual" : "Disewakan";

        return $"## {p.Title}\n" +
               $"- **Tipe**: {p.PropertyType} ({listingStr})\n" +
               $"- **Harga**: Rp {priceStr}\n" +
               $"- **Lokasi**: {p.Address}, {p.District}, {p.City}\n" +
               $"- **Luas**: Tanah {p.LandArea}m\u00B2 | Bangunan {p.BuildingArea}m\u00B2\n" +
               $"- **Kamar**: {p.Bedrooms}KT / {p.Bathrooms}KM | Lantai: {p.Floors}\n" +
               $"- **Tahun**: {p.YearBuilt}\n" +
               $"- **Fasilitas**: {p.Facilities}\n" +
               $"- **Deskripsi**: {p.Description}\n" +
               $"- **Agen**: {p.Owner?.FullName} ({p.Owner?.PhoneNumber})\n" +
               $"- **Views**: {p.ViewCount} | **Terverifikasi**: {verifiedStr}";
    }

    [KernelFunction("get_property_stats")]
    [Description("Mendapatkan statistik properti")]
    public async Task<string> GetPropertyStats()
    {
        var total = await _db.Properties.CountAsync();
        var available = await _db.Properties.CountAsync(p => p.Status == "Available");
        var avgSalePrice = await _db.Properties.Where(p => p.ListingType == "Sale").AverageAsync(p => (double?)p.Price) ?? 0;
        var avgRentPrice = await _db.Properties.Where(p => p.ListingType == "Rent").AverageAsync(p => (double?)p.Price) ?? 0;
        var cities = await _db.Properties.Where(p => p.City != null).Select(p => p.City!).Distinct().Take(15).ToListAsync();

        var avgSaleStr = avgSalePrice.ToString("N0");
        var avgRentStr = avgRentPrice.ToString("N0");
        return $"📊 **Statistik Properti**\n- Total: **{total}** (Tersedia: {available})\n- Rata-rata harga jual: **Rp {avgSaleStr}**\n- Rata-rata sewa: **Rp {avgRentStr}**/bulan\n- Kota: {string.Join(", ", cities)}";
    }

    [KernelFunction("get_user_info")]
    [Description("Mendapatkan informasi user")]
    public async Task<string> GetUserInfo([Description("User ID")] string userId)
    {
        var user = await _db.Users.FindAsync(userId);
        if (user == null) return "User tidak ditemukan.";
        return $"👤 **{user.FullName}** | Role: {user.Role} | HP: {user.PhoneNumber} | Alamat: {user.Address}";
    }

    [KernelFunction("get_booking_info")]
    [Description("Mendapatkan info booking user")]
    public async Task<string> GetBookings([Description("User ID")] string userId)
    {
        var bookings = await _db.Bookings.Where(b => b.UserId == userId)
            .Include(b => b.Property).OrderByDescending(b => b.ScheduledDate).Take(10).ToListAsync();
        if (!bookings.Any()) return "Belum ada janji temu.";
        var lines = bookings.Select(b => $"- 📅 {b.ScheduledDate:dd MMM yyyy} | **{b.Property?.Title}** | {b.Status}");
        return $"📋 **Janji Temu:**\n{string.Join("\n", lines)}";
    }

    // ============================================
    // WEB & SEARCH FUNCTIONS
    // ============================================

    [KernelFunction("search_internet")]
    [Description("Mencari informasi di internet via Tavily Search API")]
    public async Task<string> SearchInternet(
        [Description("Query pencarian")] string query,
        [Description("Max hasil (default 5)")] int maxResults = 5)
    {
        try
        {
            var apiKey = _config.GetValue<string>("ChatBot:TavilyApiKey");
            if (string.IsNullOrEmpty(apiKey))
                return "🔌 Tavily API belum dikonfigurasi. Tambahkan ChatBot:TavilyApiKey di appsettings.json.";

            var client = _http.CreateClient();
            var body = new { api_key = apiKey, query, max_results = maxResults, search_depth = "basic" };
            var resp = await client.PostAsJsonAsync("https://api.tavily.com/search", body);

            if (!resp.IsSuccessStatusCode)
                return $"❌ Pencarian gagal: HTTP {(int)resp.StatusCode}";

            var result = await resp.Content.ReadFromJsonAsync<JsonElement>();
            var items = new List<string>();
            if (result.TryGetProperty("results", out var arr))
            {
                foreach (var item in arr.EnumerateArray().Take(maxResults))
                {
                    var t = item.TryGetProperty("title", out var ti) ? ti.GetString() : "No title";
                    var u = item.TryGetProperty("url", out var ur) ? ur.GetString() : "#";
                    var c = item.TryGetProperty("content", out var co) ? co.GetString() ?? "" : "";
                    if (c.Length > 300) c = c[..300] + "...";
                    items.Add($"- **{t}**\n  {c}\n  🔗 {u}");
                }
            }
            return items.Any() ? $"🔍 Hasil \"{query}\":\n\n{string.Join("\n\n", items)}" : $"Tidak ada hasil.";
        }
        catch (Exception ex) { return $"❌ Error: {ex.Message}"; }
    }

    [KernelFunction("scrape_webpage")]
    [Description("Mengambil konten dari URL")]
    public async Task<string> ScrapeWebpage([Description("URL halaman")] string url)
    {
        try
        {
            var client = _http.CreateClient();
            client.DefaultRequestHeaders.Add("User-Agent", "EstateHub/1.0");
            var html = await client.GetStringAsync(url);
            var text = System.Text.RegularExpressions.Regex.Replace(html, "<[^>]+>", " ");
            text = System.Text.RegularExpressions.Regex.Replace(text, @"\s+", " ").Trim();
            if (text.Length > 3000) text = text[..3000] + "...";
            return $"📄 {url}:\n\n{text}";
        }
        catch (Exception ex) { return $"❌ Error scraping: {ex.Message}"; }
    }

    [KernelFunction("read_file_from_url")]
    [Description("Membaca konten file dari URL")]
    public async Task<string> ReadFileFromUrl([Description("URL file")] string fileUrl)
    {
        try
        {
            var client = _http.CreateClient();
            var resp = await client.GetAsync(fileUrl);
            if (!resp.IsSuccessStatusCode) return $"❌ HTTP {(int)resp.StatusCode}";
            var content = await resp.Content.ReadAsStringAsync();
            if (content.Length > 3000) content = content[..3000] + "...";
            return $"📄 {fileUrl}:\n\n{content}";
        }
        catch (Exception ex) { return $"❌ Error: {ex.Message}"; }
    }

    // ============================================
    // UTILITY FUNCTIONS
    // ============================================

    [KernelFunction("get_current_time")]
    [Description("Mendapatkan waktu saat ini")]
    public string GetCurrentTime()
    {
        var now = DateTime.Now;
        var utc = DateTime.UtcNow;
        return $"🕐 WIB: {now:dddd, dd MMMM yyyy HH:mm:ss} | UTC: {utc:dd MMMM yyyy HH:mm:ss} | TZ: {TimeZoneInfo.Local.DisplayName}";
    }

    [KernelFunction("calculate_math")]
    [Description("Kalkulasi matematika")]
    public string CalculateMath([Description("Ekspresi matematika")] string expression)
    {
        try
        {
            var result = new System.Data.DataTable().Compute(expression, null);
            return $"🧮 {expression} = {result}";
        }
        catch (Exception ex) { return $"❌ Error: {ex.Message}"; }
    }

    [KernelFunction("calculate_kpr")]
    [Description("Simulasi KPR lengkap")]
    public string CalculateKpr(
        [Description("Harga properti (Rp)")] decimal propertyPrice,
        [Description("Uang muka (Rp)")] decimal downPayment,
        [Description("Bunga per tahun (%)")] double interestRate,
        [Description("Tenor (tahun)")] int tenorYears)
    {
        var loanAmount = propertyPrice - downPayment;
        var monthlyRate = interestRate / 12.0 / 100.0;
        var totalMonths = tenorYears * 12;

        if (monthlyRate <= 0)
        {
            var sm = loanAmount / totalMonths;
            var smStr = sm.ToString("N0");
            var dpPct = (downPayment / propertyPrice * 100).ToString("0");
            return $"💰 **KPR Bunga 0%**\n- Harga: Rp {propertyPrice:N0}\n- DP ({dpPct}%): Rp {downPayment:N0}\n- Pinjaman: Rp {loanAmount:N0}\n- Tenor: {tenorYears}th\n- **Cicilan: Rp {smStr}/bln**";
        }

        var factor = Math.Pow(1 + monthlyRate, totalMonths);
        var monthlyPayment = loanAmount * (decimal)(monthlyRate * factor / (factor - 1));
        var totalPayment = monthlyPayment * totalMonths;
        var totalInterest = totalPayment - loanAmount;
        var dpPct2 = (downPayment / propertyPrice * 100).ToString("0");
        var mpStr = monthlyPayment.ToString("N0");
        var tpStr = totalPayment.ToString("N0");
        var tiStr = totalInterest.ToString("N0");

        return $"💰 **Simulasi KPR**\n- Harga: **Rp {propertyPrice:N0}**\n- DP ({dpPct2}%): **Rp {downPayment:N0}**\n- Pinjaman: **Rp {loanAmount:N0}**\n- Bunga: **{interestRate}%**/th | Tenor: **{tenorYears}th**\n\n📊 **Hasil:**\n- 💵 **Cicilan: Rp {mpStr}/bln**\n- 📈 Total Bayar: Rp {tpStr}\n- 💸 Total Bunga: Rp {tiStr}\n\n💡 Cicilan ideal ≤ 30% penghasilan.";
    }

    [KernelFunction("calculate_tax")]
    [Description("Hitung pajak properti")]
    public string CalculateTax(
        [Description("Harga transaksi (Rp)")] decimal price,
        [Description("Tipe: buyer / seller")] string type = "buyer")
    {
        if (type.ToLower() == "seller" || type.ToLower() == "penjual")
        {
            var pph = price * 0.025m;
            var pphStr = pph.ToString("N0");
            var netStr = (price - pph).ToString("N0");
            return $"🏛️ **Pajak Penjual**\n- Harga: Rp {price:N0}\n- PPh Final (2.5%): **Rp {pphStr}**\n- Bersih: Rp {netStr}";
        }
        else
        {
            var npoptkp = 80000000m;
            var bphtb = Math.Max(0, (price - npoptkp) * 0.05m);
            var notaris = price * 0.01m;
            var ppn = price * 0.11m;
            var total = bphtb + ppn + notaris;
            return $"🏛️ **Pajak Pembeli**\n- Harga: Rp {price:N0}\n- BPHTB (5%): **Rp {bphtb:N0}**\n- PPN (11%): **Rp {ppn:N0}**\n- Notaris (±1%): **Rp {notaris:N0}**\n- **Total Tambahan: Rp {total:N0}**";
        }
    }
}
