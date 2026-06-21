#pragma warning disable SKEXP0001
using System.ComponentModel;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.SemanticKernel;
using RentalBoil.Data;
using RentalBoil.Models;

namespace RentalBoil.Services;

public class BotKernelFunctions
{
    private readonly IConfiguration _config;
    private readonly IServiceScopeFactory _scopeFactory;

    public BotKernelFunctions(IConfiguration config, IServiceScopeFactory scopeFactory)
    { _config = config; _scopeFactory = scopeFactory; }

    private AppDbContext GetDb()
    { var scope = _scopeFactory.CreateScope(); return scope.ServiceProvider.GetRequiredService<AppDbContext>(); }

    [KernelFunction("get_current_datetime"), Description("Tanggal & waktu WIB (UTC+7)")]
    public string GetCurrentDateTime()
    { var wib = TimeZoneInfo.FindSystemTimeZoneById("SE Asia Standard Time"); return TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, wib).ToString("dd MMMM yyyy HH:mm:ss 'WIB'"); }

    [KernelFunction("get_day_of_week"), Description("Hari untuk tanggal yyyy-MM-dd")]
    public string GetDayOfWeek(string d) => DateTime.TryParse(d, out var dt) ? dt.ToString("dddd, dd MMMM yyyy") : "Format invalid";

    [KernelFunction("math_calculate"), Description("Kalkulasi: + - * / ^ sqrt abs round sin cos tan log pi e")]
    public string MathCalculate(string expr)
    { try { return $"Hasil: {new System.Data.DataTable().Compute(expr.Replace("^", "**"), null)}"; } catch (Exception ex) { return $"Error: {ex.Message}"; } }

    [KernelFunction("convert_currency_simulation"), Description("Simulasi konversi IDR/USD/EUR/SGD/JPY/MYR")]
    public string ConvertCurrency(decimal amount, string from, string to)
    { var r = new Dictionary<string, decimal> { ["IDR"] = 1, ["USD"] = 15700m, ["EUR"] = 17000m, ["SGD"] = 11700m, ["JPY"] = 105m, ["MYR"] = 3350m }; from = from.ToUpper(); to = to.ToUpper(); return !r.ContainsKey(from) || !r.ContainsKey(to) ? $"Support: {string.Join(", ", r.Keys)}" : $"{amount:N2} {from} = {amount * r[from] / r[to]:N2} {to} (simulasi)"; }

    [KernelFunction("search_internet"), Description("Cari di internet via Tavily")]
    public async Task<string> SearchInternet(string query, int maxResults = 5)
    {
        try
        {
            var key = _config.GetValue<string>("Tavily:ApiKey"); if (string.IsNullOrWhiteSpace(key)) return "⚠️ Tavily API Key belum dikonfigurasi.";
            using var h = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
            var resp = await h.PostAsync("https://api.tavily.com/search", new StringContent(JsonSerializer.Serialize(new { api_key = key, query, max_results = Math.Min(maxResults, 10), search_depth = "basic", include_answer = true }), System.Text.Encoding.UTF8, "application/json"));
            if (!resp.IsSuccessStatusCode) return $"⚠️ Tavily error ({resp.StatusCode})";
            using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync()); var root = doc.RootElement;
            var r = "🔍 **Hasil Pencarian**\n\n";
            if (root.TryGetProperty("answer", out var a) && a.GetString() is { Length: > 0 } ans) r += $"**Ringkasan:** {ans}\n\n";
            if (root.TryGetProperty("results", out var results)) { int i = 1; foreach (var x in results.EnumerateArray()) { r += $"{i}. **{x.GetProperty("title").GetString()}**\n   {x.GetProperty("content").GetString()?[..Math.Min(200, x.GetProperty("content").GetString()?.Length ?? 0)]}...\n   🔗 {x.GetProperty("url").GetString()}\n\n"; i++; } }
            return r;
        } catch (Exception ex) { return $"⚠️ Error: {ex.Message}"; }
    }

    [KernelFunction("scrap_web_page"), Description("Ambil konten teks halaman web")]
    public async Task<string> ScrapWebPage(string url)
    { try { using var h = new HttpClient { Timeout = TimeSpan.FromSeconds(15) }; h.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0"); var html = await h.GetStringAsync(url); var t = System.Text.RegularExpressions.Regex.Replace(System.Web.HttpUtility.HtmlDecode(System.Text.RegularExpressions.Regex.Replace(html, "<[^>]+>", " ")), @"\s+", " ").Trim(); return $"📄 **{url}**\n\n{t[..Math.Min(2000, t.Length)]}"; } catch (Exception ex) { return $"⚠️ {ex.Message}"; } }

    [KernelFunction("read_file_from_url"), Description("Baca file teks/JSON/CSV dari URL")]
    public async Task<string> ReadFileFromUrl(string url)
    { try { using var h = new HttpClient { Timeout = TimeSpan.FromSeconds(20) }; var c = await h.GetStringAsync(url); return $"📂 **{url}**\n```\n{c[..Math.Min(3000, c.Length)]}\n```"; } catch (Exception ex) { return $"⚠️ {ex.Message}"; } }

    // ═══ DB FUNCTIONS (via IServiceScopeFactory, case-insensitive) ═══

    [KernelFunction("search_vehicles_db"), Description("Cari kendaraan di DB. Filter: budget, tipe, kapasitas, lokasi.")]
    public async Task<string> SearchVehiclesDb(string query, string? vehicleType = null, decimal? maxBudget = null, int? minCapacity = null, string? location = null)
    {
        try
        {
            var db = GetDb(); var q = db.Vehicles.Include(v => v.Photos).Where(v => v.IsVerified && v.IsAvailable);
            if (!string.IsNullOrWhiteSpace(query)) { var ql = query.ToLowerInvariant(); q = q.Where(v => v.Name.ToLower().Contains(ql) || v.Brand.ToLower().Contains(ql) || v.Model.ToLower().Contains(ql)); }
            if (vehicleType == "motorcycle") q = q.Where(v => v.Type == VehicleType.Motorcycle); else if (vehicleType == "car") q = q.Where(v => v.Type == VehicleType.Car);
            if (maxBudget.HasValue) q = q.Where(v => v.PricePerDay <= maxBudget.Value);
            if (minCapacity.HasValue) q = q.Where(v => v.Capacity >= minCapacity.Value);
            if (!string.IsNullOrWhiteSpace(location)) q = q.Where(v => v.Location != null && v.Location.ToLower().Contains(location.ToLowerInvariant()));
            var results = await q.OrderByDescending(v => v.RentalCount).Take(5).ToListAsync();
            if (!results.Any()) return "🔍 Tidak ada kendaraan yang cocok.";
            return "🚗 **Kendaraan Tersedia**\n\n" + string.Join("\n\n", results.Select(v => $"**#{v.Id} {v.Name}** ({v.Year}) - ⭐{v.AverageRating}\n   {v.Brand} | {v.Transmission} | {v.Capacity} org\n   💰 Rp {v.PricePerDay:N0}/hari | 📍 {v.Location}")) + "\n\n💡 _Ketik 'booking [id]' untuk booking!_";
        } catch (Exception ex) { return $"⚠️ Error: {ex.Message}"; }
    }

    [KernelFunction("get_vehicle_detail_db"), Description("Detail satu kendaraan")]
    public async Task<string> GetVehicleDetailDb(int id)
    { var db = GetDb(); var v = await db.Vehicles.Include(x => x.Reviews).FirstOrDefaultAsync(x => x.Id == id); return v == null ? "❌ Tidak ditemukan." : $"🚗 **{v.Name}** (#{v.Id})\n📋 {v.Brand} {v.Model} ({v.Year})\n⚙️ {v.Transmission} | 👥 {v.Capacity} org\n💰 Rp {v.PricePerDay:N0}/hari | ⭐ {v.AverageRating}/5\n📍 {v.Location}"; }

    [KernelFunction("create_booking_via_chat"), Description("Booking kendaraan via chat")]
    public async Task<string> CreateBookingViaChat(int vehicleId, string customerId, string startDateStr, int days, string? couponCode = null)
    {
        try
        {
            var db = GetDb(); var v = await db.Vehicles.FindAsync(vehicleId);
            if (v == null) return "❌ Kendaraan tidak ditemukan."; if (!v.IsAvailable) return "❌ Tidak tersedia.";
            if (!DateTime.TryParse(startDateStr, out var sd)) return "❌ Format tanggal salah (yyyy-MM-dd).";

            var b = new Booking { VehicleId = vehicleId, CustomerId = customerId, StartDate = sd, EndDate = sd.AddDays(days), DurationDays = days, DurationHours = days * 24, CouponCode = couponCode, Status = BookingStatus.Pending, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow };
            b.BasePrice = v.PricePerDay * days * v.DynamicPriceMultiplier;
            b.InsuranceCost = v.InsuranceAvailable ? v.InsuranceCostPerDay * days : 0;
            b.TotalPrice = b.BasePrice + b.InsuranceCost;

            if (!string.IsNullOrWhiteSpace(couponCode)) { var cp = await db.Promotions.FirstOrDefaultAsync(p => p.Code.ToLower() == couponCode.ToLowerInvariant() && p.IsActive && p.StartDate <= DateTime.UtcNow && p.EndDate >= DateTime.UtcNow); if (cp != null) { b.Discount = cp.DiscountType == "percentage" ? Math.Min(b.BasePrice * cp.DiscountValue / 100, cp.MaxDiscount ?? decimal.MaxValue) : cp.DiscountValue; b.TotalPrice -= b.Discount; cp.UsageCount++; } }

            // ★ FIX: prefix-based, avoids UNIQUE constraint on BookingNumber
            var prefix = $"RB-{DateTime.UtcNow:yyyyMMdd}-";
            b.BookingNumber = $"{prefix}{(await db.Bookings.CountAsync(x => x.BookingNumber.StartsWith(prefix)) + 1):D4}";

            db.Bookings.Add(b); await db.SaveChangesAsync();
            return $"✅ **Booking Berhasil!**\n🎫 No: **{b.BookingNumber}**\n🚗 {v.Name}\n📅 {sd:dd MMM} → {sd.AddDays(days):dd MMM} ({days} hari)\n💰 Rp {b.TotalPrice:N0}\n📋 Status: Menunggu Konfirmasi";
        } catch (Exception ex) { return $"⚠️ Error: {ex.Message}"; }
    }

    [KernelFunction("check_booking_status"), Description("Cek status booking")]
    public async Task<string> CheckBookingStatus(string bn)
    { var db = GetDb(); var b = await db.Bookings.Include(x => x.Vehicle).FirstOrDefaultAsync(x => x.BookingNumber == bn); if (b == null) return $"❌ #{bn} tidak ditemukan."; var e = b.Status switch { BookingStatus.Pending => "⏳", BookingStatus.Confirmed => "✅", BookingStatus.Active => "🚗", BookingStatus.Completed => "🏁", _ => "❌" }; return $"{e} **#{b.BookingNumber}**\n🚗 {b.Vehicle?.Name}\n📅 {b.StartDate:dd MMM} → {b.EndDate:dd MMM}\n💰 Rp {b.TotalPrice:N0}\n📊 {b.Status} | 💳 {b.PaymentStatus}"; }

    [KernelFunction("get_vehicle_position"), Description("Posisi GPS + IoT")]
    public async Task<string> GetVehiclePosition(int id)
    { var db = GetDb(); var v = await db.Vehicles.FindAsync(id); return v == null ? "❌ Tidak ditemukan." : $"🛰️ **{v.Name}** (#{v.Id})\n📍 {v.Latitude?.ToString("F6")}, {v.Longitude?.ToString("F6")}\n🏃 {v.MotionStatus} | ⚡ {v.EngineStatus} | 🔐 {v.LockStatus}\n🚀 {v.CurrentSpeed} km/h | 🧭 {v.CurrentHeading}°"; }

    [KernelFunction("get_active_promotions"), Description("Promo & kupon aktif")]
    public async Task<string> GetActivePromotions()
    { var db = GetDb(); var ps = await db.Promotions.Where(p => p.IsActive && p.StartDate <= DateTime.UtcNow && p.EndDate >= DateTime.UtcNow).ToListAsync(); return !ps.Any() ? "🎁 Tidak ada promo." : "🎁 **Promo Aktif**\n\n" + string.Join("\n\n", ps.Select(p => $"🎫 **{p.Code}** - {(p.DiscountType == "percentage" ? $"{p.DiscountValue}%" : $"Rp {p.DiscountValue:N0}")}\n   {p.Description}\n   📅 s/d {p.EndDate:dd MMM}")); }

    [KernelFunction("get_faqs"), Description("Cari FAQ (case-insensitive)")]
    public async Task<string> GetFaqs(string kw)
    { try { var db = GetDb(); var k = kw.ToLowerInvariant(); var fs = await db.Faqs.Where(f => f.IsActive && (f.Question.ToLower().Contains(k) || f.Answer.ToLower().Contains(k))).Take(3).ToListAsync(); return !fs.Any() ? $"❓ Tidak ada FAQ: '{kw}'" : "❓ **FAQ**\n\n" + string.Join("\n\n", fs.Select(f => $"**Q:** {f.Question}\n**A:** {f.Answer[..Math.Min(f.Answer.Length, 300)]}")); } catch (Exception ex) { return $"⚠️ {ex.Message}"; } }

    [KernelFunction("calculate_rental_price"), Description("Estimasi biaya sewa")]
    public async Task<string> CalculateRentalPrice(int vehicleId, int days)
    { var db = GetDb(); var v = await db.Vehicles.FindAsync(vehicleId); if (v == null) return "❌ Tidak ditemukan."; var bp = v.PricePerDay * days * v.DynamicPriceMultiplier; var ins = v.InsuranceAvailable ? v.InsuranceCostPerDay * days : 0; return $"💰 **Estimasi**\n🚗 {v.Name}\n📅 {days} hari\n💵 Rp {bp:N0} + 🛡️ Rp {ins:N0}\n📊 **Total: Rp {bp + ins:N0}**"; }

    [KernelFunction("get_weather_info"), Description("Simulasi cuaca")]
    public string GetWeatherInfo(string loc)
    { var c = new[] { "Cerah ☀️", "Berawan ⛅", "Hujan Ringan 🌧️", "Hujan Lebat ⛈️" }; var t = new[] { 24, 26, 28, 30, 32, 33 }; var r = new Random(loc.GetHashCode()); return $"🌤️ **{loc}** (simulasi)\n🌡️ {t[r.Next(6)]}°C | {c[r.Next(4)]}\n💧 {r.Next(60, 95)}% | 💨 {r.Next(5, 25)} km/h"; }

    [KernelFunction("get_platform_stats"), Description("Statistik platform")]
    public async Task<string> GetPlatformStats()
    { var db = GetDb(); return $"📊 **RentalBoil**\n🚗 {await db.Vehicles.CountAsync(v => v.IsVerified)} kendaraan\n✅ {await db.Vehicles.CountAsync(v => v.IsVerified && v.IsAvailable)} tersedia\n📋 {await db.Bookings.CountAsync()} booking\n🏃 {await db.Bookings.CountAsync(b => b.Status == BookingStatus.Active)} aktif"; }
}
