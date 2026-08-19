using ClosedXML.Excel;
using HolySafar.Data;
using HolySafar.Models;
using Microsoft.EntityFrameworkCore;

namespace HolySafar.Services;

/// <summary>
/// Keamanan data — backup basis data (requirements: Admin/Operator, Keamanan Data).
///
/// Dua bentuk backup:
/// 1. Snapshot database SQLite lewat "VACUUM INTO" (aman dilakukan saat aplikasi jalan,
///    tidak menyalin file yang sedang ditulis WAL).
/// 2. Ekspor seluruh tabel ke satu workbook Excel — dipakai untuk provider selain SQLite
///    dan untuk keperluan audit/compliance karena bisa dibaca tanpa aplikasi.
/// Setiap backup dicatat di tabel BackupLog.
/// </summary>
public class BackupService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IConfiguration _config;
    private readonly IWebHostEnvironment _env;
    private readonly ILogger<BackupService> _logger;

    public BackupService(IServiceScopeFactory scopeFactory, IConfiguration config,
        IWebHostEnvironment env, ILogger<BackupService> logger)
    { _scopeFactory = scopeFactory; _config = config; _env = env; _logger = logger; }

    public string DbProvider => _config["Database:Provider"] ?? "SQLite";
    public bool DatabaseSnapshotSupported => DbProvider == "SQLite";

    /// <summary>Snapshot file database SQLite. Mengembalikan (namaFile, isi).</summary>
    public async Task<(string FileName, byte[] Content)> CreateDatabaseSnapshotAsync(string dibuatOleh)
    {
        if (!DatabaseSnapshotSupported)
            throw new InvalidOperationException($"Snapshot file hanya untuk SQLite. Provider aktif: {DbProvider}. Gunakan ekspor Excel.");

        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var dir = Path.Combine(_env.ContentRootPath, "Data", "backups");
        Directory.CreateDirectory(dir);
        var fileName = $"holysafar-{DateTime.Now:yyyyMMdd-HHmmss}.db";
        var fullPath = Path.Combine(dir, fileName);
        if (File.Exists(fullPath)) File.Delete(fullPath);

        // VACUUM INTO menulis salinan konsisten tanpa menghentikan aplikasi.
        await db.Database.ExecuteSqlRawAsync($"VACUUM INTO '{fullPath.Replace("'", "''")}'");

        var bytes = await File.ReadAllBytesAsync(fullPath);
        await CatatAsync(db, fileName, bytes.LongLength, "Database", dibuatOleh);
        _logger.LogInformation("Backup database dibuat: {File} ({Size} byte)", fileName, bytes.LongLength);
        return (fileName, bytes);
    }

    /// <summary>Ekspor seluruh tabel ke satu file Excel multi-sheet.</summary>
    public async Task<(string FileName, byte[] Content)> CreateFullExportAsync(string dibuatOleh)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        using var wb = new XLWorkbook();

        // Kolom sensitif tidak ikut diekspor.
        AddSheet(wb, "Users", (await db.Users.AsNoTracking().ToListAsync())
            .Select(u => new { u.Id, u.Username, u.FullName, u.Email, u.Phone, Role = u.Role.ToString(), u.IsActive, u.CreatedAt, u.LastLoginAt }));
        AddSheet(wb, "Jamaah", (await db.Jamaah.AsNoTracking().ToListAsync())
            .Select(j => new { j.Id, j.NamaLengkap, j.Nik, j.NoPaspor, j.NoKK, j.TempatLahir, j.TanggalLahir, j.JenisKelamin, j.Alamat, j.Kota, j.Provinsi, j.NoTelepon, j.Email, StatusDokumen = j.StatusDokumen.ToString(), StatusVisa = j.StatusVisa.ToString(), j.NoVisa, StatusKeberangkatan = j.StatusKeberangkatan.ToString(), j.NoPorsi, j.SiskohatStatus, j.PaketId, j.CreatedAt }));
        AddSheet(wb, "Paket", await db.Paket.AsNoTracking().ToListAsync());
        AddSheet(wb, "Pembayaran", (await db.Pembayaran.AsNoTracking().ToListAsync())
            .Select(p => new { p.Id, p.JamaahId, p.PaketId, p.TotalBiaya, p.TotalDibayar, Status = p.Status.ToString(), p.MetodePembayaran, p.TanggalJatuhTempo, p.CreatedAt }));
        AddSheet(wb, "Cicilan", await db.Cicilan.AsNoTracking().ToListAsync());
        AddSheet(wb, "Transaksi", (await db.PaymentTransactions.AsNoTracking().ToListAsync())
            .Select(t => new { t.Id, t.KodeTransaksi, Provider = t.Provider.ToString(), t.ReferenceType, t.ReferenceId, t.UserId, t.Jumlah, Status = t.Status.ToString(), t.ExternalId, t.CreatedAt, t.PaidAt }));
        AddSheet(wb, "Dokumen", (await db.DokumenJamaah.AsNoTracking().ToListAsync())
            .Select(d => new { d.Id, d.JamaahId, d.NamaDokumen, d.TipeDokumen, d.FileUrl, d.FileSize, Status = d.Status.ToString(), d.CatatanAdmin, d.UploadedAt }));
        AddSheet(wb, "Keberangkatan", (await db.Keberangkatan.AsNoTracking().ToListAsync())
            .Select(k => new { k.Id, k.PaketId, k.KodeKeberangkatan, k.Maskapai, k.NoPenerbangan, k.BandaraAsal, k.BandaraTujuan, k.TanggalBerangkat, k.TanggalTiba, Status = k.Status.ToString(), k.Catatan }));
        AddSheet(wb, "Itinerary", (await db.ItineraryItems.AsNoTracking().ToListAsync())
            .Select(i => new { i.Id, i.PaketId, i.Hari, i.Waktu, i.Judul, i.Jenis, i.Lokasi, i.Latitude, i.Longitude, i.Deskripsi }));
        AddSheet(wb, "Produk", await db.Produk.AsNoTracking().ToListAsync());
        AddSheet(wb, "Orders", (await db.Orders.AsNoTracking().ToListAsync())
            .Select(o => new { o.Id, o.NoOrder, o.UserId, o.Total, o.StatusOrder, o.MetodePembayaran, o.CreatedAt, o.PaidAt }));
        AddSheet(wb, "OrderItems", await db.OrderItems.AsNoTracking().ToListAsync());
        AddSheet(wb, "SOS", (await db.SOSTriggers.AsNoTracking().ToListAsync())
            .Select(s => new { s.Id, s.JamaahId, s.Latitude, s.Longitude, s.Pesan, s.TriggeredAt, s.IsResolved, s.ResolvedAt, s.CatatanResolusi }));
        AddSheet(wb, "KontakDarurat", await db.KontakDarurat.AsNoTracking().ToListAsync());
        AddSheet(wb, "Asuransi", await db.Asuransi.AsNoTracking().ToListAsync());
        AddSheet(wb, "MateriManasik", await db.MateriManasik.AsNoTracking().ToListAsync());
        AddSheet(wb, "Kuis", await db.Kuis.AsNoTracking().ToListAsync());
        AddSheet(wb, "Pengumuman", (await db.Pengumuman.AsNoTracking().ToListAsync())
            .Select(p => new { p.Id, p.Judul, p.Isi, p.IsActive, TargetRole = p.TargetRole.ToString(), p.CreatedAt }));
        AddSheet(wb, "ForumTopik", (await db.ForumTopik.AsNoTracking().ToListAsync())
            .Select(t => new { t.Id, t.Judul, t.Kategori, t.UserId, t.JumlahDilihat, t.IsLocked, t.CreatedAt }));
        AddSheet(wb, "SiskohatLog", (await db.SiskohatLogs.AsNoTracking().ToListAsync())
            .Select(l => new { l.Id, l.JamaahId, l.Nik, l.Hasil, l.NoPorsi, l.Sumber, l.SyncedAt }));

        using var ms = new MemoryStream();
        wb.SaveAs(ms);
        var bytes = ms.ToArray();
        var fileName = $"holysafar-export-{DateTime.Now:yyyyMMdd-HHmmss}.xlsx";

        await CatatAsync(db, fileName, bytes.LongLength, "ExportExcel", dibuatOleh);
        return (fileName, bytes);
    }

    private static void AddSheet<T>(XLWorkbook wb, string name, IEnumerable<T> data)
    {
        var ws = wb.Worksheets.Add(name);
        var props = typeof(T).GetProperties()
            .Where(p => p.PropertyType.IsPrimitive || p.PropertyType == typeof(string) || p.PropertyType == typeof(decimal)
                        || p.PropertyType == typeof(DateTime) || p.PropertyType == typeof(DateTime?)
                        || p.PropertyType == typeof(decimal?) || p.PropertyType == typeof(int?)
                        || p.PropertyType == typeof(double?) || p.PropertyType == typeof(bool?))
            .ToList();

        for (var c = 0; c < props.Count; c++)
        {
            ws.Cell(1, c + 1).Value = props[c].Name;
            ws.Cell(1, c + 1).Style.Font.Bold = true;
        }

        var r = 2;
        foreach (var item in data)
        {
            for (var c = 0; c < props.Count; c++)
            {
                var v = props[c].GetValue(item);
                ws.Cell(r, c + 1).Value = v == null ? XLCellValue.FromObject(null) : XLCellValue.FromObject(v.ToString());
            }
            r++;
        }
        ws.Columns().AdjustToContents();
    }

    private static async Task CatatAsync(AppDbContext db, string fileName, long size, string jenis, string dibuatOleh)
    {
        db.BackupLogs.Add(new BackupLog
        {
            NamaFile = fileName, UkuranByte = size, Jenis = jenis,
            DibuatOleh = dibuatOleh, CreatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();
    }

    public async Task<List<BackupLog>> RiwayatAsync(int take = 25)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await db.BackupLogs.OrderByDescending(b => b.Id).Take(take).ToListAsync();
    }
}
