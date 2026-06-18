using LandLord.Data;
using LandLord.Models;
using Microsoft.EntityFrameworkCore;

namespace LandLord.Services;

/// <summary>
/// Service untuk CRUD data tanah
/// </summary>
public class TanahService : ITanahService
{
    private readonly AppDbContext _context;

    public TanahService(AppDbContext context)
    {
        _context = context;
    }

    public async Task<List<Tanah>> GetAllAsync()
    {
        return await _context.Tanah
            .OrderByDescending(t => t.UpdatedAt)
            .ToListAsync();
    }

    public async Task<Tanah?> GetByIdAsync(int id)
    {
        return await _context.Tanah
            .Include(t => t.Documents)
            .FirstOrDefaultAsync(t => t.Id == id);
    }

    public async Task<Tanah> CreateAsync(Tanah tanah)
    {
        tanah.CreatedAt = DateTime.UtcNow;
        tanah.UpdatedAt = DateTime.UtcNow;
        _context.Tanah.Add(tanah);
        await _context.SaveChangesAsync();
        return tanah;
    }

    public async Task<Tanah> UpdateAsync(Tanah tanah)
    {
        var existing = await _context.Tanah.FindAsync(tanah.Id);
        if (existing == null)
            throw new KeyNotFoundException($"Tanah dengan ID {tanah.Id} tidak ditemukan.");

        // Update semua properti
        existing.NomorSertifikat = tanah.NomorSertifikat;
        existing.JenisHak = tanah.JenisHak;
        existing.Luas = tanah.Luas;
        existing.Lokasi = tanah.Lokasi;
        existing.NIB = tanah.NIB;
        existing.Kelurahan = tanah.Kelurahan;
        existing.Kecamatan = tanah.Kecamatan;
        existing.KotaKabupaten = tanah.KotaKabupaten;
        existing.Provinsi = tanah.Provinsi;
        existing.KodePos = tanah.KodePos;
        existing.Latitude = tanah.Latitude;
        existing.Longitude = tanah.Longitude;
        existing.PolygonGeoJson = tanah.PolygonGeoJson;
        existing.NilaiNjopPerMeter = tanah.NilaiNjopPerMeter;
        existing.TotalNjop = tanah.TotalNjop;
        existing.PajakTahunan = tanah.PajakTahunan;
        existing.StatusPajak = tanah.StatusPajak;
        existing.Pemilik = tanah.Pemilik;
        existing.NikPemilik = tanah.NikPemilik;
        existing.AlamatPemilik = tanah.AlamatPemilik;
        existing.Keterangan = tanah.Keterangan;
        existing.TanggalSertifikat = tanah.TanggalSertifikat;
        existing.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();
        return existing;
    }

    public async Task<bool> DeleteAsync(int id)
    {
        var tanah = await _context.Tanah.FindAsync(id);
        if (tanah == null) return false;

        _context.Tanah.Remove(tanah);
        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<List<Tanah>> SearchAsync(string keyword)
    {
        if (string.IsNullOrWhiteSpace(keyword))
            return await GetAllAsync();

        var kw = keyword.ToLower();
        return await _context.Tanah
            .Where(t =>
                t.NomorSertifikat.ToLower().Contains(kw) ||
                t.Lokasi.ToLower().Contains(kw) ||
                t.Pemilik.ToLower().Contains(kw) ||
                t.NIB.ToLower().Contains(kw) ||
                t.Kelurahan.ToLower().Contains(kw) ||
                t.Kecamatan.ToLower().Contains(kw) ||
                t.KotaKabupaten.ToLower().Contains(kw) ||
                (t.Keterangan != null && t.Keterangan.ToLower().Contains(kw)))
            .OrderByDescending(t => t.UpdatedAt)
            .ToListAsync();
    }

    public async Task<List<Tanah>> FilterAsync(string? jenisHak, string? statusPajak, string? kota)
    {
        var query = _context.Tanah.AsQueryable();

        if (!string.IsNullOrWhiteSpace(jenisHak))
            query = query.Where(t => t.JenisHak == jenisHak);

        if (!string.IsNullOrWhiteSpace(statusPajak))
            query = query.Where(t => t.StatusPajak == statusPajak);

        if (!string.IsNullOrWhiteSpace(kota))
            query = query.Where(t => t.KotaKabupaten != null && t.KotaKabupaten.Contains(kota));

        return await query.OrderByDescending(t => t.UpdatedAt).ToListAsync();
    }

    public async Task<int> GetTotalCountAsync()
    {
        return await _context.Tanah.CountAsync();
    }

    public async Task<decimal> GetTotalLuasAsync()
    {
        return await _context.Tanah.SumAsync(t => t.Luas);
    }

    public async Task<Dictionary<string, int>> GetDistribusiJenisHakAsync()
    {
        return await _context.Tanah
            .GroupBy(t => t.JenisHak)
            .Select(g => new { JenisHak = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.JenisHak, x => x.Count);
    }

    public async Task<Dictionary<string, int>> GetDistribusiStatusPajakAsync()
    {
        return await _context.Tanah
            .GroupBy(t => t.StatusPajak ?? "Unknown")
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Status, x => x.Count);
    }
}
