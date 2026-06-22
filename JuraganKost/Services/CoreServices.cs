using Microsoft.EntityFrameworkCore;
using JuraganKost.Data.Context;
using JuraganKost.Data.Models;

namespace JuraganKost.Services;

/// <summary>
/// Service for room (kamar) management with full CRUD, filtering, sorting, paging
/// </summary>
public class KamarService
{
    private readonly AppDbContext _db;
    public KamarService(AppDbContext db) => _db = db;

    public async Task<List<Kamar>> GetAllAsync(int? kostId = null)
    {
        var query = _db.Kamar.Include(k => k.Kost).AsQueryable();
        if (kostId.HasValue) query = query.Where(k => k.KostId == kostId.Value);
        return await query.OrderBy(k => k.NomorKamar).ToListAsync();
    }

    public async Task<Kamar?> GetByIdAsync(int id) =>
        await _db.Kamar.Include(k => k.Kost).Include(k => k.Penghuni)
            .FirstOrDefaultAsync(k => k.Id == id);

    public async Task<Kamar> CreateAsync(Kamar kamar)
    {
        kamar.CreatedAt = DateTime.UtcNow;
        _db.Kamar.Add(kamar);
        await _db.SaveChangesAsync();
        return kamar;
    }

    public async Task<Kamar?> UpdateAsync(Kamar kamar)
    {
        var existing = await _db.Kamar.FindAsync(kamar.Id);
        if (existing == null) return null;
        existing.NomorKamar = kamar.NomorKamar;
        existing.HargaSewa = kamar.HargaSewa;
        existing.Deposit = kamar.Deposit;
        existing.Luas = kamar.Luas;
        existing.Status = kamar.Status;
        existing.Jenis = kamar.Jenis;
        existing.Kapasitas = kamar.Kapasitas;
        existing.Fasilitas = kamar.Fasilitas;
        existing.Deskripsi = kamar.Deskripsi;
        existing.IsTersedia = kamar.IsTersedia;
        existing.GambarUrl = kamar.GambarUrl;
        existing.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        return existing;
    }

    public async Task<bool> DeleteAsync(int id)
    {
        var kamar = await _db.Kamar.FindAsync(id);
        if (kamar == null) return false;
        _db.Kamar.Remove(kamar);
        await _db.SaveChangesAsync();
        return true;
    }

    /// <summary>Filter rooms by status, type, price range</summary>
    public async Task<List<Kamar>> FilterAsync(int? kostId, StatusKamar? status, JenisKamar? jenis,
        decimal? minHarga, decimal? maxHarga, string? search)
    {
        var query = _db.Kamar.Include(k => k.Kost).AsQueryable();
        if (kostId.HasValue) query = query.Where(k => k.KostId == kostId.Value);
        if (status.HasValue) query = query.Where(k => k.Status == status.Value);
        if (jenis.HasValue) query = query.Where(k => k.Jenis == jenis.Value);
        if (minHarga.HasValue) query = query.Where(k => k.HargaSewa >= minHarga.Value);
        if (maxHarga.HasValue) query = query.Where(k => k.HargaSewa <= maxHarga.Value);
        if (!string.IsNullOrWhiteSpace(search))
            query = query.Where(k => k.NomorKamar.Contains(search) || (k.Deskripsi != null && k.Deskripsi.Contains(search)));
        return await query.OrderBy(k => k.NomorKamar).ToListAsync();
    }
}

/// <summary>
/// Service for tenant (penghuni) management
/// </summary>
public class PenghuniService
{
    private readonly AppDbContext _db;
    public PenghuniService(AppDbContext db) => _db = db;

    public async Task<List<Penghuni>> GetAllAsync() =>
        await _db.Penghuni.Include(p => p.Kamar).ThenInclude(k => k!.Kost)
            .OrderByDescending(p => p.CreatedAt).ToListAsync();

    public async Task<Penghuni?> GetByIdAsync(int id) =>
        await _db.Penghuni.Include(p => p.Kamar).Include(p => p.Kontrak)
            .FirstOrDefaultAsync(p => p.Id == id);

    public async Task<Penghuni> CreateAsync(Penghuni penghuni)
    {
        penghuni.CreatedAt = DateTime.UtcNow;
        _db.Penghuni.Add(penghuni);
        await _db.SaveChangesAsync();
        return penghuni;
    }

    public async Task<Penghuni?> UpdateAsync(Penghuni penghuni)
    {
        var existing = await _db.Penghuni.FindAsync(penghuni.Id);
        if (existing == null) return null;
        existing.NamaLengkap = penghuni.NamaLengkap;
        existing.NIK = penghuni.NIK;
        existing.NoHP = penghuni.NoHP;
        existing.Email = penghuni.Email;
        existing.Pekerjaan = penghuni.Pekerjaan;
        existing.KontakDarurat = penghuni.KontakDarurat;
        existing.HubunganKontakDarurat = penghuni.HubunganKontakDarurat;
        existing.AlamatAsal = penghuni.AlamatAsal;
        existing.Status = penghuni.Status;
        existing.KamarId = penghuni.KamarId;
        existing.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        return existing;
    }

    public async Task<bool> DeleteAsync(int id)
    {
        var penghuni = await _db.Penghuni.FindAsync(id);
        if (penghuni == null) return false;
        _db.Penghuni.Remove(penghuni);
        await _db.SaveChangesAsync();
        return true;
    }
}

/// <summary>
/// Service for billing (tagihan) management
/// </summary>
public class TagihanService
{
    private readonly AppDbContext _db;
    public TagihanService(AppDbContext db) => _db = db;

    public async Task<List<Tagihan>> GetAllAsync() =>
        await _db.Tagihan.Include(t => t.Penghuni).Include(t => t.Kamar)
            .OrderByDescending(t => t.CreatedAt).ToListAsync();

    public async Task<Tagihan?> GetByIdAsync(int id) =>
        await _db.Tagihan.Include(t => t.Penghuni).Include(t => t.Kamar)
            .FirstOrDefaultAsync(t => t.Id == id);

    public async Task<Tagihan> CreateAsync(Tagihan tagihan)
    {
        tagihan.CreatedAt = DateTime.UtcNow;
        _db.Tagihan.Add(tagihan);
        await _db.SaveChangesAsync();
        return tagihan;
    }

    public async Task<Tagihan?> UpdateAsync(Tagihan tagihan)
    {
        var existing = await _db.Tagihan.FindAsync(tagihan.Id);
        if (existing == null) return null;
        existing.Jumlah = tagihan.Jumlah;
        existing.Denda = tagihan.Denda;
        existing.Diskon = tagihan.Diskon;
        existing.Status = tagihan.Status;
        existing.JatuhTempo = tagihan.JatuhTempo;
        existing.Deskripsi = tagihan.Deskripsi;
        await _db.SaveChangesAsync();
        return existing;
    }

    /// <summary>Generate auto billing for all active tenants</summary>
    public async Task<int> GenerateMonthlyBillingsAsync()
    {
        int count = 0;
        var penghuniAktif = await _db.Penghuni.Include(p => p.Kamar)
            .Where(p => p.Status == StatusPenghuni.Aktif && p.KamarId != null).ToListAsync();

        foreach (var p in penghuniAktif)
        {
            var existing = await _db.Tagihan.AnyAsync(t =>
                t.PenghuniId == p.Id && t.JatuhTempo.Month == DateTime.Now.Month &&
                t.JatuhTempo.Year == DateTime.Now.Year && t.Jenis == JenisTagihan.SewaKamar);
            if (existing) continue;

            var tagihan = new Tagihan
            {
                NomorTagihan = $"INV-{DateTime.Now:yyyyMM}-{p.Id:D4}-{new Random().Next(100, 999)}",
                PenghuniId = p.Id,
                KamarId = p.KamarId,
                Jenis = JenisTagihan.SewaKamar,
                Jumlah = p.Kamar?.HargaSewa ?? 0,
                JatuhTempo = new DateTime(DateTime.Now.Year, DateTime.Now.Month, 10),
                Status = StatusTagihan.BelumDibayar,
                Deskripsi = $"Tagihan sewa kamar {DateTime.Now:MMMM yyyy}"
            };
            _db.Tagihan.Add(tagihan);
            count++;
        }
        await _db.SaveChangesAsync();
        return count;
    }
}

/// <summary>
/// Service for payment (pembayaran) management
/// </summary>
public class PembayaranService
{
    private readonly AppDbContext _db;
    public PembayaranService(AppDbContext db) => _db = db;

    public async Task<List<Pembayaran>> GetAllAsync() =>
        await _db.Pembayaran.Include(p => p.Penghuni).Include(p => p.Tagihan)
            .OrderByDescending(p => p.TanggalBayar).ToListAsync();

    public async Task<Pembayaran?> GetByIdAsync(int id) =>
        await _db.Pembayaran.Include(p => p.Penghuni).Include(p => p.Tagihan)
            .FirstOrDefaultAsync(p => p.Id == id);

    public async Task<Pembayaran> CreateAsync(Pembayaran pembayaran)
    {
        pembayaran.NomorPembayaran = $"PAY-{DateTime.Now:yyyyMMdd}-{new Random().Next(1000, 9999)}";
        _db.Pembayaran.Add(pembayaran);
        await _db.SaveChangesAsync();
        return pembayaran;
    }

    public async Task<bool> VerifikasiAsync(int id, bool diterima, string? catatan = null)
    {
        var pembayaran = await _db.Pembayaran.Include(p => p.Tagihan).FirstOrDefaultAsync(p => p.Id == id);
        if (pembayaran == null) return false;

        pembayaran.Status = diterima ? StatusPembayaran.Diverifikasi : StatusPembayaran.Ditolak;
        pembayaran.Catatan = catatan;

        if (diterima && pembayaran.Tagihan != null)
        {
            pembayaran.Tagihan.Status = StatusTagihan.Dibayar;
            pembayaran.Tagihan.TanggalBayar = DateTime.UtcNow;
        }
        await _db.SaveChangesAsync();
        return true;
    }
}
