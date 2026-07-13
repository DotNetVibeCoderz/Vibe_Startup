using Microsoft.EntityFrameworkCore;
using PCHub.Shared.Data;
using PCHub.Shared.DTOs;
using PCHub.Shared.Enums;
using PCHub.Shared.Interfaces;
using PCHub.Shared.Services;
using PCHub.Admin.Endpoints;
using PCHub.Admin.Services;
using Microsoft.AspNetCore.Components.Authorization;

var builder = WebApplication.CreateBuilder(args);
var config = builder.Configuration;

// ==================== DATABASE ====================
var dbProviderStr = config["Database:Provider"] ?? "SQLite";
var dbProvider = Enum.TryParse<DatabaseProvider>(dbProviderStr, out var dp) ? dp : DatabaseProvider.SQLite;
var connStr = dbProvider switch
{
    DatabaseProvider.SqlServer => config["Database:SqlServer"] ?? "Server=.;Database=PCHub;Trusted_Connection=true;TrustServerCertificate=true",
    DatabaseProvider.PostgreSQL => config["Database:PostgreSQL"] ?? "Host=localhost;Database=PCHub;Username=postgres;Password=postgres",
    DatabaseProvider.MySQL => config["Database:MySQL"] ?? "Server=localhost;Database=PCHub;User=root;Password=root",
    _ => config["Database:SQLite"] ?? "Data Source=PCHub.db"
};

builder.Services.AddDbContext<AppDbContext>(options =>
{
    switch (dbProvider)
    {
        case DatabaseProvider.SqlServer: options.UseSqlServer(connStr); break;
        case DatabaseProvider.PostgreSQL: options.UseNpgsql(connStr); break;
        case DatabaseProvider.MySQL: options.UseMySql(connStr, ServerVersion.AutoDetect(connStr)); break;
        default: options.UseSqlite(connStr); break;
    }
});

// ==================== CORE SERVICES ====================
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IPcService, PcService>();
builder.Services.AddScoped<IGameService, GameService>();
builder.Services.AddScoped<IBillingService, BillingService>();
builder.Services.AddScoped<IReservationService, ReservationService>();
builder.Services.AddScoped<IMembershipService, MembershipService>();
builder.Services.AddScoped<IPromoService, PromoService>();
builder.Services.AddScoped<IDashboardService, DashboardService>();
builder.Services.AddScoped<INotificationService, NotificationService>();
builder.Services.AddScoped<ITournamentService, TournamentService>();
builder.Services.AddScoped<IExportService, ExportService>();

// ==================== PHASE 5 SERVICES ====================
builder.Services.AddSingleton<IChatBotService>(sp => new ChatBotService(sp));
builder.Services.Configure<ChatBotSettings>(config.GetSection("ChatBot"));
builder.Services.AddSingleton<IEmailService>(sp => new EmailService(sp.GetService<IConfiguration>()));
builder.Services.AddSingleton<IPaymentService>(sp => new PaymentService(sp.GetService<IConfiguration>()));
builder.Services.AddSingleton<IStorageService>(sp => new StorageMultiProviderService(sp.GetService<IConfiguration>()));
var redisConn = config.GetConnectionString("Redis") ?? config["ConnectionStrings:Redis"];
if (!string.IsNullOrEmpty(redisConn))
    builder.Services.AddStackExchangeRedisCache(o => { o.Configuration = redisConn; o.InstanceName = "PCHub:"; });
builder.Services.AddMemoryCache();
builder.Services.AddSingleton<ICacheService, CacheService>();
builder.Services.AddSingleton<IoTService>();
builder.Services.AddSingleton<TournamentBracketService>();
builder.Services.AddSingleton<SignalRNotificationService>();
builder.Services.AddSignalR();

// ==================== AUTH ====================
// Authentication middleware (cookie-based fallback + circuit-based state)
builder.Services.AddAuthentication("PCHubCookie")
    .AddCookie("PCHubCookie", options =>
    {
        options.LoginPath = "/login";
        options.LogoutPath = "/logout";
        options.ExpireTimeSpan = TimeSpan.FromDays(7);
        options.SlidingExpiration = true;
        options.Cookie.HttpOnly = true;
        options.Cookie.Name = "PCHub.Auth";
    });

builder.Services.AddAuthorizationCore(options =>
{
    options.AddPolicy("AdminOnly", p => p.RequireRole("Admin"));
    options.AddPolicy("OperatorOnly", p => p.RequireRole("Admin", "Operator"));
});

// Custom circuit-based auth state provider
builder.Services.AddScoped<PCHubAuthStateProvider>();
builder.Services.AddScoped<AuthenticationStateProvider>(sp => sp.GetRequiredService<PCHubAuthStateProvider>());
builder.Services.AddHttpContextAccessor();

// ==================== WEB ====================
builder.Services.AddRazorComponents().AddInteractiveServerComponents();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo
    {
        Title = config["AppName"] ?? "PCHub API",
        Version = config["AppVersion"] ?? "v1",
        Description = config["AppDescription"] ?? "API for PCHub Game Center"
    });
});
var corsOrigins = config.GetSection("Security:CorsOrigins").Get<string[]>() ?? new[] { "https://localhost:5001" };
builder.Services.AddCors(o => o.AddPolicy("AllowClients", p => p.WithOrigins(corsOrigins).AllowAnyMethod().AllowAnyHeader().AllowCredentials()));

// ==================== BUILD ====================
var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await db.Database.EnsureCreatedAsync();
    await SeedData.InitializeAsync(db);
    var chatService = scope.ServiceProvider.GetRequiredService<IChatBotService>();
    var chatSettings = config.GetSection("ChatBot").Get<ChatBotSettings>();
    if (chatSettings != null) chatService.UpdateSettings(chatSettings);
}

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c => { c.SwaggerEndpoint("/swagger/v1/swagger.json", "PCHub API v1"); c.DocumentTitle = "PCHub - API"; });
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseCors("AllowClients");
app.UseAuthentication();    // ← WAJIB untuk `[Authorize]` attribute
app.UseAuthorization();     // ← WAJIB untuk `[Authorize]` attribute
app.UseAntiforgery();

app.MapApiEndpoints();
app.MapHub<NotificationHub>("/hubs/notifications");
app.MapRazorComponents<PCHub.Admin.Components.App>().AddInteractiveServerRenderMode();

app.Lifetime.ApplicationStarted.Register(() =>
{
    var logger = app.Services.GetRequiredService<ILogger<Program>>();
    logger.LogInformation("🎮 PCHub Admin v2.0 started | DB: {Provider}", dbProvider);
});

app.Run();
