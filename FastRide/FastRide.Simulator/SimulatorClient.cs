using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using FastRide.Shared.Common;
using FastRide.Shared.DTOs;
using FastRide.Shared.Models;

namespace FastRide.Simulator;

/// <summary>
/// One authenticated actor against the API.
///
/// Each simulated rider and driver gets its own client, so each carries its own token. The
/// previous simulator set a single Authorization header on one shared HttpClient — the
/// rider's — and then called driver endpoints with it, which only worked because nothing
/// was actually protected.
/// </summary>
public sealed class SimulatorClient(HttpClient http, Metrics metrics)
{
    private static readonly JsonSerializerOptions Json = CreateOptions();

    private static JsonSerializerOptions CreateOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }

    public Guid UserId { get; private set; }
    public string FullName { get; private set; } = string.Empty;
    public string? Token { get; private set; }

    public static SimulatorClient Create(string baseUrl, Metrics metrics)
    {
        var handler = new HttpClientHandler
        {
            // Local runs sit behind the ASP.NET development certificate.
            ServerCertificateCustomValidationCallback = (_, _, _, _) => true,
            MaxConnectionsPerServer = 64
        };

        return new SimulatorClient(
            new HttpClient(handler) { BaseAddress = new Uri(baseUrl), Timeout = TimeSpan.FromSeconds(20) },
            metrics);
    }

    // ─────────────────────────── auth ───────────────────────────

    public async Task<bool> LoginAsync(string email, string password, CancellationToken ct)
    {
        var (auth, _) = await AuthenticateAsync("/api/auth/login", new LoginRequest(email, password), ct);
        return Adopt(auth);
    }

    public async Task<bool> RegisterAsync(RegisterRequest request, CancellationToken ct)
    {
        var (auth, _) = await AuthenticateAsync("/api/auth/register", request, ct);
        return Adopt(auth);
    }

    /// <summary>
    /// Register, falling back to login when the account already exists from an earlier run.
    ///
    /// Auth endpoints are rate limited, and setting up a dozen actors at once will hit that
    /// ceiling — so a 429 is waited out rather than treated as a failure.
    /// </summary>
    public async Task<bool> SignUpOrSignInAsync(RegisterRequest request, CancellationToken ct)
    {
        for (var attempt = 0; attempt < 5; attempt++)
        {
            var (auth, status) = await AuthenticateAsync("/api/auth/register", request, ct);
            if (auth is not null) return Adopt(auth);

            if (status == HttpStatusCode.TooManyRequests)
            {
                await Task.Delay(TimeSpan.FromSeconds(4 + (attempt * 3)), ct);
                continue;
            }

            // Anything else — usually "email already registered" — means try signing in.
            var (existing, loginStatus) = await AuthenticateAsync(
                "/api/auth/login", new LoginRequest(request.Email, request.Password), ct);

            if (existing is not null) return Adopt(existing);

            if (loginStatus != HttpStatusCode.TooManyRequests) return false;

            await Task.Delay(TimeSpan.FromSeconds(4 + (attempt * 3)), ct);
        }

        return false;
    }

    private async Task<(AuthResponse? Auth, HttpStatusCode Status)> AuthenticateAsync<TRequest>(
        string path, TRequest body, CancellationToken ct)
    {
        var stopwatch = Stopwatch.StartNew();

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, path)
            {
                Content = JsonContent.Create(body, options: Json)
            };

            using var response = await http.SendAsync(request, ct);
            metrics.Record(stopwatch.Elapsed.TotalMilliseconds, response.IsSuccessStatusCode);

            return response.IsSuccessStatusCode
                ? (await response.Content.ReadFromJsonAsync<AuthResponse>(Json, ct), response.StatusCode)
                : (null, response.StatusCode);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception)
        {
            metrics.Record(stopwatch.Elapsed.TotalMilliseconds, false);
            return (null, HttpStatusCode.ServiceUnavailable);
        }
    }

    private bool Adopt(AuthResponse? auth)
    {
        if (auth is null) return false;

        UserId = auth.UserId;
        FullName = auth.FullName;
        Token = auth.Token;
        http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.Token);

        return true;
    }

    public Task<HealthResponse?> HealthAsync(CancellationToken ct) =>
        GetAsync<HealthResponse>("/api/health", ct);

    // ─────────────────────────── rider ───────────────────────────

    public Task<FareQuoteResponse?> QuoteAsync(FareQuoteRequest request, CancellationToken ct) =>
        SendAsync<FareQuoteRequest, FareQuoteResponse>(HttpMethod.Post, "/api/orders/quote", request, ct);

    public Task<CreateOrderResponse?> BookAsync(CreateOrderRequest request, CancellationToken ct) =>
        SendAsync<CreateOrderRequest, CreateOrderResponse>(HttpMethod.Post, "/api/orders", request, ct);

    public Task<OrderDetailResponse?> GetOrderAsync(Guid orderId, CancellationToken ct) =>
        GetAsync<OrderDetailResponse>($"/api/orders/{orderId}", ct);

    public Task<MessageResponse?> CancelOrderAsync(Guid orderId, string reason, CancellationToken ct) =>
        SendAsync<CancelOrderRequest, MessageResponse>(
            HttpMethod.Post, $"/api/orders/{orderId}/cancel", new CancelOrderRequest(reason), ct);

    public Task<MessageResponse?> ReviewAsync(Guid orderId, Guid driverId, int rating, string comment, CancellationToken ct) =>
        SendAsync<SubmitReviewRequest, MessageResponse>(
            HttpMethod.Post, "/api/reviews", new SubmitReviewRequest(orderId, UserId, driverId, rating, comment), ct);

    // ────────────────────────── payments ──────────────────────────

    public Task<PaymentResponse?> GetPaymentAsync(Guid orderId, CancellationToken ct) =>
        GetAsync<PaymentResponse>($"/api/payments/order/{orderId}", ct);

    /// <summary>Open a charge for a trip the rider still owes money on.</summary>
    public Task<PaymentResponse?> ChargeAsync(Guid orderId, PaymentMethod method, CancellationToken ct) =>
        SendAsync<PaymentRequest, PaymentResponse>(
            HttpMethod.Post, "/api/payments", new PaymentRequest(orderId, method), ct);

    /// <summary>
    /// Stand in for the payer against the sandbox provider. Drives the real signed callback
    /// path, so the smoke run exercises the same code a live gateway would reach.
    /// </summary>
    public Task<PaymentResponse?> SettlePaymentAsync(Guid orderId, CancellationToken ct) =>
        SendAsync<object?, PaymentResponse>(
            HttpMethod.Post, $"/api/payments/sandbox/{orderId}/settle", null, ct);

    // ─────────────────────────── driver ───────────────────────────

    public Task<DriverDocumentResponse?> UploadDocumentAsync(DocumentType type, CancellationToken ct) =>
        SendAsync<UploadDocumentRequest, DriverDocumentResponse>(
            HttpMethod.Post,
            $"/api/drivers/{UserId}/documents",
            // A 1x1 GIF is enough to exercise the upload path without shipping fixtures.
            new UploadDocumentRequest(type, "R0lGODlhAQABAIAAAAAAAP///yH5BAEAAAAALAAAAAABAAEAAAIBRAA7", "image/gif"),
            ct);

    public Task<DriverDocumentResponse?> ApproveDocumentAsync(Guid driverId, Guid documentId, CancellationToken ct) =>
        SendAsync<ReviewDocumentRequest, DriverDocumentResponse>(
            HttpMethod.Put,
            $"/api/drivers/{driverId}/documents/{documentId}/review",
            new ReviewDocumentRequest(DocumentStatus.Approved, "Disetujui otomatis oleh simulator"),
            ct);

    public Task<MessageResponse?> UpdateLocationAsync(double latitude, double longitude, double heading, CancellationToken ct) =>
        SendAsync<UpdateLocationRequest, MessageResponse>(
            HttpMethod.Put,
            $"/api/mobile/driver/{UserId}/location",
            new UpdateLocationRequest(latitude, longitude, heading),
            ct);

    public Task<DriverStatusResponse?> SetStatusAsync(DriverStatus status, CancellationToken ct) =>
        SendAsync<SetDriverStatusRequest, DriverStatusResponse>(
            HttpMethod.Put, $"/api/mobile/driver/{UserId}/status", new SetDriverStatusRequest(status), ct);

    public Task<List<IncomingOrderItem>> AvailableOrdersAsync(CancellationToken ct) =>
        GetListAsync<IncomingOrderItem>($"/api/mobile/driver/{UserId}/orders/available?radiusKm=25&limit=5", ct);

    /// <summary>True when this driver won the race for the order; a 409 simply means someone else did.</summary>
    public async Task<bool> AcceptOrderAsync(Guid orderId, CancellationToken ct)
    {
        var result = await SendAsync<AcceptOrderRequest, JsonDocument>(
            HttpMethod.Put, $"/api/mobile/driver/{UserId}/accept-order", new AcceptOrderRequest(orderId), ct);

        result?.Dispose();
        return result is not null;
    }

    public Task<OrderDetailResponse?> ArriveAsync(Guid orderId, CancellationToken ct) =>
        DriverAction("arrive-order", orderId, ct);

    public Task<OrderDetailResponse?> StartAsync(Guid orderId, CancellationToken ct) =>
        DriverAction("start-order", orderId, ct);

    public Task<OrderDetailResponse?> CompleteAsync(Guid orderId, CancellationToken ct) =>
        DriverAction("complete-order", orderId, ct);

    private Task<OrderDetailResponse?> DriverAction(string action, Guid orderId, CancellationToken ct) =>
        SendAsync<AcceptOrderRequest, OrderDetailResponse>(
            HttpMethod.Put, $"/api/mobile/driver/{UserId}/{action}", new AcceptOrderRequest(orderId), ct);

    // ─────────────────────────── plumbing ───────────────────────────

    private async Task<T?> GetAsync<T>(string path, CancellationToken ct) where T : class
    {
        var stopwatch = Stopwatch.StartNew();

        try
        {
            using var response = await http.GetAsync(path, ct);
            metrics.Record(stopwatch.Elapsed.TotalMilliseconds, response.IsSuccessStatusCode);

            return response.IsSuccessStatusCode
                ? await response.Content.ReadFromJsonAsync<T>(Json, ct)
                : null;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception)
        {
            metrics.Record(stopwatch.Elapsed.TotalMilliseconds, false);
            return null;
        }
    }

    private async Task<List<T>> GetListAsync<T>(string path, CancellationToken ct)
    {
        var stopwatch = Stopwatch.StartNew();

        try
        {
            using var response = await http.GetAsync(path, ct);
            metrics.Record(stopwatch.Elapsed.TotalMilliseconds, response.IsSuccessStatusCode);

            return response.IsSuccessStatusCode
                ? await response.Content.ReadFromJsonAsync<List<T>>(Json, ct) ?? []
                : [];
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception)
        {
            metrics.Record(stopwatch.Elapsed.TotalMilliseconds, false);
            return [];
        }
    }

    private async Task<TResponse?> SendAsync<TRequest, TResponse>(
        HttpMethod method, string path, TRequest body, CancellationToken ct)
    {
        var stopwatch = Stopwatch.StartNew();

        try
        {
            using var request = new HttpRequestMessage(method, path)
            {
                Content = JsonContent.Create(body, options: Json)
            };

            using var response = await http.SendAsync(request, ct);
            metrics.Record(stopwatch.Elapsed.TotalMilliseconds, response.IsSuccessStatusCode);

            // A rejected transition (409) is an expected outcome in a race, not a crash.
            return response.IsSuccessStatusCode
                ? await response.Content.ReadFromJsonAsync<TResponse>(Json, ct)
                : default;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception)
        {
            metrics.Record(stopwatch.Elapsed.TotalMilliseconds, false);
            return default;
        }
    }
}
