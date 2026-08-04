using System.Text.Json;
using FastRide.Api.Payments;
using FastRide.Shared.Models;
using FastRide.Shared.Payments;
using Microsoft.Extensions.Logging.Abstractions;

namespace FastRide.Tests.Unit;

/// <summary>
/// Provider behaviour that does not need a network: charge shapes, callback verification,
/// and the routing rules that decide who handles what.
/// </summary>
public class PaymentProviderTests
{
    private const string Secret = "rahasia-webhook-uji";

    private static PaymentProviderConfig SimulatedConfig() => new()
    {
        Name = "simulated",
        DisplayName = "Simulasi",
        IsEnabled = true,
        IsSandbox = true,
        SupportedMethods = "Qris,EWallet,VirtualAccount",
        WebhookSecret = Secret,
        MerchantId = "ID1234567890123",
        MerchantName = "FASTRIDE UJI",
        MerchantCity = "JAKARTA"
    };

    private static SimulatedPaymentProvider NewSimulated(TimeProvider? clock = null) =>
        new(SimulatedConfig(), NullLogger<SimulatedPaymentProvider>.Instance, clock ?? TimeProvider.System);

    private static PaymentChargeRequest Request(
        PaymentMethod method = PaymentMethod.Qris, decimal amount = 25000m, DateTime? expiresAt = null) =>
        new(Guid.NewGuid(), "TRX-20260804-UJI001", amount, method, EWalletChannel.Unspecified,
            "FR-ABC123", "Budi Santoso", "budi@fastride.test", "08120000000",
            expiresAt ?? DateTime.UtcNow.AddMinutes(15));

    // ─────────────────────────── manual ───────────────────────────

    [Fact]
    public async Task Manual_SettlesOnTheSpot()
    {
        var provider = new ManualPaymentProvider();

        var result = await provider.ChargeAsync(Request(PaymentMethod.Cash));

        Assert.True(result.Success);
        Assert.Equal(PaymentStatus.Completed, result.Status);
    }

    [Fact]
    public void Manual_HasNothingToCallBackAbout() =>
        Assert.Null(new ManualPaymentProvider().ReadCallback(
            new PaymentCallbackContext("{}", new Dictionary<string, string>(), null)));

    [Fact]
    public void Manual_DeclaresItSettlesWithoutThePayer() =>
        Assert.True(new ManualPaymentProvider().SettlesImmediately);

    // ────────────────────────── simulated ──────────────────────────

    [Fact]
    public async Task Simulated_IssuesAScannableQrisPayload()
    {
        var result = await NewSimulated().ChargeAsync(Request(PaymentMethod.Qris, 25460m));

        Assert.True(result.Success);
        Assert.Equal(PaymentStatus.AwaitingPayment, result.Status);
        Assert.NotNull(result.Payload);

        // The point of the simulated provider is that the artefact is genuine.
        Assert.True(Shared.Common.QrisPayload.IsValid(result.Payload!));
        Assert.Equal(25460m, Shared.Common.QrisPayload.ReadAmount(result.Payload!));
    }

    [Fact]
    public async Task Simulated_IssuesANumberForAVirtualAccount()
    {
        var result = await NewSimulated().ChargeAsync(Request(PaymentMethod.VirtualAccount));

        Assert.NotNull(result.Payload);
        Assert.All(result.Payload!, character => Assert.True(char.IsDigit(character)));
    }

    [Fact]
    public async Task Simulated_IssuesARedirectForAWallet()
    {
        var result = await NewSimulated().ChargeAsync(Request(PaymentMethod.EWallet));

        Assert.StartsWith("https://", result.Payload);
    }

    [Fact]
    public async Task Simulated_ChargeStartsAwaitingThePayer()
    {
        var provider = NewSimulated();
        var charge = await provider.ChargeAsync(Request());

        var status = await provider.QueryAsync(new PaymentQueryRequest(charge.ProviderReference!, "TRX-20260804-UJI001"));

        Assert.Equal(PaymentStatus.AwaitingPayment, status.Status);
    }

    [Fact]
    public async Task Simulated_ReportsACompletedChargeOnceThePayerActs()
    {
        var provider = NewSimulated();
        var charge = await provider.ChargeAsync(Request());

        Assert.True(provider.Advance(charge.ProviderReference!, PaymentStatus.Completed, out var reference));
        Assert.Equal("TRX-20260804-UJI001", reference);

        var status = await provider.QueryAsync(new PaymentQueryRequest(charge.ProviderReference!, reference!));

        Assert.Equal(PaymentStatus.Completed, status.Status);
    }

    [Fact]
    public async Task Simulated_ExpiresAChargeOnceItsDeadlinePasses()
    {
        // Expiry is evaluated on read, so time can be moved forward without waiting.
        var clock = new FakeTimeProvider(DateTime.UtcNow);
        var provider = NewSimulated(clock);

        var charge = await provider.ChargeAsync(Request(expiresAt: clock.GetUtcNow().UtcDateTime.AddMinutes(5)));

        clock.Advance(TimeSpan.FromMinutes(6));

        var status = await provider.QueryAsync(new PaymentQueryRequest(charge.ProviderReference!, "TRX-20260804-UJI001"));

        Assert.Equal(PaymentStatus.Expired, status.Status);
    }

    [Fact]
    public async Task Simulated_DoesNotRecogniseAForeignReference()
    {
        var status = await NewSimulated().QueryAsync(new PaymentQueryRequest("SIM-BUKAN-PUNYA-KITA", "TRX-X"));

        Assert.NotNull(status.Error);
    }

    [Fact]
    public void Simulated_AcceptsItsOwnSignedCallback()
    {
        var provider = NewSimulated();
        var (body, signature) = provider.BuildCallback("SIM-1", "TRX-1", PaymentStatus.Completed, 25000m);

        var callback = provider.ReadCallback(new PaymentCallbackContext(
            body, new Dictionary<string, string> { ["x-fastride-signature"] = signature }, Secret));

        Assert.NotNull(callback);
        Assert.Equal("TRX-1", callback!.Reference);
        Assert.Equal(PaymentStatus.Completed, callback.Status);
        Assert.Equal(25000m, callback.Amount);
    }

    [Fact]
    public void Simulated_RejectsACallbackWithAForgedSignature()
    {
        var provider = NewSimulated();
        var (body, _) = provider.BuildCallback("SIM-1", "TRX-1", PaymentStatus.Completed, 25000m);

        var callback = provider.ReadCallback(new PaymentCallbackContext(
            body, new Dictionary<string, string> { ["x-fastride-signature"] = "deadbeef" }, Secret));

        Assert.Null(callback);
    }

    [Fact]
    public void Simulated_RejectsACallbackWithNoSignatureAtAll()
    {
        var provider = NewSimulated();
        var (body, _) = provider.BuildCallback("SIM-1", "TRX-1", PaymentStatus.Completed, 25000m);

        Assert.Null(provider.ReadCallback(
            new PaymentCallbackContext(body, new Dictionary<string, string>(), Secret)));
    }

    [Fact]
    public void Simulated_RejectsACallbackSignedWithTheWrongSecret()
    {
        var provider = NewSimulated();
        var (body, signature) = provider.BuildCallback("SIM-1", "TRX-1", PaymentStatus.Completed, 25000m);

        var callback = provider.ReadCallback(new PaymentCallbackContext(
            body, new Dictionary<string, string> { ["x-fastride-signature"] = signature }, "secret-yang-salah"));

        Assert.Null(callback);
    }

    [Fact]
    public void Simulated_RejectsACallbackWhoseBodyWasAlteredAfterSigning()
    {
        var provider = NewSimulated();
        var (body, signature) = provider.BuildCallback("SIM-1", "TRX-1", PaymentStatus.Completed, 25000m);

        // Raise the amount but keep the original signature — a classic tamper attempt.
        var tampered = body.Replace("25000", "999999", StringComparison.Ordinal);

        var callback = provider.ReadCallback(new PaymentCallbackContext(
            tampered, new Dictionary<string, string> { ["x-fastride-signature"] = signature }, Secret));

        Assert.Null(callback);
    }

    [Fact]
    public void Simulated_RejectsACallbackThatIsNotJson()
    {
        const string body = "bukan json";
        var signature = SimulatedPaymentProvider.Sign(body, Secret);

        var callback = NewSimulated().ReadCallback(new PaymentCallbackContext(
            body, new Dictionary<string, string> { ["x-fastride-signature"] = signature }, Secret));

        Assert.Null(callback);
    }

    // ────────────────────────── midtrans ──────────────────────────

    [Fact]
    public void Midtrans_AcceptsACallbackSignedWithTheServerKey()
    {
        // Midtrans signs order_id + status_code + gross_amount + serverKey with SHA-512.
        const string orderId = "TRX-20260804-MID001";
        const string statusCode = "200";
        const string grossAmount = "25000.00";
        const string serverKey = "SB-Mid-server-uji";

        var signature = Convert.ToHexStringLower(
            System.Security.Cryptography.SHA512.HashData(
                System.Text.Encoding.UTF8.GetBytes(orderId + statusCode + grossAmount + serverKey)));

        var body = JsonSerializer.Serialize(new Dictionary<string, string>
        {
            ["order_id"] = orderId,
            ["status_code"] = statusCode,
            ["gross_amount"] = grossAmount,
            ["signature_key"] = signature,
            ["transaction_status"] = "settlement",
            ["transaction_id"] = "mid-tx-1"
        });

        var callback = NewMidtrans().ReadCallback(
            new PaymentCallbackContext(body, new Dictionary<string, string>(), serverKey));

        Assert.NotNull(callback);
        Assert.Equal(orderId, callback!.Reference);
        Assert.Equal(PaymentStatus.Completed, callback.Status);
        Assert.Equal(25000m, callback.Amount);
    }

    [Fact]
    public void Midtrans_RejectsACallbackWithAForgedSignature()
    {
        var body = JsonSerializer.Serialize(new Dictionary<string, string>
        {
            ["order_id"] = "TRX-1",
            ["status_code"] = "200",
            ["gross_amount"] = "25000.00",
            ["signature_key"] = new string('a', 128),
            ["transaction_status"] = "settlement"
        });

        Assert.Null(NewMidtrans().ReadCallback(
            new PaymentCallbackContext(body, new Dictionary<string, string>(), "SB-Mid-server-uji")));
    }

    [Fact]
    public void Midtrans_RejectsACallbackMissingItsSignature()
    {
        var body = JsonSerializer.Serialize(new Dictionary<string, string>
        {
            ["order_id"] = "TRX-1",
            ["status_code"] = "200",
            ["gross_amount"] = "25000.00"
        });

        Assert.Null(NewMidtrans().ReadCallback(
            new PaymentCallbackContext(body, new Dictionary<string, string>(), "SB-Mid-server-uji")));
    }

    [Fact]
    public void Midtrans_TreatsAFraudChallengeAsUnsettled()
    {
        // A captured card under fraud review is not money in hand.
        const string orderId = "TRX-CH";
        const string statusCode = "201";
        const string grossAmount = "25000.00";
        const string serverKey = "SB-Mid-server-uji";

        var signature = Convert.ToHexStringLower(
            System.Security.Cryptography.SHA512.HashData(
                System.Text.Encoding.UTF8.GetBytes(orderId + statusCode + grossAmount + serverKey)));

        var body = JsonSerializer.Serialize(new Dictionary<string, string>
        {
            ["order_id"] = orderId,
            ["status_code"] = statusCode,
            ["gross_amount"] = grossAmount,
            ["signature_key"] = signature,
            ["transaction_status"] = "capture",
            ["fraud_status"] = "challenge"
        });

        var callback = NewMidtrans().ReadCallback(
            new PaymentCallbackContext(body, new Dictionary<string, string>(), serverKey));

        Assert.NotNull(callback);
        Assert.Equal(PaymentStatus.AwaitingPayment, callback!.Status);
    }

    private static MidtransPaymentProvider NewMidtrans() => new(
        new PaymentProviderConfig { Name = "midtrans", ServerKey = "SB-Mid-server-uji", IsSandbox = true },
        new StubHttpClientFactory(),
        NullLogger<MidtransPaymentProvider>.Instance);

    // ─────────────────────────── xendit ───────────────────────────

    [Fact]
    public void Xendit_AcceptsACallbackCarryingTheRightToken()
    {
        const string token = "token-callback-uji";

        var body = JsonSerializer.Serialize(new
        {
            data = new { reference_id = "TRX-XEN-1", status = "SUCCEEDED", amount = 25000, id = "qr-1" }
        });

        var callback = NewXendit().ReadCallback(new PaymentCallbackContext(
            body, new Dictionary<string, string> { ["x-callback-token"] = token }, token));

        Assert.NotNull(callback);
        Assert.Equal("TRX-XEN-1", callback!.Reference);
        Assert.Equal(PaymentStatus.Completed, callback.Status);
    }

    [Fact]
    public void Xendit_RejectsACallbackWithTheWrongToken()
    {
        var body = JsonSerializer.Serialize(new { data = new { reference_id = "TRX-1", status = "SUCCEEDED" } });

        Assert.Null(NewXendit().ReadCallback(new PaymentCallbackContext(
            body, new Dictionary<string, string> { ["x-callback-token"] = "token-salah" }, "token-benar")));
    }

    [Fact]
    public void Xendit_RefusesToVerifyWhenNoTokenIsConfigured()
    {
        // An empty configured token must not accidentally match an empty supplied one.
        var body = JsonSerializer.Serialize(new { data = new { reference_id = "TRX-1", status = "SUCCEEDED" } });

        Assert.Null(NewXendit().ReadCallback(new PaymentCallbackContext(
            body, new Dictionary<string, string> { ["x-callback-token"] = "" }, "")));
    }

    private static XenditPaymentProvider NewXendit() => new(
        new PaymentProviderConfig { Name = "xendit", ServerKey = "xnd_uji", IsSandbox = true },
        new StubHttpClientFactory(),
        NullLogger<XenditPaymentProvider>.Instance);

    // ─────────────────────── config routing ───────────────────────

    [Theory]
    [InlineData("Qris,EWallet", PaymentMethod.Qris, true)]
    [InlineData("Qris,EWallet", PaymentMethod.CreditCard, false)]
    [InlineData("qris, ewallet", PaymentMethod.EWallet, true)]
    [InlineData("", PaymentMethod.Qris, false)]
    public void Handles_ReadsTheConfiguredMethodList(string configured, PaymentMethod method, bool expected) =>
        Assert.Equal(expected, new PaymentProviderConfig { SupportedMethods = configured }.Handles(method));

    [Fact]
    public void ParseMethods_IgnoresRubbishEntries()
    {
        var methods = new PaymentProviderConfig { SupportedMethods = "Qris,BukanMetode,EWallet" }.ParseMethods();

        Assert.Equal([PaymentMethod.EWallet, PaymentMethod.Qris], methods.OrderBy(m => m.ToString()).ToList());
    }

    [Fact]
    public void ParseMethods_DropsDuplicates() =>
        Assert.Single(new PaymentProviderConfig { SupportedMethods = "Qris,Qris,qris" }.ParseMethods());

    private sealed class StubHttpClientFactory : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new();
    }
}

/// <summary>Moves time on demand, so expiry can be tested without sleeping.</summary>
internal sealed class FakeTimeProvider(DateTime start) : TimeProvider
{
    private DateTimeOffset _now = new(start, TimeSpan.Zero);

    public override DateTimeOffset GetUtcNow() => _now;

    public void Advance(TimeSpan by) => _now = _now.Add(by);
}
