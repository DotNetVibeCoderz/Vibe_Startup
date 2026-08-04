using FastRide.Shared.Common;

namespace FastRide.Tests.Unit;

/// <summary>
/// QRIS payloads are EMVCo tag-length-value strings closed by a CRC-16/CCITT-FALSE checksum.
/// A payload that fails its own checksum will not scan, so the format is worth pinning down.
/// </summary>
public class QrisPayloadTests
{
    private const string MerchantId = "ID1234567890123";
    private const string MerchantName = "FASTRIDE";
    private const string MerchantCity = "JAKARTA";

    private static string Build(decimal amount = 25000m, string reference = "TRX-20260804-ABC123") =>
        QrisPayload.Build(MerchantId, MerchantName, MerchantCity, amount, reference);

    [Fact]
    public void Build_ProducesAPayloadThatPassesItsOwnChecksum() =>
        Assert.True(QrisPayload.IsValid(Build()));

    [Fact]
    public void Build_StartsWithTheFormatIndicator()
    {
        // Tag 00, length 02, value 01 — every EMVCo QR opens this way.
        Assert.StartsWith("000201", Build());
    }

    [Fact]
    public void Build_MarksThePayloadDynamic()
    {
        // Tag 01 = 12 means one QR per transaction, with the amount baked in. A static QR
        // (11) would let a rider pay any amount they liked.
        Assert.Equal("12", QrisPayload.ReadTag(Build(), "01"));
    }

    [Fact]
    public void Build_CarriesTheAmountInRupiah()
    {
        var payload = Build(amount: 25460m);

        Assert.Equal("360", QrisPayload.ReadTag(payload, "53"));   // ISO 4217 for IDR
        Assert.Equal(25460m, QrisPayload.ReadAmount(payload));
    }

    [Fact]
    public void Build_KeepsFractionalAmountsIntact() =>
        Assert.Equal(1500.5m, QrisPayload.ReadAmount(Build(amount: 1500.5m)));

    [Fact]
    public void Build_TagsTheCountryAndMerchant()
    {
        var payload = Build();

        Assert.Equal("ID", QrisPayload.ReadTag(payload, "58"));
        Assert.Equal(MerchantName, QrisPayload.ReadTag(payload, "59"));
        Assert.Equal(MerchantCity, QrisPayload.ReadTag(payload, "60"));
    }

    [Fact]
    public void Build_NestsTheAcquirerIdentifierInsideTheMerchantAccount()
    {
        var account = QrisPayload.ReadTag(Build(), "26");

        Assert.NotNull(account);
        Assert.Equal("ID.CO.QRIS.WWW", QrisPayload.ReadTag(account!, "00"));
        Assert.Equal(MerchantId, QrisPayload.ReadTag(account!, "01"));
    }

    [Fact]
    public void Build_CarriesOurReferenceSoTheAcquirerCanEchoItBack()
    {
        const string reference = "TRX-20260804-ZZZ999";
        var additional = QrisPayload.ReadTag(Build(reference: reference), "62");

        Assert.NotNull(additional);
        Assert.Equal(reference, QrisPayload.ReadTag(additional!, "05"));
    }

    [Fact]
    public void Build_UppercasesAndStripsUnscannableCharacters()
    {
        // The spec allows printable ASCII only; a name with an em dash would break a scanner.
        var payload = QrisPayload.Build(MerchantId, "Fast—Ride Café", "Jakarta", 10000m, "TRX-1");

        var name = QrisPayload.ReadTag(payload, "59");

        Assert.NotNull(name);
        Assert.All(name!, character => Assert.InRange(character, ' ', '~'));
        Assert.Equal(name, name!.ToUpperInvariant());
    }

    [Fact]
    public void Build_TruncatesAMerchantNameToTheSpecLimit()
    {
        var payload = QrisPayload.Build(
            MerchantId, new string('A', 60), MerchantCity, 10000m, "TRX-1");

        Assert.Equal(25, QrisPayload.ReadTag(payload, "59")!.Length);
        Assert.True(QrisPayload.IsValid(payload));
    }

    [Fact]
    public void Build_RejectsANonPositiveAmount()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => Build(amount: 0m));
        Assert.Throws<ArgumentOutOfRangeException>(() => Build(amount: -5000m));
    }

    [Fact]
    public void IsValid_RejectsAPayloadWhoseContentWasAltered()
    {
        var payload = Build(amount: 25000m);

        // Change one digit of the amount without recomputing the checksum — exactly what a
        // tampered QR would look like.
        var tampered = payload.Replace("540525000", "540599000", StringComparison.Ordinal);

        Assert.NotEqual(payload, tampered);
        Assert.False(QrisPayload.IsValid(tampered));
    }

    [Fact]
    public void IsValid_RejectsATruncatedPayload() =>
        Assert.False(QrisPayload.IsValid(Build()[..^6]));

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("nonsense")]
    public void IsValid_RejectsRubbish(string payload) =>
        Assert.False(QrisPayload.IsValid(payload));

    [Fact]
    public void Crc16_MatchesTheKnownCcittFalseVector()
    {
        // "123456789" → 0x29B1 is the standard check value for CRC-16/CCITT-FALSE.
        Assert.Equal("29B1", QrisPayload.Crc16("123456789"));
    }

    [Fact]
    public void ReadTag_ReturnsNullForATagThatIsNotThere() =>
        Assert.Null(QrisPayload.ReadTag(Build(), "99"));

    [Fact]
    public void ReadTag_DoesNotWalkOffTheEndOfAMalformedPayload()
    {
        // Declares a 99-character value that is not present.
        Assert.Null(QrisPayload.ReadTag("0099ab", "00"));
    }

    [Fact]
    public void EveryAmountRoundTripsThroughAValidPayload()
    {
        foreach (var amount in new[] { 1m, 7000m, 25460m, 999_999m })
        {
            var payload = Build(amount);

            Assert.True(QrisPayload.IsValid(payload), $"Payload for {amount} failed its checksum.");
            Assert.Equal(amount, QrisPayload.ReadAmount(payload));
        }
    }
}
