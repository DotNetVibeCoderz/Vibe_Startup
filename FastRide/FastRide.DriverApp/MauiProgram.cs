using FastRide.DriverApp.Services;
using Microsoft.Extensions.Logging;

namespace FastRide.DriverApp;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder.UseMauiApp<App>()
               .ConfigureFonts(fonts => fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular"));

        builder.Services.AddMauiBlazorWebView();

        // Both are singletons: the signed-in driver has to survive navigation between screens.
        builder.Services.AddSingleton(_ => new HttpClient(CreateHandler())
        {
            BaseAddress = new Uri(ApiEndpoint.BaseUrl),
            Timeout = TimeSpan.FromSeconds(30)
        });
        builder.Services.AddSingleton<ApiClient>();

#if DEBUG
        builder.Services.AddBlazorWebViewDeveloperTools();
#endif

        return builder.Build();
    }

    private static HttpMessageHandler CreateHandler()
    {
        var handler = new HttpClientHandler();

#if DEBUG
        // The API runs behind the ASP.NET development certificate, which no emulator trusts.
        // Debug builds only — a release build must talk to a properly certified endpoint.
        handler.ServerCertificateCustomValidationCallback =
            HttpClientHandler.DangerousAcceptAnyServerCertificateValidator;
#endif

        return handler;
    }
}

/// <summary>Where the API lives, per platform. On Android the host machine is 10.0.2.2.</summary>
public static class ApiEndpoint
{
    public static string BaseUrl =>
#if ANDROID
        "https://10.0.2.2:5001";
#else
        "https://localhost:5001";
#endif
}
