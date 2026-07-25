using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Ngibrid.Api;
using Ngibrid.Data;
using Ngibrid.Hubs;
using Ngibrid.Services;
using Ngibrid.Models;
using Microsoft.OpenApi.Models;
using System.Text.Json.Serialization;

var builder = WebApplication.CreateBuilder(args);

// EF navigation properties are bidirectional, so any endpoint that returns an entity graph
// (locker → compartments → locker) would otherwise blow up the serializer with a cycle.
builder.Services.ConfigureHttpJsonOptions(o =>
    o.SerializerOptions.ReferenceHandler = ReferenceHandler.IgnoreCycles);

// ─── Database ───
builder.Services.AddDbContext<NgibridDbContext>((sp, options) =>
{
    var config = sp.GetRequiredService<IConfiguration>();
    options.ConfigureProvider(config);
    options.EnableSensitiveDataLogging(builder.Environment.IsDevelopment());
});
builder.Configuration.EnsureDatabaseReady();

// ─── Identity ───
builder.Services.AddIdentity<ApplicationUser, ApplicationRole>(options =>
{
    var pwd = builder.Configuration.GetSection("Auth:Password");
    options.Password.RequireDigit = pwd.GetValue<bool>("RequireDigit", true);
    options.Password.RequireLowercase = pwd.GetValue<bool>("RequireLowercase", true);
    options.Password.RequireUppercase = pwd.GetValue<bool>("RequireUppercase", true);
    options.Password.RequireNonAlphanumeric = pwd.GetValue<bool>("RequireNonAlphanumeric", true);
    options.Password.RequiredLength = pwd.GetValue<int>("RequiredLength", 8);
    options.Lockout.MaxFailedAccessAttempts = pwd.GetValue<int>("MaxFailedAttempts", 5);
    options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(pwd.GetValue<int>("LockoutMinutes", 15));
    options.User.RequireUniqueEmail = true;
    options.SignIn.RequireConfirmedEmail = false;
})
.AddEntityFrameworkStores<NgibridDbContext>()
.AddDefaultTokenProviders();

builder.Services.ConfigureApplicationCookie(options =>
{
    options.Cookie.HttpOnly = true;
    // Always-secure cookies break plain-HTTP local development, where launchSettings also exposes :5182.
    options.Cookie.SecurePolicy = builder.Environment.IsDevelopment()
        ? CookieSecurePolicy.SameAsRequest
        : CookieSecurePolicy.Always;
    options.Cookie.SameSite = SameSiteMode.Lax;
    options.ExpireTimeSpan = TimeSpan.FromDays(7);
    options.SlidingExpiration = true;
    options.LoginPath = "/login";
    options.LogoutPath = "/logout";
    options.AccessDeniedPath = "/access-denied";
});

// ─── Authorization policies ───
// Named policies, because RequireAuthorization("Admin") resolves a *policy* name, not a role.
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("AdminOnly", p => p.RequireRole("Admin"));
    options.AddPolicy("AdminOrManager", p => p.RequireRole("Admin", "Manager"));
    options.AddPolicy("StaffArea", p => p.RequireRole("Admin", "Manager", "WarehouseStaff"));
    options.AddPolicy("CourierArea", p => p.RequireRole("Admin", "Manager", "Courier"));
});

// ─── CORS ───
builder.Services.AddCors(o => o.AddPolicy("AllowAll", p =>
    p.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader()));

// ─── Blazor ───
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// Without this, <AuthorizeRouteView> and <AuthorizeView> never receive an authentication state.
builder.Services.AddCascadingAuthenticationState();
builder.Services.AddHttpContextAccessor();

// ─── SignalR ───
builder.Services.AddSignalR(o =>
    o.EnableDetailedErrors = builder.Environment.IsDevelopment());

// ─── Swagger ───
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(o =>
{
    o.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Ngibrid API",
        Version = "v1",
        Description = "REST API Ngibrid Logistics — integrasi order, tracking, pricing, warehouse, dan chat bot."
    });
});

// ─── Services ───
// Scoped: a Blazor Server scope is one user's circuit, so the theme stays per-visitor.
builder.Services.AddScoped<ThemeService>();
builder.Services.AddSingleton<BarcodeService>();
builder.Services.AddScoped<AppConfigService>();
builder.Services.AddScoped<CurrentUserService>();
builder.Services.AddScoped<AuditService>();
builder.Services.AddScoped<StorageService>();
builder.Services.AddScoped<OrderService>();
builder.Services.AddScoped<TrackingService>();
builder.Services.AddScoped<PaymentService>();
builder.Services.AddScoped<InvoiceService>();
builder.Services.AddScoped<WarehouseService>();
builder.Services.AddScoped<CourierService>();
builder.Services.AddScoped<PickupService>();
builder.Services.AddScoped<SupportTicketService>();
builder.Services.AddScoped<NotificationService>();
builder.Services.AddScoped<AnalyticsService>();
builder.Services.AddScoped<ChatBotService>();
builder.Services.AddScoped<InsuranceService>();
builder.Services.AddScoped<RouteOptimizationService>();
builder.Services.AddScoped<DynamicPricingService>();
builder.Services.AddScoped<GreenLogisticsService>();
builder.Services.AddScoped<MarketplaceService>();
builder.Services.AddScoped<PartnerLogisticsService>();
builder.Services.AddScoped<SmartLockerService>();
builder.Services.AddScoped<ComplianceService>();
builder.Services.AddScoped<LoyaltyService>();
builder.Services.AddScoped<ForecastService>();
builder.Services.AddScoped<DataSeeder>();

// City master data is read on nearly every page and never changes at runtime, so it is cached in
// one singleton; like the simulators, it scopes its own DbContext.
builder.Services.AddSingleton<CityService>();

// Simulators are singletons so pages can drive them, and hosted so they tick on their own thread.
builder.Services.AddSingleton<GpsSimulatorService>();
builder.Services.AddSingleton<IotSimulatorService>();
builder.Services.AddSingleton<SmartLockerSimulatorService>();
builder.Services.AddHostedService(sp => sp.GetRequiredService<GpsSimulatorService>());
builder.Services.AddHostedService(sp => sp.GetRequiredService<IotSimulatorService>());
builder.Services.AddHostedService(sp => sp.GetRequiredService<SmartLockerSimulatorService>());

builder.Services.AddHttpClient("Default", c =>
{
    c.DefaultRequestHeaders.Add("User-Agent", "Ngibrid/1.0");
    c.Timeout = TimeSpan.FromSeconds(120);
});
builder.Services.AddHttpClient("Tavily", c =>
{
    c.BaseAddress = new Uri("https://api.tavily.com/");
    c.Timeout = TimeSpan.FromSeconds(30);
});

// Response compression keeps the Blazor circuit and API payloads light.
builder.Services.AddResponseCompression(o =>
{
    o.EnableForHttps = true;
    o.MimeTypes = Microsoft.AspNetCore.ResponseCompression.ResponseCompressionDefaults.MimeTypes
        .Concat(new[] { "application/octet-stream", "image/svg+xml" });
});

// ─── Build ───
var app = builder.Build();

// ─── DB Init ───
using (var scope = app.Services.CreateScope())
{
    try
    {
        var db = scope.ServiceProvider.GetRequiredService<NgibridDbContext>();
        await db.Database.EnsureCreatedAsync();

        // Master data first: the sample orders are built from real kota/kabupaten, and every
        // distance calculation resolves against this table.
        await app.Services.GetRequiredService<CityService>().InitializeAsync();

        var seeder = scope.ServiceProvider.GetRequiredService<DataSeeder>();
        await seeder.SeedAsync();
    }
    catch (Exception ex)
    {
        // Logged as an error, not a warning: anything thrown here leaves the app running on a
        // half-initialised database (master data present, sample data missing, or worse), which is
        // not a condition that should scroll past in the startup noise.
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
        logger.LogError(ex, "DB init failed: {Msg}", ex.Message);
    }
}

// ─── Middleware ───
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(o =>
    {
        o.SwaggerEndpoint("/swagger/v1/swagger.json", "Ngibrid API v1");
        o.RoutePrefix = "api/docs";
    });
}
else
{
    app.UseExceptionHandler("/error", createScopeForErrors: true);
    app.UseHsts();
}

app.UseResponseCompression();
app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseCors("AllowAll");
app.UseAuthentication();
app.UseAuthorization();
app.UseAntiforgery();

// ─── Routes ───
app.MapRazorComponents<Ngibrid.Components.App>()
    .AddInteractiveServerRenderMode();

app.MapHub<TrackingHub>("/hubs/tracking");
app.MapHub<ChatHub>("/hubs/chat");
app.MapHub<NotificationHub>("/hubs/notifications");
app.MapHub<CourierHub>("/hubs/courier");

app.MapAuthEndpoints();
app.MapApiEndpoints();

app.Run();
