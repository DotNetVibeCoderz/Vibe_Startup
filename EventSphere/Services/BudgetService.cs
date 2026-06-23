using Microsoft.EntityFrameworkCore;
using EventSphere.Data.Context;
using EventSphere.Data.Models;

namespace EventSphere.Services;

public class BudgetService
{
    private readonly AppDbContext _db;
    public BudgetService(AppDbContext db) => _db = db;

    public async Task<List<BudgetItem>> GetItemsForEventAsync(Guid eventId) =>
        await _db.BudgetItems.Where(b => b.EventId == eventId).OrderBy(b => b.SortOrder).ToListAsync();

    public async Task<BudgetItem> AddItemAsync(BudgetItem item) { item.Id = Guid.NewGuid(); _db.BudgetItems.Add(item); await _db.SaveChangesAsync(); return item; }
    public async Task<bool> UpdateItemAsync(BudgetItem item) { var e = await _db.BudgetItems.FindAsync(item.Id); if (e == null) return false; _db.Entry(e).CurrentValues.SetValues(item); await _db.SaveChangesAsync(); return true; }
    public async Task<bool> DeleteItemAsync(Guid id) { var item = await _db.BudgetItems.FindAsync(id); if (item == null) return false; _db.BudgetItems.Remove(item); await _db.SaveChangesAsync(); return true; }

    public async Task<decimal> GetTotalEstimatedAsync(Guid eventId) =>
        await _db.BudgetItems.Where(b => b.EventId == eventId).SumAsync(b => b.EstimatedCost);

    public async Task<decimal> GetTotalActualAsync(Guid eventId) =>
        await _db.BudgetItems.Where(b => b.EventId == eventId).SumAsync(b => b.ActualCost);
}
