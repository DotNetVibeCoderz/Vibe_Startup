using LandLord.Data;
using LandLord.Models;
using Microsoft.EntityFrameworkCore;

namespace LandLord.Services;

/// <summary>
/// Service untuk CRUD data bangunan
/// </summary>
public class BangunanService : IBangunanService
{
    private readonly AppDbContext _context;

    public BangunanService(AppDbContext context)
    {
        _context = context;
    }

    public async Task<List<Bangunan>> GetAllAsync()
    {
        return await _context.Bangunan
            .OrderByDescending(b => b.UpdatedAt)
            .ToListAsync();
    }

    public async Task<Bangunan?> GetByIdAsync(int id)
    {
        return await _context.Bangunan
            .Include(b => b.Documents)
            .FirstOrDefaultAsync(b => b.Id == id);
    }

    public async Task<Bangunan> CreateAsync(Bangunan bangunan)
    {
        bangunan.CreatedAt = DateTime.UtcNow;
        bangunan.UpdatedAt = DateTime.UtcNow;
        _context.Bangunan.Add(bangunan);
        await _context.SaveChangesAsync();
        return bangunan;
    }

    public async Task<Bangunan> UpdateAsync(Bangunan bangunan)
    {
        var existing = await _context.Bangunan.FindAsync(bangunan.Id);
        if (existing == null)
            throw new KeyNotFoundException($"Bangunan dengan ID {bangunan.Id} tidak ditemukan.");

        existing.NomorIimbPbg = bangunan.NomorIimbPbg;
        existing.NomorSertifikatTanah = bangunan.NomorSertifikatTanah;
        existing.JenisBangunan = bangunan.JenisBangunan;
        existing.JumlahLantai = bangunan.JumlahLantai;
        existing.LuasBangunan = bangunan.LuasBangunan;
        existing.MaterialUtama = bangunan.MaterialUtama;
        existing.TahunPembangunan = bangunan.TahunPembangunan;
        existing.FungsiBangunan = bangunan.FungsiBangunan;
        existing.Kepemilikan = bangunan.Kepemilikan;
        existing.Lokasi = bangunan.Lokasi;
        existing.Kelurahan = bangunan.Kelurahan;
        existing.Kecamatan = bangunan.Kecamatan;
        existing.KotaKabupaten = bangunan.KotaKabupaten;
        existing.Provinsi = bangunan.Provinsi;
        existing.Latitude = bangunan.Latitude;
        existing.Longitude = bangunan.Longitude;
        existing.PolygonGeoJson = bangunan.PolygonGeoJson;
        existing.NamaPemilik = bangunan.NamaPemilik;
        existing.NikPemilik = bangunan.NikPemilik;
        existing.Keterangan = bangunan.Keterangan;
        existing.NilaiBangunan = bangunan.NilaiBangunan;
        existing.Status = bangunan.Status;
        existing.TanggalIimbPbg = bangunan.TanggalIimbPbg;
        existing.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();
        return existing;
    }

    public async Task<bool> DeleteAsync(int id)
    {
        var bangunan = await _context.Bangunan.FindAsync(id);
        if (bangunan == null) return false;

        _context.Bangunan.Remove(bangunan);
        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<List<Bangunan>> SearchAsync(string keyword)
    {
        if (string.IsNullOrWhiteSpace(keyword))
            return await GetAllAsync();

        var kw = keyword.ToLower();
        return await _context.Bangunan
            .Where(b =>
                b.NomorIimbPbg.ToLower().Contains(kw) ||
                b.Lokasi.ToLower().Contains(kw) ||
                (b.NamaPemilik != null && b.NamaPemilik.ToLower().Contains(kw)) ||
                b.JenisBangunan.ToLower().Contains(kw) ||
                (b.FungsiBangunan != null && b.FungsiBangunan.ToLower().Contains(kw)) ||
                (b.Kelurahan != null && b.Kelurahan.ToLower().Contains(kw)) ||
                (b.Kecamatan != null && b.Kecamatan.ToLower().Contains(kw)) ||
                (b.Keterangan != null && b.Keterangan.ToLower().Contains(kw)))
            .OrderByDescending(b => b.UpdatedAt)
            .ToListAsync();
    }

    public async Task<List<Bangunan>> FilterAsync(string? jenisBangunan, string? fungsi, string? status)
    {
        var query = _context.Bangunan.AsQueryable();

        if (!string.IsNullOrWhiteSpace(jenisBangunan))
            query = query.Where(b => b.JenisBangunan == jenisBangunan);

        if (!string.IsNullOrWhiteSpace(fungsi))
            query = query.Where(b => b.FungsiBangunan == fungsi);

        if (!string.IsNullOrWhiteSpace(status))
            query = query.Where(b => b.Status == status);

        return await query.OrderByDescending(b => b.UpdatedAt).ToListAsync();
    }

    public async Task<int> GetTotalCountAsync()
    {
        return await _context.Bangunan.CountAsync();
    }

    public async Task<Dictionary<string, int>> GetDistribusiJenisAsync()
    {
        return await _context.Bangunan
            .GroupBy(b => b.JenisBangunan)
            .Select(g => new { Jenis = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Jenis, x => x.Count);
    }

    public async Task<Dictionary<string, int>> GetDistribusiFungsiAsync()
    {
        return await _context.Bangunan
            .Where(b => b.FungsiBangunan != null)
            .GroupBy(b => b.FungsiBangunan!)
            .Select(g => new { Fungsi = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Fungsi, x => x.Count);
    }
}
