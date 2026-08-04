using FastRide.AdminWeb.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// One session per Blazor circuit — two browser tabs must not share an admin token.
builder.Services.AddScoped<AdminSession>();

builder.Services.AddHttpClient<ApiClient>(client =>
{
    client.BaseAddress = new Uri(builder.Configuration["ApiSettings:BaseUrl"] ?? "https://localhost:5001");
    client.Timeout = TimeSpan.FromSeconds(builder.Configuration.GetValue("ApiSettings:TimeoutSeconds", 30));
})
.ConfigurePrimaryHttpMessageHandler(() =>
{
    var handler = new HttpClientHandler();

    // The API runs behind a development certificate locally; trusting it here keeps the
    // console usable without an extra certificate dance. Never enabled outside Development.
    if (builder.Environment.IsDevelopment())
        handler.ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator;

    return handler;
});

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseAntiforgery();

app.MapRazorComponents<FastRide.AdminWeb.Components.App>()
    .AddInteractiveServerRenderMode();

app.Run();
