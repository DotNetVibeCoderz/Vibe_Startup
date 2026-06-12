using FastRide.RiderApp.Services;
using Microsoft.Extensions.Logging;

namespace FastRide.RiderApp;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder.UseMauiApp<App>()
               .ConfigureFonts(f => f.AddFont("OpenSans-Regular.ttf", "OpenSansRegular"));

        builder.Services.AddMauiBlazorWebView();
        builder.Services.AddHttpClient<ApiClient>(client =>
        {
            client.BaseAddress = new Uri("https://localhost:5001");
            client.Timeout = TimeSpan.FromSeconds(30);
        });

#if DEBUG
        builder.Services.AddBlazorWebViewDeveloperTools();
#endif

        return builder.Build();
    }
}
