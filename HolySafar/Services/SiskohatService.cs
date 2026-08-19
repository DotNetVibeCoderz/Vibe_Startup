using System.Net.Http.Json;
using System.Text.Json;
using HolySafar.Data;
using HolySafar.Models;
using Microsoft.EntityFrameworkCore;

namespace HolySafar.Services;

/// <summary>
/// Integrasi SISKOHAT Kemenag untuk validasi data jamaah (requirements: Integrasi Pemerintah).
///
/// SISKOHAT tidak menyediakan API publik — endpoint dan API key diisi admin di
/// menu Pengaturan (Siskohat:Endpoint, Siskohat:ApiKey). Selama endpoint kosong,
/// service berjalan dalam MODE SIMULASI: validasi format NIK/paspor dilakukan lokal
/// dan nomor porsi dibangkitkan deterministik, sehingga alur UI tetap bisa dipakai
/// dan tinggal diarahkan ke endpoint asli saat kerja sama data sudah tersedia.
/// </summary>
public class SiskohatService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IHttpClientFactory _httpFactory;
    private readonly SettingsService _settings;
    private readonly ILogger<SiskohatService> _logger;

    public SiskohatService(IServiceScopeFactory scopeFactory, IHttpClientFactory httpFactory,
        SettingsService settings, ILogger<SiskohatService> logger)
    { _scopeFactory = scopeFactory; _httpFactory = httpFactory; _settings = settings; _logger = logger; }

    public string Endpoint => _settings.Get("Siskohat:Endpoint");
    public bool IsLive => !string.IsNullOrWhiteSpace(Endpoint);
    public string Mode => IsLive ? "Live" : "Simulasi";

    /// <summary>Sinkronisasi satu jamaah. Hasil dicatat ke SiskohatLog dan menempel di entitas Jamaah.</summary>
    public async Task<SiskohatLog> SyncJamaahAsync(int jamaahId)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var jamaah = await db.Jamaah.FirstOrDefaultAsync(j => j.Id == jamaahId);

        if (jamaah == null)
        {
            var missing = new SiskohatLog { JamaahId = jamaahId, Hasil = "Error", Pesan = "Jamaah tidak ditemukan.", Sumber = Mode };
            db.SiskohatLogs.Add(missing);
            await db.SaveChangesAsync();
            return missing;
        }

        var log = IsLive ? await SyncLiveAsync(jamaah) : SyncSimulasi(jamaah);
        log.JamaahId = jamaah.Id;
        log.Nik = jamaah.Nik;
        log.Sumber = Mode;
        log.SyncedAt = DateTime.UtcNow;

        jamaah.SiskohatStatus = log.Hasil;
        jamaah.SiskohatSyncedAt = log.SyncedAt;
        if (!string.IsNullOrEmpty(log.NoPorsi)) jamaah.NoPorsi = log.NoPorsi;

        db.SiskohatLogs.Add(log);
        await db.SaveChangesAsync();
        return log;
    }

    /// <summary>Sinkronisasi massal seluruh jamaah yang punya NIK.</summary>
    public async Task<(int Total, int Valid, int Bermasalah)> SyncSemuaAsync()
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var ids = await db.Jamaah.Where(j => j.Nik != "").Select(j => j.Id).ToListAsync();

        int valid = 0, bermasalah = 0;
        foreach (var id in ids)
        {
            var log = await SyncJamaahAsync(id);
            if (log.Hasil == "Valid") valid++; else bermasalah++;
        }
        return (ids.Count, valid, bermasalah);
    }

    private async Task<SiskohatLog> SyncLiveAsync(Jamaah j)
    {
        try
        {
            var http = _httpFactory.CreateClient();
            http.Timeout = TimeSpan.FromSeconds(30);
            var apiKey = _settings.Get("Siskohat:ApiKey");
            if (!string.IsNullOrEmpty(apiKey)) http.DefaultRequestHeaders.Add("X-Api-Key", apiKey);

            var res = await http.PostAsJsonAsync(Endpoint, new
            {
                nik = j.Nik,
                nama = j.NamaLengkap,
                no_paspor = j.NoPaspor,
                tanggal_lahir = j.TanggalLahir?.ToString("yyyy-MM-dd")
            });

            var body = await res.Content.ReadAsStringAsync();
            if (!res.IsSuccessStatusCode)
                return new SiskohatLog { Hasil = "Error", Pesan = $"HTTP {(int)res.StatusCode}: {Potong(body)}" };

            using var doc = JsonDocument.Parse(body);
            var root = doc.RootElement;
            var status = root.TryGetProperty("status", out var s) ? (s.GetString() ?? "Valid") : "Valid";
            var porsi = root.TryGetProperty("no_porsi", out var p) ? p.GetString() : null;
            return new SiskohatLog { Hasil = status, NoPorsi = porsi, Pesan = Potong(body) };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Sinkronisasi SISKOHAT gagal untuk jamaah {Id}", j.Id);
            return new SiskohatLog { Hasil = "Error", Pesan = ex.Message };
        }
    }

    /// <summary>Validasi lokal saat endpoint resmi belum tersedia.</summary>
    private static SiskohatLog SyncSimulasi(Jamaah j)
    {
        var masalah = new List<string>();
        if (string.IsNullOrWhiteSpace(j.Nik) || j.Nik.Length != 16 || !j.Nik.All(char.IsDigit))
            masalah.Add("NIK harus 16 digit angka");
        if (string.IsNullOrWhiteSpace(j.NamaLengkap)) masalah.Add("Nama lengkap kosong");
        if (j.TanggalLahir == null) masalah.Add("Tanggal lahir kosong");
        if (string.IsNullOrWhiteSpace(j.NoPaspor)) masalah.Add("Nomor paspor kosong");
        else if (j.NoPaspor.Length < 6) masalah.Add("Nomor paspor tidak valid");

        if (masalah.Count > 0)
            return new SiskohatLog { Hasil = "DataBerbeda", Pesan = "Data belum memenuhi syarat SISKOHAT: " + string.Join("; ", masalah) };

        // nomor porsi deterministik dari NIK supaya stabil antar sinkronisasi
        var porsi = "1" + Math.Abs(j.Nik.GetHashCode()).ToString().PadLeft(9, '0')[..9];
        return new SiskohatLog
        {
            Hasil = "Valid",
            NoPorsi = porsi,
            Pesan = "Data valid (mode simulasi — endpoint SISKOHAT resmi belum dikonfigurasi di menu Pengaturan)."
        };
    }

    private static string Potong(string s) => string.IsNullOrEmpty(s) || s.Length <= 1900 ? s : s[..1900];
}
