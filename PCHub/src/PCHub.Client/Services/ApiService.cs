using System.Net.Http;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using PCHub.Shared.DTOs;

namespace PCHub.Client.Services;

/// <summary>HTTP client untuk komunikasi dengan PCHub Admin API</summary>
public class ApiService
{
    private readonly HttpClient _http;
    private readonly string _baseUrl;

    public ApiService(string baseUrl)
    {
        // Normalisasi: pastikan baseUrl tidak akhiran "/api" agar tidak double
        _baseUrl = baseUrl.TrimEnd('/');
        // Jika baseUrl berakhiran /api, strip supaya relative path bisa pakai /api/xxx
        if (_baseUrl.EndsWith("/api", StringComparison.OrdinalIgnoreCase))
            _baseUrl = _baseUrl[..^4];

        _http = new HttpClient { BaseAddress = new Uri(_baseUrl) };
    }

    public void SetToken(string token)
    {
        _http.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
    }

    // ==================== AUTH ====================
    public async Task<AuthResponse?> LoginAsync(string username, string password)
    {
        var content = new StringContent(
            JsonSerializer.Serialize(new { username, password }),
            Encoding.UTF8, "application/json");
        var response = await _http.PostAsync("/api/auth/login", content);
        if (!response.IsSuccessStatusCode) return null;
        return await response.Content.ReadFromJsonAsync<AuthResponse>();
    }

    // ==================== PCs ====================
    public async Task<List<PcDto>> GetPcsAsync()
    {
        // Sertakan query params wajib: page=1, pageSize=100 (ambil semua)
        var response = await _http.GetAsync("/api/pcs?page=1&pageSize=100&sortDesc=false");
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<PagedResult<PcDto>>();
        return result?.Items ?? [];
    }

    // ==================== GAMES ====================
    public async Task<List<GameDto>> GetGamesAsync()
    {
        var response = await _http.GetAsync("/api/games?page=1&pageSize=100");
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<PagedResult<GameDto>>();
        return result?.Items ?? [];
    }

    // ==================== BILLING ====================
    public async Task<BillingDto?> StartBillingAsync(Guid userId, Guid pcId)
    {
        var payload = new { userId, pcId, paymentMethod = "Cash" };
        var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
        var response = await _http.PostAsync("/api/billing/start", content);
        if (!response.IsSuccessStatusCode) return null;
        return await response.Content.ReadFromJsonAsync<BillingDto>();
    }

    public async Task<BillingDto?> StopBillingAsync(Guid billingId)
    {
        var response = await _http.PostAsync($"/api/billing/stop/{billingId}", null);
        if (!response.IsSuccessStatusCode) return null;
        return await response.Content.ReadFromJsonAsync<BillingDto>();
    }

    public async Task<BillingDto?> GetActiveBillingAsync(Guid userId)
    {
        var response = await _http.GetAsync($"/api/billing/active/{userId}");
        if (response.StatusCode == System.Net.HttpStatusCode.NoContent) return null;
        if (!response.IsSuccessStatusCode) return null;
        return await response.Content.ReadFromJsonAsync<BillingDto>();
    }

    public async Task<List<BillingDto>> GetBillingHistoryAsync(Guid userId)
    {
        var response = await _http.GetAsync($"/api/billing/history/{userId}");
        if (!response.IsSuccessStatusCode) return [];
        return await response.Content.ReadFromJsonAsync<List<BillingDto>>() ?? [];
    }

    // ==================== DASHBOARD ====================
    public async Task<DashboardStats?> GetDashboardStatsAsync()
    {
        var response = await _http.GetAsync("/api/dashboard/stats");
        if (!response.IsSuccessStatusCode) return null;
        return await response.Content.ReadFromJsonAsync<DashboardStats>();
    }

    // ==================== CHAT ====================
    public async Task<ChatSessionDto?> CreateChatSessionAsync()
    {
        var content = new StringContent("{}", Encoding.UTF8, "application/json");
        var response = await _http.PostAsync("/api/chat/sessions", content);
        if (!response.IsSuccessStatusCode) return null;
        return await response.Content.ReadFromJsonAsync<ChatSessionDto>();
    }

    public async Task<ChatMessageDto?> SendChatMessageAsync(Guid sessionId, string message)
    {
        var payload = new { sessionId, message };
        var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
        var response = await _http.PostAsync("/api/chat/send", content);
        if (!response.IsSuccessStatusCode) return null;
        return await response.Content.ReadFromJsonAsync<ChatMessageDto>();
    }

    public async Task<List<ChatSessionDto>> GetChatSessionsAsync()
    {
        var response = await _http.GetAsync("/api/chat/sessions");
        if (!response.IsSuccessStatusCode) return [];
        return await response.Content.ReadFromJsonAsync<List<ChatSessionDto>>() ?? [];
    }

    // ==================== RESERVATIONS ====================
    public async Task<List<ReservationDto>> GetReservationsAsync()
    {
        var response = await _http.GetAsync("/api/reservations?page=1&pageSize=50");
        if (!response.IsSuccessStatusCode) return [];
        var result = await response.Content.ReadFromJsonAsync<PagedResult<ReservationDto>>();
        return result?.Items ?? [];
    }

    // ==================== MEMBERSHIPS ====================
    public async Task<List<MembershipDto>> GetMembershipsAsync()
    {
        var response = await _http.GetAsync("/api/memberships");
        if (!response.IsSuccessStatusCode) return [];
        return await response.Content.ReadFromJsonAsync<List<MembershipDto>>() ?? [];
    }

    // ==================== PROMOS ====================
    public async Task<List<PromoDto>> GetActivePromosAsync()
    {
        var response = await _http.GetAsync("/api/promos?page=1&pageSize=20");
        if (!response.IsSuccessStatusCode) return [];
        var result = await response.Content.ReadFromJsonAsync<PagedResult<PromoDto>>();
        return result?.Items ?? [];
    }

    // ==================== NOTIFICATIONS ====================
    public async Task<List<NotificationDto>> GetNotificationsAsync(Guid userId)
    {
        var response = await _http.GetAsync($"/api/notifications/{userId}");
        if (!response.IsSuccessStatusCode) return [];
        return await response.Content.ReadFromJsonAsync<List<NotificationDto>>() ?? [];
    }

    // ==================== RESOURCE UPDATE ====================
    public async Task UpdatePcResourceAsync(Guid pcId, double cpu, double gpu, double ram)
    {
        try
        {
            await _http.PutAsync($"/api/pcs/{pcId}/resources?cpu={cpu}&gpu={gpu}&ram={ram}", null);
        }
        catch { /* Silently ignore */ }
    }
}
