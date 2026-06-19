namespace Comblang.Services.Location;

/// <summary>
/// Geographical utility class providing Haversine distance calculations
/// and bounding-box generation for location-based discovery.
/// </summary>
public static class GeoService
{
    /// <summary>Earth's mean radius in kilometres.</summary>
    private const double EarthRadiusKm = 6371.0;

    /// <summary>
    /// Calculates the great-circle distance (in kilometres) between two
    /// latitude/longitude points using the Haversine formula.
    /// </summary>
    public static double CalculateDistance(
        double lat1, double lon1,
        double lat2, double lon2)
    {
        var dLat = ToRadians(lat2 - lat1);
        var dLon = ToRadians(lon2 - lon1);

        var a = Math.Sin(dLat / 2.0) * Math.Sin(dLat / 2.0) +
                Math.Cos(ToRadians(lat1)) *
                Math.Cos(ToRadians(lat2)) *
                Math.Sin(dLon / 2.0) * Math.Sin(dLon / 2.0);

        var c = 2.0 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1.0 - a));
        return EarthRadiusKm * c;
    }

    /// <summary>
    /// Converts degrees to radians.
    /// </summary>
    private static double ToRadians(double degrees) => degrees * Math.PI / 180.0;

    /// <summary>
    /// Generates a bounding box (min/max latitude and longitude) for a
    /// circular area defined by a centre point and radius in kilometres.
    /// Useful for database queries that use simple lat/lng range filters
    /// before applying the precise Haversine distance.
    /// </summary>
    /// <returns>
    /// A tuple (minLat, maxLat, minLng, maxLng).
    /// </returns>
    public static (double minLat, double maxLat, double minLng, double maxLng)
        GetBoundingBox(double lat, double lng, double radiusKm)
    {
        // Latitude: 1 degree ≈ 111.32 km
        var latDelta = radiusKm / 111.32;

        // Longitude: degrees per km depends on latitude
        var lngDelta = radiusKm / (111.32 * Math.Cos(ToRadians(lat)));

        return (
            minLat: lat - latDelta,
            maxLat: lat + latDelta,
            minLng: lng - lngDelta,
            maxLng: lng + lngDelta
        );
    }
}
