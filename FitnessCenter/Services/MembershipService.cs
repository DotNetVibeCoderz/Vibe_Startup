using Microsoft.EntityFrameworkCore;
using FitnessCenter.Data;
using FitnessCenter.Models;

namespace FitnessCenter.Services;

public class MembershipService
{
    private readonly AppDbContext _db;
    public MembershipService(AppDbContext db) => _db = db;

    public async Task<List<MembershipPlan>> GetAllPlansAsync() =>
        await _db.MembershipPlans.Where(p => p.IsActive).OrderBy(p => p.Price).ToListAsync();

    public async Task<MembershipPlan?> GetPlanByIdAsync(int id) =>
        await _db.MembershipPlans.FindAsync(id);

    public async Task<MembershipPlan> CreatePlanAsync(MembershipPlan plan) { _db.MembershipPlans.Add(plan); await _db.SaveChangesAsync(); return plan; }
    public async Task UpdatePlanAsync(MembershipPlan plan) { _db.MembershipPlans.Update(plan); await _db.SaveChangesAsync(); }
    public async Task DeletePlanAsync(int id) { var p = await _db.MembershipPlans.FindAsync(id); if (p != null) { _db.MembershipPlans.Remove(p); await _db.SaveChangesAsync(); } }

    public async Task<List<MemberMembership>> GetMemberMembershipsAsync(string userId) =>
        await _db.MemberMemberships.Include(m => m.MembershipPlan).Where(m => m.UserId == userId).OrderByDescending(m => m.StartDate).ToListAsync();

    public async Task<MemberMembership?> CreateMembershipAsync(MemberMembership membership)
    {
        _db.MemberMemberships.Add(membership);
        var user = await _db.Users.FindAsync(membership.UserId);
        if (user != null) { user.MembershipExpiryDate = membership.EndDate; _db.Users.Update(user); }
        await _db.SaveChangesAsync();
        return membership;
    }
}
