using Microsoft.EntityFrameworkCore;
using JuraganKost.Data.Context;
using JuraganKost.Data.Models;

namespace JuraganKost.Services;

/// <summary>
/// Service for complaint (komplain) management
/// </summary>
public class KomplainService
{
    private readonly AppDbContext _db;
    public KomplainService(AppDbContext db) => _db = db;

    public async Task<List<Komplain>> GetAllAsync() =>
        await _db.Komplain.Include(k => k.Penghuni).Include(k => k.Kamar)
            .OrderByDescending(k => k.CreatedAt).ToListAsync();

    public async Task<Komplain?> GetByIdAsync(int id) =>
        await _db.Komplain.Include(k => k.Penghuni).Include(k => k.Kamar)
            .FirstOrDefaultAsync(k => k.Id == id);

    public async Task<Komplain> CreateAsync(Komplain komplain)
    {
        komplain.NomorKomplain = $"CMP-{DateTime.Now:yyyyMMdd}-{new Random().Next(1000, 9999)}";
        komplain.CreatedAt = DateTime.UtcNow;
        _db.Komplain.Add(komplain);
        await _db.SaveChangesAsync();
        return komplain;
    }

    public async Task<Komplain?> UpdateStatusAsync(int id, StatusKomplain status, string? respon = null)
    {
        var komplain = await _db.Komplain.FindAsync(id);
        if (komplain == null) return null;
        komplain.Status = status;
        komplain.Respon = respon ?? komplain.Respon;
        komplain.UpdatedAt = DateTime.UtcNow;
        if (status == StatusKomplain.Selesai) komplain.SelesaiAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        return komplain;
    }

    public async Task<List<Komplain>> GetByPenghuniAsync(int penghuniId) =>
        await _db.Komplain.Where(k => k.PenghuniId == penghuniId)
            .OrderByDescending(k => k.CreatedAt).ToListAsync();
}

/// <summary>
/// Service for inventory management
/// </summary>
public class InventarisService
{
    private readonly AppDbContext _db;
    public InventarisService(AppDbContext db) => _db = db;

    public async Task<List<InventarisItem>> GetAllAsync(int? kostId = null)
    {
        var query = _db.Inventaris.Include(i => i.Kost).Include(i => i.Kamar).AsQueryable();
        if (kostId.HasValue) query = query.Where(i => i.KostId == kostId.Value);
        return await query.OrderBy(i => i.Nama).ToListAsync();
    }

    public async Task<InventarisItem?> GetByIdAsync(int id) =>
        await _db.Inventaris.Include(i => i.Kost).Include(i => i.Kamar)
            .FirstOrDefaultAsync(i => i.Id == id);

    public async Task<InventarisItem> CreateAsync(InventarisItem item)
    {
        item.CreatedAt = DateTime.UtcNow;
        _db.Inventaris.Add(item);
        await _db.SaveChangesAsync();
        return item;
    }

    public async Task<InventarisItem?> UpdateAsync(InventarisItem item)
    {
        var existing = await _db.Inventaris.FindAsync(item.Id);
        if (existing == null) return null;
        existing.Nama = item.Nama;
        existing.Kode = item.Kode;
        existing.Kategori = item.Kategori;
        existing.Jumlah = item.Jumlah;
        existing.Status = item.Status;
        existing.Deskripsi = item.Deskripsi;
        existing.HargaBeli = item.HargaBeli;
        existing.TanggalBeli = item.TanggalBeli;
        existing.TanggalPerawatan = item.TanggalPerawatan;
        existing.KamarId = item.KamarId;
        await _db.SaveChangesAsync();
        return existing;
    }

    public async Task<bool> DeleteAsync(int id)
    {
        var item = await _db.Inventaris.FindAsync(id);
        if (item == null) return false;
        _db.Inventaris.Remove(item);
        await _db.SaveChangesAsync();
        return true;
    }
}

/// <summary>
/// Service for staff management
/// </summary>
public class StaffService
{
    private readonly AppDbContext _db;
    public StaffService(AppDbContext db) => _db = db;

    public async Task<List<Staff>> GetAllAsync(int? kostId = null)
    {
        var query = _db.Staff.Include(s => s.Kost).AsQueryable();
        if (kostId.HasValue) query = query.Where(s => s.KostId == kostId.Value);
        return await query.OrderBy(s => s.Nama).ToListAsync();
    }

    public async Task<Staff?> GetByIdAsync(int id) =>
        await _db.Staff.Include(s => s.Kost).FirstOrDefaultAsync(s => s.Id == id);

    public async Task<Staff> CreateAsync(Staff staff)
    {
        staff.CreatedAt = DateTime.UtcNow;
        _db.Staff.Add(staff);
        await _db.SaveChangesAsync();
        return staff;
    }

    public async Task<Staff?> UpdateAsync(Staff staff)
    {
        var existing = await _db.Staff.FindAsync(staff.Id);
        if (existing == null) return null;
        existing.Nama = staff.Nama;
        existing.Posisi = staff.Posisi;
        existing.NoHP = staff.NoHP;
        existing.Email = staff.Email;
        existing.Gaji = staff.Gaji;
        existing.JadwalKerja = staff.JadwalKerja;
        existing.Status = staff.Status;
        await _db.SaveChangesAsync();
        return existing;
    }

    public async Task<bool> DeleteAsync(int id)
    {
        var staff = await _db.Staff.FindAsync(id);
        if (staff == null) return false;
        _db.Staff.Remove(staff);
        await _db.SaveChangesAsync();
        return true;
    }
}

/// <summary>
/// Service for contract (kontrak) management
/// </summary>
public class KontrakService
{
    private readonly AppDbContext _db;
    public KontrakService(AppDbContext db) => _db = db;

    public async Task<List<Kontrak>> GetAllAsync() =>
        await _db.Kontrak.Include(k => k.Penghuni).Include(k => k.Kamar)
            .OrderByDescending(k => k.CreatedAt).ToListAsync();

    public async Task<Kontrak?> GetByIdAsync(int id) =>
        await _db.Kontrak.Include(k => k.Penghuni).Include(k => k.Kamar)
            .FirstOrDefaultAsync(k => k.Id == id);

    public async Task<Kontrak> CreateAsync(Kontrak kontrak)
    {
        kontrak.NomorKontrak = $"KTR-{DateTime.Now:yyyyMMdd}-{new Random().Next(1000, 9999)}";
        kontrak.CreatedAt = DateTime.UtcNow;
        _db.Kontrak.Add(kontrak);
        await _db.SaveChangesAsync();
        return kontrak;
    }

    public async Task<Kontrak?> UpdateAsync(Kontrak kontrak)
    {
        var existing = await _db.Kontrak.FindAsync(kontrak.Id);
        if (existing == null) return null;
        existing.TanggalMulai = kontrak.TanggalMulai;
        existing.TanggalSelesai = kontrak.TanggalSelesai;
        existing.HargaSewa = kontrak.HargaSewa;
        existing.Deposit = kontrak.Deposit;
        existing.DendaPerHari = kontrak.DendaPerHari;
        existing.Status = kontrak.Status;
        existing.Catatan = kontrak.Catatan;
        existing.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        return existing;
    }
}

/// <summary>
/// Service for review & rating management
/// </summary>
public class ReviewService
{
    private readonly AppDbContext _db;
    public ReviewService(AppDbContext db) => _db = db;

    public async Task<List<Review>> GetByKostAsync(int kostId) =>
        await _db.Review.Include(r => r.Penghuni)
            .Where(r => r.KostId == kostId)
            .OrderByDescending(r => r.CreatedAt).ToListAsync();

    public async Task<Review> CreateAsync(Review review)
    {
        review.CreatedAt = DateTime.UtcNow;
        _db.Review.Add(review);
        await _db.SaveChangesAsync();
        return review;
    }

    public async Task<double> GetAverageRatingAsync(int kostId) =>
        await _db.Review.Where(r => r.KostId == kostId).AverageAsync(r => (double?)r.Rating) ?? 0;
}

/// <summary>
/// Service for notification management
/// </summary>
public class NotifikasiService
{
    private readonly AppDbContext _db;
    public NotifikasiService(AppDbContext db) => _db = db;

    public async Task<List<Notifikasi>> GetByUserAsync(string userId) =>
        await _db.Notifikasi.Where(n => n.UserId == userId)
            .OrderByDescending(n => n.CreatedAt).Take(50).ToListAsync();

    public async Task<int> GetUnreadCountAsync(string userId) =>
        await _db.Notifikasi.CountAsync(n => n.UserId == userId && !n.IsDibaca);

    public async Task SendAsync(string? userId, string judul, string pesan, TipeNotifikasi tipe, string? link = null)
    {
        _db.Notifikasi.Add(new Notifikasi
        {
            UserId = userId, Judul = judul, Pesan = pesan, Tipe = tipe,
            LinkUrl = link, CreatedAt = DateTime.UtcNow
        });
        await _db.SaveChangesAsync();
    }

    public async Task MarkReadAsync(int id)
    {
        var n = await _db.Notifikasi.FindAsync(id);
        if (n != null) { n.IsDibaca = true; n.DibacaAt = DateTime.UtcNow; await _db.SaveChangesAsync(); }
    }
}

/// <summary>
/// Service for IoT sensor data management
/// </summary>
public class IoTService
{
    private readonly AppDbContext _db;
    public IoTService(AppDbContext db) => _db = db;

    public async Task<List<IoTSensorData>> GetLatestAsync(int? kamarId = null)
    {
        var query = _db.IoTSensorData.Include(s => s.Kamar).AsQueryable();
        if (kamarId.HasValue) query = query.Where(s => s.KamarId == kamarId.Value);

        return await query.GroupBy(s => new { s.DeviceId, s.Jenis })
            .Select(g => g.OrderByDescending(s => s.Timestamp).First())
            .ToListAsync();
    }

    public async Task<List<IoTSensorData>> GetHistoryAsync(string deviceId, int hours = 24) =>
        await _db.IoTSensorData.Where(s => s.DeviceId == deviceId &&
            s.Timestamp >= DateTime.UtcNow.AddHours(-hours))
            .OrderByDescending(s => s.Timestamp).Take(100).ToListAsync();

    public async Task<IoTSensorData> RecordAsync(IoTSensorData data)
    {
        data.Timestamp = DateTime.UtcNow;
        _db.IoTSensorData.Add(data);
        await _db.SaveChangesAsync();
        return data;
    }

    /// <summary>Simulate IoT data for demo purposes</summary>
    public async Task<List<IoTSensorData>> SimulateAsync(int kostId)
    {
        var kamar = await _db.Kamar.Where(k => k.KostId == kostId).ToListAsync();
        var random = new Random();
        var results = new List<IoTSensorData>();

        foreach (var k in kamar)
        {
            var sensors = new List<IoTSensorData>
            {
                new() { DeviceId = $"SENSOR-LISTRIK-{k.Id}", KamarId = k.Id, Jenis = JenisSensor.Listrik_kWh,
                    Nilai = Math.Round(random.NextDouble() * 5, 2), Satuan = "kWh" },
                new() { DeviceId = $"SENSOR-AIR-{k.Id}", KamarId = k.Id, Jenis = JenisSensor.Air_Liter,
                    Nilai = Math.Round(random.NextDouble() * 50, 2), Satuan = "Liter" },
                new() { DeviceId = $"SENSOR-SUHU-{k.Id}", KamarId = k.Id, Jenis = JenisSensor.Suhu_C,
                    Nilai = Math.Round(24 + random.NextDouble() * 8, 1), Satuan = "°C" },
                new() { DeviceId = $"SENSOR-LEMBAB-{k.Id}", KamarId = k.Id, Jenis = JenisSensor.Kelembaban_Persen,
                    Nilai = Math.Round(50 + random.NextDouble() * 30, 1), Satuan = "%" }
            };

            foreach (var s in sensors)
            {
                s.Timestamp = DateTime.UtcNow;
                _db.IoTSensorData.Add(s);
                results.Add(s);
            }
        }
        await _db.SaveChangesAsync();
        return results;
    }
}
