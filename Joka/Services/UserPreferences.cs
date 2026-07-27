// Per-visitor language and currency, kept in a cookie.
//
// A cookie rather than localStorage because the choice has to be readable
// during static SSR and by the localisation middleware, both of which run
// before any JavaScript.
using Microsoft.AspNetCore.Http;

namespace Joka.Services;

public class UserPreferences
{
    public const string CurrencyCookie = "joka-currency";
    public const string CultureCookie = ".AspNetCore.Culture";

    private readonly IHttpContextAccessor _accessor;
    private readonly CurrencyService _currency;

    public UserPreferences(IHttpContextAccessor accessor, CurrencyService currency)
    {
        _accessor = accessor;
        _currency = currency;
    }

    public string CurrencyCode
    {
        get
        {
            var fromCookie = _accessor.HttpContext?.Request.Cookies[CurrencyCookie];
            return _currency.Resolve(fromCookie).Code;
        }
    }

    public string Format(decimal amountInIdr) => _currency.Format(amountInIdr, CurrencyCode);

    public string? OriginalHint(decimal amountInIdr) => _currency.OriginalHint(amountInIdr, CurrencyCode);
}
