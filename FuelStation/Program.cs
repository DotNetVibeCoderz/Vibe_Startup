using FuelStation.Components;
using FuelStation.Data;
using FuelStation.Hubs;
using FuelStation.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;

var builder = WebApplication.CreateBuilder(args);

// ==========================================
// DATABASE CONFIGURATION
// ==========================================
builder.Services.AddDatabase(builder.Configuration);

// ==========================================
// IDENTITY & AUTHENTICATION
// ==========================================
builder.Services.AddIdentity<ApplicationUser, ApplicationRole>(options =>
{
    options.Password.RequireDigit = true;
    options.Password.RequiredLength = 6;
    options.Password.RequireNonAlphanumeric = false;
    options.Password.RequireUppercase = false;
    options.Password.RequireLowercase = false;
    options.User.RequireUniqueEmail = true;
    options.SignIn.RequireConfirmedAccount = false;
})
.AddEntityFrameworkStores<AppDbContext>()
.AddDefaultTokenProviders();

builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/login";
    options.LogoutPath = "/logout";
    options.AccessDeniedPath = "/access-denied";
    options.ExpireTimeSpan = TimeSpan.FromHours(8);
    options.SlidingExpiration = true;
});

// ==========================================
// BLAZOR & SIGNALR
// ==========================================
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddCascadingAuthenticationState();
builder.Services.AddAuthorization(options =>
{
    options.FallbackPolicy = new AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .Build();
});

builder.Services.AddSignalR(options =>
{
    options.MaximumReceiveMessageSize = 512 * 1024; // 512KB
});

// ==========================================
// SWAGGER / OPENAPI
// ==========================================
builder.Services.AddControllersWithViews();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new() { Title = "FuelStation API", Version = "v1" });
});

// ==========================================
// APPLICATION SERVICES — Singletons
// ==========================================
builder.Services.AddStorageService(builder.Configuration);
builder.Services.AddSingleton<ChatBotService>();
builder.Services.AddSingleton<IPrinterService, PrinterService>();
builder.Services.AddSingleton<MLPredictionService>();

// Simulator + IoT: Singleton (for UI access) + HostedService (for background loop)
builder.Services.AddSingleton<SimulatorService>();
builder.Services.AddHostedService(sp => sp.GetRequiredService<SimulatorService>());

builder.Services.AddSingleton<IoTSensorSimulatorService>();
builder.Services.AddHostedService(sp => sp.GetRequiredService<IoTSensorSimulatorService>());

// ==========================================
// APPLICATION SERVICES — Scoped
// ==========================================
builder.Services.AddScoped<SeedDataService>();
builder.Services.AddScoped<NotificationService>();

// ==========================================
// HTTP CLIENT
// ==========================================
builder.Services.AddHttpClient();

// ==========================================
// CORS (for API access)
// ==========================================
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

var app = builder.Build();

// ==========================================
// DATABASE SETUP & SEEDING
// ==========================================
using (var scope = app.Services.CreateScope())
{
    var seeder = scope.ServiceProvider.GetRequiredService<SeedDataService>();
    await seeder.SeedAsync();
}

// ==========================================
// MIDDLEWARE PIPELINE
// ==========================================
if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "FuelStation API v1");
        c.RoutePrefix = "swagger";
    });
}
else
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseHttpsRedirection();
app.UseStaticFiles();

// Static assets harus tetap bisa diakses sebelum login
var staticAssets = app.MapStaticAssets();
staticAssets.AllowAnonymous();

app.UseRouting();
app.UseCors("AllowAll");

app.UseAuthentication();
app.UseAuthorization();

app.UseAntiforgery();

// Map SignalR Hubs
app.MapHub<NotificationHub>("/notificationHub");

// Map controllers (REST API)
app.MapControllers();

// Map Blazor
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
