// Currency conversion and formatting.
//
// All prices are stored in IDR. This converts at display time only - nothing
// in the database changes, so a rate update never rewrites history.
using System.Globalization;

namespace Joka.Services;

public record CurrencyInfo(string Code, string Symbol, decimal PerIdr, int Decimals, string Culture);

public class CurrencyService
{
    private readonly IConfiguration _config;

    public CurrencyService(IConfiguration config) => _config = config;

    /// <summary>
    /// Rate = how many IDR one unit of the currency is worth. Overridable from
    /// appsettings (Currency:Rates:USD) and therefore from the Settings page.
    /// </summary>
    private static readonly CurrencyInfo[] Defaults =
    {
        new("IDR", "Rp", 1m, 0, "id-ID"),
        new("USD", "$", 16250m, 2, "en-US"),
        new("SGD", "S$", 12100m, 2, "en-SG"),
        new("MYR", "RM", 3650m, 2, "ms-MY"),
        new("EUR", "€", 17600m, 2, "de-DE"),
        new("AUD", "A$", 10700m, 2, "en-AU"),
        new("JPY", "¥", 108m, 0, "ja-JP"),
        new("GBP", "£", 20500m, 2, "en-GB")
    };

    public IReadOnlyList<CurrencyInfo> Supported
    {
        get
        {
            var allowed = _config.GetSection("AppSettings:SupportedCurrencies").Get<string[]>();

            var list = allowed is { Length: > 0 }
                ? Defaults.Where(d => allowed.Contains(d.Code, StringComparer.OrdinalIgnoreCase)).ToList()
                : Defaults.ToList();

            // Apply any rate overrides without losing the rest of the definition.
            return list.Select(c =>
            {
                var raw = _config[$"Currency:Rates:{c.Code}"];
                return decimal.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out var rate) && rate > 0
                    ? c with { PerIdr = rate }
                    : c;
            }).ToList();
        }
    }

    public string DefaultCode => _config["AppSettings:DefaultCurrency"] ?? "IDR";

    public CurrencyInfo Resolve(string? code) =>
        Supported.FirstOrDefault(c => c.Code.Equals(code, StringComparison.OrdinalIgnoreCase))
        ?? Supported.FirstOrDefault(c => c.Code.Equals(DefaultCode, StringComparison.OrdinalIgnoreCase))
        ?? Defaults[0];

    public decimal Convert(decimal amountInIdr, string? toCode)
    {
        var target = Resolve(toCode);
        return target.PerIdr <= 0 ? amountInIdr : amountInIdr / target.PerIdr;
    }

    /// <summary>Formats an IDR amount in the requested currency.</summary>
    public string Format(decimal amountInIdr, string? toCode)
    {
        var target = Resolve(toCode);
        var value = Convert(amountInIdr, target.Code);

        // Indonesian grouping for IDR, the target locale's for the rest.
        var culture = CultureInfo.GetCultureInfo(target.Culture);
        var text = value.ToString($"N{target.Decimals}", culture);

        return target.Code == "IDR" ? $"{target.Symbol}{text}" : $"{target.Symbol}{text}";
    }

    /// <summary>Shown next to a converted price so the source amount stays visible.</summary>
    public string? OriginalHint(decimal amountInIdr, string? toCode) =>
        Resolve(toCode).Code == "IDR" ? null : $"Rp{amountInIdr.ToString("N0", CultureInfo.GetCultureInfo("id-ID"))}";
}
