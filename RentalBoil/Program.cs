using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RentalBoil.Api;
using RentalBoil.Components;
using RentalBoil.Data;
using RentalBoil.Models;
using RentalBoil.Services;
using RentalBoil.Hubs;

var builder = WebApplication.CreateBuilder(args);

// ---- Database ----
var dbProvider = builder.Configuration.GetValue<string>("Database:Provider") ?? "SQLite";
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection") ?? "Data Source=RentalBoil.db";

builder.Services.AddDbContext<AppDbContext>(options =>
{
    switch (dbProvider)
    {
        case "SqlServer": options.UseSqlServer(connectionString); break;
        case "MySQL": options.UseMySql(connectionString, ServerVersion.AutoDetect(connectionString)); break;
        case "PostgreSQL": options.UseNpgsql(connectionString); break;
        default: options.UseSqlite(connectionString); break;
    }
});

// ---- Identity ----
builder.Services.AddIdentity<ApplicationUser, IdentityRole>(options =>
{
    options.Password.RequiredLength = 8;
    options.Password.RequireDigit = true;
    options.Password.RequireLowercase = true;
    options.Password.RequireUppercase = true;
    options.Password.RequireNonAlphanumeric = true;
    options.SignIn.RequireConfirmedEmail = false;
    options.User.RequireUniqueEmail = true;
    options.Lockout.MaxFailedAccessAttempts = 5;
})
.AddEntityFrameworkStores<AppDbContext>()
.AddDefaultTokenProviders();

builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/login";
    options.LogoutPath = "/logout";
    options.ExpireTimeSpan = TimeSpan.FromDays(7);
});

// ---- Blazor ----
builder.Services.AddRazorComponents().AddInteractiveServerComponents();

// ---- Swagger / OpenAPI ----
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new()
    {
        Title = "RentalBoil API",
        Version = "v1",
        Description = "REST API untuk RentalBoil - Platform Rental Kendaraan.\n\n🔐 Gunakan header **X-Api-Key** untuk autentikasi.\n🛰️ GPS Simulator endpoint tersedia di /api/vehicles/{id}/simulator-update"
    });
    c.AddSecurityDefinition("ApiKey", new()
    {
        Type = Microsoft.OpenApi.Models.SecuritySchemeType.ApiKey,
        In = Microsoft.OpenApi.Models.ParameterLocation.Header,
        Name = "X-Api-Key",
        Description = "Default: rntl-2025-secure-api-key-change-in-production"
    });
    c.AddSecurityRequirement(new()
    {
        {
            new() { Reference = new() { Type = Microsoft.OpenApi.Models.ReferenceType.SecurityScheme, Id = "ApiKey" } },
            Array.Empty<string>()
        }
    });
});

// ---- Services ----
builder.Services.AddScoped<VehicleService>();
builder.Services.AddScoped<BookingService>();
builder.Services.AddScoped<PaymentService>();
builder.Services.AddScoped<NotificationService>();
builder.Services.AddScoped<ChatService>();
builder.Services.AddScoped<ReviewService>();
builder.Services.AddScoped<PromotionService>();
builder.Services.AddScoped<LoyaltyService>();
builder.Services.AddScoped<StorageService>();
builder.Services.AddScoped<ExportService>();
builder.Services.AddScoped<GpsSimulatorService>();
builder.Services.AddScoped<BotService>();
builder.Services.AddScoped<ThemeService>();
builder.Services.AddScoped<LanguageService>();
builder.Services.AddScoped<BotKernelFunctions>();

builder.Services.AddSingleton<GpsSimulatorHostedService>();
builder.Services.AddHostedService(sp => sp.GetRequiredService<GpsSimulatorHostedService>());

// ---- HttpClient ----
builder.Services.AddHttpClient(); // generic
builder.Services.AddHttpClient("SimulatorClient", client =>
{
    client.Timeout = TimeSpan.FromSeconds(10);
    client.DefaultRequestHeaders.Accept.Add(
        new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));
}); // named client untuk GPS simulator

builder.Services.AddSignalR();

// ---- CORS untuk API & Simulator ----
builder.Services.AddCors(options =>
{
    options.AddPolicy("ApiCors", policy =>
        policy.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader());
});

var app = builder.Build();

// ---- Middleware ----
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}
else
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "RentalBoil API v1");
        c.RoutePrefix = "swagger";
    });
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseCors("ApiCors");

// ---- API Key Middleware untuk /api routes ----
app.UseMiddleware<ApiKeyMiddleware>();

app.UseAuthentication();
app.UseAuthorization();
app.UseAntiforgery();

// ---- Endpoint Auth (cookie harus diset via HTTP request biasa) ----
app.MapPost("/account/login", async (
    HttpContext httpContext,
    SignInManager<ApplicationUser> signInManager,
    UserManager<ApplicationUser> userManager,
    [FromForm] string email,
    [FromForm] string password,
    [FromForm] string? returnUrl) =>
{
    var safeReturnUrl = IsLocalUrl(returnUrl) ? returnUrl! : "/";
    var encodedReturnUrl = Uri.EscapeDataString(safeReturnUrl);

    var user = await userManager.FindByEmailAsync(email);
    if (user != null && user.IsSuspended)
        return Results.Redirect($"/login?error=suspended&returnUrl={encodedReturnUrl}");

    var result = await signInManager.PasswordSignInAsync(email, password, true, false);
    if (result.Succeeded)
        return Results.Redirect(safeReturnUrl);

    return Results.Redirect($"/login?error=invalid&returnUrl={encodedReturnUrl}");
});

app.MapGet("/account/logout", async (SignInManager<ApplicationUser> signInManager) =>
{
    await signInManager.SignOutAsync();
    return Results.Redirect("/");
});

// ---- Map API Endpoints ----
app.MapApiEndpoints();

// ---- Map SignalR Hubs ----
app.MapHub<NotificationHub>("/hubs/notification");
app.MapHub<ChatHub>("/hubs/chat");
app.MapHub<GpsHub>("/hubs/gps");

// ---- Map Blazor ----
app.MapRazorComponents<App>().AddInteractiveServerRenderMode();

// ---- Seed Database ----
using (var scope = app.Services.CreateScope())
    await DbInitializer.SeedAsync(scope.ServiceProvider);

app.Run();

static bool IsLocalUrl(string? url)
{
    if (string.IsNullOrWhiteSpace(url))
        return false;

    return Uri.TryCreate(url, UriKind.Relative, out _) && url.StartsWith('/');
}
