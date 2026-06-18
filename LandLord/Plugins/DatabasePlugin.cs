using System.ComponentModel;
using System.Text;
using LandLord.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.SemanticKernel;

namespace LandLord.Plugins;

/// <summary>
/// Semantic Kernel Plugin — Database queries for Tanah & Bangunan
/// </summary>
public class DatabasePlugin
{
    private readonly IServiceScopeFactory _scopeFactory;

    public DatabasePlugin(IServiceScopeFactory scopeFactory)
    {
        _scopeFactory = scopeFactory;
    }

    /// <summary>
    /// Mencari data tanah di database berdasarkan keyword.
    /// </summary>
    [KernelFunction("query_tanah")]
    [Description("Search tanah (land) data in the LandLord database by keyword. Searches across sertifikat number, location, owner name, NIB, kelurahan, and description.")]
    [return: Description("Markdown-formatted list of matching tanah records")]
    public async Task<string> QueryTanahAsync(
        [Description("Search keyword for tanah (sertifikat, location, owner, NIB)")] string keyword)
    {
        if (string.IsNullOrWhiteSpace(keyword))
            return "❌ Keyword tidak boleh kosong.";

        try
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            var kw = keyword.ToLower();
            var results = await db.Tanah
                .Where(t =>
                    t.NomorSertifikat.ToLower().Contains(kw) ||
                    t.Lokasi.ToLower().Contains(kw) ||
                    t.Pemilik.ToLower().Contains(kw) ||
                    (t.NIB != null && t.NIB.ToLower().Contains(kw)) ||
                    (t.Kelurahan != null && t.Kelurahan.ToLower().Contains(kw)) ||
                    (t.Kecamatan != null && t.Kecamatan.ToLower().Contains(kw)) ||
                    (t.KotaKabupaten != null && t.KotaKabupaten.ToLower().Contains(kw)) ||
                    (t.Keterangan != null && t.Keterangan.ToLower().Contains(kw)))
                .Take(5)
                .ToListAsync();

            if (!results.Any())
                return $"🔍 Tidak ditemukan data tanah dengan keyword **\"{keyword}\"**.";

            var sb = new StringBuilder();
            sb.AppendLine($"🏞️ **{results.Count} data tanah ditemukan** untuk \"{keyword}\":\n");
            foreach (var t in results)
            {
                sb.AppendLine($"📋 **{t.NomorSertifikat}**");
                sb.AppendLine($"   • Jenis Hak: {t.JenisHak} | Luas: {t.Luas:N0} m²");
                sb.AppendLine($"   • Lokasi: {t.Lokasi}");
                sb.AppendLine($"   • Pemilik: {t.Pemilik} | Pajak: {t.StatusPajak ?? "-"}");
                sb.AppendLine($"   • Kota: {t.KotaKabupaten}, {t.Provinsi}");
                sb.AppendLine();
            }
            return sb.ToString();
        }
        catch (Exception ex)
        {
            return $"❌ Error database: {ex.Message}";
        }
    }

    /// <summary>
    /// Mencari data bangunan di database berdasarkan keyword.
    /// </summary>
    [KernelFunction("query_bangunan")]
    [Description("Search bangunan (building) data in the LandLord database by keyword. Searches across IMB/PBG number, location, owner, building type, and function.")]
    [return: Description("Markdown-formatted list of matching bangunan records")]
    public async Task<string> QueryBangunanAsync(
        [Description("Search keyword for bangunan (IMB, location, owner, type, function)")] string keyword)
    {
        if (string.IsNullOrWhiteSpace(keyword))
            return "❌ Keyword tidak boleh kosong.";

        try
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            var kw = keyword.ToLower();
            var results = await db.Bangunan
                .Where(b =>
                    b.NomorIimbPbg.ToLower().Contains(kw) ||
                    b.Lokasi.ToLower().Contains(kw) ||
                    (b.NamaPemilik != null && b.NamaPemilik.ToLower().Contains(kw)) ||
                    b.JenisBangunan.ToLower().Contains(kw) ||
                    (b.FungsiBangunan != null && b.FungsiBangunan.ToLower().Contains(kw)) ||
                    (b.Kelurahan != null && b.Kelurahan.ToLower().Contains(kw)) ||
                    (b.Keterangan != null && b.Keterangan.ToLower().Contains(kw)))
                .Take(5)
                .ToListAsync();

            if (!results.Any())
                return $"🔍 Tidak ditemukan data bangunan dengan keyword **\"{keyword}\"**.";

            var sb = new StringBuilder();
            sb.AppendLine($"🏗️ **{results.Count} data bangunan ditemukan** untuk \"{keyword}\":\n");
            foreach (var b in results)
            {
                sb.AppendLine($"📋 **{b.NomorIimbPbg}**");
                sb.AppendLine($"   • Jenis: {b.JenisBangunan} | Luas: {b.LuasBangunan:N0} m² | Lantai: {b.JumlahLantai}");
                sb.AppendLine($"   • Lokasi: {b.Lokasi}");
                sb.AppendLine($"   • Pemilik: {b.NamaPemilik} | Status: {b.Status}");
                sb.AppendLine($"   • Fungsi: {b.FungsiBangunan} | Tahun: {b.TahunPembangunan}");
                sb.AppendLine();
            }
            return sb.ToString();
        }
        catch (Exception ex)
        {
            return $"❌ Error database: {ex.Message}";
        }
    }

    /// <summary>
    /// Menampilkan ringkasan statistik data properti.
    /// </summary>
    [KernelFunction("get_statistics")]
    [Description("Get summary statistics of land and building data: total counts, total area, tax status distribution, land rights distribution, building type distribution.")]
    [return: Description("Markdown-formatted property statistics")]
    public async Task<string> GetStatisticsAsync()
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            var totalTanah = await db.Tanah.CountAsync();
            var totalLuas = await db.Tanah.SumAsync(t => t.Luas);
            var totalBangunan = await db.Bangunan.CountAsync();
            var lunas = await db.Tanah.CountAsync(t => t.StatusPajak == "Lunas");
            var menunggak = await db.Tanah.CountAsync(t => t.StatusPajak == "Menunggak");

            var hakDist = await db.Tanah.GroupBy(t => t.JenisHak)
                .Select(g => new { Jenis = g.Key, Count = g.Count() })
                .OrderByDescending(x => x.Count).ToListAsync();

            var jenisDist = await db.Bangunan.GroupBy(b => b.JenisBangunan)
                .Select(g => new { Jenis = g.Key, Count = g.Count() })
                .OrderByDescending(x => x.Count).Take(5).ToListAsync();

            var sb = new StringBuilder();
            sb.AppendLine("📊 **Statistik LandLord**\n");
            sb.AppendLine("### 🏞️ Tanah");
            sb.AppendLine($"| Metrik | Nilai |");
            sb.AppendLine($"|--------|-------|");
            sb.AppendLine($"| Total Bidang | **{totalTanah:N0}** |");
            sb.AppendLine($"| Total Luas | **{totalLuas:N0} m²** |");
            sb.AppendLine($"| Pajak Lunas | **{lunas}** ({(totalTanah > 0 ? (double)lunas / totalTanah * 100 : 0):F1}%) |");
            sb.AppendLine($"| Pajak Menunggak | **{menunggak}** ({(totalTanah > 0 ? (double)menunggak / totalTanah * 100 : 0):F1}%) |");
            sb.AppendLine();
            sb.AppendLine("### 🏗️ Bangunan");
            sb.AppendLine($"| Metrik | Nilai |");
            sb.AppendLine($"|--------|-------|");
            sb.AppendLine($"| Total Unit | **{totalBangunan:N0}** |");
            sb.AppendLine();

            sb.AppendLine("### 📊 Distribusi Jenis Hak");
            foreach (var h in hakDist) sb.AppendLine($"- {h.Jenis}: **{h.Count}** ({((double)h.Count / totalTanah * 100):F1}%)");

            sb.AppendLine("\n### 🏢 Top 5 Jenis Bangunan");
            foreach (var j in jenisDist) sb.AppendLine($"- {j.Jenis}: **{j.Count}**");

            return sb.ToString();
        }
        catch (Exception ex)
        {
            return $"❌ Error: {ex.Message}";
        }
    }
}
