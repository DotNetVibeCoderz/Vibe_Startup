using FastRide.AdminWeb.Services;

var builder = WebApplication.CreateBuilder(args);

// Blazor Server services
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// HttpClient for API
builder.Services.AddHttpClient<ApiClient>(client =>
{
    client.BaseAddress = new Uri(builder.Configuration["ApiSettings:BaseUrl"]
        ?? "https://localhost:5001");
    client.Timeout = TimeSpan.FromSeconds(
        builder.Configuration.GetValue<int>("ApiSettings:TimeoutSeconds", 30));
});

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseAntiforgery();

app.MapRazorComponents<FastRide.AdminWeb.Components.App>()
    .AddInteractiveServerRenderMode();

app.Run();
