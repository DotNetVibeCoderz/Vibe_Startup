using HolySafar.Data;
using HolySafar.Models;
using Microsoft.EntityFrameworkCore;

namespace HolySafar.Services;

/// <summary>
/// Notifikasi &amp; reminder otomatis (requirements: pengingat pembayaran, jadwal manasik,
/// update keberangkatan). Berjalan sebagai background service, memeriksa berkala dan
/// menulis ke tabel Notifikasi. Anti-duplikat: satu jenis reminder maksimal sekali per hari
/// per jamaah, dicek dari judul + tanggal notifikasi.
/// </summary>
public class ReminderService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly SettingsService _settings;
    private readonly ILogger<ReminderService> _logger;

    public ReminderService(IServiceScopeFactory scopeFactory, SettingsService settings, ILogger<ReminderService> logger)
    { _scopeFactory = scopeFactory; _settings = settings; _logger = logger; }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // beri jeda agar migrasi/seed selesai lebih dulu
        try { await Task.Delay(TimeSpan.FromSeconds(20), stoppingToken); } catch (TaskCanceledException) { return; }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await _settings.EnsureLoadedAsync();
                if (_settings.GetBool("Reminder:Enabled", true)) await RunOnceAsync(stoppingToken);
            }
            catch (Exception ex) { _logger.LogError(ex, "Reminder gagal dijalankan"); }

            var jam = Math.Max(1, _settings.GetInt("Reminder:IntervalHours", 6));
            try { await Task.Delay(TimeSpan.FromHours(jam), stoppingToken); } catch (TaskCanceledException) { return; }
        }
    }

    /// <summary>Sekali putaran pemeriksaan. Dipanggil juga dari tombol "Jalankan sekarang" di UI admin.</summary>
    public async Task<int> RunOnceAsync(CancellationToken ct = default)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var dibuat = 0;
        var hariIni = DateTime.UtcNow.Date;

        // ---------- 1. Pengingat jatuh tempo pembayaran ----------
        var ambangHari = _settings.Get("Reminder:PaymentDaysBefore", "7,3,1")
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(s => int.TryParse(s, out var d) ? d : -1).Where(d => d >= 0).ToList();

        var tagihan = await db.Pembayaran
            .Include(p => p.Jamaah)
            .Where(p => p.Status != PaymentStatus.Paid && p.Status != PaymentStatus.Cancelled && p.TanggalJatuhTempo != null)
            .ToListAsync(ct);

        foreach (var p in tagihan)
        {
            var sisaHari = (p.TanggalJatuhTempo!.Value.Date - hariIni).Days;
            var userId = p.Jamaah?.UserId;
            if (userId == null) continue;

            if (sisaHari < 0)
            {
                if (p.Status != PaymentStatus.Overdue) p.Status = PaymentStatus.Overdue;
                if (await Tambah(db, userId, "Tagihan terlambat",
                        $"Pembayaran paket Anda melewati jatuh tempo {p.TanggalJatuhTempo:dd MMM yyyy}. Sisa {(p.TotalBiaya - p.TotalDibayar):C0}. Segera lakukan pembayaran.",
                        "danger", hariIni, ct)) dibuat++;
            }
            else if (ambangHari.Contains(sisaHari))
            {
                if (await Tambah(db, userId, "Pengingat pembayaran",
                        $"Tagihan paket Anda jatuh tempo dalam {sisaHari} hari ({p.TanggalJatuhTempo:dd MMM yyyy}). Sisa {(p.TotalBiaya - p.TotalDibayar):C0}.",
                        "warning", hariIni, ct)) dibuat++;
            }
        }

        // ---------- 2. Pengingat keberangkatan ----------
        var hariSebelumBerangkat = _settings.GetInt("Reminder:DepartureDaysBefore", 7);
        var keberangkatan = await db.Keberangkatan
            .Where(k => k.TanggalBerangkat != null && k.Status != DepartureStatus.Cancelled && k.Status != DepartureStatus.Completed)
            .ToListAsync(ct);

        foreach (var k in keberangkatan)
        {
            var sisaHari = (k.TanggalBerangkat!.Value.Date - hariIni).Days;
            if (sisaHari != hariSebelumBerangkat && sisaHari != 1) continue;

            var jamaahIds = await db.Jamaah
                .Where(j => j.PaketId == k.PaketId && j.UserId != null)
                .Select(j => j.UserId).ToListAsync(ct);

            foreach (var uid in jamaahIds)
                if (await Tambah(db, uid, "Persiapan keberangkatan",
                        $"Keberangkatan {k.KodeKeberangkatan} tinggal {sisaHari} hari lagi ({k.TanggalBerangkat:dd MMM yyyy}, {k.Maskapai} {k.NoPenerbangan} dari {k.BandaraAsal}). Pastikan dokumen dan bagasi siap.",
                        "info", hariIni, ct)) dibuat++;
        }

        // ---------- 3. Pengingat jadwal manasik ----------
        var manasik = await db.ItineraryItems
            .Include(i => i.Paket)
            .Where(i => i.Jenis == "Manasik")
            .ToListAsync(ct);

        foreach (var m in manasik)
        {
            var jamaahIds = await db.Jamaah
                .Where(j => j.PaketId == m.PaketId && j.UserId != null)
                .Select(j => j.UserId).ToListAsync(ct);

            foreach (var uid in jamaahIds)
                if (await Tambah(db, uid, "Jadwal manasik",
                        $"Jangan lupa manasik: {m.Judul} — {m.Waktu} di {m.Lokasi}.",
                        "info", hariIni, ct)) dibuat++;
        }

        // ---------- 4. Pengingat dokumen belum lengkap ----------
        var dokumenWajib = new[] { "KTP", "Paspor", "KK", "Vaksin" };
        var jamaahAktif = await db.Jamaah.Where(j => j.UserId != null && j.StatusDokumen != DocumentStatus.Verified).ToListAsync(ct);
        foreach (var j in jamaahAktif)
        {
            var punya = await db.DokumenJamaah.Where(d => d.JamaahId == j.Id).Select(d => d.TipeDokumen).ToListAsync(ct);
            var kurang = dokumenWajib.Where(d => !punya.Contains(d)).ToList();
            if (kurang.Count == 0) continue;
            if (await Tambah(db, j.UserId, "Dokumen belum lengkap",
                    $"Dokumen berikut belum diunggah: {string.Join(", ", kurang)}. Unggah di menu Dokumen Saya.",
                    "warning", hariIni, ct)) dibuat++;
        }

        await db.SaveChangesAsync(ct);
        if (dibuat > 0) _logger.LogInformation("Reminder membuat {Jumlah} notifikasi", dibuat);
        return dibuat;
    }

    /// <summary>Tambah notifikasi bila judul yang sama belum dikirim ke user hari ini.</summary>
    private static async Task<bool> Tambah(AppDbContext db, int? userId, string judul, string pesan, string tipe, DateTime hariIni, CancellationToken ct)
    {
        if (userId == null) return false;
        var besok = hariIni.AddDays(1);
        var sudahAda = await db.Notifikasi.AnyAsync(n =>
            n.UserId == userId && n.Judul == judul && n.CreatedAt >= hariIni && n.CreatedAt < besok, ct);
        if (sudahAda) return false;

        db.Notifikasi.Add(new Notifikasi { UserId = userId, Judul = judul, Pesan = pesan, Tipe = tipe, CreatedAt = DateTime.UtcNow });
        return true;
    }
}
