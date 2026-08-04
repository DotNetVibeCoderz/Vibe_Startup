using FastRide.Shared.DTOs;
using FastRide.Shared.Models;
using FastRide.Tests.Infrastructure;

namespace FastRide.Tests.Integration;

/// <summary>Matching riders and drivers: proximity, freshness, and category.</summary>
[Collection(ApiCollection.Name)]
public class DispatchTests(ApiFixture fixture)
{
    // Kelapa Gading, deliberately ~9 km away from the Sudirman coordinates the other test
    // classes book from. Offers are ranked by longest wait first, so a shared pickup point
    // would bury this class's brand-new order behind everyone else's older ones.
    private const double CentreLat = -6.1580, CentreLon = 106.9060;

    [Fact]
    public async Task NearbyDrivers_FindsAnOnlineDriverInRange()
    {
        var driver = await OnlineDriverAsync(CentreLat, CentreLon);

        var nearby = await fixture.Admin.GetAndReadAsync<List<NearbyDriverItem>>(
            $"/api/drivers/nearby?lat={Fmt(CentreLat)}&lng={Fmt(CentreLon)}&radiusKm=3");

        var found = nearby.SingleOrDefault(item => item.DriverId == driver.Id);

        Assert.NotNull(found);
        Assert.InRange(found!.DistanceKm, 0, 3);
    }

    [Fact]
    public async Task NearbyDrivers_IgnoresDriversOutsideTheRadius()
    {
        // Bogor is about 50 km from the Jakarta centre point.
        var driver = await OnlineDriverAsync(-6.5950, 106.7900);

        var nearby = await fixture.Admin.GetAndReadAsync<List<NearbyDriverItem>>(
            $"/api/drivers/nearby?lat={Fmt(CentreLat)}&lng={Fmt(CentreLon)}&radiusKm=5");

        Assert.DoesNotContain(nearby, item => item.DriverId == driver.Id);
    }

    [Fact]
    public async Task NearbyDrivers_IgnoresOfflineDrivers()
    {
        var driver = await OnlineDriverAsync(CentreLat, CentreLon);

        using var goOffline = await driver.Client.PutJsonAsync(
            $"/api/mobile/driver/{driver.Id}/status", new SetDriverStatusRequest(DriverStatus.Offline));
        goOffline.EnsureSuccessStatusCode();

        var nearby = await fixture.Admin.GetAndReadAsync<List<NearbyDriverItem>>(
            $"/api/drivers/nearby?lat={Fmt(CentreLat)}&lng={Fmt(CentreLon)}&radiusKm=5");

        Assert.DoesNotContain(nearby, item => item.DriverId == driver.Id);
    }

    [Fact]
    public async Task NearbyDrivers_IgnoresDriversThatNeverSentAPosition()
    {
        // Verified and online, but no GPS fix: not matchable.
        var driver = await fixture.NewVerifiedDriverAsync();

        using var online = await driver.Client.PutJsonAsync(
            $"/api/mobile/driver/{driver.Id}/status", new SetDriverStatusRequest(DriverStatus.Online));
        online.EnsureSuccessStatusCode();

        var nearby = await fixture.Admin.GetAndReadAsync<List<NearbyDriverItem>>(
            $"/api/drivers/nearby?lat={Fmt(CentreLat)}&lng={Fmt(CentreLon)}&radiusKm=50");

        Assert.DoesNotContain(nearby, item => item.DriverId == driver.Id);
    }

    [Fact]
    public async Task NearbyDrivers_CanBeNarrowedToOneCategory()
    {
        var bike = await OnlineDriverAsync(CentreLat, CentreLon, VehicleCategory.Bike);
        var premium = await OnlineDriverAsync(CentreLat, CentreLon, VehicleCategory.Premium);

        var bikesOnly = await fixture.Admin.GetAndReadAsync<List<NearbyDriverItem>>(
            $"/api/drivers/nearby?lat={Fmt(CentreLat)}&lng={Fmt(CentreLon)}&radiusKm=5&category=Bike");

        Assert.Contains(bikesOnly, item => item.DriverId == bike.Id);
        Assert.DoesNotContain(bikesOnly, item => item.DriverId == premium.Id);
    }

    [Fact]
    public async Task NearbyDrivers_ReturnsTheClosestFirst()
    {
        await OnlineDriverAsync(CentreLat, CentreLon);
        await OnlineDriverAsync(CentreLat - 0.02, CentreLon - 0.02);

        var nearby = await fixture.Admin.GetAndReadAsync<List<NearbyDriverItem>>(
            $"/api/drivers/nearby?lat={Fmt(CentreLat)}&lng={Fmt(CentreLon)}&radiusKm=10");

        var distances = nearby.Select(item => item.DistanceKm).ToList();

        Assert.Equal(distances.OrderBy(distance => distance), distances);
    }

    [Fact]
    public async Task AvailableOrders_ShowsAnOpenBookingWithItsPickupDistance()
    {
        var driver = await OnlineDriverAsync(CentreLat, CentreLon);
        var rider = await fixture.NewRiderAsync();

        var order = await rider.Client.PostAndReadAsync<CreateOrderRequest, CreateOrderResponse>(
            "/api/orders",
            new CreateOrderRequest(rider.Id,
                CentreLat, CentreLon, "Jl. Sudirman No. 1",
                -6.1751, 106.8650, "Jl. Thamrin No. 9"));

        var offers = await driver.Client.GetAndReadAsync<List<IncomingOrderItem>>(
            $"/api/mobile/driver/{driver.Id}/orders/available?radiusKm=3");

        var offer = offers.SingleOrDefault(item => item.OrderId == order.Id);

        Assert.NotNull(offer);
        Assert.InRange(offer!.PickupDistanceKm, 0, 1);
        Assert.True(offer.EstimatedFare > 0);
    }

    [Fact]
    public async Task AvailableOrders_DropsAnOrderOnceItIsTaken()
    {
        var driver = await OnlineDriverAsync(CentreLat, CentreLon);
        var other = await OnlineDriverAsync(CentreLat, CentreLon);
        var rider = await fixture.NewRiderAsync();

        var order = await rider.Client.PostAndReadAsync<CreateOrderRequest, CreateOrderResponse>(
            "/api/orders",
            new CreateOrderRequest(rider.Id,
                CentreLat, CentreLon, "Jl. Sudirman No. 1",
                -6.1751, 106.8650, "Jl. Thamrin No. 9"));

        using var accept = await other.Client.PutJsonAsync(
            $"/api/mobile/driver/{other.Id}/accept-order", new AcceptOrderRequest(order.Id));
        accept.EnsureSuccessStatusCode();

        var offers = await driver.Client.GetAndReadAsync<List<IncomingOrderItem>>(
            $"/api/mobile/driver/{driver.Id}/orders/available?radiusKm=3");

        Assert.DoesNotContain(offers, item => item.OrderId == order.Id);
    }

    [Fact]
    public async Task TheDriverHome_HidesOffersWhileATripIsRunning()
    {
        var driver = await OnlineDriverAsync(CentreLat, CentreLon);
        var rider = await fixture.NewRiderAsync();

        var order = await rider.Client.PostAndReadAsync<CreateOrderRequest, CreateOrderResponse>(
            "/api/orders",
            new CreateOrderRequest(rider.Id,
                CentreLat, CentreLon, "Jl. Sudirman No. 1",
                -6.1751, 106.8650, "Jl. Thamrin No. 9"));

        using var accept = await driver.Client.PutJsonAsync(
            $"/api/mobile/driver/{driver.Id}/accept-order", new AcceptOrderRequest(order.Id));
        accept.EnsureSuccessStatusCode();

        var home = await driver.Client.GetAndReadAsync<DriverHomeResponse>(
            $"/api/mobile/driver/{driver.Id}/home");

        Assert.NotNull(home.ActiveTrip);
        Assert.Equal(order.Id, home.ActiveTrip!.Id);
        Assert.Empty(home.IncomingOrders);
    }

    [Fact]
    public async Task UpdatingLocation_RejectsImpossibleCoordinates()
    {
        var driver = await fixture.NewVerifiedDriverAsync();

        using var response = await driver.Client.PutJsonAsync(
            $"/api/mobile/driver/{driver.Id}/location", new UpdateLocationRequest(999, 999));

        Assert.Equal(System.Net.HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task ToggleOnline_FlipsBetweenTheTwoStates()
    {
        var driver = await fixture.NewVerifiedDriverAsync();

        var first = await driver.Client.PutAndReadAsync<object?, DriverStatusResponse>(
            $"/api/mobile/driver/{driver.Id}/toggle-online", null);

        var second = await driver.Client.PutAndReadAsync<object?, DriverStatusResponse>(
            $"/api/mobile/driver/{driver.Id}/toggle-online", null);

        Assert.Equal(DriverStatus.Online, first.Status);
        Assert.Equal(DriverStatus.Offline, second.Status);
    }

    private async Task<TestActor> OnlineDriverAsync(
        double latitude, double longitude, VehicleCategory category = VehicleCategory.Economy)
    {
        var driver = await fixture.NewVerifiedDriverAsync(category);

        using var location = await driver.Client.PutJsonAsync(
            $"/api/mobile/driver/{driver.Id}/location", new UpdateLocationRequest(latitude, longitude, 0));
        location.EnsureSuccessStatusCode();

        using var status = await driver.Client.PutJsonAsync(
            $"/api/mobile/driver/{driver.Id}/status", new SetDriverStatusRequest(DriverStatus.Online));
        status.EnsureSuccessStatusCode();

        return driver;
    }

    private static string Fmt(double value) =>
        value.ToString(System.Globalization.CultureInfo.InvariantCulture);
}
