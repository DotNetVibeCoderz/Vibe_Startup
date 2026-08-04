using QRCoder;

namespace FastRide.Api.Payments;

/// <summary>
/// Turns a QRIS payload into a scannable SVG.
///
/// Rendering happens here rather than in the apps so both mobile clients get an identical
/// code without carrying a graphics dependency, and so the payload itself never has to be
/// re-encoded on a device.
/// </summary>
public static class QrCodeRenderer
{
    /// <summary>
    /// SVG for a payload, as a data URI ready to drop into an <c>img</c> tag.
    /// </summary>
    /// <param name="payload">The QRIS string.</param>
    /// <param name="darkHex">Module colour. Defaults to near-black for maximum contrast.</param>
    public static string ToDataUri(string payload, string darkHex = "#0E1524")
    {
        var svg = ToSvg(payload, darkHex);

        return "data:image/svg+xml;base64," + Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(svg));
    }

    public static string ToSvg(string payload, string darkHex = "#0E1524")
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(payload);

        using var generator = new QRCodeGenerator();

        // Level M tolerates roughly 15% damage, which is what QRIS specifies and what a
        // phone screen photographed at an angle actually needs.
        using var data = generator.CreateQrCode(payload, QRCodeGenerator.ECCLevel.M);

        var svg = new SvgQRCode(data);

        // White quiet zone included: a QR printed edge-to-edge on a dark card will not scan.
        return svg.GetGraphic(
            pixelsPerModule: 8,
            darkColorHex: darkHex,
            lightColorHex: "#FFFFFF",
            drawQuietZones: true);
    }
}
