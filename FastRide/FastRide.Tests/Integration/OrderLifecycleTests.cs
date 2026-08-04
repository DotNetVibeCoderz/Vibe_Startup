using System.Net;
using FastRide.Shared.Common;
using FastRide.Shared.DTOs;
using FastRide.Shared.Models;
using FastRide.Tests.Infrastructure;

namespace FastRide.Tests.Integration;

[Collection(ApiCollection.Name)]
public class OrderLifecycleTests(ApiFixture fixture)
{
    private const double PickupLat = -6.2088, PickupLon = 106.8456;
    private const double DropoffLat = -6.1751, DropoffLon = 106.8650;

    private static CreateOrderRequest Booking(Guid riderId, string? promo = null, List<TripStopRequest>? stops = null) =>
        new(riderId,
            PickupLat, PickupLon, "Jl. Sudirman No. 1",
            DropoffLat, DropoffLon, "Jl. Thamrin No. 9",
            VehicleCategory.Economy, PaymentMethod.Cash, promo, stops);

    [Fact]
    public async Task Quote_PricesATripWithoutBookingIt()
    {
        var rider = await fixture.NewRiderAsync();

        var quote = await rider.Client.PostAndReadAsync<FareQuoteRequest, FareQuoteResponse>(
            "/api/orders/quote",
            new FareQuoteRequest(PickupLat, PickupLon, DropoffLat, DropoffLon, VehicleCategory.Economy));

        Assert.InRange(quote.DistanceKm, 4.2, 4.4);
        Assert.True(quote.EstimatedFare > 0);
        Assert.Equal(quote.EstimatedFare, quote.FinalFare);

        var trips = await rider.Client.GetAndReadAsync<PagedResult<OrderListItem>>(
            $"/api/mobile/rider/{rider.Id}/trips");

        Assert.Equal(0, trips.Total);
    }

    [Fact]
    public async Task Quote_ChargesMoreForAMultiStopRoute()
    {
        var rider = await fixture.NewRiderAsync();

        var direct = await rider.Client.PostAndReadAsync<FareQuoteRequest, FareQuoteResponse>(
            "/api/orders/quote",
            new FareQuoteRequest(PickupLat, PickupLon, DropoffLat, DropoffLon, VehicleCategory.Economy));

        var viaStop = await rider.Client.PostAndReadAsync<FareQuoteRequest, FareQuoteResponse>(
            "/api/orders/quote",
            new FareQuoteRequest(PickupLat, PickupLon, DropoffLat, DropoffLon, VehicleCategory.Economy,
                Stops: [new TripStopRequest(-6.25, 106.90, "Detour jauh")]));

        Assert.True(viaStop.DistanceKm > direct.DistanceKm);
        Assert.True(viaStop.FinalFare > direct.FinalFare);
    }

    [Fact]
    public async Task Booking_StartsInRequestedAndMatchesItsQuote()
    {
        var rider = await fixture.NewRiderAsync();

        var quote = await rider.Client.PostAndReadAsync<FareQuoteRequest, FareQuoteResponse>(
            "/api/orders/quote",
            new FareQuoteRequest(PickupLat, PickupLon, DropoffLat, DropoffLon, VehicleCategory.Economy));

        var order = await rider.Client.PostAndReadAsync<CreateOrderRequest, CreateOrderResponse>(
            "/api/orders", Booking(rider.Id));

        Assert.Equal(OrderStatus.Requested, order.Status);
        Assert.StartsWith("FR-", order.Code);

        // What the app showed is what the rider is charged.
        Assert.Equal(quote.FinalFare, order.FinalFare);
    }

    [Fact]
    public async Task Booking_RecordsWaypoints()
    {
        var rider = await fixture.NewRiderAsync();

        var order = await rider.Client.PostAndReadAsync<CreateOrderRequest, CreateOrderResponse>(
            "/api/orders",
            Booking(rider.Id, stops: [new TripStopRequest(-6.19, 106.85, "Halte Benhil")]));

        var detail = await rider.Client.GetAndReadAsync<OrderDetailResponse>($"/api/orders/{order.Id}");

        Assert.Single(detail.Stops);
        Assert.Equal("Halte Benhil", detail.Stops[0].Address);
        Assert.Equal(1, detail.Stops[0].SequenceNumber);
    }

    [Fact]
    public async Task ARider_CannotBookASecondTripWhileOneIsRunning()
    {
        var rider = await fixture.NewRiderAsync();

        await rider.Client.PostAndReadAsync<CreateOrderRequest, CreateOrderResponse>("/api/orders", Booking(rider.Id));

        using var second = await rider.Client.PostJsonAsync("/api/orders", Booking(rider.Id));

        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
    }

    [Fact]
    public async Task ARider_CannotBookOnSomeoneElsesBehalf()
    {
        var booker = await fixture.NewRiderAsync();
        var victim = await fixture.NewRiderAsync();

        using var response = await booker.Client.PostJsonAsync("/api/orders", Booking(victim.Id));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task ATripRunsThroughItsWholeLifecycle()
    {
        var rider = await fixture.NewRiderAsync();
        var driver = await fixture.NewVerifiedDriverAsync();

        var order = await rider.Client.PostAndReadAsync<CreateOrderRequest, CreateOrderResponse>(
            "/api/orders", Booking(rider.Id));

        using var accept = await driver.Client.PutJsonAsync(
            $"/api/mobile/driver/{driver.Id}/accept-order", new AcceptOrderRequest(order.Id));
        accept.EnsureSuccessStatusCode();

        Assert.Equal(OrderStatus.Accepted, await StatusOfAsync(rider, order.Id));

        await AdvanceAsync(driver, order.Id, "arrive-order");
        Assert.Equal(OrderStatus.DriverArrived, await StatusOfAsync(rider, order.Id));

        await AdvanceAsync(driver, order.Id, "start-order");
        Assert.Equal(OrderStatus.Started, await StatusOfAsync(rider, order.Id));

        await AdvanceAsync(driver, order.Id, "complete-order");

        var final = await rider.Client.GetAndReadAsync<OrderDetailResponse>($"/api/orders/{order.Id}");

        Assert.Equal(OrderStatus.Completed, final.Status);
        Assert.NotNull(final.CompletedAt);
        Assert.NotNull(final.Payment);
        Assert.Equal(final.FinalFare, final.Payment!.Amount);
    }

    [Fact]
    public async Task ATrip_CannotSkipStraightToCompleted()
    {
        var rider = await fixture.NewRiderAsync();
        var driver = await fixture.NewVerifiedDriverAsync();

        var order = await rider.Client.PostAndReadAsync<CreateOrderRequest, CreateOrderResponse>(
            "/api/orders", Booking(rider.Id));

        using var accept = await driver.Client.PutJsonAsync(
            $"/api/mobile/driver/{driver.Id}/accept-order", new AcceptOrderRequest(order.Id));
        accept.EnsureSuccessStatusCode();

        using var complete = await driver.Client.PutJsonAsync(
            $"/api/mobile/driver/{driver.Id}/complete-order", new AcceptOrderRequest(order.Id));

        Assert.Equal(HttpStatusCode.Conflict, complete.StatusCode);
    }

    [Fact]
    public async Task OnlyOneDriver_CanTakeAnOrder()
    {
        var rider = await fixture.NewRiderAsync();
        var first = await fixture.NewVerifiedDriverAsync();
        var second = await fixture.NewVerifiedDriverAsync();

        var order = await rider.Client.PostAndReadAsync<CreateOrderRequest, CreateOrderResponse>(
            "/api/orders", Booking(rider.Id));

        using var winner = await first.Client.PutJsonAsync(
            $"/api/mobile/driver/{first.Id}/accept-order", new AcceptOrderRequest(order.Id));

        using var loser = await second.Client.PutJsonAsync(
            $"/api/mobile/driver/{second.Id}/accept-order", new AcceptOrderRequest(order.Id));

        Assert.Equal(HttpStatusCode.OK, winner.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, loser.StatusCode);
    }

    [Fact]
    public async Task ADriver_CannotTakeTwoTripsAtOnce()
    {
        var driver = await fixture.NewVerifiedDriverAsync();

        var firstRider = await fixture.NewRiderAsync();
        var firstOrder = await firstRider.Client.PostAndReadAsync<CreateOrderRequest, CreateOrderResponse>(
            "/api/orders", Booking(firstRider.Id));

        using var firstAccept = await driver.Client.PutJsonAsync(
            $"/api/mobile/driver/{driver.Id}/accept-order", new AcceptOrderRequest(firstOrder.Id));
        firstAccept.EnsureSuccessStatusCode();

        var secondRider = await fixture.NewRiderAsync();
        var secondOrder = await secondRider.Client.PostAndReadAsync<CreateOrderRequest, CreateOrderResponse>(
            "/api/orders", Booking(secondRider.Id));

        using var secondAccept = await driver.Client.PutJsonAsync(
            $"/api/mobile/driver/{driver.Id}/accept-order", new AcceptOrderRequest(secondOrder.Id));

        Assert.Equal(HttpStatusCode.Conflict, secondAccept.StatusCode);
    }

    [Fact]
    public async Task ADriver_CannotAdvanceAnotherDriversTrip()
    {
        var rider = await fixture.NewRiderAsync();
        var owner = await fixture.NewVerifiedDriverAsync();
        var stranger = await fixture.NewVerifiedDriverAsync();

        var order = await rider.Client.PostAndReadAsync<CreateOrderRequest, CreateOrderResponse>(
            "/api/orders", Booking(rider.Id));

        using var accept = await owner.Client.PutJsonAsync(
            $"/api/mobile/driver/{owner.Id}/accept-order", new AcceptOrderRequest(order.Id));
        accept.EnsureSuccessStatusCode();

        using var hijack = await stranger.Client.PutJsonAsync(
            $"/api/mobile/driver/{stranger.Id}/arrive-order", new AcceptOrderRequest(order.Id));

        Assert.Equal(HttpStatusCode.Forbidden, hijack.StatusCode);
    }

    [Fact]
    public async Task ARider_CanCancelBeforePickup()
    {
        var rider = await fixture.NewRiderAsync();

        var order = await rider.Client.PostAndReadAsync<CreateOrderRequest, CreateOrderResponse>(
            "/api/orders", Booking(rider.Id));

        var cancelled = await rider.Client.PostAndReadAsync<CancelOrderRequest, OrderDetailResponse>(
            $"/api/orders/{order.Id}/cancel", new CancelOrderRequest("Berubah rencana"));

        Assert.Equal(OrderStatus.Cancelled, cancelled.Status);
        Assert.Equal(CancelledByParty.Rider, cancelled.CancelledBy);
        Assert.Equal("Berubah rencana", cancelled.CancellationReason);
    }

    [Fact]
    public async Task ACompletedTrip_CannotBeCancelled()
    {
        var rider = await fixture.NewRiderAsync();
        var driver = await fixture.NewVerifiedDriverAsync();

        var order = await CompleteATripAsync(rider, driver);

        using var cancel = await rider.Client.PostJsonAsync(
            $"/api/orders/{order.Id}/cancel", new CancelOrderRequest("Terlambat"));

        Assert.Equal(HttpStatusCode.Conflict, cancel.StatusCode);
    }

    [Fact]
    public async Task AStrangerCannotReadSomeoneElsesOrder()
    {
        var rider = await fixture.NewRiderAsync();
        var stranger = await fixture.NewRiderAsync();

        var order = await rider.Client.PostAndReadAsync<CreateOrderRequest, CreateOrderResponse>(
            "/api/orders", Booking(rider.Id));

        using var response = await stranger.Client.GetAsync($"/api/orders/{order.Id}");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task AnUnknownOrder_IsNotFound()
    {
        var rider = await fixture.NewRiderAsync();

        using var response = await rider.Client.GetAsync($"/api/orders/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Tracking_ReportsTheDriversDistanceAndEta()
    {
        var rider = await fixture.NewRiderAsync();
        var driver = await fixture.NewVerifiedDriverAsync();

        var order = await rider.Client.PostAndReadAsync<CreateOrderRequest, CreateOrderResponse>(
            "/api/orders", Booking(rider.Id));

        // Put the driver a little way from the pickup point.
        using var location = await driver.Client.PutJsonAsync(
            $"/api/mobile/driver/{driver.Id}/location", new UpdateLocationRequest(-6.2200, 106.8500, 90));
        location.EnsureSuccessStatusCode();

        using var accept = await driver.Client.PutJsonAsync(
            $"/api/mobile/driver/{driver.Id}/accept-order", new AcceptOrderRequest(order.Id));
        accept.EnsureSuccessStatusCode();

        var tracking = await rider.Client.GetAndReadAsync<OrderTrackingResponse>($"/api/orders/{order.Id}/tracking");

        Assert.Equal(OrderStatus.Accepted, tracking.Status);
        Assert.NotNull(tracking.DriverName);
        Assert.NotNull(tracking.DriverDistanceKm);
        Assert.True(tracking.EtaMinutes > 0);
    }

    [Fact]
    public async Task TheRidersTripList_ShowsTheirOwnTrips()
    {
        var rider = await fixture.NewRiderAsync();

        var order = await rider.Client.PostAndReadAsync<CreateOrderRequest, CreateOrderResponse>(
            "/api/orders", Booking(rider.Id));

        var trips = await rider.Client.GetAndReadAsync<PagedResult<OrderListItem>>(
            $"/api/mobile/rider/{rider.Id}/trips");

        Assert.Equal(1, trips.Total);
        Assert.Equal(order.Code, trips.Data[0].Code);
    }

    [Fact]
    public async Task TheRidersHome_SurfacesTheTripInProgress()
    {
        var rider = await fixture.NewRiderAsync();

        var order = await rider.Client.PostAndReadAsync<CreateOrderRequest, CreateOrderResponse>(
            "/api/orders", Booking(rider.Id));

        var home = await rider.Client.GetAndReadAsync<RiderHomeResponse>($"/api/mobile/rider/{rider.Id}/home");

        Assert.NotNull(home.ActiveOrder);
        Assert.Equal(order.Id, home.ActiveOrder!.Id);
    }

    [Fact]
    public async Task CompletingATrip_CreditsTheDriver()
    {
        var rider = await fixture.NewRiderAsync();
        var driver = await fixture.NewVerifiedDriverAsync();

        var order = await CompleteATripAsync(rider, driver);
        var profile = await driver.Client.GetAndReadAsync<UserProfileResponse>($"/api/profile/{driver.Id}");

        Assert.NotNull(profile.Driver);
        Assert.Equal(1, profile.Driver!.TotalTrips);
        Assert.Equal(order.FinalFare, profile.Driver.TotalEarnings);
        Assert.Equal(DriverStatus.Online, profile.Driver.Status);
    }

    // ─────────────────────────── helpers ───────────────────────────

    private static async Task<OrderStatus> StatusOfAsync(TestActor rider, Guid orderId)
    {
        var detail = await rider.Client.GetAndReadAsync<OrderDetailResponse>($"/api/orders/{orderId}");
        return detail.Status;
    }

    private static async Task AdvanceAsync(TestActor driver, Guid orderId, string action)
    {
        using var response = await driver.Client.PutJsonAsync(
            $"/api/mobile/driver/{driver.Id}/{action}", new AcceptOrderRequest(orderId));

        response.EnsureSuccessStatusCode();
    }

    internal static async Task<CreateOrderResponse> CompleteATripAsync(TestActor rider, TestActor driver)
    {
        var order = await rider.Client.PostAndReadAsync<CreateOrderRequest, CreateOrderResponse>(
            "/api/orders", Booking(rider.Id));

        using var accept = await driver.Client.PutJsonAsync(
            $"/api/mobile/driver/{driver.Id}/accept-order", new AcceptOrderRequest(order.Id));
        accept.EnsureSuccessStatusCode();

        await AdvanceAsync(driver, order.Id, "arrive-order");
        await AdvanceAsync(driver, order.Id, "start-order");
        await AdvanceAsync(driver, order.Id, "complete-order");

        return order;
    }
}
