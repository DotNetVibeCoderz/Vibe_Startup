using Microsoft.EntityFrameworkCore;
using RentalBoil.Data;
using RentalBoil.Models;

namespace RentalBoil.Services;

public class VehicleService
{
    private readonly AppDbContext _db;
    private readonly ILogger<VehicleService> _logger;

    public VehicleService(AppDbContext db, ILogger<VehicleService> logger)
    { _db = db; _logger = logger; }

    public async Task<(List<Vehicle> Items, int Total)> GetVehiclesAsync(
        string? search = null, VehicleType? type = null, string? brand = null,
        int? minCapacity = null, int? maxCapacity = null,
        TransmissionType? transmission = null, FuelType? fuelType = null,
        decimal? minPrice = null, decimal? maxPrice = null,
        int? minYear = null, int? maxYear = null, double? minRating = null,
        bool? isAvailable = null, string? location = null,
        string? sortBy = null, bool sortDesc = false, int page = 1, int pageSize = 12)
    {
        var query = _db.Vehicles.Include(v => v.Photos).Include(v => v.Owner).AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
        { var s = search.ToLowerInvariant(); query = query.Where(v => v.Name.ToLower().Contains(s) || v.Brand.ToLower().Contains(s) || v.Model.ToLower().Contains(s) || v.PlateNumber.ToLower().Contains(s)); }
        if (type.HasValue) query = query.Where(v => v.Type == type.Value);
        if (!string.IsNullOrWhiteSpace(brand)) query = query.Where(v => v.Brand.ToLower() == brand.ToLowerInvariant());
        if (minCapacity.HasValue) query = query.Where(v => v.Capacity >= minCapacity.Value);
        if (maxCapacity.HasValue) query = query.Where(v => v.Capacity <= maxCapacity.Value);
        if (transmission.HasValue) query = query.Where(v => v.Transmission == transmission.Value);
        if (fuelType.HasValue) query = query.Where(v => v.FuelType == fuelType.Value);
        if (minPrice.HasValue) query = query.Where(v => v.PricePerDay >= minPrice.Value);
        if (maxPrice.HasValue) query = query.Where(v => v.PricePerDay <= maxPrice.Value);
        if (minYear.HasValue) query = query.Where(v => v.Year >= minYear.Value);
        if (maxYear.HasValue) query = query.Where(v => v.Year <= maxYear.Value);
        if (minRating.HasValue) query = query.Where(v => v.AverageRating >= minRating.Value);
        if (isAvailable.HasValue) query = query.Where(v => v.IsAvailable == isAvailable.Value);
        if (!string.IsNullOrWhiteSpace(location)) { var loc = location.ToLowerInvariant(); query = query.Where(v => v.Location != null && v.Location.ToLower().Contains(loc)); }
        query = query.Where(v => v.IsVerified);

        query = sortBy?.ToLower() switch
        {
            "name" => sortDesc ? query.OrderByDescending(v => v.Name) : query.OrderBy(v => v.Name),
            "price" => sortDesc ? query.OrderByDescending(v => v.PricePerDay) : query.OrderBy(v => v.PricePerDay),
            "rating" => sortDesc ? query.OrderByDescending(v => v.AverageRating) : query.OrderBy(v => v.AverageRating),
            "year" => sortDesc ? query.OrderByDescending(v => v.Year) : query.OrderBy(v => v.Year),
            "capacity" => sortDesc ? query.OrderByDescending(v => v.Capacity) : query.OrderBy(v => v.Capacity),
            "popular" => sortDesc ? query.OrderByDescending(v => v.RentalCount) : query.OrderBy(v => v.RentalCount),
            _ => sortDesc ? query.OrderByDescending(v => v.CreatedAt) : query.OrderBy(v => v.CreatedAt)
        };

        var total = await query.CountAsync();
        var items = await query.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();
        return (items, total);
    }

    public async Task<Vehicle?> GetVehicleByIdAsync(int id)
        => await _db.Vehicles.Include(v => v.Photos).Include(v => v.Owner).Include(v => v.Reviews).ThenInclude(r => r.User).FirstOrDefaultAsync(v => v.Id == id);

    public async Task<List<Vehicle>> GetPartnerVehiclesAsync(string ownerId)
        => await _db.Vehicles.Include(v => v.Photos).Include(v => v.Bookings).Where(v => v.OwnerId == ownerId).OrderByDescending(v => v.CreatedAt).ToListAsync();

    public async Task<Vehicle> CreateVehicleAsync(Vehicle vehicle)
    { _db.Vehicles.Add(vehicle); await _db.SaveChangesAsync(); return vehicle; }

    /// <summary>
    /// ★ FIX: Update hanya field yang boleh diedit. Tidak overwrite OwnerId, CreatedAt, dll.
    /// </summary>
    public async Task<Vehicle?> UpdateVehicleAsync(Vehicle vehicle)
    {
        var v = await _db.Vehicles.FindAsync(vehicle.Id);
        if (v == null) return null;

        v.Name = vehicle.Name;
        v.PlateNumber = vehicle.PlateNumber;
        v.Type = vehicle.Type;
        v.Brand = vehicle.Brand;
        v.Model = vehicle.Model;
        v.Year = vehicle.Year;
        v.Color = vehicle.Color;
        v.Transmission = vehicle.Transmission;
        v.FuelType = vehicle.FuelType;
        v.Capacity = vehicle.Capacity;
        v.LuggageCapacity = vehicle.LuggageCapacity;
        v.PricePerHour = vehicle.PricePerHour;
        v.PricePerDay = vehicle.PricePerDay;
        v.Description = vehicle.Description;
        v.Specifications = vehicle.Specifications;
        v.Location = vehicle.Location;
        v.Latitude = vehicle.Latitude;
        v.Longitude = vehicle.Longitude;
        v.InsuranceAvailable = vehicle.InsuranceAvailable;
        v.InsuranceCostPerDay = vehicle.InsuranceCostPerDay;
        v.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();
        return v;
    }

    public async Task<bool> DeleteVehicleAsync(int id)
    { var v = await _db.Vehicles.FindAsync(id); if (v == null) return false; _db.Vehicles.Remove(v); await _db.SaveChangesAsync(); return true; }

    public async Task<bool> ToggleAvailabilityAsync(int id)
    { var v = await _db.Vehicles.FindAsync(id); if (v == null) return false; v.IsAvailable = !v.IsAvailable; v.UpdatedAt = DateTime.UtcNow; await _db.SaveChangesAsync(); return true; }

    public async Task<List<Vehicle>> GetPopularVehiclesAsync(int count = 6)
        => await _db.Vehicles.Include(v => v.Photos).Where(v => v.IsAvailable && v.IsVerified).OrderByDescending(v => v.RentalCount).Take(count).ToListAsync();

    public async Task<List<string>> GetBrandsAsync()
        => await _db.Vehicles.Where(v => v.IsVerified).Select(v => v.Brand).Distinct().OrderBy(b => b).ToListAsync();

    public async Task<List<Vehicle>> GetPendingVerificationAsync()
        => await _db.Vehicles.Include(v => v.Photos).Include(v => v.Owner).Where(v => !v.IsVerified).OrderBy(v => v.CreatedAt).ToListAsync();

    public async Task<bool> VerifyVehicleAsync(int id, bool verified = true)
    { var v = await _db.Vehicles.FindAsync(id); if (v == null) return false; v.IsVerified = verified; v.UpdatedAt = DateTime.UtcNow; await _db.SaveChangesAsync(); return true; }

    public async Task<object> GetVehicleStatsAsync()
        => new { TotalVehicles = await _db.Vehicles.CountAsync(), AvailableVehicles = await _db.Vehicles.CountAsync(v => v.IsAvailable), TotalCars = await _db.Vehicles.CountAsync(v => v.Type == VehicleType.Car), TotalMotorcycles = await _db.Vehicles.CountAsync(v => v.Type == VehicleType.Motorcycle), PendingVerification = await _db.Vehicles.CountAsync(v => !v.IsVerified) };
}
