using HolySafar.Data;
using HolySafar.Models;
using Microsoft.EntityFrameworkCore;

namespace HolySafar.Services;

/// <summary>
/// Service untuk notifikasi sistem
/// </summary>
public class NotifikasiService
{
    private readonly AppDbContext _db;

    public NotifikasiService(AppDbContext db) => _db = db;

    public async Task SendAsync(int? userId, string judul, string pesan, string tipe = "info")
    {
        var notif = new Notifikasi
        {
            UserId = userId,
            Judul = judul,
            Pesan = pesan,
            Tipe = tipe,
            IsRead = false,
            CreatedAt = DateTime.UtcNow
        };
        _db.Notifikasi.Add(notif);
        await _db.SaveChangesAsync();
    }

    public async Task<List<Notifikasi>> GetForUserAsync(int userId, int limit = 20)
    {
        return await _db.Notifikasi
            .Where(n => n.UserId == userId || n.UserId == null)
            .OrderByDescending(n => n.CreatedAt)
            .Take(limit)
            .ToListAsync();
    }

    public async Task<int> GetUnreadCountAsync(int userId)
    {
        return await _db.Notifikasi
            .CountAsync(n => (n.UserId == userId || n.UserId == null) && !n.IsRead);
    }

    public async Task MarkAsReadAsync(int notifId)
    {
        var notif = await _db.Notifikasi.FindAsync(notifId);
        if (notif != null)
        {
            notif.IsRead = true;
            await _db.SaveChangesAsync();
        }
    }
}
