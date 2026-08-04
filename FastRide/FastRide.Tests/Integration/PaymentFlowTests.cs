using System.Net;
using FastRide.Shared.Common;
using FastRide.Shared.DTOs;
using FastRide.Shared.Models;
using FastRide.Tests.Infrastructure;

namespace FastRide.Tests.Integration;

/// <summary>
/// The payment intent from end to end: opening a charge, paying it, failing it, retrying,
/// and the guarantees that hold throughout.
/// </summary>
[Collection(ApiCollection.Name)]
public class PaymentFlowTests(ApiFixture fixture)
{
    private static CreateOrderRequest Booking(Guid riderId, PaymentMethod method = PaymentMethod.Qris) =>
        new(riderId,
            -6.2088, 106.8456, "Jl. Sudirman No. 1",
            -6.1751, 106.8650, "Jl. Thamrin No. 9",
            VehicleCategory.Economy, method);

    private async Task<(TestActor Rider, CreateOrderResponse Order)> NewBookingAsync(
        PaymentMethod method = PaymentMethod.Qris)
    {
        var rider = await fixture.NewRiderAsync();

        var order = await rider.Client.PostAndReadAsync<CreateOrderRequest, CreateOrderResponse>(
            "/api/orders", Booking(rider.Id, method));

        return (rider, order);
    }

    // ───────────────────── available methods ─────────────────────

    [Fact]
    public async Task TheMethodListComesFromWhatIsSwitchedOn()
    {
        var rider = await fixture.NewRiderAsync();

        var methods = await rider.Client.GetAndReadAsync<AvailablePaymentMethodsResponse>("/api/payments/methods");

        Assert.NotEmpty(methods.Methods);
        Assert.Contains(methods.Methods, option => option.Method == PaymentMethod.Qris);
        Assert.Contains(methods.Methods, option => option.Method == PaymentMethod.Cash);

        // Cash needs nothing from the rider; everything else routes through a provider.
        Assert.False(methods.Methods.Single(option => option.Method == PaymentMethod.Cash).RequiresApp);
        Assert.True(methods.Methods.Single(option => option.Method == PaymentMethod.Qris).RequiresApp);
    }

    // ─────────────────────────── QRIS ───────────────────────────

    [Fact]
    public async Task ChargingWithQris_ReturnsAScannableCode()
    {
        var (rider, order) = await NewBookingAsync();

        var payment = await rider.Client.PostAndReadAsync<PaymentRequest, PaymentResponse>(
            "/api/payments", new PaymentRequest(order.Id, PaymentMethod.Qris));

        Assert.Equal(PaymentStatus.AwaitingPayment, payment.Status);
        Assert.Equal(order.FinalFare, payment.Amount);
        Assert.NotNull(payment.PaymentPayload);

        // The payload is a real EMVCo string carrying the real amount.
        Assert.True(QrisPayload.IsValid(payment.PaymentPayload!));
        Assert.Equal(order.FinalFare, QrisPayload.ReadAmount(payment.PaymentPayload!));

        // And it arrives rendered, so the app needs no QR encoder.
        Assert.StartsWith("data:image/svg+xml;base64,", payment.QrImage);
    }

    [Fact]
    public async Task ChargingTwiceReturnsTheSameCode()
    {
        // Issuing a second QR would leave two live charges for one trip at the provider.
        var (rider, order) = await NewBookingAsync();

        var first = await rider.Client.PostAndReadAsync<PaymentRequest, PaymentResponse>(
            "/api/payments", new PaymentRequest(order.Id, PaymentMethod.Qris));

        var second = await rider.Client.PostAndReadAsync<PaymentRequest, PaymentResponse>(
            "/api/payments", new PaymentRequest(order.Id, PaymentMethod.Qris));

        Assert.Equal(first.Id, second.Id);
        Assert.Equal(first.PaymentPayload, second.PaymentPayload);
        Assert.Equal(first.TransactionReference, second.TransactionReference);
    }

    [Fact]
    public async Task PayingAQrisChargeSettlesIt()
    {
        var (rider, order) = await NewBookingAsync();

        await rider.Client.PostAndReadAsync<PaymentRequest, PaymentResponse>(
            "/api/payments", new PaymentRequest(order.Id, PaymentMethod.Qris));

        // Goes through the provider's signed callback path, not a shortcut.
        var settled = await rider.Client.PostAndReadAsync<object?, PaymentResponse>(
            $"/api/payments/sandbox/{order.Id}/settle", null);

        Assert.Equal(PaymentStatus.Completed, settled.Status);
        Assert.NotNull(settled.CompletedAt);
        Assert.True(settled.IsSettled);

        // A settled charge stops advertising a payable code.
        Assert.Null(settled.QrImage);
    }

    [Fact]
    public async Task ASettledChargeIsNotReopenedByChargingAgain()
    {
        var (rider, order) = await NewBookingAsync();

        await rider.Client.PostAndReadAsync<PaymentRequest, PaymentResponse>(
            "/api/payments", new PaymentRequest(order.Id, PaymentMethod.Qris));

        var settled = await rider.Client.PostAndReadAsync<object?, PaymentResponse>(
            $"/api/payments/sandbox/{order.Id}/settle", null);

        var again = await rider.Client.PostAndReadAsync<PaymentRequest, PaymentResponse>(
            "/api/payments", new PaymentRequest(order.Id, PaymentMethod.EWallet));

        Assert.Equal(PaymentStatus.Completed, again.Status);
        Assert.Equal(settled.Id, again.Id);
        Assert.Equal(settled.CompletedAt, again.CompletedAt);
    }

    // ───────────────────── failure and retry ─────────────────────

    [Fact]
    public async Task ADeclinedChargeCanBeRetried()
    {
        // The unique index on Payment.OrderId used to make this impossible: one failure would
        // lock the order out of ever being paid.
        var (rider, order) = await NewBookingAsync();

        await rider.Client.PostAndReadAsync<PaymentRequest, PaymentResponse>(
            "/api/payments", new PaymentRequest(order.Id, PaymentMethod.Qris));

        var failed = await rider.Client.PostAndReadAsync<object?, PaymentResponse>(
            $"/api/payments/sandbox/{order.Id}/fail", null);

        Assert.Equal(PaymentStatus.Failed, failed.Status);
        Assert.True(failed.CanRetry);
        Assert.NotNull(failed.FailureReason);

        var retried = await rider.Client.PostAndReadAsync<PaymentRequest, PaymentResponse>(
            "/api/payments", new PaymentRequest(order.Id, PaymentMethod.EWallet, 0, EWalletChannel.Dana));

        Assert.Equal(PaymentStatus.AwaitingPayment, retried.Status);
        Assert.Equal(PaymentMethod.EWallet, retried.Method);
        Assert.Equal(EWalletChannel.Dana, retried.WalletChannel);
        Assert.Null(retried.FailureReason);

        var settled = await rider.Client.PostAndReadAsync<object?, PaymentResponse>(
            $"/api/payments/sandbox/{order.Id}/settle", null);

        Assert.Equal(PaymentStatus.Completed, settled.Status);
    }

    [Fact]
    public async Task RetryingNeverCreatesASecondPayment()
    {
        var (rider, order) = await NewBookingAsync();

        for (var attempt = 0; attempt < 3; attempt++)
        {
            await rider.Client.PostAndReadAsync<PaymentRequest, PaymentResponse>(
                "/api/payments", new PaymentRequest(order.Id, PaymentMethod.Qris));

            await rider.Client.PostAndReadAsync<object?, PaymentResponse>(
                $"/api/payments/sandbox/{order.Id}/fail", null);
        }

        await rider.Client.PostAndReadAsync<PaymentRequest, PaymentResponse>(
            "/api/payments", new PaymentRequest(order.Id, PaymentMethod.Qris));

        await rider.Client.PostAndReadAsync<object?, PaymentResponse>(
            $"/api/payments/sandbox/{order.Id}/settle", null);

        var page = await fixture.Admin.GetAndReadAsync<PagedResult<PaymentResponse>>("/api/payments?limit=200");

        Assert.Single(page.Data.Where(payment => payment.OrderId == order.Id));
    }

    // ─────────────────────────── cash ───────────────────────────

    [Fact]
    public async Task CashSettlesWithoutAnyProviderRoundTrip()
    {
        var (rider, order) = await NewBookingAsync(PaymentMethod.Cash);

        var payment = await rider.Client.PostAndReadAsync<PaymentRequest, PaymentResponse>(
            "/api/payments", new PaymentRequest(order.Id, PaymentMethod.Cash));

        Assert.Equal(PaymentStatus.Completed, payment.Status);
        Assert.Equal("manual", payment.ProviderName);
        Assert.Null(payment.PaymentPayload);
    }

    [Fact]
    public async Task FinishingACashTripLeavesItPaid()
    {
        var rider = await fixture.NewRiderAsync();
        var driver = await fixture.NewVerifiedDriverAsync();

        var order = await CompleteTripAsync(rider, driver, PaymentMethod.Cash);

        var payment = await rider.Client.GetAndReadAsync<PaymentResponse>($"/api/payments/order/{order.Id}");

        Assert.Equal(PaymentStatus.Completed, payment.Status);
        Assert.Equal(order.FinalFare, payment.Amount);
    }

    [Fact]
    public async Task FinishingAQrisTripLeavesThePaymentOutstanding()
    {
        // The trip is over but the rider still has to pay — the driver should not be blocked
        // waiting for them.
        var rider = await fixture.NewRiderAsync();
        var driver = await fixture.NewVerifiedDriverAsync();

        var order = await CompleteTripAsync(rider, driver, PaymentMethod.Qris);

        var detail = await rider.Client.GetAndReadAsync<OrderDetailResponse>($"/api/orders/{order.Id}");
        Assert.Equal(OrderStatus.Completed, detail.Status);

        var payment = await rider.Client.GetAndReadAsync<PaymentResponse>($"/api/payments/order/{order.Id}");
        Assert.False(payment.IsSettled);

        // And the rider can finish it from the app.
        await rider.Client.PostAndReadAsync<PaymentRequest, PaymentResponse>(
            "/api/payments", new PaymentRequest(order.Id, PaymentMethod.Qris));

        var settled = await rider.Client.PostAndReadAsync<object?, PaymentResponse>(
            $"/api/payments/sandbox/{order.Id}/settle", null);

        Assert.Equal(PaymentStatus.Completed, settled.Status);
    }

    [Fact]
    public async Task PayingATripInProgressClosesItOut()
    {
        var rider = await fixture.NewRiderAsync();
        var driver = await fixture.NewVerifiedDriverAsync();

        var order = await rider.Client.PostAndReadAsync<CreateOrderRequest, CreateOrderResponse>(
            "/api/orders", Booking(rider.Id));

        await AcceptAndStartAsync(driver, order.Id);

        await rider.Client.PostAndReadAsync<PaymentRequest, PaymentResponse>(
            "/api/payments", new PaymentRequest(order.Id, PaymentMethod.Qris));

        await rider.Client.PostAndReadAsync<object?, PaymentResponse>(
            $"/api/payments/sandbox/{order.Id}/settle", null);

        var detail = await rider.Client.GetAndReadAsync<OrderDetailResponse>($"/api/orders/{order.Id}");

        Assert.Equal(OrderStatus.Completed, detail.Status);
    }

    // ─────────────────────────── access ───────────────────────────

    [Fact]
    public async Task AStrangerCannotOpenAChargeOnSomeoneElsesTrip()
    {
        var (_, order) = await NewBookingAsync();
        var stranger = await fixture.NewRiderAsync();

        using var response = await stranger.Client.PostJsonAsync(
            "/api/payments", new PaymentRequest(order.Id, PaymentMethod.Qris));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task AStrangerCannotReadSomeoneElsesPayment()
    {
        var (rider, order) = await NewBookingAsync();
        var stranger = await fixture.NewRiderAsync();

        await rider.Client.PostAndReadAsync<PaymentRequest, PaymentResponse>(
            "/api/payments", new PaymentRequest(order.Id, PaymentMethod.Qris));

        using var response = await stranger.Client.GetAsync($"/api/payments/order/{order.Id}");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task AStrangerCannotSettleSomeoneElsesCharge()
    {
        var (rider, order) = await NewBookingAsync();
        var stranger = await fixture.NewRiderAsync();

        await rider.Client.PostAndReadAsync<PaymentRequest, PaymentResponse>(
            "/api/payments", new PaymentRequest(order.Id, PaymentMethod.Qris));

        using var response = await stranger.Client.PostAsync($"/api/payments/sandbox/{order.Id}/settle", null);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task ACancelledTripCannotBeCharged()
    {
        var (rider, order) = await NewBookingAsync();

        using var cancel = await rider.Client.PostJsonAsync(
            $"/api/orders/{order.Id}/cancel", new CancelOrderRequest("Batal"));
        cancel.EnsureSuccessStatusCode();

        using var response = await rider.Client.PostJsonAsync(
            "/api/payments", new PaymentRequest(order.Id, PaymentMethod.Qris));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    // ────────────────────────── webhook ──────────────────────────

    [Fact]
    public async Task AWebhookWithoutASignatureIsRefused()
    {
        var client = fixture.NewClient();

        using var response = await client.PostJsonAsync("/api/payments/webhook/simulated",
            new { reference = "TRX-PALSU", status = "Completed", amount = "999999" });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task AWebhookWithAForgedSignatureIsRefused()
    {
        var client = fixture.NewClient();

        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/payments/webhook/simulated")
        {
            Content = new StringContent(
                """{"reference":"TRX-PALSU","status":"Completed","amount":"999999"}""",
                System.Text.Encoding.UTF8, "application/json")
        };

        request.Headers.TryAddWithoutValidation("x-fastride-signature", "deadbeef");

        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task AWebhookForAnUnknownProviderIsNotFound()
    {
        var client = fixture.NewClient();

        using var response = await client.PostJsonAsync("/api/payments/webhook/bukanprovider", new { });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task ARepeatedSettlementCallbackChangesNothing()
    {
        // Providers retry callbacks; the same "paid" message may arrive several times.
        var (rider, order) = await NewBookingAsync();

        await rider.Client.PostAndReadAsync<PaymentRequest, PaymentResponse>(
            "/api/payments", new PaymentRequest(order.Id, PaymentMethod.Qris));

        var first = await rider.Client.PostAndReadAsync<object?, PaymentResponse>(
            $"/api/payments/sandbox/{order.Id}/settle", null);

        var second = await rider.Client.PostAndReadAsync<object?, PaymentResponse>(
            $"/api/payments/sandbox/{order.Id}/settle", null);

        Assert.Equal(first.Id, second.Id);
        Assert.Equal(first.CompletedAt, second.CompletedAt);
        Assert.Equal(PaymentStatus.Completed, second.Status);
    }

    [Fact]
    public async Task ALateFailureCallbackCannotUnpayASettledTrip()
    {
        var (rider, order) = await NewBookingAsync();

        await rider.Client.PostAndReadAsync<PaymentRequest, PaymentResponse>(
            "/api/payments", new PaymentRequest(order.Id, PaymentMethod.Qris));

        await rider.Client.PostAndReadAsync<object?, PaymentResponse>(
            $"/api/payments/sandbox/{order.Id}/settle", null);

        // A stale "declined" arriving after the money landed must be ignored.
        using var late = await rider.Client.PostAsync($"/api/payments/sandbox/{order.Id}/fail", null);
        late.EnsureSuccessStatusCode();

        var payment = await rider.Client.GetAndReadAsync<PaymentResponse>($"/api/payments/order/{order.Id}");

        Assert.Equal(PaymentStatus.Completed, payment.Status);
    }

    // ─────────────────────────── helpers ───────────────────────────

    private static async Task AcceptAndStartAsync(TestActor driver, Guid orderId)
    {
        using var accept = await driver.Client.PutJsonAsync(
            $"/api/mobile/driver/{driver.Id}/accept-order", new AcceptOrderRequest(orderId));
        accept.EnsureSuccessStatusCode();

        using var arrive = await driver.Client.PutJsonAsync(
            $"/api/mobile/driver/{driver.Id}/arrive-order", new AcceptOrderRequest(orderId));
        arrive.EnsureSuccessStatusCode();

        using var start = await driver.Client.PutJsonAsync(
            $"/api/mobile/driver/{driver.Id}/start-order", new AcceptOrderRequest(orderId));
        start.EnsureSuccessStatusCode();
    }

    private static async Task<CreateOrderResponse> CompleteTripAsync(
        TestActor rider, TestActor driver, PaymentMethod method)
    {
        var order = await rider.Client.PostAndReadAsync<CreateOrderRequest, CreateOrderResponse>(
            "/api/orders", Booking(rider.Id, method));

        await AcceptAndStartAsync(driver, order.Id);

        using var complete = await driver.Client.PutJsonAsync(
            $"/api/mobile/driver/{driver.Id}/complete-order", new AcceptOrderRequest(order.Id));
        complete.EnsureSuccessStatusCode();

        return order;
    }
}
