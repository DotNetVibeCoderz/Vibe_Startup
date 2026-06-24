using HolySafar.Data;
using HolySafar.Models;
using Microsoft.EntityFrameworkCore;

namespace HolySafar.Api;

public static class ApiEndpoints
{
    private const string ApiKeyHeader = "X-Api-Key";

    public static void MapApi(WebApplication app)
    {
        var api = app.MapGroup("/api");

        // Auth filter
        api.AddEndpointFilter(async (ctx, next) =>
        {
            var config = ctx.HttpContext.RequestServices.GetRequiredService<IConfiguration>();
            var validKey = config["AppSettings:ApiKey"] ?? "HolySafar-API-Key-2025!";
            if (!ctx.HttpContext.Request.Headers.TryGetValue(ApiKeyHeader, out var key) || key != validKey)
                return Results.Unauthorized();
            return await next(ctx);
        });

        // ==================== JAMAAH ====================
        api.MapGet("/jamaah", async (AppDbContext db) =>
            await db.Jamaah.Include(j => j.Paket).OrderByDescending(j => j.Id).ToListAsync()).WithTags("Jamaah");

        api.MapGet("/jamaah/{id}", async (int id, AppDbContext db) =>
            await db.Jamaah.Include(j => j.Paket).FirstOrDefaultAsync(j => j.Id == id) is Jamaah j ? Results.Ok(j) : Results.NotFound()).WithTags("Jamaah");

        api.MapGet("/jamaah/search", async (string? nama, string? nik, AppDbContext db) =>
        {
            var q = db.Jamaah.Include(j => j.Paket).AsQueryable();
            if (!string.IsNullOrEmpty(nama)) q = q.Where(j => j.NamaLengkap.Contains(nama));
            if (!string.IsNullOrEmpty(nik)) q = q.Where(j => j.Nik != null && j.Nik.Contains(nik));
            return await q.Take(50).ToListAsync();
        }).WithTags("Jamaah");

        // ==================== GPS ====================
        api.MapPost("/jamaah/{id}/gps", async (int id, GpsUpdateRequest req, AppDbContext db) =>
        {
            var j = await db.Jamaah.FindAsync(id);
            if (j == null) return Results.NotFound();
            j.Latitude = req.Latitude; j.Longitude = req.Longitude; j.LastLocationUpdate = DateTime.UtcNow;
            await db.SaveChangesAsync();
            return Results.Ok(new { j.Id, j.NamaLengkap, j.Latitude, j.Longitude, j.LastLocationUpdate });
        }).WithTags("GPS");

        api.MapGet("/jamaah/gps/active", async (AppDbContext db) =>
            await db.Jamaah.Where(j => j.Latitude != null && j.Longitude != null)
                .Select(j => new { j.Id, j.NamaLengkap, j.Latitude, j.Longitude, j.LastLocationUpdate, j.StatusKeberangkatan })
                .OrderByDescending(j => j.LastLocationUpdate).Take(100).ToListAsync()).WithTags("GPS");

        // ==================== USERS ====================
        api.MapGet("/users", async (AppDbContext db) =>
            await db.Users.Select(u => new { u.Id, u.Username, u.FullName, u.Email, u.Phone, u.Role, u.IsActive, u.CreatedAt }).OrderByDescending(u => u.Id).ToListAsync()).WithTags("Users");

        // ==================== PAKET ====================
        api.MapGet("/paket", async (AppDbContext db) =>
            await db.Paket.Where(p => p.IsActive).OrderBy(p => p.Harga).ToListAsync()).WithTags("Paket");
        api.MapGet("/paket/{id}", async (int id, AppDbContext db) =>
            await db.Paket.FindAsync(id) is Paket p ? Results.Ok(p) : Results.NotFound()).WithTags("Paket");

        // ==================== PEMBAYARAN ====================
        api.MapGet("/pembayaran", async (AppDbContext db) =>
            await db.Pembayaran.Include(p => p.Jamaah).Include(p => p.Paket).OrderByDescending(p => p.Id).Take(100).ToListAsync()).WithTags("Pembayaran");

        api.MapGet("/pembayaran/{id}", async (int id, AppDbContext db) =>
            await db.Pembayaran.Include(p => p.Jamaah).Include(p => p.Paket).FirstOrDefaultAsync(p => p.Id == id) is Pembayaran p ? Results.Ok(p) : Results.NotFound()).WithTags("Pembayaran");

        api.MapPost("/pembayaran/{id}/cicilan", async (int id, CicilanRequest req, AppDbContext db) =>
        {
            var pm = await db.Pembayaran.FindAsync(id);
            if (pm == null) return Results.NotFound();
            var c = new Cicilan { PembayaranId = id, Jumlah = req.Jumlah, TanggalBayar = DateTime.UtcNow, MetodePembayaran = req.Metode ?? "Transfer", Catatan = req.Catatan, Dikonfirmasi = true };
            db.Cicilan.Add(c);
            pm.TotalDibayar += req.Jumlah;
            if (pm.TotalDibayar >= pm.TotalBiaya) { pm.Status = PaymentStatus.Paid; pm.TotalDibayar = pm.TotalBiaya; }
            else pm.Status = PaymentStatus.Partial;
            await db.SaveChangesAsync();
            return Results.Ok(pm);
        }).WithTags("Pembayaran");

        // ==================== KEBERANGKATAN ====================
        api.MapGet("/keberangkatan", async (AppDbContext db) =>
            await db.Keberangkatan.Include(k => k.Paket).OrderByDescending(k => k.TanggalBerangkat).ToListAsync()).WithTags("Keberangkatan");

        // ==================== PRODUK ====================
        api.MapGet("/produk", async (AppDbContext db) =>
            await db.Produk.Where(p => p.IsActive).ToListAsync()).WithTags("Produk");

        // ==================== PENGUMUMAN ====================
        api.MapGet("/pengumuman", async (AppDbContext db) =>
            await db.Pengumuman.Where(p => p.IsActive).OrderByDescending(p => p.CreatedAt).Take(20).ToListAsync()).WithTags("Pengumuman");

        // ==================== SOS ====================
        api.MapGet("/sos", async (AppDbContext db) =>
            await db.SOSTriggers.Include(s => s.Jamaah).OrderByDescending(s => s.TriggeredAt).Take(50).ToListAsync()).WithTags("SOS");

        api.MapPost("/sos", async (SosCreateRequest req, AppDbContext db) =>
        {
            var sos = new SOSTrigger { JamaahId = req.JamaahId, Latitude = req.Latitude, Longitude = req.Longitude, Pesan = req.Pesan ?? "SOS!", TriggeredAt = DateTime.UtcNow };
            db.SOSTriggers.Add(sos);
            await db.SaveChangesAsync();
            return Results.Created($"/api/sos/{sos.Id}", sos);
        }).WithTags("SOS");

        // ==================== DARURAT ====================
        api.MapGet("/kontak-darurat", async (AppDbContext db) =>
            await db.KontakDarurat.Where(k => k.IsActive).ToListAsync()).WithTags("Darurat");

        // ==================== EDUKASI ====================
        api.MapGet("/materi-manasik", async (AppDbContext db) =>
            await db.MateriManasik.Where(m => m.IsPublished).OrderBy(m => m.Urutan).ToListAsync()).WithTags("Edukasi");
        api.MapGet("/kuis", async (AppDbContext db) => await db.Kuis.ToListAsync()).WithTags("Edukasi");

        // ==================== ORDERS ====================
        api.MapGet("/orders", async (AppDbContext db) =>
            await db.Orders.Include(o => o.User).Include(o => o.Items).ThenInclude(i => i.Produk).OrderByDescending(o => o.Id).Take(50).ToListAsync()).WithTags("Orders");

        // ==================== CHAT ====================
        api.MapGet("/chat", async (AppDbContext db) =>
            await db.ChatMessages.Include(m => m.Sender).OrderByDescending(m => m.SentAt).Take(100).ToListAsync()).WithTags("Chat");

        // ==================== DASHBOARD ====================
        api.MapGet("/dashboard", async (AppDbContext db) => new
        {
            TotalJamaah = await db.Jamaah.CountAsync(),
            TotalPaket = await db.Paket.CountAsync(p => p.IsActive && p.IsPublished),
            TotalPembayaran = await db.Pembayaran.SumAsync(p => p.TotalDibayar),
            KeberangkatanBulanIni = await db.Keberangkatan.CountAsync(k => k.TanggalBerangkat >= DateTime.UtcNow && k.TanggalBerangkat <= DateTime.UtcNow.AddMonths(1)),
            SOSAktif = await db.SOSTriggers.CountAsync(s => !s.IsResolved),
            TotalOrders = await db.Orders.CountAsync()
        }).WithTags("Dashboard");
    }
}

public record GpsUpdateRequest(double Latitude, double Longitude);
public record CicilanRequest(decimal Jumlah, string? Metode, string? Catatan);
public record SosCreateRequest(int JamaahId, double Latitude, double Longitude, string? Pesan);
