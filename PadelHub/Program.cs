using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using MudBlazor.Services;
using PadelHub.Data;
using PadelHub.Models;
using PadelHub.Services;
using PadelHub.Services.Payments;
using PadelHub.Components;

var builder = WebApplication.CreateBuilder(args);

// ============================================================
// DATABASE CONFIGURATION - Multi-Provider Support
// ============================================================
var dbProvider = builder.Configuration.GetValue<string>("DatabaseProvider") ?? "SQLite";
var connectionString = dbProvider switch
{
    "SqlServer" => builder.Configuration.GetConnectionString("SqlServer"),
    "MySql" => builder.Configuration.GetConnectionString("MySql"),
    "PostgreSQL" => builder.Configuration.GetConnectionString("PostgreSQL"),
    _ => builder.Configuration.GetConnectionString("DefaultConnection")
};

builder.Services.AddDbContext<AppDbContext>(options =>
{
    switch (dbProvider)
    {
        case "SqlServer":
            options.UseSqlServer(connectionString);
            break;
        case "MySql":
            options.UseMySql(connectionString, ServerVersion.AutoDetect(connectionString));
            break;
        case "PostgreSQL":
            options.UseNpgsql(connectionString);
            break;
        default:
            options.UseSqlite(connectionString);
            break;
    }
});

// ============================================================
// ASP.NET IDENTITY
// ============================================================
builder.Services.AddIdentity<ApplicationUser, IdentityRole>(options =>
{
    options.Password.RequiredLength = 8;
    options.Password.RequireDigit = true;
    options.Password.RequireUppercase = true;
    options.Password.RequireLowercase = true;
    options.Password.RequireNonAlphanumeric = false;
    options.User.RequireUniqueEmail = true;
    options.SignIn.RequireConfirmedAccount = false;
})
.AddEntityFrameworkStores<AppDbContext>()
.AddClaimsPrincipalFactory<ApplicationUserClaimsPrincipalFactory>()
.AddDefaultTokenProviders();

builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/auth/login";
    options.LogoutPath = "/auth/logout";
    options.AccessDeniedPath = "/auth/access-denied";
    options.ExpireTimeSpan = TimeSpan.FromDays(7);
    options.SlidingExpiration = true;
});

builder.Services.AddRazorComponents().AddInteractiveServerComponents();

builder.Services.AddMudServices();

// ============================================================
// APPLICATION SERVICES - Dependency Injection
// ============================================================
builder.Services.AddScoped<SeedDataService>();
builder.Services.AddScoped<AuditLogService>();
builder.Services.AddScoped<FileStorageService>();   // Multi-provider: FileSystem, AzureBlob, S3, MinIO
builder.Services.AddScoped<ExportService>();
builder.Services.AddScoped<QrCodeService>();
builder.Services.AddScoped<IoTSimulatorService>();
builder.Services.AddScoped<RankingService>();
builder.Services.AddScoped<PaymentService>();
builder.Services.AddScoped<NotificationService>();

// ChatBot Service dengan Semantic Kernel
builder.Services.AddScoped<ChatBotService>();

// ============================================================
// PAYMENT GATEWAY - daftarkan provider baru di sini saja
// ============================================================
builder.Services.AddScoped<IPaymentGateway, ManualTransferGateway>();
builder.Services.AddScoped<IPaymentGateway, XenditGateway>();
builder.Services.AddScoped<IPaymentGateway, MidtransGateway>();
builder.Services.AddScoped<PaymentGatewayRegistry>();
builder.Services.AddScoped<PaymentCheckoutService>();

// ============================================================
// HTTP CLIENTS
// ============================================================
builder.Services.AddHttpClient();
builder.Services.AddHttpClient("Tavily", client =>
{
    client.BaseAddress = new Uri("https://api.tavily.com/");
    client.DefaultRequestHeaders.Add("Accept", "application/json");
});
builder.Services.AddHttpClient("Scraper", client =>
{
    client.DefaultRequestHeaders.Add("User-Agent", "PadelHub-Bot/1.0");
    client.Timeout = TimeSpan.FromSeconds(15);
});
builder.Services.AddHttpClient("FileReader", client =>
{
    client.DefaultRequestHeaders.Add("User-Agent", "PadelHub-Bot/1.0");
    client.Timeout = TimeSpan.FromSeconds(30);
});
builder.Services.AddHttpClient("Xendit", client =>
{
    client.DefaultRequestHeaders.Add("Accept", "application/json");
    client.Timeout = TimeSpan.FromSeconds(30);
});
builder.Services.AddHttpClient("Midtrans", client =>
{
    client.Timeout = TimeSpan.FromSeconds(30);
});

// Swagger
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// Database seed
using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    var seeder = scope.ServiceProvider.GetRequiredService<SeedDataService>();
    dbContext.Database.EnsureCreated();
    if (!dbContext.Users.Any())
    {
        await seeder.SeedAsync();
    }
}

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();
app.UseAntiforgery();

app.MapRazorComponents<App>().AddInteractiveServerRenderMode();

// ============================================================
// AUTH ENDPOINTS (POST) - dedicated path to avoid route ambiguity
// ============================================================
app.MapPost("/auth/login-handler", async (HttpContext httpContext, SignInManager<ApplicationUser> signInManager, UserManager<ApplicationUser> userManager) =>
{
    var form = await httpContext.Request.ReadFormAsync();
    var email = form["Email"].ToString();
    var password = form["Password"].ToString();
    var rememberMe = form["RememberMe"].ToString() is "true" or "on";
    var returnUrl = form["ReturnUrl"].ToString();

    if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
    {
        return Results.Redirect($"/auth/login?error={Uri.EscapeDataString("Email dan password wajib diisi.")}");
    }

    var user = await userManager.FindByEmailAsync(email);
    if (user == null)
    {
        return Results.Redirect($"/auth/login?error={Uri.EscapeDataString("Email tidak terdaftar.")}");
    }

    if (!user.IsActive)
    {
        return Results.Redirect($"/auth/login?error={Uri.EscapeDataString("Akun dinonaktifkan.")}");
    }

    var result = await signInManager.PasswordSignInAsync(user.UserName ?? email, password, rememberMe, lockoutOnFailure: true);
    if (result.Succeeded)
    {
        user.LastLoginAt = DateTime.UtcNow;
        await userManager.UpdateAsync(user);
        return Results.Redirect(string.IsNullOrWhiteSpace(returnUrl) ? "/" : returnUrl);
    }

    if (result.IsLockedOut)
    {
        return Results.Redirect($"/auth/login?error={Uri.EscapeDataString("Akun terkunci.")}");
    }

    return Results.Redirect($"/auth/login?error={Uri.EscapeDataString("Password salah.")}");
}).DisableAntiforgery();

// Logout endpoint untuk menghindari error header sudah terkirim
app.MapGet("/auth/logout-handler", async (HttpContext httpContext, SignInManager<ApplicationUser> signInManager) =>
{
    await signInManager.SignOutAsync();
    return Results.Redirect("/auth/login");
}).DisableAntiforgery();

// ============================================================
// MINIMAL API ENDPOINTS
// ============================================================
app.MapGet("/api/health", () => Results.Ok(new { Status = "Healthy", Timestamp = DateTime.UtcNow, AppName = "PadelHub API" }));
app.MapGet("/api/players", async (AppDbContext db) => Results.Ok(await db.PlayerProfiles.Include(p => p.User).Take(100).ToListAsync()));
app.MapGet("/api/players/{id}", async (int id, AppDbContext db) => await db.PlayerProfiles.Include(p => p.User).FirstOrDefaultAsync(p => p.Id == id) is PlayerProfile p ? Results.Ok(p) : Results.NotFound());
app.MapGet("/api/clubs", async (AppDbContext db) => Results.Ok(await db.Clubs.Take(50).ToListAsync()));
app.MapGet("/api/clubs/{id}", async (int id, AppDbContext db) => await db.Clubs.Include(c => c.Courts).FirstOrDefaultAsync(c => c.Id == id) is Club club ? Results.Ok(club) : Results.NotFound());
app.MapGet("/api/courts", async (AppDbContext db) => Results.Ok(await db.Courts.Include(c => c.Club).Take(100).ToListAsync()));
app.MapGet("/api/tournaments", async (AppDbContext db) => Results.Ok(await db.Tournaments.Take(50).ToListAsync()));
app.MapPost("/api/sensors/push", async (SensorData data, AppDbContext db) => { data.RecordedAt = DateTime.UtcNow; db.SensorData.Add(data); await db.SaveChangesAsync(); return Results.Created($"/api/sensors/{data.Id}", data); });
app.MapGet("/api/sensors/{courtId}", async (int courtId, AppDbContext db) => Results.Ok(await db.SensorData.Where(s => s.CourtId == courtId).OrderByDescending(s => s.RecordedAt).Take(100).ToListAsync()));
app.MapGet("/api/rankings", async (AppDbContext db) => Results.Ok(await db.PlayerProfiles.Include(p => p.User).OrderBy(p => p.Ranking).Take(50).Select(p => new { p.Id, p.User!.FullName, p.Ranking, p.Rating, p.Wins, p.Losses, p.Level }).ToListAsync()));

// ============================================================
// WEBHOOK PAYMENT GATEWAY
// Provider memanggil endpoint ini tanpa cookie/antiforgery. Keasliannya
// diverifikasi di dalam ParseCallback masing-masing gateway (callback token
// untuk Xendit, signature SHA-512 untuk Midtrans) — jangan pernah mengubah
// status pembayaran sebelum verifikasi itu lolos.
// ============================================================
async Task<IResult> HandleWebhookAsync(string providerKey, HttpContext httpContext,
    PaymentGatewayRegistry registry, PaymentCheckoutService checkout, ILoggerFactory loggerFactory)
{
    var logger = loggerFactory.CreateLogger("PaymentWebhook");
    var gateway = registry.Find(providerKey);

    if (gateway is null || !gateway.IsEnabled)
    {
        logger.LogWarning("Webhook {Provider} ditolak: provider tidak aktif.", providerKey);
        return Results.NotFound();
    }

    using var reader = new StreamReader(httpContext.Request.Body);
    var body = await reader.ReadToEndAsync();

    var callback = gateway.ParseCallback(body, httpContext.Request.Headers);
    if (!callback.Valid)
    {
        logger.LogWarning("Webhook {Provider} ditolak: {Error}", providerKey, callback.Error);
        return Results.Unauthorized();
    }

    var applied = await checkout.ApplyCallbackAsync(callback, body, gateway.Key);

    // 200 = notifikasi diterima dan diproses; 422 memberi tahu provider untuk
    // mencoba lagi (misal tagihan belum tersimpan saat notifikasi tiba).
    return applied
        ? Results.Ok(new { received = true })
        : Results.UnprocessableEntity(new { received = false, reason = "Tagihan tidak cocok." });
}

app.MapPost("/api/payments/webhook/xendit", (HttpContext ctx, PaymentGatewayRegistry registry, PaymentCheckoutService checkout, ILoggerFactory lf) =>
    HandleWebhookAsync(PaymentProviders.Xendit, ctx, registry, checkout, lf)).DisableAntiforgery();

app.MapPost("/api/payments/webhook/midtrans", (HttpContext ctx, PaymentGatewayRegistry registry, PaymentCheckoutService checkout, ILoggerFactory lf) =>
    HandleWebhookAsync(PaymentProviders.Midtrans, ctx, registry, checkout, lf)).DisableAntiforgery();

// Daftar provider aktif — dipakai halaman checkout dan berguna untuk memeriksa
// konfigurasi tanpa membuka appsettings.
app.MapGet("/api/payments/providers", (PaymentGatewayRegistry registry) => Results.Ok(
    registry.All.Select(g => new { g.Key, g.DisplayName, g.Description, g.IsEnabled, g.IsSandbox })));

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.Run();
