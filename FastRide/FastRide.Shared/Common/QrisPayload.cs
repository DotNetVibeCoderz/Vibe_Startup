using System.Globalization;
using System.Text;

namespace FastRide.Shared.Common;

/// <summary>
/// Builds and reads QRIS payloads.
///
/// QRIS is Indonesia's unified QR standard, built on the EMVCo merchant-presented QR spec:
/// a flat sequence of tag-length-value triples, closed by a CRC-16/CCITT-FALSE checksum over
/// everything before it. Any bank or wallet app can read one.
///
/// Real providers hand back the payload string themselves — this builder exists so the
/// simulated provider produces something structurally genuine that a scanner will parse and
/// checksum-validate, rather than a placeholder that only looks like a QR string.
/// </summary>
public static class QrisPayload
{
    // Tags used here. The spec defines many more; these are what a dynamic merchant QR needs.
    private const string TagFormatIndicator = "00";
    private const string TagPointOfInitiation = "01";
    private const string TagMerchantAccount = "26";   // domestic merchant account (National Merchant ID)
    private const string TagMerchantCategory = "52";
    private const string TagCurrency = "53";
    private const string TagAmount = "54";
    private const string TagCountry = "58";
    private const string TagMerchantName = "59";
    private const string TagMerchantCity = "60";
    private const string TagAdditionalData = "62";
    private const string TagCrc = "63";

    private const string CurrencyIdr = "360";          // ISO 4217 numeric for the rupiah
    private const string CountryId = "ID";
    private const string CategoryTransport = "4121";   // ISO 18245: taxi and limousine services

    /// <summary>"12" marks a dynamic QR: one payload per transaction, amount baked in.</summary>
    private const string DynamicQr = "12";

    /// <summary>
    /// Compose a dynamic merchant QR for a single charge.
    /// </summary>
    /// <param name="merchantId">National Merchant ID issued by the acquirer.</param>
    /// <param name="merchantName">Trading name, truncated to the 25 characters the spec allows.</param>
    /// <param name="merchantCity">City, truncated to 15 characters.</param>
    /// <param name="amount">Charge in rupiah. QRIS carries it as a decimal string.</param>
    /// <param name="reference">Our transaction reference, echoed back by the acquirer.</param>
    public static string Build(
        string merchantId,
        string merchantName,
        string merchantCity,
        decimal amount,
        string reference)
    {
        if (amount <= 0) throw new ArgumentOutOfRangeException(nameof(amount), "A QRIS charge must be positive.");

        var builder = new StringBuilder();

        Append(builder, TagFormatIndicator, "01");
        Append(builder, TagPointOfInitiation, DynamicQr);

        // The merchant account template nests its own TLVs: the acquirer's reverse-domain
        // identifier, the merchant id, and the merchant criteria.
        var merchantAccount = new StringBuilder();
        Append(merchantAccount, "00", "ID.CO.QRIS.WWW");
        Append(merchantAccount, "01", Clamp(merchantId, 15));
        Append(merchantAccount, "02", "UMI");           // micro merchant category
        Append(builder, TagMerchantAccount, merchantAccount.ToString());

        Append(builder, TagMerchantCategory, CategoryTransport);
        Append(builder, TagCurrency, CurrencyIdr);
        Append(builder, TagAmount, amount.ToString("0.##", CultureInfo.InvariantCulture));
        Append(builder, TagCountry, CountryId);
        Append(builder, TagMerchantName, Clamp(Sanitise(merchantName), 25));
        Append(builder, TagMerchantCity, Clamp(Sanitise(merchantCity), 15));

        var additional = new StringBuilder();
        Append(additional, "05", Clamp(Sanitise(reference), 25));   // bill/reference number
        Append(builder, TagAdditionalData, additional.ToString());

        // The CRC covers the tag and length of the CRC field itself, so they are appended
        // before the checksum is computed.
        builder.Append(TagCrc).Append("04");
        builder.Append(Crc16(builder.ToString()));

        return builder.ToString();
    }

    /// <summary>Verify the trailing checksum. A payload that fails this will not scan.</summary>
    public static bool IsValid(string payload)
    {
        if (string.IsNullOrWhiteSpace(payload) || payload.Length < 8) return false;

        var checksumStart = payload.Length - 4;
        var body = payload[..checksumStart];

        // The four characters before the checksum must be the CRC tag and its length.
        if (!body.EndsWith(TagCrc + "04", StringComparison.Ordinal)) return false;

        var declared = payload[checksumStart..];

        return string.Equals(declared, Crc16(body), StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Pull the amount back out of a payload, for reconciliation.</summary>
    public static decimal? ReadAmount(string payload)
    {
        var raw = ReadTag(payload, TagAmount);

        return decimal.TryParse(raw, NumberStyles.Number, CultureInfo.InvariantCulture, out var amount)
            ? amount
            : null;
    }

    /// <summary>Read one top-level tag. Returns null when the tag is absent or malformed.</summary>
    public static string? ReadTag(string payload, string tag)
    {
        if (string.IsNullOrWhiteSpace(payload)) return null;

        var index = 0;

        while (index + 4 <= payload.Length)
        {
            var currentTag = payload.Substring(index, 2);

            if (!int.TryParse(payload.AsSpan(index + 2, 2), NumberStyles.None, CultureInfo.InvariantCulture, out var length))
                return null;

            var valueStart = index + 4;
            if (valueStart + length > payload.Length) return null;

            if (currentTag == tag) return payload.Substring(valueStart, length);

            index = valueStart + length;
        }

        return null;
    }

    private static void Append(StringBuilder builder, string tag, string value) =>
        builder.Append(tag).Append(value.Length.ToString("D2", CultureInfo.InvariantCulture)).Append(value);

    private static string Clamp(string value, int maxLength) =>
        value.Length <= maxLength ? value : value[..maxLength];

    /// <summary>The spec allows printable ASCII only; anything else would break a scanner.</summary>
    private static string Sanitise(string value)
    {
        var cleaned = new string(value.Where(c => c is >= ' ' and <= '~').ToArray()).Trim();

        return cleaned.Length == 0 ? "FASTRIDE" : cleaned.ToUpperInvariant();
    }

    /// <summary>CRC-16/CCITT-FALSE: polynomial 0x1021, seed 0xFFFF, no reflection.</summary>
    internal static string Crc16(string input)
    {
        const ushort polynomial = 0x1021;
        ushort crc = 0xFFFF;

        foreach (var value in Encoding.ASCII.GetBytes(input))
        {
            crc ^= (ushort)(value << 8);

            for (var bit = 0; bit < 8; bit++)
                crc = (crc & 0x8000) != 0 ? (ushort)((crc << 1) ^ polynomial) : (ushort)(crc << 1);
        }

        return crc.ToString("X4", CultureInfo.InvariantCulture);
    }
}
