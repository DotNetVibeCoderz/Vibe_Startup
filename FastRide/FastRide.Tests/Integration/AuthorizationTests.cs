using System.Net;
using FastRide.Shared.DTOs;
using FastRide.Tests.Infrastructure;

namespace FastRide.Tests.Integration;

/// <summary>
/// Until v2.0 the API issued tokens but never checked them, so every route was reachable
/// anonymously. These tests exist so that cannot come back unnoticed.
/// </summary>
[Collection(ApiCollection.Name)]
public class AuthorizationTests(ApiFixture fixture)
{
    [Theory]
    [InlineData("/api/orders")]
    [InlineData("/api/riders")]
    [InlineData("/api/drivers")]
    [InlineData("/api/payments")]
    [InlineData("/api/promos")]
    [InlineData("/api/fares")]
    [InlineData("/api/dashboard/stats")]
    [InlineData("/api/dashboard/overview")]
    [InlineData("/api/admin/users")]
    public async Task ProtectedRoutes_RejectAnonymousCallers(string url)
    {
        var client = fixture.NewClient();

        using var response = await client.GetAsync(url);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Theory]
    [InlineData("/api/health")]
    public async Task PublicRoutes_StayPublic(string url)
    {
        var client = fixture.NewClient();

        using var response = await client.GetAsync(url);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Theory]
    [InlineData("/api/orders")]
    [InlineData("/api/riders")]
    [InlineData("/api/drivers")]
    [InlineData("/api/dashboard/stats")]
    [InlineData("/api/admin/users")]
    public async Task AdminRoutes_RejectARider(string url)
    {
        var rider = await fixture.NewRiderAsync();

        using var response = await rider.Client.GetAsync(url);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task AdminRoutes_AcceptAnAdmin()
    {
        using var response = await fixture.Admin.GetAsync("/api/orders?limit=1");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task ARider_CannotReadAnotherRidersProfile()
    {
        var first = await fixture.NewRiderAsync();
        var second = await fixture.NewRiderAsync();

        using var response = await first.Client.GetAsync($"/api/profile/{second.Id}");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task ARider_CannotReadAnotherRidersHome()
    {
        var first = await fixture.NewRiderAsync();
        var second = await fixture.NewRiderAsync();

        using var response = await first.Client.GetAsync($"/api/mobile/rider/{second.Id}/home");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task ARider_CannotReadAnotherRidersTrips()
    {
        // Changing the id in the URL is the obvious attack; it must not work.
        var first = await fixture.NewRiderAsync();
        var second = await fixture.NewRiderAsync();

        using var response = await first.Client.GetAsync($"/api/mobile/rider/{second.Id}/trips");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task ARider_CannotReadAnotherRidersNotifications()
    {
        var first = await fixture.NewRiderAsync();
        var second = await fixture.NewRiderAsync();

        using var response = await first.Client.GetAsync($"/api/notifications/{second.Id}");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task ARider_CannotDriveSomeoneElsesAccount()
    {
        var rider = await fixture.NewRiderAsync();
        var driver = await fixture.NewVerifiedDriverAsync();

        using var response = await rider.Client.PutJsonAsync(
            $"/api/mobile/driver/{driver.Id}/location", new UpdateLocationRequest(-6.2, 106.8));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task AnAdmin_CanReadAnyProfile()
    {
        var rider = await fixture.NewRiderAsync();

        var profile = await fixture.Admin.GetAndReadAsync<UserProfileResponse>($"/api/profile/{rider.Id}");

        Assert.Equal(rider.Id, profile.Id);
    }

    [Fact]
    public async Task ADriver_CannotApproveItsOwnDocuments()
    {
        var driver = await fixture.NewUnverifiedDriverAsync();

        var document = await driver.Client.PostAndReadAsync<UploadDocumentRequest, DriverDocumentResponse>(
            $"/api/drivers/{driver.Id}/documents",
            new UploadDocumentRequest(Shared.Models.DocumentType.DriverLicense, ApiFixture.TinyGifBase64, "image/gif"));

        using var response = await driver.Client.PutJsonAsync(
            $"/api/drivers/{driver.Id}/documents/{document.Id}/review",
            new ReviewDocumentRequest(Shared.Models.DocumentStatus.Approved, "Saya setujui sendiri"));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task AGarbledToken_IsRejected()
    {
        var client = fixture.NewClient();
        client.DefaultRequestHeaders.Authorization = new("Bearer", "ini.bukan.token");

        using var response = await client.GetAsync("/api/auth/me");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
