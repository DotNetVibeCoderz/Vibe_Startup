using Microsoft.EntityFrameworkCore;
using Bioskop.Data;
using Bioskop.Models;

namespace Bioskop.Services;

/// <summary>
/// Service untuk mengelola menu snack
/// </summary>
public class SnackService
{
    private readonly ApplicationDbContext _context;

    public SnackService(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<List<Snack>> GetAllAsync(string? category = null)
    {
        var query = _context.Snacks.Where(s => s.IsAvailable).AsQueryable();
        if (!string.IsNullOrEmpty(category))
            query = query.Where(s => s.Category == category);
        return await query.OrderBy(s => s.Category).ThenBy(s => s.Name).ToListAsync();
    }

    public async Task<List<Snack>> GetByCategoryAsync(string category)
    {
        return await _context.Snacks
            .Where(s => s.Category == category && s.IsAvailable)
            .OrderBy(s => s.Name)
            .ToListAsync();
    }

    public async Task<Snack?> GetByIdAsync(int id)
    {
        return await _context.Snacks.FindAsync(id);
    }

    public async Task<Snack> CreateAsync(Snack snack)
    {
        _context.Snacks.Add(snack);
        await _context.SaveChangesAsync();
        return snack;
    }

    public async Task<Snack?> UpdateAsync(Snack snack)
    {
        var existing = await _context.Snacks.FindAsync(snack.Id);
        if (existing == null) return null;

        existing.Name = snack.Name;
        existing.Description = snack.Description;
        existing.Price = snack.Price;
        existing.Category = snack.Category;
        existing.ImageUrl = snack.ImageUrl;
        existing.IsAvailable = snack.IsAvailable;
        existing.Stock = snack.Stock;
        existing.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();
        return existing;
    }

    public async Task<bool> DeleteAsync(int id)
    {
        var snack = await _context.Snacks.FindAsync(id);
        if (snack == null) return false;
        _context.Snacks.Remove(snack);
        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<Dictionary<string, decimal>> GetSnackSalesStatsAsync(DateTime? from = null, DateTime? to = null)
    {
        var query = _context.OrderSnacks
            .Include(os => os.Snack)
            .Join(_context.Orders,
                os => os.OrderId,
                o => o.Id,
                (os, o) => new { os, o })
            .Where(x => x.o.Status == "Paid");

        if (from.HasValue)
            query = query.Where(x => x.o.OrderDate >= from.Value);
        if (to.HasValue)
            query = query.Where(x => x.o.OrderDate <= to.Value);

        var data = await query.ToListAsync();
        return data
            .GroupBy(x => x.os.Snack?.Name ?? "Unknown")
            .ToDictionary(g => g.Key, g => g.Sum(x => x.os.Subtotal));
    }
}
