using System.Net;
using FastRide.Shared.Common;
using FastRide.Shared.DTOs;
using FastRide.Tests.Infrastructure;

namespace FastRide.Tests.Integration;

[Collection(ApiCollection.Name)]
public class ReviewTests(ApiFixture fixture)
{
    [Fact]
    public async Task ARiderCanRateADriverAfterTheTrip()
    {
        var rider = await fixture.NewRiderAsync();
        var driver = await fixture.NewVerifiedDriverAsync();

        var order = await OrderLifecycleTests.CompleteATripAsync(rider, driver);

        using var response = await rider.Client.PostJsonAsync("/api/reviews",
            new SubmitReviewRequest(order.Id, rider.Id, driver.Id, 5, "Mantap, tepat waktu"));

        response.EnsureSuccessStatusCode();

        var detail = await rider.Client.GetAndReadAsync<OrderDetailResponse>($"/api/orders/{order.Id}");

        Assert.Equal(5, detail.DriverRating);
        Assert.Equal("Mantap, tepat waktu", detail.ReviewComment);
    }

    [Fact]
    public async Task AReviewUpdatesTheDriversHeadlineRating()
    {
        var driver = await fixture.NewVerifiedDriverAsync();

        foreach (var rating in new[] { 5, 3 })
        {
            var rider = await fixture.NewRiderAsync();
            var order = await OrderLifecycleTests.CompleteATripAsync(rider, driver);

            using var response = await rider.Client.PostJsonAsync("/api/reviews",
                new SubmitReviewRequest(order.Id, rider.Id, driver.Id, rating, null));

            response.EnsureSuccessStatusCode();
        }

        var profile = await driver.Client.GetAndReadAsync<UserProfileResponse>($"/api/profile/{driver.Id}");

        // The headline number must agree with the reviews behind it.
        Assert.Equal(2, profile.Driver!.RatingCount);
        Assert.Equal(4.0, profile.Driver.Rating, 2);
    }

    [Fact]
    public async Task ATripCannotBeRatedTwiceByTheSamePerson()
    {
        var rider = await fixture.NewRiderAsync();
        var driver = await fixture.NewVerifiedDriverAsync();

        var order = await OrderLifecycleTests.CompleteATripAsync(rider, driver);

        using var first = await rider.Client.PostJsonAsync("/api/reviews",
            new SubmitReviewRequest(order.Id, rider.Id, driver.Id, 5, null));
        first.EnsureSuccessStatusCode();

        using var second = await rider.Client.PostJsonAsync("/api/reviews",
            new SubmitReviewRequest(order.Id, rider.Id, driver.Id, 1, "Berubah pikiran"));

        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
    }

    [Fact]
    public async Task AnUnfinishedTripCannotBeRated()
    {
        var rider = await fixture.NewRiderAsync();
        var driver = await fixture.NewVerifiedDriverAsync();

        var order = await rider.Client.PostAndReadAsync<CreateOrderRequest, CreateOrderResponse>(
            "/api/orders",
            new CreateOrderRequest(rider.Id,
                -6.2088, 106.8456, "Jl. Sudirman No. 1",
                -6.1751, 106.8650, "Jl. Thamrin No. 9"));

        using var response = await rider.Client.PostJsonAsync("/api/reviews",
            new SubmitReviewRequest(order.Id, rider.Id, driver.Id, 5, null));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task SomeoneOutsideTheTripCannotRateIt()
    {
        var rider = await fixture.NewRiderAsync();
        var driver = await fixture.NewVerifiedDriverAsync();
        var stranger = await fixture.NewRiderAsync();

        var order = await OrderLifecycleTests.CompleteATripAsync(rider, driver);

        using var response = await stranger.Client.PostJsonAsync("/api/reviews",
            new SubmitReviewRequest(order.Id, stranger.Id, driver.Id, 1, "Tidak ikut naik"));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task AReviewCannotBeFiledUnderSomeoneElsesName()
    {
        var rider = await fixture.NewRiderAsync();
        var driver = await fixture.NewVerifiedDriverAsync();
        var impostor = await fixture.NewRiderAsync();

        var order = await OrderLifecycleTests.CompleteATripAsync(rider, driver);

        using var response = await impostor.Client.PostJsonAsync("/api/reviews",
            new SubmitReviewRequest(order.Id, rider.Id, driver.Id, 1, "Atas nama orang lain"));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(6)]
    [InlineData(-1)]
    public async Task ARatingOutsideOneToFiveIsRefused(int rating)
    {
        var rider = await fixture.NewRiderAsync();
        var driver = await fixture.NewVerifiedDriverAsync();

        var order = await OrderLifecycleTests.CompleteATripAsync(rider, driver);

        using var response = await rider.Client.PostJsonAsync("/api/reviews",
            new SubmitReviewRequest(order.Id, rider.Id, driver.Id, rating, null));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task ADriversReviewsArePubliclyReadable()
    {
        var rider = await fixture.NewRiderAsync();
        var driver = await fixture.NewVerifiedDriverAsync();

        var order = await OrderLifecycleTests.CompleteATripAsync(rider, driver);

        using var submit = await rider.Client.PostJsonAsync("/api/reviews",
            new SubmitReviewRequest(order.Id, rider.Id, driver.Id, 4, "Nyaman"));
        submit.EnsureSuccessStatusCode();

        // No token: a rider choosing a driver should be able to see their reputation.
        var anonymous = fixture.NewClient();
        var reviews = await anonymous.GetAndReadAsync<PagedResult<ReviewResponse>>(
            $"/api/reviews/user/{driver.Id}");

        Assert.Equal(1, reviews.Total);
        Assert.Equal(4, reviews.Data[0].Rating);
        Assert.Equal("Nyaman", reviews.Data[0].Comment);
        Assert.False(string.IsNullOrWhiteSpace(reviews.Data[0].ReviewerName));
    }
}
