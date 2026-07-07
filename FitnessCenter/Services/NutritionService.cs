using Microsoft.EntityFrameworkCore;
using FitnessCenter.Data;
using FitnessCenter.Models;

namespace FitnessCenter.Services;

public class NutritionService
{
    private readonly AppDbContext _db;
    public NutritionService(AppDbContext db) => _db = db;

    public async Task<List<NutritionPlan>> GetAllPlansAsync() =>
        await _db.NutritionPlans.OrderBy(n => n.Name).ToListAsync();

    public async Task<NutritionPlan?> GetPlanByIdAsync(int id) =>
        await _db.NutritionPlans.FindAsync(id);

    public async Task<NutritionPlan> CreatePlanAsync(NutritionPlan plan) { _db.NutritionPlans.Add(plan); await _db.SaveChangesAsync(); return plan; }
    public async Task UpdatePlanAsync(NutritionPlan plan) { _db.NutritionPlans.Update(plan); await _db.SaveChangesAsync(); }
    public async Task DeletePlanAsync(int id) { var p = await _db.NutritionPlans.FindAsync(id); if (p != null) { _db.NutritionPlans.Remove(p); await _db.SaveChangesAsync(); } }

    public async Task<List<MealPlan>> GetUserMealsAsync(string userId, DateTime? date = null)
    {
        var query = _db.MealPlans.Include(m => m.NutritionPlan).Where(m => m.UserId == userId);
        if (date.HasValue) query = query.Where(m => m.Date.Date == date.Value.Date);
        return await query.OrderBy(m => m.Date).ThenBy(m => m.MealType).ToListAsync();
    }

    public async Task<MealPlan> AddMealAsync(MealPlan meal) { _db.MealPlans.Add(meal); await _db.SaveChangesAsync(); return meal; }
    public async Task DeleteMealAsync(int id) { var m = await _db.MealPlans.FindAsync(id); if (m != null) { _db.MealPlans.Remove(m); await _db.SaveChangesAsync(); } }
}
