using FastRide.Shared.Common;
using FastRide.Shared.Models;

namespace FastRide.Tests.Unit;

/// <summary>
/// The status→signal-colour mapping is shared by the admin console and both mobile apps, so
/// a status cannot mean one thing on the dashboard and another on a phone.
/// </summary>
public class DisplayTests
{
    [Theory]
    [InlineData(OrderStatus.Requested, "pill--wait")]
    [InlineData(OrderStatus.Accepted, "pill--move")]
    [InlineData(OrderStatus.DriverArrived, "pill--move")]
    [InlineData(OrderStatus.Started, "pill--move")]
    [InlineData(OrderStatus.Completed, "pill--go")]
    [InlineData(OrderStatus.Cancelled, "pill--stop")]
    [InlineData(OrderStatus.Expired, "pill--stop")]
    public void PillClass_MapsOrderStatusToItsSignalColour(OrderStatus status, string expected) =>
        Assert.Contains(expected, Display.PillClass(status));

    [Theory]
    [InlineData(DriverStatus.Online, "pill--go")]
    [InlineData(DriverStatus.OnTrip, "pill--wait")]
    [InlineData(DriverStatus.Break, "pill--move")]
    [InlineData(DriverStatus.Offline, "pill--idle")]
    public void PillClass_MapsDriverStatusToItsSignalColour(DriverStatus status, string expected) =>
        Assert.Contains(expected, Display.PillClass(status));

    [Fact]
    public void EveryOrderStatus_HasAnIndonesianLabel()
    {
        foreach (var status in Enum.GetValues<OrderStatus>())
        {
            var label = Display.Label(status);

            Assert.False(string.IsNullOrWhiteSpace(label));
            Assert.NotEqual(status.ToString(), label);
        }
    }

    [Fact]
    public void EveryPaymentMethod_HasAnIndonesianLabel()
    {
        foreach (var method in Enum.GetValues<PaymentMethod>())
            Assert.False(string.IsNullOrWhiteSpace(Display.Label(method)));
    }

    [Fact]
    public void EveryVehicleCategory_HasALabelAndAnIcon()
    {
        foreach (var category in Enum.GetValues<VehicleCategory>())
        {
            Assert.False(string.IsNullOrWhiteSpace(Display.Label(category)));
            Assert.StartsWith("bi ", Display.Icon(category));
        }
    }

    [Theory]
    [InlineData(OrderStatus.Requested, 0)]
    [InlineData(OrderStatus.Accepted, 1)]
    [InlineData(OrderStatus.DriverArrived, 2)]
    [InlineData(OrderStatus.Started, 3)]
    [InlineData(OrderStatus.Completed, 4)]
    public void TripStep_AdvancesAlongTheProgressRail(OrderStatus status, int expected) =>
        Assert.Equal(expected, Display.TripStep(status));

    [Fact]
    public void Rupiah_GroupsThousands()
    {
        // The separator character depends on whether ICU data is available, so assert on the
        // grouping itself rather than on a specific glyph.
        var text = Display.Rupiah(1_250_000m);

        Assert.StartsWith("Rp ", text);
        Assert.DoesNotContain("1250000", text);
    }

    [Theory]
    [InlineData(750, "Rp")]
    [InlineData(12_500, "rb")]
    [InlineData(3_400_000, "jt")]
    [InlineData(2_100_000_000, "M")]
    public void RupiahShort_PicksTheRightMagnitude(decimal amount, string expectedSuffix) =>
        Assert.Contains(expectedSuffix, Display.RupiahShort(amount));

    [Fact]
    public void Avatar_KeepsAnExistingPhoto()
    {
        const string url = "/uploads/photos/abc.jpg";

        Assert.Equal(url, Display.Avatar(url, "Budi Santoso"));
    }

    [Fact]
    public void Avatar_FallsBackToInitials()
    {
        var avatar = Display.Avatar(null, "Budi Santoso");

        Assert.StartsWith("data:image/svg+xml;base64,", avatar);

        var svg = System.Text.Encoding.UTF8.GetString(
            Convert.FromBase64String(avatar["data:image/svg+xml;base64,".Length..]));

        Assert.Contains(">BS<", svg);
    }

    [Fact]
    public void Avatar_HandlesANameWithoutSpaces()
    {
        var avatar = Display.Avatar(null, "Budi");

        Assert.StartsWith("data:image/svg+xml;base64,", avatar);
    }

    [Fact]
    public void Avatar_HandlesAnEmptyName()
    {
        var avatar = Display.Avatar(null, "   ");
        var svg = System.Text.Encoding.UTF8.GetString(
            Convert.FromBase64String(avatar["data:image/svg+xml;base64,".Length..]));

        Assert.Contains(">FR<", svg);
    }

    [Fact]
    public void Since_DescribesRecentTimesInSeconds() =>
        Assert.Contains("dtk", Display.Since(DateTime.UtcNow.AddSeconds(-10)));

    [Fact]
    public void Since_DescribesOlderTimesInMinutesAndHours()
    {
        Assert.Contains("mnt", Display.Since(DateTime.UtcNow.AddMinutes(-15)));
        Assert.Contains("jam", Display.Since(DateTime.UtcNow.AddHours(-5)));
        Assert.Contains("hari", Display.Since(DateTime.UtcNow.AddDays(-3)));
    }

    [Fact]
    public void ChartColour_UsesTheSamePaletteTokensAsTheCss()
    {
        string[] tokens = ["lampu", "jalan", "sirene", "lintas"];

        foreach (var status in Enum.GetValues<OrderStatus>())
            Assert.Contains(Display.ChartColour(status), tokens);
    }
}
