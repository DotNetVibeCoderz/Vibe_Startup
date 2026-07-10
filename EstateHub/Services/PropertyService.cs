using Microsoft.EntityFrameworkCore;
using EstateHub.Data;
using EstateHub.Models;

namespace EstateHub.Services;

/// <summary>
/// Service for property operations: CRUD, search, filtering
/// </summary>
public class PropertyService
{
    private readonly AppDbContext _db;

    public PropertyService(AppDbContext db) => _db = db;

    public async Task<Property> CreatePropertyAsync(Property property)
    {
        property.CreatedAt = DateTime.UtcNow;
        property.UpdatedAt = DateTime.UtcNow;
        _db.Properties.Add(property);
        await _db.SaveChangesAsync();
        return property;
    }

    public async Task<Property?> GetByIdAsync(int id)
    {
        return await _db.Properties
            .Include(p => p.Owner)
            .Include(p => p.Reviews)
            .FirstOrDefaultAsync(p => p.Id == id);
    }

    public async Task<List<Property>> SearchAsync(string? keyword = null, string? type = null,
        string? listingType = null, string? city = null, decimal? minPrice = null,
        decimal? maxPrice = null, double? minArea = null, double? maxArea = null,
        int? bedrooms = null, string? facilities = null, string? status = "Available",
        int page = 1, int pageSize = 20)
    {
        var query = _db.Properties.AsQueryable();

        if (!string.IsNullOrWhiteSpace(keyword))
            query = query.Where(p => p.Title.Contains(keyword) || p.Description.Contains(keyword) || p.Address.Contains(keyword));

        if (!string.IsNullOrWhiteSpace(type))
            query = query.Where(p => p.PropertyType == type);

        if (!string.IsNullOrWhiteSpace(listingType))
            query = query.Where(p => p.ListingType == listingType);

        if (!string.IsNullOrWhiteSpace(city))
            query = query.Where(p => p.City == city);

        if (!string.IsNullOrWhiteSpace(status))
            query = query.Where(p => p.Status == status);

        if (minPrice.HasValue)
            query = query.Where(p => p.Price >= minPrice.Value);

        if (maxPrice.HasValue)
            query = query.Where(p => p.Price <= maxPrice.Value);

        if (minArea.HasValue)
            query = query.Where(p => p.BuildingArea >= minArea.Value);

        if (maxArea.HasValue)
            query = query.Where(p => p.BuildingArea <= maxArea.Value);

        if (bedrooms.HasValue)
            query = query.Where(p => p.Bedrooms >= bedrooms.Value);

        if (!string.IsNullOrWhiteSpace(facilities))
        {
            var facList = facilities.Split(',').Select(f => f.Trim().ToLower());
            foreach (var fac in facList)
                query = query.Where(p => p.Facilities != null && p.Facilities.ToLower().Contains(fac));
        }

        // Only verified properties shown
        query = query.Where(p => p.IsVerified);

        return await query
            .OrderByDescending(p => p.IsPremium)
            .ThenByDescending(p => p.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Include(p => p.Owner)
            .ToListAsync();
    }

    public async Task<int> SearchCountAsync(string? keyword = null, string? type = null,
        string? listingType = null, string? city = null, decimal? minPrice = null,
        decimal? maxPrice = null, string? status = "Available")
    {
        var query = _db.Properties.AsQueryable().Where(p => p.IsVerified);

        if (!string.IsNullOrWhiteSpace(keyword))
            query = query.Where(p => p.Title.Contains(keyword) || p.Description.Contains(keyword));

        if (!string.IsNullOrWhiteSpace(type))
            query = query.Where(p => p.PropertyType == type);

        if (!string.IsNullOrWhiteSpace(listingType))
            query = query.Where(p => p.ListingType == listingType);

        if (!string.IsNullOrWhiteSpace(city))
            query = query.Where(p => p.City == city);

        if (!string.IsNullOrWhiteSpace(status))
            query = query.Where(p => p.Status == status);

        if (minPrice.HasValue) query = query.Where(p => p.Price >= minPrice.Value);
        if (maxPrice.HasValue) query = query.Where(p => p.Price <= maxPrice.Value);

        return await query.CountAsync();
    }

    public async Task<List<Property>> GetByOwnerAsync(string ownerId)
    {
        return await _db.Properties
            .Where(p => p.OwnerId == ownerId)
            .OrderByDescending(p => p.CreatedAt)
            .ToListAsync();
    }

    public async Task<List<Property>> GetFeaturedAsync(int count = 10)
    {
        return await _db.Properties
            .Where(p => p.IsVerified && p.Status == "Available")
            .OrderByDescending(p => p.IsFeatured)
            .ThenByDescending(p => p.ViewCount)
            .Take(count)
            .Include(p => p.Owner)
            .ToListAsync();
    }

    public async Task UpdateAsync(Property property)
    {
        property.UpdatedAt = DateTime.UtcNow;
        _db.Properties.Update(property);
        await _db.SaveChangesAsync();
    }

    public async Task DeleteAsync(int id)
    {
        var property = await _db.Properties.FindAsync(id);
        if (property != null)
        {
            _db.Properties.Remove(property);
            await _db.SaveChangesAsync();
        }
    }

    public async Task IncrementViewCountAsync(int id)
    {
        var property = await _db.Properties.FindAsync(id);
        if (property != null)
        {
            property.ViewCount++;
            await _db.SaveChangesAsync();
        }
    }

    public async Task<List<string>> GetCitiesAsync()
    {
        return await _db.Properties.Where(p => p.City != null)
            .Select(p => p.City!).Distinct().OrderBy(c => c).ToListAsync();
    }

    public async Task<Dictionary<string, int>> GetPropertyTypeStatsAsync()
    {
        return await _db.Properties
            .GroupBy(p => p.PropertyType)
            .Select(g => new { Type = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Type, x => x.Count);
    }
}
