using System.Net.Http.Headers;
using FastRide.Api.Endpoints;
using FastRide.Data;
using FastRide.Shared.DTOs;
using FastRide.Shared.Models;
using Microsoft.Extensions.DependencyInjection;

namespace FastRide.Tests.Infrastructure;

/// <summary>
/// One API host shared by the whole integration suite.
///
/// Starting a host and hashing passwords with BCrypt work factor 12 is expensive, so the
/// host is created once. Isolation comes from every test creating its own users and orders
/// rather than from a fresh database per test.
/// </summary>
public sealed class ApiFixture : IAsyncLifetime
{
    public const string AdminEmail = "admin@fastride.test";
    public const string Password = "Password123";

    private int _counter;

    public FastRideApiFactory Factory { get; } = new();

    /// <summary>Client authenticated as the seeded admin.</summary>
    public HttpClient Admin { get; private set; } = default!;

    public async Task InitializeAsync()
    {
        // Touching Services builds the host, which runs EnsureCreated and the schema probe.
        // The isolation check comes first: if the settings were not applied, the whole suite
        // would run against the developer's own database instead of a throwaway one.
        Factory.AssertIsIsolated();

        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<FastRideDbContext>();

        // The admin cannot be created through /auth/register — that endpoint rejects the
        // Admin role on purpose — so it is inserted directly.
        db.Users.Add(new User
        {
            FullName = "Admin Uji",
            Email = AdminEmail,
            PhoneNumber = "0800000000",
            PasswordHash = AuthEndpoints.HashPassword(Password),
            Role = UserRole.Admin,
            IsVerified = true,
            IsActive = true
        });

        await db.SaveChangesAsync();

        Admin = await SignInAsync(AdminEmail, Password);
    }

    public Task DisposeAsync()
    {
        Admin?.Dispose();
        Factory.Dispose();
        return Task.CompletedTask;
    }

    /// <summary>Unique per call, so tests never collide on the unique email index.</summary>
    public string NextEmail(string prefix) =>
        $"{prefix}.{Interlocked.Increment(ref _counter)}.{Guid.NewGuid():N}@fastride.test";

    public HttpClient NewClient() => Factory.CreateClient();

    public async Task<HttpClient> SignInAsync(string email, string password)
    {
        var client = Factory.CreateClient();
        var auth = await client.PostAndReadAsync<LoginRequest, AuthResponse>(
            "/api/auth/login", new LoginRequest(email, password));

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.Token);
        return client;
    }

    /// <summary>Register a brand-new rider and return a client already carrying its token.</summary>
    public async Task<TestActor> NewRiderAsync()
    {
        var email = NextEmail("rider");
        var client = Factory.CreateClient();

        var auth = await client.PostAndReadAsync<RegisterRequest, AuthResponse>(
            "/api/auth/register",
            new RegisterRequest("Rider Uji", email, "08120000000", Password, UserRole.Rider));

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.Token);
        return new TestActor(auth.UserId, email, auth.Token, client);
    }

    /// <summary>
    /// Register a driver whose documents are still pending — the state a real driver starts in.
    /// </summary>
    public async Task<TestActor> NewUnverifiedDriverAsync(VehicleCategory category = VehicleCategory.Economy)
    {
        var email = NextEmail("driver");
        var client = Factory.CreateClient();

        var auth = await client.PostAndReadAsync<RegisterRequest, AuthResponse>(
            "/api/auth/register",
            new RegisterRequest(
                "Driver Uji", email, "08130000000", Password, UserRole.Driver,
                "SIM-000111", "Toyota Avanza", "B 1111 UJI", category));

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.Token);
        return new TestActor(auth.UserId, email, auth.Token, client);
    }

    /// <summary>Register a driver and walk the real verification flow so it can take trips.</summary>
    public async Task<TestActor> NewVerifiedDriverAsync(VehicleCategory category = VehicleCategory.Economy)
    {
        var driver = await NewUnverifiedDriverAsync(category);
        await ApproveDocumentsAsync(driver);

        return driver;
    }

    public async Task ApproveDocumentsAsync(TestActor driver)
    {
        DocumentType[] required =
            [DocumentType.DriverLicense, DocumentType.VehicleRegistration, DocumentType.IdentityCard];

        foreach (var type in required)
        {
            var document = await driver.Client.PostAndReadAsync<UploadDocumentRequest, DriverDocumentResponse>(
                $"/api/drivers/{driver.Id}/documents",
                new UploadDocumentRequest(type, TinyGifBase64, "image/gif"));

            using var review = await Admin.PutJsonAsync(
                $"/api/drivers/{driver.Id}/documents/{document.Id}/review",
                new ReviewDocumentRequest(DocumentStatus.Approved, null));

            review.EnsureSuccessStatusCode();
        }
    }

    /// <summary>Smallest valid image, so document upload tests need no fixture files.</summary>
    public const string TinyGifBase64 = "R0lGODlhAQABAIAAAAAAAP///yH5BAEAAAAALAAAAAABAAEAAAIBRAA7";
}

/// <summary>A signed-in user plus the client carrying its token.</summary>
public sealed record TestActor(Guid Id, string Email, string Token, HttpClient Client);

[CollectionDefinition(ApiCollection.Name)]
public sealed class ApiCollection : ICollectionFixture<ApiFixture>
{
    public const string Name = "FastRide API";
}
