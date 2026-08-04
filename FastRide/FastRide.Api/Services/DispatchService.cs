using FastRide.Data;
using FastRide.Shared.Common;
using FastRide.Shared.DTOs;
using FastRide.Shared.Models;
using Microsoft.EntityFrameworkCore;

namespace FastRide.Api.Services;

/// <summary>
/// Matching between riders and drivers.
///
/// Both directions pre-filter in SQL with a bounding box (index-friendly) and only then run
/// the exact haversine in memory — computing distance for every driver in the database would
/// scan the whole table on every poll.
/// </summary>
public sealed class DispatchService(FastRideDbContext db)
{
    /// <summary>A driver whose last GPS ping is older than this is not considered available.</summary>
    public static readonly TimeSpan LocationFreshness = TimeSpan.FromMinutes(10);

    public async Task<List<NearbyDriverItem>> FindNearbyDriversAsync(
        double latitude, double longitude, double radiusKm, VehicleCategory? category, int limit, CancellationToken ct)
    {
        var box = GeoUtils.BoundingBox(latitude, longitude, radiusKm);
        var freshAfter = DateTime.UtcNow - LocationFreshness;

        var candidates = await db.DriverProfiles
            .AsNoTracking()
            .Where(p => p.Status == DriverStatus.Online
                        && p.IsDocumentVerified
                        && p.User.IsActive
                        && p.LocationUpdatedAt != null && p.LocationUpdatedAt >= freshAfter
                        && p.CurrentLatitude >= box.MinLat && p.CurrentLatitude <= box.MaxLat
                        && p.CurrentLongitude >= box.MinLon && p.CurrentLongitude <= box.MaxLon
                        && (category == null || p.VehicleCategory == category))
            .Select(p => new
            {
                p.UserId,
                p.User.FullName,
                p.VehicleType,
                p.VehiclePlate,
                p.Rating,
                p.CurrentLatitude,
                p.CurrentLongitude,
                p.Heading
            })
            .Take(limit * 4)
            .ToListAsync(ct);

        return candidates
            .Select(c => new NearbyDriverItem(
                c.UserId, c.FullName, c.VehicleType, c.VehiclePlate, c.Rating,
                c.CurrentLatitude, c.CurrentLongitude, c.Heading,
                Math.Round(GeoUtils.DistanceKm(latitude, longitude, c.CurrentLatitude, c.CurrentLongitude), 2)))
            .Where(d => d.DistanceKm <= radiusKm)
            .OrderBy(d => d.DistanceKm)
            .Take(limit)
            .ToList();
    }

    /// <summary>Open orders a specific driver could take, nearest pickup first.</summary>
    public async Task<List<IncomingOrderItem>> FindOpenOrdersForDriverAsync(
        DriverProfile driver, double radiusKm, int limit, CancellationToken ct)
    {
        var hasLocation = driver.CurrentLatitude != 0 || driver.CurrentLongitude != 0;
        var query = db.Orders
            .AsNoTracking()
            .Where(o => o.Status == OrderStatus.Requested && o.DriverId == null);

        if (hasLocation)
        {
            var box = GeoUtils.BoundingBox(driver.CurrentLatitude, driver.CurrentLongitude, radiusKm);
            query = query.Where(o => o.PickupLatitude >= box.MinLat && o.PickupLatitude <= box.MaxLat
                                     && o.PickupLongitude >= box.MinLon && o.PickupLongitude <= box.MaxLon);
        }

        var now = DateTime.UtcNow;
        var candidates = await query
            .OrderByDescending(o => o.CreatedAt)
            .Take(limit * 3)
            .Select(o => new
            {
                o.Id,
                o.Code,
                RiderName = o.Rider.FullName,
                o.PickupAddress,
                o.PickupLatitude,
                o.PickupLongitude,
                o.DropoffAddress,
                o.DistanceKm,
                o.EstimatedFare,
                o.VehicleCategory,
                o.PaymentMethod,
                o.CreatedAt
            })
            .ToListAsync(ct);

        return candidates
            .Select(o => new IncomingOrderItem(
                o.Id, o.Code, o.RiderName, o.PickupAddress, o.DropoffAddress,
                o.DistanceKm,
                hasLocation
                    ? Math.Round(GeoUtils.DistanceKm(driver.CurrentLatitude, driver.CurrentLongitude, o.PickupLatitude, o.PickupLongitude), 2)
                    : 0,
                o.EstimatedFare, o.VehicleCategory, o.PaymentMethod,
                (int)(now - o.CreatedAt).TotalSeconds))
            .OrderBy(o => o.PickupDistanceKm)
            .ThenByDescending(o => o.WaitSeconds)
            .Take(limit)
            .ToList();
    }

    /// <summary>Live tracking payload for the rider's map screen.</summary>
    public async Task<OrderTrackingResponse?> GetTrackingAsync(Guid orderId, CancellationToken ct)
    {
        var tracking = await db.Orders
            .AsNoTracking()
            .Where(o => o.Id == orderId)
            .Select(o => new
            {
                o.Id,
                o.Code,
                o.Status,
                DriverName = o.Driver != null ? o.Driver.FullName : null,
                DriverPhoto = o.Driver != null ? o.Driver.PhotoUrl : null,
                VehicleType = o.Driver != null && o.Driver.DriverProfile != null ? o.Driver.DriverProfile.VehicleType : null,
                VehiclePlate = o.Driver != null && o.Driver.DriverProfile != null ? o.Driver.DriverProfile.VehiclePlate : null,
                DriverRating = o.Driver != null && o.Driver.DriverProfile != null ? (double?)o.Driver.DriverProfile.Rating : null,
                DriverLat = o.Driver != null && o.Driver.DriverProfile != null ? (double?)o.Driver.DriverProfile.CurrentLatitude : null,
                DriverLon = o.Driver != null && o.Driver.DriverProfile != null ? (double?)o.Driver.DriverProfile.CurrentLongitude : null,
                o.PickupLatitude,
                o.PickupLongitude,
                o.DropoffLatitude,
                o.DropoffLongitude
            })
            .FirstOrDefaultAsync(ct);

        if (tracking is null) return null;

        // Before pickup the rider wants distance to them; after pickup, distance to destination.
        double? distance = null;
        int? eta = null;

        if (tracking.DriverLat is { } lat && tracking.DriverLon is { } lon)
        {
            var towardsDropoff = tracking.Status == OrderStatus.Started;
            var targetLat = towardsDropoff ? tracking.DropoffLatitude : tracking.PickupLatitude;
            var targetLon = towardsDropoff ? tracking.DropoffLongitude : tracking.PickupLongitude;

            distance = Math.Round(GeoUtils.DistanceKm(lat, lon, targetLat, targetLon), 2);
            eta = GeoUtils.EstimateDurationMinutes(distance.Value);
        }

        return new OrderTrackingResponse(
            tracking.Id, tracking.Code, tracking.Status,
            tracking.DriverName, tracking.VehicleType, tracking.VehiclePlate, tracking.DriverPhoto, tracking.DriverRating,
            tracking.DriverLat, tracking.DriverLon,
            tracking.PickupLatitude, tracking.PickupLongitude,
            tracking.DropoffLatitude, tracking.DropoffLongitude,
            distance, eta, DateTime.UtcNow);
    }
}
