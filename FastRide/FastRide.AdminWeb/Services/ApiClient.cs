using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using FastRide.Shared.Common;
using FastRide.Shared.DTOs;
using FastRide.Shared.Models;

namespace FastRide.AdminWeb.Services;

/// <summary>
/// Typed client for the FastRide API.
///
/// Response shapes come from FastRide.Shared.DTOs — the dashboard no longer keeps its own
/// parallel copy of every model, which is how the old client ended up with fields the API
/// never returned.
/// </summary>
public sealed class ApiClient(HttpClient http, AdminSession session, ILogger<ApiClient> logger)
{
    private static readonly JsonSerializerOptions Json = CreateOptions();

    private static JsonSerializerOptions CreateOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }

    // ─────────────────────────── auth ───────────────────────────

    public async Task<ApiCallResult<AuthResponse>> LoginAsync(string email, string password, CancellationToken ct = default)
    {
        try
        {
            var response = await http.PostAsJsonAsync("/api/auth/login", new LoginRequest(email, password), Json, ct);

            if (response.StatusCode is HttpStatusCode.Unauthorized)
                return ApiCallResult<AuthResponse>.Fail("Email atau kata sandi salah.");

            if (response.StatusCode is HttpStatusCode.TooManyRequests)
                return ApiCallResult<AuthResponse>.Fail("Terlalu banyak percobaan masuk. Tunggu satu menit.");

            if (!response.IsSuccessStatusCode)
                return ApiCallResult<AuthResponse>.Fail(await ReadErrorAsync(response, ct));

            var auth = await response.Content.ReadFromJsonAsync<AuthResponse>(Json, ct);
            if (auth is null) return ApiCallResult<AuthResponse>.Fail("Balasan server tidak bisa dibaca.");

            if (auth.Role != UserRole.Admin)
                return ApiCallResult<AuthResponse>.Fail("Konsol ini hanya untuk akun admin.");

            return ApiCallResult<AuthResponse>.Ok(auth);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Login request failed.");
            return ApiCallResult<AuthResponse>.Fail("Tidak bisa menghubungi API. Pastikan FastRide.Api sedang berjalan.");
        }
    }

    public async Task LogoutAsync(CancellationToken ct = default)
    {
        try
        {
            using var request = Authorized(HttpMethod.Post, "/api/auth/logout");
            await http.SendAsync(request, ct);
        }
        catch (Exception ex)
        {
            // The local session is cleared either way; the server-side stamp bump is best effort.
            logger.LogWarning(ex, "Logout call failed.");
        }
    }

    // ───────────────────────── dashboard ─────────────────────────

    public Task<DashboardOverviewResponse?> GetOverviewAsync(CancellationToken ct = default) =>
        GetAsync<DashboardOverviewResponse>("/api/dashboard/overview", ct);

    public Task<DashboardStatsResponse?> GetStatsAsync(CancellationToken ct = default) =>
        GetAsync<DashboardStatsResponse>("/api/dashboard/stats", ct);

    public Task<List<HourlyStats>> GetHourlyAsync(DateTime? date = null, CancellationToken ct = default) =>
        GetListAsync<HourlyStats>($"/api/dashboard/orders-by-hour{(date is null ? "" : $"?date={date:yyyy-MM-dd}")}", ct);

    public Task<List<RevenuePoint>> GetRevenueSeriesAsync(int days = 30, CancellationToken ct = default) =>
        GetListAsync<RevenuePoint>($"/api/dashboard/revenue-series?days={days}", ct);

    public Task<List<TopDriverItem>> GetTopDriversAsync(int limit = 10, CancellationToken ct = default) =>
        GetListAsync<TopDriverItem>($"/api/dashboard/top-drivers?limit={limit}", ct);

    public Task<FinancialSummaryResponse?> GetFinancialSummaryAsync(DateTime? from, DateTime? to, CancellationToken ct = default) =>
        GetAsync<FinancialSummaryResponse>($"/api/dashboard/financial-summary{DateRange(from, to)}", ct);

    // ─────────────────────────── orders ───────────────────────────

    public Task<PagedResult<OrderListItem>> GetOrdersAsync(OrderFilter filter, CancellationToken ct = default) =>
        GetPagedAsync<OrderListItem>($"/api/orders{filter.ToQueryString()}", ct);

    public Task<OrderDetailResponse?> GetOrderAsync(Guid id, CancellationToken ct = default) =>
        GetAsync<OrderDetailResponse>($"/api/orders/{id}", ct);

    public Task<byte[]?> ExportOrdersCsvAsync(OrderFilter filter, CancellationToken ct = default) =>
        DownloadAsync($"/api/orders/export.csv{filter.ToQueryString()}", ct);

    public Task<byte[]?> ExportFinancialCsvAsync(DateTime? from, DateTime? to, CancellationToken ct = default) =>
        DownloadAsync($"/api/dashboard/financial-summary/export.csv{DateRange(from, to)}", ct);

    public async Task<string?> CancelOrderAsync(Guid id, string reason, CancellationToken ct = default)
    {
        using var request = Authorized(HttpMethod.Post, $"/api/orders/{id}/cancel");
        request.Content = JsonContent.Create(new CancelOrderRequest(reason), options: Json);

        using var response = await http.SendAsync(request, ct);
        return response.IsSuccessStatusCode ? null : await ReadErrorAsync(response, ct);
    }

    // ──────────────────────── people ────────────────────────

    public Task<PagedResult<DriverListItem>> GetDriversAsync(
        int page = 1, int limit = 20, string? search = null, DriverStatus? status = null, bool? verified = null,
        CancellationToken ct = default)
    {
        var query = $"/api/drivers?page={page}&limit={limit}";
        if (!string.IsNullOrWhiteSpace(search)) query += $"&search={Uri.EscapeDataString(search)}";
        if (status is not null) query += $"&status={status}";
        if (verified is not null) query += $"&verified={verified.Value.ToString().ToLowerInvariant()}";

        return GetPagedAsync<DriverListItem>(query, ct);
    }

    public Task<PagedResult<RiderListItem>> GetRidersAsync(
        int page = 1, int limit = 20, string? search = null, CancellationToken ct = default)
    {
        var query = $"/api/riders?page={page}&limit={limit}";
        if (!string.IsNullOrWhiteSpace(search)) query += $"&search={Uri.EscapeDataString(search)}";

        return GetPagedAsync<RiderListItem>(query, ct);
    }

    public Task<PagedResult<UserProfileResponse>> GetUsersAsync(
        int page = 1, int limit = 25, string? search = null, UserRole? role = null, bool? active = null,
        CancellationToken ct = default)
    {
        var query = $"/api/admin/users?page={page}&limit={limit}";
        if (!string.IsNullOrWhiteSpace(search)) query += $"&search={Uri.EscapeDataString(search)}";
        if (role is not null) query += $"&role={role}";
        if (active is not null) query += $"&active={active.Value.ToString().ToLowerInvariant()}";

        return GetPagedAsync<UserProfileResponse>(query, ct);
    }

    public async Task<string?> SetUserActiveAsync(Guid userId, bool active, string? reason, CancellationToken ct = default)
    {
        using var request = Authorized(HttpMethod.Put, $"/api/admin/users/{userId}/active");
        request.Content = JsonContent.Create(new SetUserActiveRequest(active, reason), options: Json);

        using var response = await http.SendAsync(request, ct);
        return response.IsSuccessStatusCode ? null : await ReadErrorAsync(response, ct);
    }

    public Task<List<PendingDriverItem>> GetPendingVerificationAsync(CancellationToken ct = default) =>
        GetListAsync<PendingDriverItem>("/api/admin/drivers/pending-verification", ct);

    public async Task<string?> ReviewDocumentAsync(
        Guid driverId, Guid documentId, DocumentStatus status, string? notes, CancellationToken ct = default)
    {
        using var request = Authorized(HttpMethod.Put, $"/api/drivers/{driverId}/documents/{documentId}/review");
        request.Content = JsonContent.Create(new ReviewDocumentRequest(status, notes), options: Json);

        using var response = await http.SendAsync(request, ct);
        return response.IsSuccessStatusCode ? null : await ReadErrorAsync(response, ct);
    }

    // ──────────────────────── payments ────────────────────────

    public Task<PagedResult<PaymentResponse>> GetPaymentsAsync(
        int page = 1, int limit = 25, PaymentMethod? method = null, DateTime? from = null, DateTime? to = null,
        CancellationToken ct = default)
    {
        var query = $"/api/payments?page={page}&limit={limit}";
        if (method is not null) query += $"&method={method}";
        if (from is not null) query += $"&from={from:yyyy-MM-dd}";
        if (to is not null) query += $"&to={to:yyyy-MM-dd}";

        return GetPagedAsync<PaymentResponse>(query, ct);
    }

    // ───────────────────── promos & fares ─────────────────────

    public Task<List<PromoResponse>> GetPromosAsync(CancellationToken ct = default) =>
        GetListAsync<PromoResponse>("/api/promos", ct);

    public async Task<string?> SavePromoAsync(Guid? id, SavePromoRequest promo, CancellationToken ct = default)
    {
        using var request = id is null
            ? Authorized(HttpMethod.Post, "/api/promos")
            : Authorized(HttpMethod.Put, $"/api/promos/{id}");

        request.Content = JsonContent.Create(promo, options: Json);

        using var response = await http.SendAsync(request, ct);
        return response.IsSuccessStatusCode ? null : await ReadErrorAsync(response, ct);
    }

    public async Task<string?> DeletePromoAsync(Guid id, CancellationToken ct = default)
    {
        using var request = Authorized(HttpMethod.Delete, $"/api/promos/{id}");
        using var response = await http.SendAsync(request, ct);
        return response.IsSuccessStatusCode ? null : await ReadErrorAsync(response, ct);
    }

    public Task<List<FareConfigResponse>> GetFaresAsync(CancellationToken ct = default) =>
        GetListAsync<FareConfigResponse>("/api/fares", ct);

    // ───────────────────── payment providers ─────────────────────

    public Task<List<PaymentProviderResponse>> GetPaymentProvidersAsync(CancellationToken ct = default) =>
        GetListAsync<PaymentProviderResponse>("/api/admin/payment-providers", ct);

    public async Task<List<PaymentMethod>> GetAvailablePaymentMethodsAsync(CancellationToken ct = default)
    {
        var response = await GetAsync<AvailablePaymentMethodsResponse>("/api/payments/methods", ct);

        return response?.Methods.Select(option => option.Method).ToList() ?? [];
    }

    public async Task<string?> SavePaymentProviderAsync(
        string name, SavePaymentProviderRequest request, CancellationToken ct = default)
    {
        using var message = Authorized(HttpMethod.Put, $"/api/admin/payment-providers/{name}");
        message.Content = JsonContent.Create(request, options: Json);

        using var response = await http.SendAsync(message, ct);
        return response.IsSuccessStatusCode ? null : await ReadErrorAsync(response, ct);
    }

    public async Task<(string? Message, string? Error)> TestPaymentProviderAsync(
        string name, CancellationToken ct = default)
    {
        using var request = Authorized(HttpMethod.Post, $"/api/admin/payment-providers/{name}/test");
        using var response = await http.SendAsync(request, ct);

        if (!response.IsSuccessStatusCode) return (null, await ReadErrorAsync(response, ct));

        var body = await response.Content.ReadFromJsonAsync<MessageResponse>(Json, ct);
        return (body?.Message ?? "Provider merespons.", null);
    }

    public async Task<string?> UpdateFareAsync(VehicleCategory category, UpdateFareConfigRequest fare, CancellationToken ct = default)
    {
        using var request = Authorized(HttpMethod.Put, $"/api/fares/{category}");
        request.Content = JsonContent.Create(fare, options: Json);

        using var response = await http.SendAsync(request, ct);
        return response.IsSuccessStatusCode ? null : await ReadErrorAsync(response, ct);
    }

    // ─────────────────────────── plumbing ───────────────────────────

    private HttpRequestMessage Authorized(HttpMethod method, string path)
    {
        var request = new HttpRequestMessage(method, path);

        if (!string.IsNullOrWhiteSpace(session.Token))
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", session.Token);

        return request;
    }

    private async Task<T?> GetAsync<T>(string path, CancellationToken ct) where T : class
    {
        try
        {
            using var request = Authorized(HttpMethod.Get, path);
            using var response = await http.SendAsync(request, ct);

            ThrowIfSessionEnded(response);
            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning("GET {Path} returned {Status}.", path, (int)response.StatusCode);
                return null;
            }

            return await response.Content.ReadFromJsonAsync<T>(Json, ct);
        }
        catch (SessionExpiredException) { throw; }
        catch (Exception ex)
        {
            logger.LogError(ex, "GET {Path} failed.", path);
            return null;
        }
    }

    private async Task<List<T>> GetListAsync<T>(string path, CancellationToken ct)
    {
        try
        {
            using var request = Authorized(HttpMethod.Get, path);
            using var response = await http.SendAsync(request, ct);

            ThrowIfSessionEnded(response);
            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning("GET {Path} returned {Status}.", path, (int)response.StatusCode);
                return [];
            }

            return await response.Content.ReadFromJsonAsync<List<T>>(Json, ct) ?? [];
        }
        catch (SessionExpiredException) { throw; }
        catch (Exception ex)
        {
            logger.LogError(ex, "GET {Path} failed.", path);
            return [];
        }
    }

    private async Task<PagedResult<T>> GetPagedAsync<T>(string path, CancellationToken ct) where T : class =>
        await GetAsync<PagedResult<T>>(path, ct) ?? PagedResult<T>.Empty();

    private async Task<byte[]?> DownloadAsync(string path, CancellationToken ct)
    {
        try
        {
            using var request = Authorized(HttpMethod.Get, path);
            using var response = await http.SendAsync(request, ct);

            ThrowIfSessionEnded(response);
            return response.IsSuccessStatusCode ? await response.Content.ReadAsByteArrayAsync(ct) : null;
        }
        catch (SessionExpiredException) { throw; }
        catch (Exception ex)
        {
            logger.LogError(ex, "Download of {Path} failed.", path);
            return null;
        }
    }

    /// <summary>A 401 means the token was revoked or expired; the shell turns this into a sign-in prompt.</summary>
    private static void ThrowIfSessionEnded(HttpResponseMessage response)
    {
        if (response.StatusCode == HttpStatusCode.Unauthorized) throw new SessionExpiredException();
    }

    private static async Task<string> ReadErrorAsync(HttpResponseMessage response, CancellationToken ct)
    {
        try
        {
            var error = await response.Content.ReadFromJsonAsync<ApiError>(Json, ct);
            if (!string.IsNullOrWhiteSpace(error?.Detail)) return error.Detail;
            if (!string.IsNullOrWhiteSpace(error?.Error)) return error.Error;
        }
        catch
        {
            // Fall through to the status code.
        }

        return $"Permintaan gagal ({(int)response.StatusCode}).";
    }

    private static string DateRange(DateTime? from, DateTime? to)
    {
        var parts = new List<string>();
        if (from is not null) parts.Add($"from={from:yyyy-MM-dd}");
        if (to is not null) parts.Add($"to={to:yyyy-MM-dd}");

        return parts.Count == 0 ? string.Empty : "?" + string.Join('&', parts);
    }
}

/// <summary>Order list filter, kept in one place because the table and the CSV export share it.</summary>
public sealed class OrderFilter
{
    public int Page { get; set; } = 1;
    public int Limit { get; set; } = 25;
    public OrderStatus? Status { get; set; }
    public VehicleCategory? VehicleCategory { get; set; }
    public PaymentMethod? PaymentMethod { get; set; }
    public DateTime? From { get; set; }
    public DateTime? To { get; set; }
    public string? Search { get; set; }

    public string ToQueryString()
    {
        var parts = new List<string> { $"page={Page}", $"limit={Limit}" };

        if (Status is not null) parts.Add($"status={Status}");
        if (VehicleCategory is not null) parts.Add($"vehicleCategory={VehicleCategory}");
        if (PaymentMethod is not null) parts.Add($"paymentMethod={PaymentMethod}");
        if (From is not null) parts.Add($"from={From:yyyy-MM-dd}");
        if (To is not null) parts.Add($"to={To:yyyy-MM-ddTHH:mm:ss}");
        if (!string.IsNullOrWhiteSpace(Search)) parts.Add($"search={Uri.EscapeDataString(Search)}");

        return "?" + string.Join('&', parts);
    }

    public OrderFilter Clone() => (OrderFilter)MemberwiseClone();
}

/// <summary>Row of the driver verification queue.</summary>
public sealed record PendingDriverItem(
    Guid DriverId, string FullName, string Email, string? PhotoUrl,
    string VehicleType, string VehiclePlate, DateTime JoinedAt,
    List<DriverDocumentResponse> Documents);
