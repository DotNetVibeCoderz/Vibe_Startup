using System.Net.Http.Json;
using System.Text.Json;

namespace FastRide.AdminWeb.Services;

/// <summary>
/// Typed HttpClient for FastRide API communication.
/// Handles serialization, error handling, and paginated responses.
/// </summary>
public class ApiClient
{
    private readonly HttpClient _http;
    private readonly ILogger<ApiClient> _logger;

    public ApiClient(HttpClient http, ILogger<ApiClient> logger)
    {
        _http = http;
        _logger = logger;
    }

    // ─── Generic helpers ───

    public async Task<T?> GetAsync<T>(string path) where T : class
    {
        try
        {
            var response = await _http.GetAsync(path);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<T>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "GET {Path} failed", path);
            return null;
        }
    }

    public async Task<List<T>> GetListAsync<T>(string path)
    {
        try
        {
            return await _http.GetFromJsonAsync<List<T>>(path) ?? new();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "GET list {Path} failed", path);
            return new();
        }
    }

    public async Task<PaginatedResult<T>> GetPaginatedAsync<T>(string path) where T : class
    {
        try
        {
            return await _http.GetFromJsonAsync<PaginatedResult<T>>(path) ?? new();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "GET paginated {Path} failed", path);
            return new();
        }
    }

    // ─── Dashboard ───
    public Task<DashboardStats?> GetDashboardStats() =>
        GetAsync<DashboardStats>("/api/dashboard/stats");

    public Task<List<OrderStatusCount>> GetOrdersByStatus() =>
        GetListAsync<OrderStatusCount>("/api/dashboard/orders-by-status");

    public Task<List<HourlyData>> GetOrdersByHour(DateTime? date = null) =>
        GetListAsync<HourlyData>(
            $"/api/dashboard/orders-by-hour{(date.HasValue ? $"?date={date.Value:yyyy-MM-dd}" : "")}");

    // ─── Orders ───
    public Task<PaginatedResult<OrderItem>> GetOrders(int page = 1, int limit = 25, string? status = null)
    {
        var query = $"/api/orders?page={page}&limit={limit}";
        if (!string.IsNullOrWhiteSpace(status)) query += $"&status={status}";
        return GetPaginatedAsync<OrderItem>(query);
    }

    public Task<OrderDetail?> GetOrderDetail(Guid id) =>
        GetAsync<OrderDetail>($"/api/orders/{id}");

    // ─── Riders ───
    public Task<PaginatedResult<RiderItem>> GetRiders(int page = 1, int limit = 20, string? search = null)
    {
        var query = $"/api/riders?page={page}&limit={limit}";
        if (!string.IsNullOrWhiteSpace(search)) query += $"&search={Uri.EscapeDataString(search)}";
        return GetPaginatedAsync<RiderItem>(query);
    }

    // ─── Drivers ───
    public Task<PaginatedResult<DriverItem>> GetDrivers(int page = 1, int limit = 20, string? search = null)
    {
        var query = $"/api/drivers?page={page}&limit={limit}";
        if (!string.IsNullOrWhiteSpace(search)) query += $"&search={Uri.EscapeDataString(search)}";
        return GetPaginatedAsync<DriverItem>(query);
    }

    // ─── Payments ───
    public Task<PaginatedResult<PaymentItem>> GetPayments(int page = 1, int limit = 25) =>
        GetPaginatedAsync<PaymentItem>($"/api/payments?page={page}&limit={limit}");

    // ─── Promos ───
    public Task<List<PromoItem>> GetPromos() =>
        GetListAsync<PromoItem>("/api/promos");
}

// ─── API Response Models ───

public class PaginatedResult<T> where T : class
{
    public int Total { get; set; }
    public int Page { get; set; }
    public int Limit { get; set; }
    public List<T> Data { get; set; } = new();
    public int TotalPages => (int)Math.Ceiling((double)Total / Limit);
}

public class DashboardStats
{
    public int TotalOrdersToday { get; set; }
    public int ActiveDrivers { get; set; }
    public int ActiveRiders { get; set; }
    public decimal RevenueToday { get; set; }
    public double AverageRating { get; set; }
    public int PendingOrders { get; set; }
    public int TotalTripsToday { get; set; }
    public DateTime Timestamp { get; set; }
}

public class OrderStatusCount
{
    public string Status { get; set; } = "";
    public int Count { get; set; }
}

public class HourlyData
{
    public int Hour { get; set; }
    public int Count { get; set; }
    public decimal Revenue { get; set; }
}

public class OrderItem
{
    public Guid Id { get; set; }
    public Guid RiderId { get; set; }
    public string RiderName { get; set; } = "";
    public Guid? DriverId { get; set; }
    public string? DriverName { get; set; }
    public string PickupAddress { get; set; } = "";
    public string DropoffAddress { get; set; } = "";
    public double DistanceKm { get; set; }
    public int EstimatedDurationMinutes { get; set; }
    public decimal EstimatedFare { get; set; }
    public decimal FinalFare { get; set; }
    public string VehicleCategory { get; set; } = "";
    public string PaymentMethod { get; set; } = "";
    public string Status { get; set; } = "";
    public DateTime CreatedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
}

public class OrderDetail
{
    public Guid Id { get; set; }
    public object? Rider { get; set; }
    public object? Driver { get; set; }
    public string PickupAddress { get; set; } = "";
    public string DropoffAddress { get; set; } = "";
    public double DistanceKm { get; set; }
    public decimal FinalFare { get; set; }
    public string VehicleCategory { get; set; } = "";
    public string PaymentMethod { get; set; } = "";
    public string Status { get; set; } = "";
    public DateTime CreatedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
}

public class RiderItem
{
    public Guid Id { get; set; }
    public string FullName { get; set; } = "";
    public string Email { get; set; } = "";
    public string PhoneNumber { get; set; } = "";
    public bool IsVerified { get; set; }
    public DateTime CreatedAt { get; set; }
    public int TotalTrips { get; set; }
}

public class DriverItem
{
    public Guid Id { get; set; }
    public string FullName { get; set; } = "";
    public string Email { get; set; } = "";
    public string Status { get; set; } = "";
    public double Rating { get; set; }
    public int TotalTrips { get; set; }
    public decimal TotalEarnings { get; set; }
    public string VehicleType { get; set; } = "";
    public string VehiclePlate { get; set; } = "";
}

public class PaymentItem
{
    public Guid Id { get; set; }
    public Guid OrderId { get; set; }
    public decimal Amount { get; set; }
    public string Method { get; set; } = "";
    public string Status { get; set; } = "";
    public DateTime CreatedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public string? TransactionReference { get; set; }
}

public class PromoItem
{
    public Guid Id { get; set; }
    public string Code { get; set; } = "";
    public string Description { get; set; } = "";
    public string Type { get; set; } = "";
    public decimal Value { get; set; }
    public decimal MaxDiscount { get; set; }
    public DateTime ValidFrom { get; set; }
    public DateTime ValidUntil { get; set; }
    public bool IsActive { get; set; }
    public int UsageLimit { get; set; }
    public int UsageCount { get; set; }
}
