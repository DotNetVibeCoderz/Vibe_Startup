using Microsoft.EntityFrameworkCore;
using EventSphere.Data.Context;
using EventSphere.Data.Models;

namespace EventSphere.Services;

/// <summary>
/// Dashboard analytics: statistik, chart data
/// </summary>
public class DashboardService
{
    private readonly AppDbContext _db;
    public DashboardService(AppDbContext db) => _db = db;

    public async Task<DashboardStats> GetStatsAsync()
    {
        var now = DateTime.UtcNow;
        return new DashboardStats
        {
            TotalEvents = await _db.Events.CountAsync(),
            UpcomingEvents = await _db.Events.CountAsync(e => e.EventDate >= now && e.Status != EventStatus.Cancelled),
            ActiveEvents = await _db.Events.CountAsync(e => e.Status == EventStatus.Confirmed || e.Status == EventStatus.InProgress),
            TotalVendors = await _db.Vendors.CountAsync(),
            TotalUsers = await _db.Users.CountAsync(),
            TotalRevenue = await _db.VendorContracts.SumAsync(vc => vc.Amount),
            TotalPaid = await _db.VendorContracts.SumAsync(vc => vc.PaidAmount),
            AvgGuestSatisfaction = await _db.Feedbacks.AnyAsync() ? await _db.Feedbacks.AverageAsync(f => (double)f.Rating) : 0,
            TasksPending = await _db.TaskItems.CountAsync(t => t.Status != TaskItemStatus.Done),
            TasksDone = await _db.TaskItems.CountAsync(t => t.Status == TaskItemStatus.Done),
        };
    }

    public async Task<List<MonthlyStats>> GetMonthlyStatsAsync(int months = 6)
    {
        var stats = new List<MonthlyStats>();
        for (int i = months - 1; i >= 0; i--)
        {
            var start = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1).AddMonths(-i);
            var end = start.AddMonths(1);
            stats.Add(new MonthlyStats
            {
                Month = start.ToString("MMM yyyy"),
                EventsCreated = await _db.Events.CountAsync(e => e.CreatedAt >= start && e.CreatedAt < end),
                Revenue = await _db.VendorContracts.Where(vc => vc.CreatedAt >= start && vc.CreatedAt < end).SumAsync(vc => vc.Amount),
                NewUsers = await _db.Users.CountAsync(u => u.CreatedAt >= start && u.CreatedAt < end),
            });
        }
        return stats;
    }

    public async Task<List<CategoryBreakdown>> GetEventTypeBreakdownAsync()
    {
        return await _db.Events.GroupBy(e => e.EventType ?? "Other")
            .Select(g => new CategoryBreakdown { Category = g.Key, Count = g.Count() })
            .ToListAsync();
    }

    public async Task<List<CategoryBreakdown>> GetVendorCategoryBreakdownAsync()
    {
        return await _db.Vendors.GroupBy(v => v.Category)
            .Select(g => new CategoryBreakdown { Category = g.Key, Count = g.Count() })
            .ToListAsync();
    }
}

public class DashboardStats
{
    public int TotalEvents { get; set; }
    public int UpcomingEvents { get; set; }
    public int ActiveEvents { get; set; }
    public int TotalVendors { get; set; }
    public int TotalUsers { get; set; }
    public decimal TotalRevenue { get; set; }
    public decimal TotalPaid { get; set; }
    public double AvgGuestSatisfaction { get; set; }
    public int TasksPending { get; set; }
    public int TasksDone { get; set; }
}

public class MonthlyStats
{
    public string Month { get; set; } = string.Empty;
    public int EventsCreated { get; set; }
    public decimal Revenue { get; set; }
    public int NewUsers { get; set; }
}

public class CategoryBreakdown
{
    public string Category { get; set; } = string.Empty;
    public int Count { get; set; }
}
