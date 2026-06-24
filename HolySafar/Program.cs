using System.Globalization;
using HolySafar.Api;
using HolySafar.Components;
using HolySafar.Data;
using HolySafar.Services;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

var defaultLang = builder.Configuration["AppSettings:DefaultLanguage"] ?? "id";
var cultureName = defaultLang switch { "id" => "id-ID", "en" => "en-US", _ => "id-ID" };
CultureInfo.DefaultThreadCurrentCulture = new CultureInfo(cultureName);
CultureInfo.DefaultThreadCurrentUICulture = new CultureInfo(cultureName);

builder.Services.Configure<FormOptions>(o => o.MultipartBodyLengthLimit = 50 * 1024 * 1024);
builder.WebHost.ConfigureKestrel(o => o.Limits.MaxRequestBodySize = 50 * 1024 * 1024);

var dbProvider = builder.Configuration["Database:Provider"] ?? "SQLite";
builder.Services.AddDbContext<AppDbContext>(options =>
{
    var conn = dbProvider switch
    {
        "SqlServer" => builder.Configuration.GetConnectionString("SqlServer") ?? builder.Configuration["Database:ConnectionStrings:SqlServer"],
        "MySQL" => builder.Configuration.GetConnectionString("MySQL") ?? builder.Configuration["Database:ConnectionStrings:MySQL"],
        "Postgre" => builder.Configuration.GetConnectionString("Postgre") ?? builder.Configuration["Database:ConnectionStrings:Postgre"],
        _ => builder.Configuration.GetConnectionString("SQLite") ?? builder.Configuration["Database:ConnectionStrings:SQLite"] ?? "Data Source=Data/holysafar.db"
    };
    switch (dbProvider)
    {
        case "SqlServer" when !string.IsNullOrEmpty(conn): options.UseSqlServer(conn); break;
        case "MySQL" when !string.IsNullOrEmpty(conn): options.UseMySql(conn, ServerVersion.AutoDetect(conn)); break;
        case "Postgre" when !string.IsNullOrEmpty(conn): options.UseNpgsql(conn); break;
        default: options.UseSqlite(conn ?? "Data Source=Data/holysafar.db"); break;
    }
});

builder.Services.AddScoped<AuthService>();
builder.Services.AddScoped<StorageService>();
builder.Services.AddScoped<ExportService>();
builder.Services.AddScoped<ChatbotService>();
builder.Services.AddScoped<NotifikasiService>();
builder.Services.AddSingleton<GpsSimulatorService>();
builder.Services.AddHttpClient();

// Swagger (simple, no ApiKey UI - documented in docs)
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c => c.SwaggerDoc("v1", new() { Title = "HolySafar API", Version = "v1", Description = "REST API Travel Haji/Umroh. Auth via X-Api-Key header." }));

builder.Services.AddRazorComponents().AddInteractiveServerComponents();

var app = builder.Build();
app.UseStaticFiles();
app.MapStaticAssets();
app.UseSwagger();
app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "HolySafar API v1"));
ApiEndpoints.MapApi(app);

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await db.Database.EnsureCreatedAsync();
    await DataSeeder.SeedAsync(db);
}

if (!app.Environment.IsDevelopment()) { app.UseExceptionHandler("/Error", createScopeForErrors: true); app.UseHsts(); }
app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseHttpsRedirection();
app.UseAntiforgery();
app.MapRazorComponents<App>().AddInteractiveServerRenderMode();
app.Run();
