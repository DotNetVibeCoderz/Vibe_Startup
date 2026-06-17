using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Mvc;
using Bioskop.Data;
using Bioskop.Services;
using Bioskop.Models;
using Bioskop.Api;

var builder = WebApplication.CreateBuilder(args);

// ===== Database =====
var dbProvider = builder.Configuration.GetValue<string>("DatabaseProvider") ?? "SQLite";
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");

builder.Services.AddDbContext<ApplicationDbContext>(options =>
{
    switch (dbProvider)
    {
        case "SqlServer":
            options.UseSqlServer(builder.Configuration.GetConnectionString("SqlServer"));
            break;
        case "MySql":
            options.UseMySql(builder.Configuration.GetConnectionString("MySql"),
                ServerVersion.AutoDetect(builder.Configuration.GetConnectionString("MySql")));
            break;
        case "PostgreSql":
            options.UseNpgsql(builder.Configuration.GetConnectionString("PostgreSql"));
            break;
        default:
            options.UseSqlite(connectionString);
            break;
    }
});

// ===== Identity =====
builder.Services.AddIdentity<ApplicationUser, IdentityRole>(options =>
{
    options.Password.RequireDigit = true;
    options.Password.RequiredLength = 6;
    options.Password.RequireNonAlphanumeric = false;
    options.Password.RequireUppercase = true;
    options.Password.RequireLowercase = true;
    options.User.RequireUniqueEmail = true;
    options.SignIn.RequireConfirmedAccount = false;
})
.AddEntityFrameworkStores<ApplicationDbContext>()
.AddDefaultTokenProviders();

builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/auth/login";
    options.LogoutPath = "/auth/logout";
    options.AccessDeniedPath = "/auth/access-denied";
    options.ExpireTimeSpan = TimeSpan.FromDays(7);
    options.SlidingExpiration = true;
});

// ===== Application Services =====
builder.Services.AddScoped<MovieService>();
builder.Services.AddScoped<ShowtimeService>();
builder.Services.AddScoped<SeatService>();
builder.Services.AddScoped<OrderService>();
builder.Services.AddScoped<TicketService>();
builder.Services.AddScoped<SnackService>();
builder.Services.AddScoped<PaymentService>();
builder.Services.AddScoped<PostService>();
builder.Services.AddScoped<AuditService>();
builder.Services.AddScoped<ChatBotService>();
builder.Services.AddScoped<StorageService>();

builder.Services.AddMemoryCache();
builder.Services.AddHttpContextAccessor();

// ===== Blazor SignalR (KRITIS untuk interactivity!) =====
builder.Services.AddSignalR(options =>
{
    options.EnableDetailedErrors = builder.Environment.IsDevelopment();
    options.MaximumReceiveMessageSize = 128 * 1024; // 128KB
});

// ===== Blazor Interactive Server =====
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents(options =>
    {
        // Circuit timeout 30 menit agar session tidak cepat putus
        options.DisconnectedCircuitRetentionPeriod = TimeSpan.FromMinutes(30);
    });

builder.Services.AddControllers();

// ===== Swagger =====
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// ===== CORS =====
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
        policy.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader());
});

var app = builder.Build();

// ===== Database Initialize =====
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    try
    {
        var context = services.GetRequiredService<ApplicationDbContext>();
        await context.Database.EnsureCreatedAsync();
        await SeedData.Initialize(services);
    }
    catch (Exception ex)
    {
        var logger = services.GetRequiredService<ILogger<Program>>();
        logger.LogError(ex, "Error seeding database");
    }
}

// ===== Middleware Pipeline =====
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}
else
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseCors("AllowAll");

app.UseAuthentication();
app.UseAuthorization();

// Penting: Antiforgery HARUS sebelum MapRazorComponents
app.UseAntiforgery();

// === Traffic Logging (inline, tidak pakai Task.Run yang bisa dispose services) ===
app.Use(async (context, next) =>
{
    var sw = System.Diagnostics.Stopwatch.StartNew();
    await next();
    sw.Stop();

    // Log traffic secara inline agar tidak ada akses disposed services
    try
    {
        var auditService = context.RequestServices.GetRequiredService<AuditService>();
        await auditService.LogTrafficAsync(
            context.Request.Path.ToString() ?? "/",
            context.Request.Method,
            context.Response.StatusCode,
            context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            context.Request.Headers.UserAgent.ToString(),
            sw.ElapsedMilliseconds
        );
    }
    catch { /* silent fail - jangan ganggu request utama */ }
});

// ===== API Key Middleware =====
app.UseWhen(context => context.Request.Path.StartsWithSegments("/api") &&
                       !context.Request.Path.StartsWithSegments("/api/health") &&
                       !context.Request.Path.StartsWithSegments("/swagger"),
    builder =>
    {
        builder.Use(async (context, next) =>
        {
            var config = context.RequestServices.GetRequiredService<IConfiguration>();
            var expectedApiKey = config.GetValue<string>("ApiKey");
            var apiKey = context.Request.Headers["X-Api-Key"].FirstOrDefault();
            if (string.IsNullOrEmpty(apiKey) || apiKey != expectedApiKey)
            {
                context.Response.StatusCode = 401;
                context.Response.ContentType = "application/json";
                await context.Response.WriteAsync("{\"error\":\"Invalid or missing API Key\"}");
                return;
            }
            await next();
        });
    });

// ===== Auth Endpoint (POST) - login via browser form =====
app.MapPost("/auth/login", async (
    SignInManager<ApplicationUser> signInManager,
    [FromForm] LoginRequest request) =>
{
    var result = await signInManager.PasswordSignInAsync(
        request.Email, request.Password, request.RememberMe, lockoutOnFailure: false);

    if (result.Succeeded)
    {
        return Results.Redirect("/");
    }

    if (result.IsLockedOut)
    {
        return Results.Redirect("/auth/login?error=locked");
    }

    return Results.Redirect("/auth/login?error=invalid");
});

// ===== Auth Endpoint (POST) - register via browser form =====
app.MapPost("/auth/register", async (
    UserManager<ApplicationUser> userManager,
    SignInManager<ApplicationUser> signInManager,
    [FromForm] RegisterRequest request) =>
{
    if (!string.Equals(request.Password, request.ConfirmPassword, StringComparison.Ordinal))
    {
        return Results.Redirect("/auth/register?error=mismatch");
    }

    var user = new ApplicationUser
    {
        UserName = request.Email,
        Email = request.Email,
        FullName = request.FullName,
        EmailConfirmed = true,
        CreatedAt = DateTime.UtcNow
    };

    var result = await userManager.CreateAsync(user, request.Password);
    if (result.Succeeded)
    {
        await userManager.AddToRoleAsync(user, "User");
        await signInManager.SignInAsync(user, isPersistent: false);
        return Results.Redirect("/");
    }

    var errorCode = result.Errors.FirstOrDefault()?.Code ?? "Unknown";
    return errorCode switch
    {
        "DuplicateUserName" or "DuplicateEmail" => Results.Redirect("/auth/register?error=duplicate"),
        "PasswordTooShort" or "PasswordRequiresUpper" or "PasswordRequiresLower" or "PasswordRequiresDigit" =>
            Results.Redirect("/auth/register?error=password"),
        _ => Results.Redirect("/auth/register?error=unknown")
    };
});

// ===== Auth Endpoint (GET) - logout via browser =====
app.MapGet("/auth/logout", async (SignInManager<ApplicationUser> signInManager) =>
{
    await signInManager.SignOutAsync();
    return Results.Redirect("/");
});

// ===== Map Routes =====
app.MapRazorComponents<Bioskop.Components.App>()
    .AddInteractiveServerRenderMode();

app.MapControllers();
app.MapGet("/api/health", () => Results.Ok(new { Status = "Healthy", Timestamp = DateTime.UtcNow }));
app.MapApiEndpoints();

await app.RunAsync();

public record LoginRequest(string Email, string Password, bool RememberMe);
public record RegisterRequest(string FullName, string Email, string Password, string ConfirmPassword);
