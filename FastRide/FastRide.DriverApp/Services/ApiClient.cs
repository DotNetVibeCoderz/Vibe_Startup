using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using FastRide.Shared.Common;
using FastRide.Shared.DTOs;
using FastRide.Shared.Models;

namespace FastRide.DriverApp.Services;

/// <summary>
/// Driver-side API client, built on the shared contracts rather than a private copy of them.
/// The session survives an app restart via SecureStorage.
/// </summary>
public sealed class ApiClient(HttpClient http)
{
    private const string TokenKey = "fastride.driver.token";
    private const string UserKey = "fastride.driver.user";
    private const string NameKey = "fastride.driver.name";

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

    public event Action? SignedOut;

    // ─────────────────────────── session ───────────────────────────

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

            if (!response.IsSuccessStatusCode) return (false, await ReadErrorAsync(response));

            var auth = await response.Content.ReadFromJsonAsync<AuthResponse>(Json);
            if (auth is null) return (false, "Balasan server tidak bisa dibaca.");

            if (auth.Role != UserRole.Driver)
                return (false, "Akun ini bukan akun driver.");

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

    // ─────────────────────────── driver data ───────────────────────────

    public Task<UserProfileResponse?> GetProfileAsync() =>
        GetAsync<UserProfileResponse>($"/api/profile/{CurrentUserId}");

    public Task<DriverHomeResponse?> GetHomeAsync() =>
        GetAsync<DriverHomeResponse>($"/api/mobile/driver/{CurrentUserId}/home");

    public Task<DriverEarningsResponse?> GetEarningsAsync(string period = "month") =>
        GetAsync<DriverEarningsResponse>($"/api/mobile/driver/{CurrentUserId}/earnings?period={period}");

    public Task<List<IncomingOrderItem>> GetAvailableOrdersAsync(double radiusKm = 12) =>
        GetListAsync<IncomingOrderItem>(
            $"/api/mobile/driver/{CurrentUserId}/orders/available?radiusKm={Fmt(radiusKm)}");

    public Task<List<DriverDocumentResponse>> GetDocumentsAsync() =>
        GetListAsync<DriverDocumentResponse>($"/api/drivers/{CurrentUserId}/documents");

    public Task<OrderDetailResponse?> GetOrderAsync(Guid orderId) =>
        GetAsync<OrderDetailResponse>($"/api/orders/{orderId}");

    public Task<UnreadCountResponse?> GetUnreadAsync() =>
        GetAsync<UnreadCountResponse>($"/api/notifications/{CurrentUserId}/unread-count");

    // ─────────────────────────── driver actions ───────────────────────────

    /// <summary>Push a GPS ping. The dispatcher ignores drivers whose last fix is over ten minutes old.</summary>
    public async Task<bool> UpdateLocationAsync(double latitude, double longitude, double heading = 0)
    {
        try
        {
            var response = await http.PutAsJsonAsync($"/api/mobile/driver/{CurrentUserId}/location",
                new UpdateLocationRequest(latitude, longitude, heading), Json);

            return !SignalIfUnauthorized(response) && response.IsSuccessStatusCode;
        }
        catch (Exception)
        {
            return false;
        }
    }

    public async Task<(DriverStatus? Status, string? Error)> SetStatusAsync(DriverStatus status)
    {
        try
        {
            var response = await http.PutAsJsonAsync($"/api/mobile/driver/{CurrentUserId}/status",
                new SetDriverStatusRequest(status), Json);

            if (SignalIfUnauthorized(response)) return (null, "Sesi berakhir. Masuk lagi ya.");
            if (!response.IsSuccessStatusCode) return (null, await ReadErrorAsync(response));

            var result = await response.Content.ReadFromJsonAsync<DriverStatusResponse>(Json);
            return (result?.Status, null);
        }
        catch (Exception ex)
        {
            return (null, $"Gagal mengubah status. {ex.Message}");
        }
    }

    public Task<string?> AcceptOrderAsync(Guid orderId) => ActOnOrderAsync("accept-order", orderId);
    public Task<string?> ArriveOrderAsync(Guid orderId) => ActOnOrderAsync("arrive-order", orderId);
    public Task<string?> StartOrderAsync(Guid orderId) => ActOnOrderAsync("start-order", orderId);
    public Task<string?> CompleteOrderAsync(Guid orderId) => ActOnOrderAsync("complete-order", orderId);

    public async Task<string?> CancelOrderAsync(Guid orderId, string reason)
    {
        try
        {
            var response = await http.PostAsJsonAsync($"/api/orders/{orderId}/cancel",
                new CancelOrderRequest(reason), Json);

            if (SignalIfUnauthorized(response)) return "Sesi berakhir. Masuk lagi ya.";
            return response.IsSuccessStatusCode ? null : await ReadErrorAsync(response);
        }
        catch (Exception ex)
        {
            return $"Gagal membatalkan. {ex.Message}";
        }
    }

    public async Task<string?> UploadDocumentAsync(DocumentType type, byte[] content, string mimeType, DateTime? expiresAt)
    {
        try
        {
            var response = await http.PostAsJsonAsync($"/api/drivers/{CurrentUserId}/documents",
                new UploadDocumentRequest(type, Convert.ToBase64String(content), mimeType, expiresAt), Json);

            if (SignalIfUnauthorized(response)) return "Sesi berakhir. Masuk lagi ya.";
            return response.IsSuccessStatusCode ? null : await ReadErrorAsync(response);
        }
        catch (Exception ex)
        {
            return $"Gagal mengunggah dokumen. {ex.Message}";
        }
    }

    public async Task<string?> UpdateProfileAsync(string? fullName, string? phone)
    {
        try
        {
            var response = await http.PutAsJsonAsync($"/api/profile/{CurrentUserId}",
                new UpdateProfileRequest(fullName, phone), Json);

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

    public async Task<string?> UpdateVehicleAsync(string? licenseNumber, string? vehicleType, string? plate, VehicleCategory? category)
    {
        try
        {
            var response = await http.PutAsJsonAsync($"/api/profile/{CurrentUserId}/driver",
                new UpdateDriverProfileRequest(licenseNumber, vehicleType, plate, category), Json);

            if (SignalIfUnauthorized(response)) return "Sesi berakhir. Masuk lagi ya.";
            return response.IsSuccessStatusCode ? null : await ReadErrorAsync(response);
        }
        catch (Exception ex)
        {
            return $"Gagal menyimpan kendaraan. {ex.Message}";
        }
    }

    // ─────────────────────────── plumbing ───────────────────────────

    private async Task<string?> ActOnOrderAsync(string action, Guid orderId)
    {
        try
        {
            var response = await http.PutAsJsonAsync($"/api/mobile/driver/{CurrentUserId}/{action}",
                new AcceptOrderRequest(orderId), Json);

            if (SignalIfUnauthorized(response)) return "Sesi berakhir. Masuk lagi ya.";
            return response.IsSuccessStatusCode ? null : await ReadErrorAsync(response);
        }
        catch (Exception ex)
        {
            return $"Perintah gagal. {ex.Message}";
        }
    }

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
            // Keystore unavailable; the in-memory session still works for this run.
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
            // Fall through to the status code.
        }

        return $"Permintaan gagal ({(int)response.StatusCode}).";
    }

    /// <summary>Coordinates must go on the wire with a dot, whatever the phone's locale is.</summary>
    private static string Fmt(double value) =>
        value.ToString(System.Globalization.CultureInfo.InvariantCulture);
}
