using QRCoder;

namespace Ngibrid.Services;

/// <summary>
/// Generates the QR and Code128 barcodes printed on shipping labels.
/// Both return data URIs so a Blazor page can bind them straight to an img src without a round trip.
/// </summary>
public class BarcodeService
{
    /// <summary>QR code as a PNG data URI.</summary>
    public string GenerateQrDataUri(string payload, int pixelsPerModule = 6)
    {
        using var generator = new QRCodeGenerator();
        using var data = generator.CreateQrCode(payload, QRCodeGenerator.ECCLevel.Q);
        using var qr = new PngByteQRCode(data);
        var bytes = qr.GetGraphic(pixelsPerModule);
        return $"data:image/png;base64,{Convert.ToBase64String(bytes)}";
    }

    /// <summary>QR code as raw PNG bytes, for the REST endpoint.</summary>
    public byte[] GenerateQrPng(string payload, int pixelsPerModule = 6)
    {
        using var generator = new QRCodeGenerator();
        using var data = generator.CreateQrCode(payload, QRCodeGenerator.ECCLevel.Q);
        using var qr = new PngByteQRCode(data);
        return qr.GetGraphic(pixelsPerModule);
    }

    /// <summary>
    /// Code128-B barcode rendered as an SVG data URI. Scanners need the full frame —
    /// start code, encoded data, modulo-103 checksum, stop pattern, and quiet zones.
    /// </summary>
    public string GenerateBarcodeDataUri(string payload, int height = 60, int moduleWidth = 2)
    {
        var svg = GenerateBarcodeSvg(payload, height, moduleWidth);
        var encoded = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(svg));
        return $"data:image/svg+xml;base64,{encoded}";
    }

    public string GenerateBarcodeSvg(string payload, int height = 60, int moduleWidth = 2)
    {
        var pattern = EncodeCode128B(payload);
        const int quietZone = 10; // modules
        var totalModules = pattern.Length + quietZone * 2;
        var width = totalModules * moduleWidth;

        var sb = new System.Text.StringBuilder();
        sb.Append($"<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"{width}\" height=\"{height}\" ")
          .Append($"viewBox=\"0 0 {width} {height}\" shape-rendering=\"crispEdges\">");
        sb.Append($"<rect width=\"{width}\" height=\"{height}\" fill=\"#ffffff\"/>");

        var x = quietZone * moduleWidth;
        foreach (var bit in pattern)
        {
            if (bit == '1')
                sb.Append($"<rect x=\"{x}\" y=\"0\" width=\"{moduleWidth}\" height=\"{height}\" fill=\"#000000\"/>");
            x += moduleWidth;
        }

        sb.Append("</svg>");
        return sb.ToString();
    }

    /// <summary>
    /// Code128 code set B bit patterns, indexed by symbol value 0-106.
    /// </summary>
    private static readonly string[] Code128Patterns =
    {
        "11011001100","11001101100","11001100110","10010011000","10010001100","10001001100","10011001000","10011000100",
        "10001100100","11001001000","11001000100","11000100100","10110011100","10011011100","10011001110","10111001100",
        "10011101100","10011100110","11001110010","11001011100","11001001110","11011100100","11001110100","11101101110",
        "11101001100","11100101100","11100100110","11101100100","11100110100","11100110010","11011011000","11011000110",
        "11000110110","10100011000","10001011000","10001000110","10110001000","10001101000","10001100010","11010001000",
        "11000101000","11000100010","10110111000","10110001110","10001101110","10111011000","10111000110","10001110110",
        "11101110110","11010001110","11000101110","11011101000","11011100010","11011101110","11101011000","11101000110",
        "11100010110","11101101000","11101100010","11100011010","11101111010","11001000010","11110001010","10100110000",
        "10100001100","10010110000","10010000110","10000101100","10000100110","10110010000","10110000100","10011010000",
        "10011000010","10000110100","10000110010","11000010010","11001010000","11110111010","11000010100","10001111010",
        "10100111100","10010111100","10010011110","10111100100","10011110100","10011110010","11110100100","11110010100",
        "11110010010","11011011110","11011110110","11110110110","10101111000","10100011110","10001011110","10111101000",
        "10111100010","11110101000","11110100010","10111011110","10111101110","11101011110","11110101110","11010000100",
        "11010010000","11010011100","1100011101011"
    };

    private const int StartB = 104;
    private const int Stop = 106;

    private static string EncodeCode128B(string payload)
    {
        // Code set B covers ASCII 32-126; anything outside is replaced so a stray character
        // produces a scannable label instead of an exception.
        var clean = new string(payload.Select(c => c is >= ' ' and <= '~' ? c : '?').ToArray());

        var values = new List<int> { StartB };
        foreach (var c in clean)
            values.Add(c - 32);

        var checksum = StartB;
        for (var i = 1; i < values.Count; i++)
            checksum += values[i] * i;
        values.Add(checksum % 103);
        values.Add(Stop);

        return string.Concat(values.Select(v => Code128Patterns[v]));
    }
}
