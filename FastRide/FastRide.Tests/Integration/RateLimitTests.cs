using System.Net;
using FastRide.Shared.DTOs;
using FastRide.Tests.Infrastructure;

namespace FastRide.Tests.Integration;

/// <summary>
/// Runs against its own API instance with a deliberately tiny limit — the shared fixture
/// raises the ceiling so the rest of the suite is not throttled.
/// </summary>
public class RateLimitTests : IDisposable
{
    private readonly FastRideApiFactory _factory = new() { AuthPermitPerMinute = 5 };

    public void Dispose()
    {
        _factory.Dispose();
        GC.SuppressFinalize(this);
    }

    [Fact]
    public async Task RepeatedSignInAttempts_AreEventuallyThrottled()
    {
        var client = _factory.CreateClient();
        var statuses = new List<HttpStatusCode>();

        for (var attempt = 0; attempt < 12; attempt++)
        {
            using var response = await client.PostJsonAsync("/api/auth/login",
                new LoginRequest("penebak@fastride.test", $"tebakan-{attempt}"));

            statuses.Add(response.StatusCode);
        }

        Assert.Contains(HttpStatusCode.Unauthorized, statuses);
        Assert.Contains(HttpStatusCode.TooManyRequests, statuses);

        // Once throttled it stays throttled for the rest of the window.
        Assert.Equal(HttpStatusCode.TooManyRequests, statuses[^1]);
    }

    [Fact]
    public async Task AThrottledResponse_ExplainsItselfInIndonesian()
    {
        var client = _factory.CreateClient();

        HttpStatusCode last = HttpStatusCode.OK;
        var body = string.Empty;

        for (var attempt = 0; attempt < 12 && last != HttpStatusCode.TooManyRequests; attempt++)
        {
            using var response = await client.PostJsonAsync("/api/auth/login",
                new LoginRequest("penebak2@fastride.test", "salah"));

            last = response.StatusCode;
            body = await response.Content.ReadAsStringAsync();
        }

        Assert.Equal(HttpStatusCode.TooManyRequests, last);
        Assert.Contains("Terlalu banyak permintaan", body);
    }

    [Fact]
    public async Task HealthIsNotThrottledByTheAuthLimit()
    {
        var client = _factory.CreateClient();

        // Burn the auth budget.
        for (var attempt = 0; attempt < 10; attempt++)
        {
            using var login = await client.PostJsonAsync("/api/auth/login",
                new LoginRequest("penebak3@fastride.test", "salah"));
        }

        using var health = await client.GetAsync("/api/health");

        Assert.Equal(HttpStatusCode.OK, health.StatusCode);
    }
}
