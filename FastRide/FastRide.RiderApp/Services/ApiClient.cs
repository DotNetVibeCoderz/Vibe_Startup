using System.Net.Http.Json;

namespace FastRide.RiderApp.Services;

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
        catch(Exception ex)
        {
            return null;
        }
    }
    public void Logout() { CurrentUserId = Guid.Empty; AuthToken = null; _http.DefaultRequestHeaders.Authorization = null; }

    public async Task<ProfileResult?> GetProfile() { SetAuth(); return await _http.GetFromJsonAsync<ProfileResult>($"/api/profile/{CurrentUserId}"); }
    public async Task<bool> UpdateProfile(string? name, string? phone, string? photoB64 = null, string? mime = null)
    { SetAuth(); var resp = await _http.PutAsJsonAsync($"/api/profile/{CurrentUserId}", new { fullName = name, phoneNumber = phone, profilePhotoBase64 = photoB64, profilePhotoMimeType = mime }); return resp.IsSuccessStatusCode; }
    public async Task<RiderHomeResult?> GetHome() { SetAuth(); return await _http.GetFromJsonAsync<RiderHomeResult>($"/api/mobile/rider/{CurrentUserId}/home"); }
    public async Task<TripListResult?> GetTrips(int page = 1) { SetAuth(); return await _http.GetFromJsonAsync<TripListResult>($"/api/mobile/rider/{CurrentUserId}/trips?page={page}"); }

    public async Task<BookResult?> BookRide(BookRequest req)
    {
        SetAuth();
        var resp = await _http.PostAsJsonAsync("/api/orders", new { riderId = CurrentUserId, req.PickupLatitude, req.PickupLongitude, req.PickupAddress, req.DropoffLatitude, req.DropoffLongitude, req.DropoffAddress, vehicleCategory = (int)req.VehicleCategory, paymentMethod = (int)req.PaymentMethod, req.PromoCode });
        if (!resp.IsSuccessStatusCode) return null;
        return await resp.Content.ReadFromJsonAsync<BookResult>();
    }
    public async Task<bool> SubmitReview(Guid orderId, Guid targetUserId, int rating, string? comment)
    { SetAuth(); var resp = await _http.PostAsJsonAsync("/api/reviews", new { orderId, reviewerId = CurrentUserId, targetUserId, rating, comment }); return resp.IsSuccessStatusCode; }
}

public class LoginResult { public Guid UserId { get; set; } public string FullName { get; set; } = ""; public string Email { get; set; } = ""; public string Token { get; set; } = ""; public string Role { get; set; } = ""; public DateTime ExpiresAt { get; set; } public string? PhotoUrl { get; set; } public string? ProfilePhotoMimeType { get; set; } }
public class ProfileResult { public Guid Id { get; set; } public string FullName { get; set; } = ""; public string Email { get; set; } = ""; public string PhoneNumber { get; set; } = ""; public string? PhotoUrl { get; set; } public string? ProfilePhotoMimeType { get; set; } public object? Driver { get; set; } public object? RiderStats { get; set; } }
public class RiderHomeResult { public Guid UserId { get; set; } public string FullName { get; set; } = ""; public int TotalTrips { get; set; } public decimal TotalSpent { get; set; } public List<RecentTrip> RecentTrips { get; set; } = new(); }
public class RecentTrip { public Guid OrderId { get; set; } public string DriverName { get; set; } = ""; public string PickupAddress { get; set; } = ""; public string DropoffAddress { get; set; } = ""; public decimal Fare { get; set; } public string Status { get; set; } = ""; public DateTime CreatedAt { get; set; } }
public class TripListResult { public int Total { get; set; } public int Page { get; set; } public List<TripItem> Data { get; set; } = new(); }
public class TripItem { public Guid Id { get; set; } public string? DriverName { get; set; } public string PickupAddress { get; set; } = ""; public string DropoffAddress { get; set; } = ""; public double DistanceKm { get; set; } public decimal FinalFare { get; set; } public string Status { get; set; } = ""; public DateTime CreatedAt { get; set; } public int? DriverRating { get; set; } public string VehicleCategory { get; set; } = ""; }
public class BookRequest { public double PickupLatitude { get; set; } public double PickupLongitude { get; set; } public string PickupAddress { get; set; } = ""; public double DropoffLatitude { get; set; } public double DropoffLongitude { get; set; } public string DropoffAddress { get; set; } = ""; public VehicleCategory VehicleCategory { get; set; } = VehicleCategory.Economy; public PaymentMethod PaymentMethod { get; set; } = PaymentMethod.Cash; public string? PromoCode { get; set; } }
public class BookResult { public Guid Id { get; set; } public string Status { get; set; } = ""; public decimal EstimatedFare { get; set; } public decimal FinalFare { get; set; } public double DistanceKm { get; set; } public int EstimatedDurationMinutes { get; set; } public string? PromoApplied { get; set; } }
public enum VehicleCategory { Economy = 1, Comfort = 2, Premium = 3, Bike = 4, Electric = 5 }
public enum PaymentMethod { Cash = 1, EWallet = 2, CreditCard = 3, BankTransfer = 4 }
