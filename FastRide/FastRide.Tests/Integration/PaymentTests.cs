using System.Net;
using FastRide.Shared.Common;
using FastRide.Shared.DTOs;
using FastRide.Shared.Models;
using FastRide.Tests.Infrastructure;

namespace FastRide.Tests.Integration;

/// <summary>
/// A trip can be settled from two directions — the driver finishing it, or a payment being
/// posted. Both must land on exactly one payment row. Before v2.0 they each created their
/// own, so a single trip could be charged twice.
/// </summary>
[Collection(ApiCollection.Name)]
public class PaymentTests(ApiFixture fixture)
{
    [Fact]
    public async Task CompletingATrip_RecordsExactlyOnePayment()
    {
        var rider = await fixture.NewRiderAsync();
        var driver = await fixture.NewVerifiedDriverAsync();

        var order = await OrderLifecycleTests.CompleteATripAsync(rider, driver);
        var payments = await PaymentsForAsync(order.Id);

        Assert.Single(payments);
        Assert.Equal(order.FinalFare, payments[0].Amount);
        Assert.Equal(PaymentStatus.Completed, payments[0].Status);
    }

    [Fact]
    public async Task PostingAPaymentForAnAlreadySettledTrip_ReturnsTheExistingOne()
    {
        var rider = await fixture.NewRiderAsync();
        var driver = await fixture.NewVerifiedDriverAsync();

        var order = await OrderLifecycleTests.CompleteATripAsync(rider, driver);
        var original = (await PaymentsForAsync(order.Id)).Single();

        var settled = await rider.Client.PostAndReadAsync<PaymentRequest, PaymentResponse>(
            "/api/payments", new PaymentRequest(order.Id, PaymentMethod.EWallet));

        Assert.Equal(original.Id, settled.Id);
        Assert.Single(await PaymentsForAsync(order.Id));
    }

    [Fact]
    public async Task PostingAPaymentTwice_NeverChargesTwice()
    {
        var rider = await fixture.NewRiderAsync();
        var driver = await fixture.NewVerifiedDriverAsync();

        var order = await OrderLifecycleTests.CompleteATripAsync(rider, driver);

        for (var attempt = 0; attempt < 3; attempt++)
        {
            using var response = await rider.Client.PostJsonAsync(
                "/api/payments", new PaymentRequest(order.Id, PaymentMethod.Cash));

            Assert.True(response.IsSuccessStatusCode);
        }

        var payments = await PaymentsForAsync(order.Id);

        Assert.Single(payments);
    }

    [Fact]
    public async Task OpeningAChargeOnAStartedTripDoesNotCloseItYet()
    {
        // Posting a charge only opens one. Until the provider says the money arrived, the
        // trip is still running — the earlier model that treated "payment posted" as "paid"
        // is exactly what made a real gateway impossible to plug in.
        var rider = await fixture.NewRiderAsync();
        var driver = await fixture.NewVerifiedDriverAsync();

        var order = await StartTripAsync(rider, driver, PaymentMethod.EWallet);

        var payment = await rider.Client.PostAndReadAsync<PaymentRequest, PaymentResponse>(
            "/api/payments", new PaymentRequest(order.Id, PaymentMethod.EWallet));

        Assert.Equal(PaymentStatus.AwaitingPayment, payment.Status);

        var detail = await rider.Client.GetAndReadAsync<OrderDetailResponse>($"/api/orders/{order.Id}");

        Assert.Equal(OrderStatus.Started, detail.Status);
        Assert.Single(await PaymentsForAsync(order.Id));
    }

    [Fact]
    public async Task SettlingAChargeOnAStartedTripClosesItOut()
    {
        var rider = await fixture.NewRiderAsync();
        var driver = await fixture.NewVerifiedDriverAsync();

        var order = await StartTripAsync(rider, driver, PaymentMethod.EWallet);

        await rider.Client.PostAndReadAsync<PaymentRequest, PaymentResponse>(
            "/api/payments", new PaymentRequest(order.Id, PaymentMethod.EWallet));

        await rider.Client.PostAndReadAsync<object?, PaymentResponse>(
            $"/api/payments/sandbox/{order.Id}/settle", null);

        var detail = await rider.Client.GetAndReadAsync<OrderDetailResponse>($"/api/orders/{order.Id}");

        Assert.Equal(OrderStatus.Completed, detail.Status);
        Assert.Single(await PaymentsForAsync(order.Id));
    }

    private static async Task<CreateOrderResponse> StartTripAsync(
        TestActor rider, TestActor driver, PaymentMethod method)
    {
        var order = await rider.Client.PostAndReadAsync<CreateOrderRequest, CreateOrderResponse>(
            "/api/orders",
            new CreateOrderRequest(rider.Id,
                -6.2088, 106.8456, "Jl. Sudirman No. 1",
                -6.1751, 106.8650, "Jl. Thamrin No. 9",
                VehicleCategory.Economy, method));

        foreach (var action in new[] { "accept-order", "arrive-order", "start-order" })
        {
            using var response = await driver.Client.PutJsonAsync(
                $"/api/mobile/driver/{driver.Id}/{action}", new AcceptOrderRequest(order.Id));

            response.EnsureSuccessStatusCode();
        }

        return order;
    }

    [Fact]
    public async Task ACancelledTrip_CannotBePaid()
    {
        var rider = await fixture.NewRiderAsync();

        var order = await rider.Client.PostAndReadAsync<CreateOrderRequest, CreateOrderResponse>(
            "/api/orders",
            new CreateOrderRequest(rider.Id,
                -6.2088, 106.8456, "Jl. Sudirman No. 1",
                -6.1751, 106.8650, "Jl. Thamrin No. 9"));

        using var cancel = await rider.Client.PostJsonAsync(
            $"/api/orders/{order.Id}/cancel", new CancelOrderRequest("Batal"));
        cancel.EnsureSuccessStatusCode();

        using var payment = await rider.Client.PostJsonAsync(
            "/api/payments", new PaymentRequest(order.Id, PaymentMethod.Cash));

        Assert.Equal(HttpStatusCode.Conflict, payment.StatusCode);
    }

    [Fact]
    public async Task AStranger_CannotPaySomeoneElsesTrip()
    {
        var rider = await fixture.NewRiderAsync();
        var driver = await fixture.NewVerifiedDriverAsync();
        var stranger = await fixture.NewRiderAsync();

        var order = await OrderLifecycleTests.CompleteATripAsync(rider, driver);

        using var response = await stranger.Client.PostJsonAsync(
            "/api/payments", new PaymentRequest(order.Id, PaymentMethod.Cash));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task EveryPaymentCarriesATransactionReference()
    {
        var rider = await fixture.NewRiderAsync();
        var driver = await fixture.NewVerifiedDriverAsync();

        var order = await OrderLifecycleTests.CompleteATripAsync(rider, driver);
        var payment = (await PaymentsForAsync(order.Id)).Single();

        Assert.StartsWith("TRX-", payment.TransactionReference);
    }

    private async Task<List<PaymentResponse>> PaymentsForAsync(Guid orderId)
    {
        var page = await fixture.Admin.GetAndReadAsync<PagedResult<PaymentResponse>>("/api/payments?limit=200");

        return page.Data.Where(payment => payment.OrderId == orderId).ToList();
    }
}
