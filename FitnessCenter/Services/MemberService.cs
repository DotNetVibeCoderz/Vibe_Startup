using Microsoft.EntityFrameworkCore;
using FitnessCenter.Data;
using FitnessCenter.Models;

namespace FitnessCenter.Services;

public class MemberService
{
    private readonly AppDbContext _db;
    public MemberService(AppDbContext db) => _db = db;

    public async Task<List<ApplicationUser>> GetAllAsync() =>
        await _db.Users.Include(u => u.MemberMemberships).OrderByDescending(u => u.RegisteredAt).ToListAsync();

    public async Task<ApplicationUser?> GetByIdAsync(string id) =>
        await _db.Users.Include(u => u.MemberMemberships).ThenInclude(m => m.MembershipPlan)
            .Include(u => u.Attendances).Include(u => u.Achievements)
            .FirstOrDefaultAsync(u => u.Id == id);

    public async Task<int> GetTotalMembersAsync() => await _db.Users.CountAsync(u => u.IsActive);
    public async Task<int> GetActiveMembersAsync() => await _db.Users.CountAsync(u => u.IsActive && u.MembershipExpiryDate > DateTime.UtcNow);
    public async Task<decimal> GetRetentionRateAsync()
    {
        var total = await _db.Users.CountAsync();
        if (total == 0) return 100;
        var active = await _db.Users.CountAsync(u => u.MembershipExpiryDate > DateTime.UtcNow || u.MembershipExpiryDate == null);
        return Math.Round((decimal)active / total * 100, 1);
    }
}
