using Microsoft.EntityFrameworkCore;
using JuraganKost.Data.Context;
using JuraganKost.Data.Models;

namespace JuraganKost.Services;

/// <summary>
/// Service for managing Kost (boarding house) operations
/// </summary>
public class KostService
{
    private readonly AppDbContext _db;

    public KostService(AppDbContext db) => _db = db;

    public async Task<List<Kost>> GetAllAsync() =>
        await _db.Kost.Include(k => k.Kamar).OrderByDescending(k => k.CreatedAt).ToListAsync();

    public async Task<Kost?> GetByIdAsync(int id) =>
        await _db.Kost.Include(k => k.Kamar).Include(k => k.Staff)
            .FirstOrDefaultAsync(k => k.Id == id);

    public async Task<Kost> CreateAsync(Kost kost)
    {
        kost.CreatedAt = DateTime.UtcNow;
        _db.Kost.Add(kost);
        await _db.SaveChangesAsync();
        return kost;
    }

    public async Task<Kost?> UpdateAsync(Kost kost)
    {
        var existing = await _db.Kost.FindAsync(kost.Id);
        if (existing == null) return null;
        existing.Nama = kost.Nama;
        existing.Alamat = kost.Alamat;
        existing.Kota = kost.Kota;
        existing.Provinsi = kost.Provinsi;
        existing.KodePos = kost.KodePos;
        existing.Deskripsi = kost.Deskripsi;
        existing.Telepon = kost.Telepon;
        existing.Email = kost.Email;
        existing.Jenis = kost.Jenis;
        existing.Status = kost.Status;
        existing.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        return existing;
    }

    public async Task<bool> DeleteAsync(int id)
    {
        var kost = await _db.Kost.FindAsync(id);
        if (kost == null) return false;
        _db.Kost.Remove(kost);
        await _db.SaveChangesAsync();
        return true;
    }

    /// <summary>Get dashboard summary for a kost</summary>
    public async Task<DashboardSummary> GetDashboardAsync(int kostId)
    {
        var kost = await _db.Kost.Include(k => k.Kamar).FirstOrDefaultAsync(k => k.Id == kostId);
        if (kost == null) return new();

        var totalKamar = kost.Kamar.Count;
        var terisi = kost.Kamar.Count(k => k.Status == StatusKamar.Terisi);
        var kosong = kost.Kamar.Count(k => k.Status == StatusKamar.Kosong);
        var booking = kost.Kamar.Count(k => k.Status == StatusKamar.Booking);

        var pemasukanBulanIni = await _db.Pembayaran
            .Where(p => p.Status == StatusPembayaran.Diverifikasi &&
                        p.TanggalBayar.Month == DateTime.Now.Month &&
                        p.TanggalBayar.Year == DateTime.Now.Year)
            .SumAsync(p => p.Jumlah);

        // EF Core cannot translate computed property Tagihan.Total to SQL.
        // Use the raw expression: Jumlah + Denda - Diskon
        var piutang = await _db.Tagihan
            .Where(t => t.Status == StatusTagihan.BelumDibayar || t.Status == StatusTagihan.Terlambat)
            .SumAsync(t => t.Jumlah + (t.Denda ?? 0) - (t.Diskon ?? 0));

        return new DashboardSummary
        {
            TotalKamar = totalKamar,
            KamarTerisi = terisi,
            KamarKosong = kosong,
            KamarBooking = booking,
            Okupansi = totalKamar > 0 ? Math.Round((double)terisi / totalKamar * 100, 1) : 0,
            PemasukanBulanIni = pemasukanBulanIni,
            Piutang = piutang,
            TotalPenghuni = await _db.Penghuni.CountAsync(p => p.Status == StatusPenghuni.Aktif),
            KomplainPending = await _db.Komplain.CountAsync(k => k.Status == StatusKomplain.Menunggu)
        };
    }
}

public class DashboardSummary
{
    public int TotalKamar { get; set; }
    public int KamarTerisi { get; set; }
    public int KamarKosong { get; set; }
    public int KamarBooking { get; set; }
    public double Okupansi { get; set; }
    public decimal PemasukanBulanIni { get; set; }
    public decimal Piutang { get; set; }
    public int TotalPenghuni { get; set; }
    public int KomplainPending { get; set; }
}
