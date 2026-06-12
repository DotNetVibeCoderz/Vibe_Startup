using System.Net.Http.Json;

namespace FastRide.DriverApp.Services;

public class ApiClient
{
    private readonly HttpClient _http;
    public Guid CurrentUserId { get; set; }
    public string? AuthToken { get; set; }
    public bool IsLoggedIn => CurrentUserId != Guid.Empty && !string.IsNullOrWhiteSpace(AuthToken);

    public ApiClient(HttpClient http) { _http = http; }

    private void SetAuth() { if (AuthToken != null) _http.DefaultRequestHeaders.Authorization = new("Bearer", AuthToken); }

    public async Task<LoginResult?> Login(string email, string password)
    {
        try
        {
            var resp = await _http.PostAsJsonAsync("/api/auth/login", new { email, password });
            if (!resp.IsSuccessStatusCode) return null;
            var r = await resp.Content.ReadFromJsonAsync<LoginResult>();
            if (r != null) { CurrentUserId = r.UserId; AuthToken = r.Token; SetAuth(); }
            return r;
        }
        catch (Exception ex){ return null; }
    }

    public void Logout() { CurrentUserId = Guid.Empty; AuthToken = null; _http.DefaultRequestHeaders.Authorization = null; }

    public async Task<DriverHomeResult?> GetHome()
    { SetAuth(); return await _http.GetFromJsonAsync<DriverHomeResult>($"/api/mobile/driver/{CurrentUserId}/home"); }

    public async Task<DriverEarningsResult?> GetEarnings(string period = "today")
    { SetAuth(); return await _http.GetFromJsonAsync<DriverEarningsResult>($"/api/mobile/driver/{CurrentUserId}/earnings?period={period}"); }

    public async Task<bool?> ToggleOnline()
    {
        SetAuth();
        var resp = await _http.PutAsync($"/api/mobile/driver/{CurrentUserId}/toggle-online", null);
        if (!resp.IsSuccessStatusCode) return null;
        var r = await resp.Content.ReadFromJsonAsync<ToggleResult>();
        return r?.Status == "Online";
    }

    public async Task<bool> AcceptOrder(Guid orderId)
    {
        SetAuth();
        var resp = await _http.PutAsJsonAsync($"/api/mobile/driver/{CurrentUserId}/accept-order", new { orderId });
        return resp.IsSuccessStatusCode;
    }

    public async Task<bool> CompleteOrder(Guid orderId)
    {
        SetAuth();
        var resp = await _http.PutAsJsonAsync($"/api/mobile/driver/{CurrentUserId}/complete-order", new { orderId });
        return resp.IsSuccessStatusCode;
    }

    public async Task<ProfileResult?> GetProfile()
    { SetAuth(); return await _http.GetFromJsonAsync<ProfileResult>($"/api/profile/{CurrentUserId}"); }

    public async Task<bool> UpdateProfile(string? name, string? phone, string? photo = null, string? mime = null)
    { SetAuth(); var resp = await _http.PutAsJsonAsync($"/api/profile/{CurrentUserId}", new { fullName = name, phoneNumber = phone, profilePhotoBase64 = photo, profilePhotoMimeType = mime }); return resp.IsSuccessStatusCode; }
}

public class LoginResult { public Guid UserId { get; set; } public string FullName { get; set; } = ""; public string Token { get; set; } = ""; public string Role { get; set; } = ""; public string? ProfilePhotoBase64 { get; set; } public string? ProfilePhotoMimeType { get; set; } }
public class DriverHomeResult { public Guid DriverId { get; set; } public string FullName { get; set; } = ""; public bool IsOnline { get; set; } public decimal TodayEarnings { get; set; } public int TodayTrips { get; set; } public double Rating { get; set; } public List<IncomingOrder> IncomingOrders { get; set; } = new(); public List<RecentTrip> RecentTrips { get; set; } = new(); }
public class IncomingOrder { public Guid OrderId { get; set; } public string RiderName { get; set; } = ""; public string PickupAddress { get; set; } = ""; public string DropoffAddress { get; set; } = ""; public double DistanceKm { get; set; } public decimal EstimatedFare { get; set; } public int WaitSeconds { get; set; } }
public class RecentTrip { public Guid OrderId { get; set; } public string RiderName { get; set; } = ""; public string DropoffAddress { get; set; } = ""; public decimal Fare { get; set; } public string Status { get; set; } = ""; public DateTime CreatedAt { get; set; } }
public class DriverEarningsResult { public decimal TodayEarnings { get; set; } public decimal WeekEarnings { get; set; } public decimal MonthEarnings { get; set; } public int TodayTrips { get; set; } public int WeekTrips { get; set; } public int MonthTrips { get; set; } public decimal BaseFareEarnings { get; set; } public decimal BonusEarnings { get; set; } public decimal TipEarnings { get; set; } public List<DailyEarning> DailyBreakdown { get; set; } = new(); }
public class DailyEarning { public DateTime Date { get; set; } public decimal Earnings { get; set; } public int Trips { get; set; } }
public class ToggleResult { public string Status { get; set; } = ""; }
public class ProfileResult { public string FullName { get; set; } = ""; public string Email { get; set; } = ""; public string PhoneNumber { get; set; } = ""; public string? ProfilePhotoBase64 { get; set; } public string? ProfilePhotoMimeType { get; set; } public DriverProfileData? Driver { get; set; } }
public class DriverProfileData { public string LicenseNumber { get; set; } = ""; public string VehicleType { get; set; } = ""; public string VehiclePlate { get; set; } = ""; public string Status { get; set; } = ""; public double Rating { get; set; } public int TotalTrips { get; set; } public decimal TotalEarnings { get; set; } }
