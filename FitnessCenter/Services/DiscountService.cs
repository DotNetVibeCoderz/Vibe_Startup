using Microsoft.EntityFrameworkCore;
using FitnessCenter.Data;
using FitnessCenter.Models;

namespace FitnessCenter.Services;

public class DiscountService
{
    private readonly AppDbContext _db;
    public DiscountService(AppDbContext db) => _db = db;

    public async Task<List<Discount>> GetAllAsync() => await _db.Discounts.OrderByDescending(d => d.ValidFrom).ToListAsync();
    public async Task<Discount?> GetByCodeAsync(string code) =>
        await _db.Discounts.FirstOrDefaultAsync(d => d.Code == code.ToUpper() && d.IsActive && d.ValidFrom <= DateTime.UtcNow && d.ValidUntil >= DateTime.UtcNow);
    public async Task<Discount> CreateAsync(Discount d) { _db.Discounts.Add(d); await _db.SaveChangesAsync(); return d; }
    public async Task UpdateAsync(Discount d) { _db.Discounts.Update(d); await _db.SaveChangesAsync(); }
    public async Task DeleteAsync(int id) { var d = await _db.Discounts.FindAsync(id); if (d != null) { d.IsActive = false; await _db.SaveChangesAsync(); } }

    public async Task<decimal> CalculateDiscountAsync(string code, decimal amount)
    {
        var discount = await GetByCodeAsync(code);
        if (discount == null || (discount.MinPurchase.HasValue && amount < discount.MinPurchase.Value)) return 0;
        if (discount.MaxUses.HasValue && discount.CurrentUses >= discount.MaxUses.Value) return 0;

        var discountAmount = discount.Type == DiscountType.Percentage
            ? amount * discount.Value / 100m
            : discount.Value;
        return Math.Min(discountAmount, amount);
    }
}
