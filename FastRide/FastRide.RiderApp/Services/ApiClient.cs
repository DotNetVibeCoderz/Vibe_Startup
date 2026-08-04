using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using FastRide.Shared.Common;
using FastRide.Shared.DTOs;
using FastRide.Shared.Models;

namespace FastRide.RiderApp.Services;

/// <summary>
/// Rider-side API client.
///
/// Request and response types come from FastRide.Shared — this app used to keep its own
/// copies of the DTOs *and its own copies of the enums*, so renumbering a category in one
/// place silently changed what the other one meant.
///
/// The session is persisted with MAUI SecureStorage, so closing the app no longer signs
/// the rider out.
/// </summary>
public sealed class ApiClient(HttpClient http)
{
    private const string TokenKey = "fastride.rider.token";
    private const string UserKey = "fastride.rider.user";
    private const string NameKey = "fastride.rider.name";

    private static readonly JsonSerializerOptions Json = CreateOptions();

    private static JsonSerializerOptions CreateOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }

    public Guid CurrentUserId { get; private set; }
    public string CurrentUserName { get; private set; } = string.Empty;
    public string? AuthToken { get; private set; }

    public bool IsLoggedIn => CurrentUserId != Guid.Empty && !string.IsNullOrWhiteSpace(AuthToken);

    /// <summary>Raised when the API rejects the token, so screens can drop back to sign-in.</summary>
    public event Action? SignedOut;

    // ─────────────────────────── session ───────────────────────────

    /// <summary>Reload a stored session on launch. Safe to call more than once.</summary>
    public async Task<bool> RestoreSessionAsync()
    {
        if (IsLoggedIn) return true;

        try
        {
            var token = await SecureStorage.Default.GetAsync(TokenKey);
            var userId = await SecureStorage.Default.GetAsync(UserKey);
            var name = await SecureStorage.Default.GetAsync(NameKey);

            if (string.IsNullOrWhiteSpace(token) || !Guid.TryParse(userId, out var id)) return false;

            AuthToken = token;
            CurrentUserId = id;
            CurrentUserName = name ?? string.Empty;
            ApplyAuthHeader();

            // A stored token may have been revoked while the app was closed.
            return await GetProfileAsync() is not null;
        }
        catch (Exception)
        {
            // SecureStorage is unavailable on some targets; fall back to signing in again.
            return false;
        }
    }

    public async Task<(bool Success, string? Error)> LoginAsync(string email, string password)
    {
        try
        {
            var response = await http.PostAsJsonAsync("/api/auth/login", new LoginRequest(email, password), Json);

            if (response.StatusCode == HttpStatusCode.Unauthorized)
                return (false, "Email atau kata sandi salah.");

            if (response.StatusCode == HttpStatusCode.TooManyRequests)
                return (false, "Terlalu banyak percobaan. Tunggu sebentar.");

            if (!response.IsSuccessStatusCode)
                return (false, await ReadErrorAsync(response));

            var auth = await response.Content.ReadFromJsonAsync<AuthResponse>(Json);
            if (auth is null) return (false, "Balasan server tidak bisa dibaca.");

            if (auth.Role != UserRole.Rider)
                return (false, "Akun ini bukan akun penumpang.");

            CurrentUserId = auth.UserId;
            CurrentUserName = auth.FullName;
            AuthToken = auth.Token;
            ApplyAuthHeader();

            await PersistAsync();
            return (true, null);
        }
        catch (Exception ex)
        {
            return (false, $"Tidak bisa menghubungi server. {ex.Message}");
        }
    }

    public async Task<(bool Success, string? Error)> RegisterAsync(
        string fullName, string email, string phone, string password)
    {
        try
        {
            var response = await http.PostAsJsonAsync("/api/auth/register",
                new RegisterRequest(fullName, email, phone, password, UserRole.Rider), Json);

            if (!response.IsSuccessStatusCode) return (false, await ReadErrorAsync(response));

            var auth = await response.Content.ReadFromJsonAsync<AuthResponse>(Json);
            if (auth is null) return (false, "Balasan server tidak bisa dibaca.");

            CurrentUserId = auth.UserId;
            CurrentUserName = auth.FullName;
            AuthToken = auth.Token;
            ApplyAuthHeader();

            await PersistAsync();
            return (true, null);
        }
        catch (Exception ex)
        {
            return (false, $"Tidak bisa menghubungi server. {ex.Message}");
        }
    }

    public async Task LogoutAsync()
    {
        try
        {
            await http.PostAsync("/api/auth/logout", null);
        }
        catch (Exception)
        {
            // Clearing the local session is what matters; the server call is best effort.
        }

        ClearSession();

        // Same signal as an expired token, so the shell drops back to the sign-in screen.
        SignedOut?.Invoke();
    }

    // ─────────────────────────── rider data ───────────────────────────

    public Task<UserProfileResponse?> GetProfileAsync() =>
        GetAsync<UserProfileResponse>($"/api/profile/{CurrentUserId}");

    public Task<RiderHomeResponse?> GetHomeAsync() =>
        GetAsync<RiderHomeResponse>($"/api/mobile/rider/{CurrentUserId}/home");

    public async Task<PagedResult<OrderListItem>> GetTripsAsync(int page = 1, int limit = 20) =>
        await GetAsync<PagedResult<OrderListItem>>($"/api/mobile/rider/{CurrentUserId}/trips?page={page}&limit={limit}")
        ?? PagedResult<OrderListItem>.Empty(page, limit);

    public Task<OrderDetailResponse?> GetOrderAsync(Guid orderId) =>
        GetAsync<OrderDetailResponse>($"/api/orders/{orderId}");

    public Task<OrderTrackingResponse?> TrackOrderAsync(Guid orderId) =>
        GetAsync<OrderTrackingResponse>($"/api/orders/{orderId}/tracking");

    public Task<List<NearbyDriverItem>> GetNearbyDriversAsync(double latitude, double longitude, double radiusKm = 5) =>
        GetListAsync<NearbyDriverItem>(
            $"/api/drivers/nearby?lat={Fmt(latitude)}&lng={Fmt(longitude)}&radiusKm={Fmt(radiusKm)}");

    public Task<UnreadCountResponse?> GetUnreadAsync() =>
        GetAsync<UnreadCountResponse>($"/api/notifications/{CurrentUserId}/unread-count");

    public async Task<PagedResult<NotificationResponse>> GetNotificationsAsync(int page = 1) =>
        await GetAsync<PagedResult<NotificationResponse>>($"/api/notifications/{CurrentUserId}?page={page}")
        ?? PagedResult<NotificationResponse>.Empty(page);

    // ─────────────────────────── booking ───────────────────────────

    /// <summary>
    /// Ask the API what a trip will cost. The price shown to the rider now comes from the
    /// same fare table the booking is charged against — the old app displayed hardcoded
    /// numbers that had nothing to do with the real fare.
    /// </summary>
    public Task<FareQuoteResponse?> QuoteAsync(FareQuoteRequest request) =>
        PostAsync<FareQuoteRequest, FareQuoteResponse>("/api/orders/quote", request);

    public async Task<(CreateOrderResponse? Order, string? Error)> BookAsync(CreateOrderRequest request)
    {
        try
        {
            var response = await http.PostAsJsonAsync("/api/orders", request, Json);
            if (SignalIfUnauthorized(response)) return (null, "Sesi berakhir. Masuk lagi ya.");

            if (!response.IsSuccessStatusCode) return (null, await ReadErrorAsync(response));

            return (await response.Content.ReadFromJsonAsync<CreateOrderResponse>(Json), null);
        }
        catch (Exception ex)
        {
            return (null, $"Gagal memesan. {ex.Message}");
        }
    }

    public async Task<string?> CancelOrderAsync(Guid orderId, string reason)
    {
        try
        {
            var response = await http.PostAsJsonAsync($"/api/orders/{orderId}/cancel", new CancelOrderRequest(reason), Json);
            if (SignalIfUnauthorized(response)) return "Sesi berakhir. Masuk lagi ya.";

            return response.IsSuccessStatusCode ? null : await ReadErrorAsync(response);
        }
        catch (Exception ex)
        {
            return $"Gagal membatalkan. {ex.Message}";
        }
    }

    public Task<ValidatePromoResponse?> ValidatePromoAsync(string code, decimal amount, VehicleCategory category) =>
        PostAsync<ValidatePromoRequest, ValidatePromoResponse>(
            "/api/promos/validate", new ValidatePromoRequest(code, amount, category));

    // ─────────────────────────── payments ───────────────────────────

    /// <summary>Methods the platform currently has switched on. Never hardcode this list.</summary>
    public async Task<List<PaymentMethodOption>> GetPaymentMethodsAsync()
    {
        var response = await GetAsync<AvailablePaymentMethodsResponse>("/api/payments/methods");

        return response?.Methods ?? [];
    }

    public Task<PaymentResponse?> GetPaymentAsync(Guid orderId) =>
        GetAsync<PaymentResponse>($"/api/payments/order/{orderId}");

    /// <summary>
    /// Start or retry a charge. Safe to call again: a charge already waiting on the payer
    /// returns the same QR rather than issuing a second one.
    /// </summary>
    public async Task<(PaymentResponse? Payment, string? Error)> ChargeAsync(
        Guid orderId, PaymentMethod method, EWalletChannel channel = EWalletChannel.Unspecified)
    {
        try
        {
            var response = await http.PostAsJsonAsync(
                "/api/payments", new PaymentRequest(orderId, method, 0, channel), Json);

            if (SignalIfUnauthorized(response)) return (null, "Sesi berakhir. Masuk lagi ya.");
            if (!response.IsSuccessStatusCode) return (null, await ReadErrorAsync(response));

            return (await response.Content.ReadFromJsonAsync<PaymentResponse>(Json), null);
        }
        catch (Exception ex)
        {
            return (null, $"Gagal memulai pembayaran. {ex.Message}");
        }
    }

    /// <summary>
    /// Stand in for the payer against the sandbox provider. Present only outside production —
    /// it lets the demo complete a payment without scanning anything.
    /// </summary>
    public async Task<(PaymentResponse? Payment, string? Error)> SandboxSettleAsync(Guid orderId)
    {
        try
        {
            var response = await http.PostAsync($"/api/payments/sandbox/{orderId}/settle", null);

            if (SignalIfUnauthorized(response)) return (null, "Sesi berakhir. Masuk lagi ya.");
            if (!response.IsSuccessStatusCode) return (null, await ReadErrorAsync(response));

            return (await response.Content.ReadFromJsonAsync<PaymentResponse>(Json), null);
        }
        catch (Exception ex)
        {
            return (null, $"Gagal menyelesaikan simulasi. {ex.Message}");
        }
    }

    public async Task<string?> SubmitReviewAsync(Guid orderId, Guid driverId, int rating, string? comment)
    {
        try
        {
            var response = await http.PostAsJsonAsync("/api/reviews",
                new SubmitReviewRequest(orderId, CurrentUserId, driverId, rating, comment), Json);

            if (SignalIfUnauthorized(response)) return "Sesi berakhir. Masuk lagi ya.";
            return response.IsSuccessStatusCode ? null : await ReadErrorAsync(response);
        }
        catch (Exception ex)
        {
            return $"Gagal mengirim ulasan. {ex.Message}";
        }
    }

    public async Task<string?> UpdateProfileAsync(string? fullName, string? phone, string? photoBase64, string? mimeType)
    {
        try
        {
            var response = await http.PutAsJsonAsync($"/api/profile/{CurrentUserId}",
                new UpdateProfileRequest(fullName, phone, photoBase64, mimeType), Json);

            if (SignalIfUnauthorized(response)) return "Sesi berakhir. Masuk lagi ya.";
            if (!response.IsSuccessStatusCode) return await ReadErrorAsync(response);

            if (!string.IsNullOrWhiteSpace(fullName))
            {
                CurrentUserName = fullName;
                await PersistAsync();
            }

            return null;
        }
        catch (Exception ex)
        {
            return $"Gagal menyimpan profil. {ex.Message}";
        }
    }

    // ─────────────────────────── plumbing ───────────────────────────

    private void ApplyAuthHeader() =>
        http.DefaultRequestHeaders.Authorization = AuthToken is null
            ? null
            : new AuthenticationHeaderValue("Bearer", AuthToken);

    private async Task PersistAsync()
    {
        try
        {
            await SecureStorage.Default.SetAsync(TokenKey, AuthToken ?? string.Empty);
            await SecureStorage.Default.SetAsync(UserKey, CurrentUserId.ToString());
            await SecureStorage.Default.SetAsync(NameKey, CurrentUserName);
        }
        catch (Exception)
        {
            // Not every platform provides a keystore; the in-memory session still works.
        }
    }

    private void ClearSession()
    {
        CurrentUserId = Guid.Empty;
        CurrentUserName = string.Empty;
        AuthToken = null;
        ApplyAuthHeader();

        try
        {
            SecureStorage.Default.Remove(TokenKey);
            SecureStorage.Default.Remove(UserKey);
            SecureStorage.Default.Remove(NameKey);
        }
        catch (Exception)
        {
            // Nothing stored.
        }
    }

    private bool SignalIfUnauthorized(HttpResponseMessage response)
    {
        if (response.StatusCode != HttpStatusCode.Unauthorized) return false;

        ClearSession();
        SignedOut?.Invoke();
        return true;
    }

    private async Task<T?> GetAsync<T>(string path) where T : class
    {
        try
        {
            using var response = await http.GetAsync(path);
            if (SignalIfUnauthorized(response) || !response.IsSuccessStatusCode) return null;

            return await response.Content.ReadFromJsonAsync<T>(Json);
        }
        catch (Exception)
        {
            return null;
        }
    }

    private async Task<List<T>> GetListAsync<T>(string path)
    {
        try
        {
            using var response = await http.GetAsync(path);
            if (SignalIfUnauthorized(response) || !response.IsSuccessStatusCode) return [];

            return await response.Content.ReadFromJsonAsync<List<T>>(Json) ?? [];
        }
        catch (Exception)
        {
            return [];
        }
    }

    private async Task<TResponse?> PostAsync<TRequest, TResponse>(string path, TRequest body)
        where TResponse : class
    {
        try
        {
            using var response = await http.PostAsJsonAsync(path, body, Json);
            if (SignalIfUnauthorized(response) || !response.IsSuccessStatusCode) return null;

            return await response.Content.ReadFromJsonAsync<TResponse>(Json);
        }
        catch (Exception)
        {
            return null;
        }
    }

    private static async Task<string> ReadErrorAsync(HttpResponseMessage response)
    {
        try
        {
            var error = await response.Content.ReadFromJsonAsync<ApiError>(Json);
            if (!string.IsNullOrWhiteSpace(error?.Detail)) return error.Detail;
            if (!string.IsNullOrWhiteSpace(error?.Error)) return error.Error;
        }
        catch
        {
            // Fall through.
        }

        return $"Permintaan gagal ({(int)response.StatusCode}).";
    }

    /// <summary>Coordinates must go on the wire with a dot, whatever the phone's locale is.</summary>
    private static string Fmt(double value) =>
        value.ToString(System.Globalization.CultureInfo.InvariantCulture);
}
