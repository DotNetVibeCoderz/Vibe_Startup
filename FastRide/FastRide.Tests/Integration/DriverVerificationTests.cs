using System.Net;
using FastRide.Shared.DTOs;
using FastRide.Shared.Models;
using FastRide.Tests.Infrastructure;

namespace FastRide.Tests.Integration;

/// <summary>
/// Authentication alone is not enough for a driver: SIM, STNK and KTP must be approved
/// before they can go online or take a trip.
/// </summary>
[Collection(ApiCollection.Name)]
public class DriverVerificationTests(ApiFixture fixture)
{
    [Fact]
    public async Task ANewDriver_StartsUnverified()
    {
        var driver = await fixture.NewUnverifiedDriverAsync();

        var profile = await driver.Client.GetAndReadAsync<UserProfileResponse>($"/api/profile/{driver.Id}");

        Assert.NotNull(profile.Driver);
        Assert.False(profile.Driver!.IsDocumentVerified);
        Assert.Equal(DriverStatus.Offline, profile.Driver.Status);
    }

    [Fact]
    public async Task AnUnverifiedDriver_CannotGoOnline()
    {
        var driver = await fixture.NewUnverifiedDriverAsync();

        using var response = await driver.Client.PutJsonAsync(
            $"/api/mobile/driver/{driver.Id}/status", new SetDriverStatusRequest(DriverStatus.Online));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task AnUnverifiedDriver_CannotAcceptATrip()
    {
        var rider = await fixture.NewRiderAsync();
        var driver = await fixture.NewUnverifiedDriverAsync();

        var order = await rider.Client.PostAndReadAsync<CreateOrderRequest, CreateOrderResponse>(
            "/api/orders",
            new CreateOrderRequest(rider.Id,
                -6.2088, 106.8456, "Jl. Sudirman No. 1",
                -6.1751, 106.8650, "Jl. Thamrin No. 9"));

        using var response = await driver.Client.PutJsonAsync(
            $"/api/mobile/driver/{driver.Id}/accept-order", new AcceptOrderRequest(order.Id));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task ApprovingAllThreeDocuments_UnlocksTheDriver()
    {
        var driver = await fixture.NewUnverifiedDriverAsync();

        await fixture.ApproveDocumentsAsync(driver);

        var profile = await driver.Client.GetAndReadAsync<UserProfileResponse>($"/api/profile/{driver.Id}");
        Assert.True(profile.Driver!.IsDocumentVerified);

        var status = await driver.Client.PutAndReadAsync<SetDriverStatusRequest, DriverStatusResponse>(
            $"/api/mobile/driver/{driver.Id}/status", new SetDriverStatusRequest(DriverStatus.Online));

        Assert.Equal(DriverStatus.Online, status.Status);
        Assert.True(status.IsOnline);
    }

    [Fact]
    public async Task ApprovingOnlyTwoDocuments_IsNotEnough()
    {
        var driver = await fixture.NewUnverifiedDriverAsync();

        foreach (var type in new[] { DocumentType.DriverLicense, DocumentType.VehicleRegistration })
        {
            var document = await driver.Client.PostAndReadAsync<UploadDocumentRequest, DriverDocumentResponse>(
                $"/api/drivers/{driver.Id}/documents",
                new UploadDocumentRequest(type, ApiFixture.TinyGifBase64, "image/gif"));

            using var review = await fixture.Admin.PutJsonAsync(
                $"/api/drivers/{driver.Id}/documents/{document.Id}/review",
                new ReviewDocumentRequest(DocumentStatus.Approved, null));

            review.EnsureSuccessStatusCode();
        }

        var profile = await driver.Client.GetAndReadAsync<UserProfileResponse>($"/api/profile/{driver.Id}");

        Assert.False(profile.Driver!.IsDocumentVerified);
    }

    [Fact]
    public async Task ReUploadingADocument_SendsTheDriverBackToTheQueue()
    {
        var driver = await fixture.NewVerifiedDriverAsync();

        var before = await driver.Client.GetAndReadAsync<UserProfileResponse>($"/api/profile/{driver.Id}");
        Assert.True(before.Driver!.IsDocumentVerified);

        await driver.Client.PostAndReadAsync<UploadDocumentRequest, DriverDocumentResponse>(
            $"/api/drivers/{driver.Id}/documents",
            new UploadDocumentRequest(DocumentType.DriverLicense, ApiFixture.TinyGifBase64, "image/gif"));

        var after = await driver.Client.GetAndReadAsync<UserProfileResponse>($"/api/profile/{driver.Id}");

        Assert.False(after.Driver!.IsDocumentVerified);
    }

    [Fact]
    public async Task ReUploadingADocument_ReplacesItRatherThanStacking()
    {
        var driver = await fixture.NewUnverifiedDriverAsync();

        for (var attempt = 0; attempt < 3; attempt++)
        {
            await driver.Client.PostAndReadAsync<UploadDocumentRequest, DriverDocumentResponse>(
                $"/api/drivers/{driver.Id}/documents",
                new UploadDocumentRequest(DocumentType.IdentityCard, ApiFixture.TinyGifBase64, "image/gif"));
        }

        var documents = await driver.Client.GetAndReadAsync<List<DriverDocumentResponse>>(
            $"/api/drivers/{driver.Id}/documents");

        Assert.Single(documents.Where(d => d.Type == DocumentType.IdentityCard));
    }

    [Fact]
    public async Task ARejectedDocument_CarriesTheReviewersNote()
    {
        var driver = await fixture.NewUnverifiedDriverAsync();

        var document = await driver.Client.PostAndReadAsync<UploadDocumentRequest, DriverDocumentResponse>(
            $"/api/drivers/{driver.Id}/documents",
            new UploadDocumentRequest(DocumentType.DriverLicense, ApiFixture.TinyGifBase64, "image/gif"));

        var reviewed = await fixture.Admin.PutAndReadAsync<ReviewDocumentRequest, DriverDocumentResponse>(
            $"/api/drivers/{driver.Id}/documents/{document.Id}/review",
            new ReviewDocumentRequest(DocumentStatus.Rejected, "Foto buram"));

        Assert.Equal(DocumentStatus.Rejected, reviewed.Status);
        Assert.Equal("Foto buram", reviewed.Notes);
        Assert.NotNull(reviewed.ReviewedAt);
    }

    [Fact]
    public async Task AnUploadedDocument_IsStoredAndLinked()
    {
        var driver = await fixture.NewUnverifiedDriverAsync();

        var document = await driver.Client.PostAndReadAsync<UploadDocumentRequest, DriverDocumentResponse>(
            $"/api/drivers/{driver.Id}/documents",
            new UploadDocumentRequest(DocumentType.VehicleRegistration, ApiFixture.TinyGifBase64, "image/gif"));

        Assert.Equal(DocumentStatus.Pending, document.Status);
        Assert.StartsWith("/uploads/documents/vehicleregistration/", document.FileUrl);
    }

    [Fact]
    public async Task RubbishBase64_IsRejected()
    {
        var driver = await fixture.NewUnverifiedDriverAsync();

        using var response = await driver.Client.PostJsonAsync(
            $"/api/drivers/{driver.Id}/documents",
            new UploadDocumentRequest(DocumentType.IdentityCard, "ini-jelas-bukan-base64!!", "image/gif"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task TheVerificationQueue_ListsDriversAwaitingReview()
    {
        var driver = await fixture.NewUnverifiedDriverAsync();

        await driver.Client.PostAndReadAsync<UploadDocumentRequest, DriverDocumentResponse>(
            $"/api/drivers/{driver.Id}/documents",
            new UploadDocumentRequest(DocumentType.DriverLicense, ApiFixture.TinyGifBase64, "image/gif"));

        var queue = await fixture.Admin.GetAndReadAsync<List<PendingDriver>>(
            "/api/admin/drivers/pending-verification");

        var entry = queue.SingleOrDefault(item => item.DriverId == driver.Id);

        Assert.NotNull(entry);
        Assert.Contains(entry!.Documents, document => document.Type == DocumentType.DriverLicense);
    }

    [Fact]
    public async Task ADriverOnATrip_CannotGoOffline()
    {
        var rider = await fixture.NewRiderAsync();
        var driver = await fixture.NewVerifiedDriverAsync();

        var order = await rider.Client.PostAndReadAsync<CreateOrderRequest, CreateOrderResponse>(
            "/api/orders",
            new CreateOrderRequest(rider.Id,
                -6.2088, 106.8456, "Jl. Sudirman No. 1",
                -6.1751, 106.8650, "Jl. Thamrin No. 9"));

        using var accept = await driver.Client.PutJsonAsync(
            $"/api/mobile/driver/{driver.Id}/accept-order", new AcceptOrderRequest(order.Id));
        accept.EnsureSuccessStatusCode();

        using var goOffline = await driver.Client.PutJsonAsync(
            $"/api/mobile/driver/{driver.Id}/status", new SetDriverStatusRequest(DriverStatus.Offline));

        Assert.Equal(HttpStatusCode.Conflict, goOffline.StatusCode);
    }

    /// <summary>Shape of a verification queue row (the endpoint returns an anonymous object).</summary>
    private sealed record PendingDriver(
        Guid DriverId, string FullName, string Email, string? PhotoUrl,
        string VehicleType, string VehiclePlate, DateTime JoinedAt,
        List<DriverDocumentResponse> Documents);
}
